using System.Runtime.CompilerServices;
using FlexDash.Api.Data;
using FlexDash.Api.Plugins;
using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Services;

public sealed class DataSourceOrchestrator {
    private readonly Dictionary<DataSourceType, IDataSourcePlugin> _plugins;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataPointBuffer _buffer;
    private readonly ILogger<DataSourceOrchestrator> _logger;

    public DataSourceOrchestrator(
        IEnumerable<IDataSourcePlugin> plugins,
        IServiceScopeFactory scopeFactory,
        DataPointBuffer buffer,
        ILogger<DataSourceOrchestrator> logger) {
        _plugins = plugins.ToDictionary(plugin => plugin.Type);
        _scopeFactory = scopeFactory;
        _buffer = buffer;
        _logger = logger;
    }

    public async Task<List<DataPointDto>> PollAllAsync(CancellationToken ct) {
        using var scope = _scopeFactory.CreateScope();
        DataSourceDbContext db = scope.ServiceProvider.GetRequiredService<DataSourceDbContext>();

        List<DataSource> sources = await db.DataSources.ToListAsync(ct);
        var allPoints = new List<DataPointDto>();

        foreach (DataSource source in sources) {
            try {
                List<DataPointDto> points = await FetchFromPluginAsync(source, ct);
                _buffer.Add(source.Id, points);
                allPoints.AddRange(points);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error polling data source {SourceId}", source.Id);
            }
        }

        return allPoints;
    }

    public async Task<List<DataPointDto>> FetchForSourceAsync(Guid id, CancellationToken ct) {
        using var scope = _scopeFactory.CreateScope();
        DataSourceDbContext db = scope.ServiceProvider.GetRequiredService<DataSourceDbContext>();

        DataSource? source = await db.DataSources.FindAsync([id], ct);
        if (source is null) {
            return [];
        }

        List<DataPointDto> points = await FetchFromPluginAsync(source, ct);
        _buffer.Add(source.Id, points);
        return points;
    }

    public ValidationResultDto ValidateConfig(string type, string configJson) {
        if (!Enum.TryParse<DataSourceType>(type, out DataSourceType dsType)) {
            return new ValidationResultDto(false, $"Unknown data source type: {type}");
        }

        if (!_plugins.TryGetValue(dsType, out IDataSourcePlugin? plugin)) {
            return new ValidationResultDto(false, $"No plugin found for type: {type}");
        }

        return plugin.ValidateConfig(configJson);
    }

    public async IAsyncEnumerable<DataPointDto> StreamAllAsync([EnumeratorCancellation] CancellationToken ct) {
        using var scope = _scopeFactory.CreateScope();
        DataSourceDbContext db = scope.ServiceProvider.GetRequiredService<DataSourceDbContext>();

        List<DataSource> sources = await db.DataSources.ToListAsync(ct);

        // Merge all streaming sources into a single channel
        var channel = System.Threading.Channels.Channel.CreateUnbounded<DataPointDto>();

        int activeStreams = 0;
        foreach (DataSource source in sources) {
            if (!_plugins.TryGetValue(source.Type, out IDataSourcePlugin? plugin)) {
                continue;
            }

            IAsyncEnumerable<DataPointDto>? stream = plugin.StreamAsync(source, ct);
            if (stream is null) {
                continue;
            }

            activeStreams++;
            _ = ForwardStreamAsync(stream, source.Id, channel.Writer, ct);
        }

        if (activeStreams == 0) {
            yield break;
        }

        await foreach (DataPointDto point in channel.Reader.ReadAllAsync(ct)) {
            _buffer.Add(point.DataSourceId, [point]);
            yield return point;
        }
    }

    private async Task ForwardStreamAsync(
        IAsyncEnumerable<DataPointDto> stream,
        Guid sourceId,
        System.Threading.Channels.ChannelWriter<DataPointDto> writer,
        CancellationToken ct) {
        try {
            await foreach (DataPointDto point in stream.WithCancellation(ct)) {
                await writer.WriteAsync(point, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            _logger.LogError(ex, "Error in stream for source {SourceId}", sourceId);
        }
    }

    private async Task<List<DataPointDto>> FetchFromPluginAsync(DataSource source, CancellationToken ct) {
        if (!_plugins.TryGetValue(source.Type, out IDataSourcePlugin? plugin)) {
            _logger.LogWarning("No plugin registered for type {Type}", source.Type);
            return [];
        }

        return await plugin.FetchAsync(source, ct);
    }
}
