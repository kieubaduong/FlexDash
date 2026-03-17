using FlexDash.Api.Plugins.Metrics;
using System.Collections.Concurrent;

namespace FlexDash.Api.Plugins;

public sealed class SystemMetricsPlugin : IDataSourcePlugin, IAsyncDisposable {
    public DataSourceType Type => DataSourceType.SystemMetrics;

    private readonly ConcurrentDictionary<Guid, CollectorEntry> _collectors = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<List<DataPointDto>> FetchAsync(DataSource source, CancellationToken ct) {
        IMetricsCollector collector = await GetOrCreateCollectorAsync(source, ct);
        MetricsSnapshot snapshot = await collector.CollectAsync(ct);

        return [
            new(source.Id, snapshot.CpuPercent, "cpu", DateTime.UtcNow),
            new(source.Id, snapshot.MemoryPercent, "memory", DateTime.UtcNow),
            new(source.Id, snapshot.DiskPercent, "disk", DateTime.UtcNow)
        ];
    }

    public IAsyncEnumerable<DataPointDto>? StreamAsync(DataSource source, CancellationToken ct) => null;

    public ValidationResultDto ValidateConfig(string configJson) {
        Result<SshConnectionConfig> result = JsonHelper.TryDeserialize<SshConnectionConfig>(configJson);
        if (!result.IsOk) {
            return new(false, result.GetError());
        }

        SshConnectionConfig config = result.GetData();
        if (!config.IsRemote) {
            return new(true, null);
        }

        if (string.IsNullOrWhiteSpace(config.Username)) {
            return new(false, "Username is required for remote monitoring.");
        }

        if (string.IsNullOrWhiteSpace(config.Password) && string.IsNullOrWhiteSpace(config.PrivateKeyPath)) {
            return new(false, "Either Password or PrivateKeyPath is required.");
        }

        return new(true, null);
    }

    private async Task<IMetricsCollector> GetOrCreateCollectorAsync(DataSource source, CancellationToken ct) {
        Result<SshConnectionConfig> result = JsonHelper.TryDeserialize<SshConnectionConfig>(source.ConfigJson);
        SshConnectionConfig config = result.IsOk ? result.GetData() : new SshConnectionConfig();

        string configHash = source.ConfigJson;

        if (_collectors.TryGetValue(source.Id, out CollectorEntry? entry) && entry.ConfigHash == configHash) {
            return entry.Collector;
        }

        await _lock.WaitAsync(ct);
        try {
            if (_collectors.TryGetValue(source.Id, out entry) && entry.ConfigHash == configHash) {
                return entry.Collector;
            }

            if (entry is not null) {
                await entry.Collector.DisposeAsync();
            }

            IMetricsCollector collector = await MetricsCollectorFactory.CreateAsync(config, ct);
            _collectors[source.Id] = new CollectorEntry(collector, configHash);
            return collector;
        }
        finally {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        foreach (CollectorEntry entry in _collectors.Values) {
            await entry.Collector.DisposeAsync();
        }
        _collectors.Clear();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record CollectorEntry(IMetricsCollector Collector, string ConfigHash);
}
