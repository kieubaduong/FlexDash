using FlexDash.Api.Core;
using FlexDash.Api.Data;
using FlexDash.Api.Dtos;
using FlexDash.Api.Models;
using FlexDash.Api.Plugins;
using FlexDash.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlexDash.Tests.Services;

public class DataSourceOrchestratorTests {
    private static DataSourceDbContext CreateInMemoryDb() {
        DbContextOptions<DataSourceDbContext> options = new DbContextOptionsBuilder<DataSourceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DataSourceDbContext(options);
    }

    private static DataSourceOrchestrator CreateOrchestrator(DataSourceDbContext db, DataPointBuffer? buffer = null, params IDataSourcePlugin[] plugins) {
        IServiceScopeFactory scopeFactory = new TestScopeFactory<DataSourceDbContext>(db);
        return new DataSourceOrchestrator(plugins, scopeFactory, buffer ?? new DataPointBuffer(), NullLogger<DataSourceOrchestrator>.Instance);
    }

    [Fact]
    public void ValidateConfig_Routes_To_Correct_Plugin() {
        DataSourceDbContext db = CreateInMemoryDb();
        FakePlugin plugin = new(DataSourceType.SystemMetrics);
        DataSourceOrchestrator orchestrator = CreateOrchestrator(db, null, plugin);

        ValidationResultDto result = orchestrator.ValidateConfig("SystemMetrics", "{}");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateConfig_Unknown_Type_Returns_Error() {
        DataSourceDbContext db = CreateInMemoryDb();
        DataSourceOrchestrator orchestrator = CreateOrchestrator(db);

        ValidationResultDto result = orchestrator.ValidateConfig("NonExistentType", "{}");

        Assert.False(result.IsValid);
        Assert.Contains("Unknown", result.ErrorMessage);
    }

    [Fact]
    public async Task PollAllAsync_Calls_Plugin_For_Each_Source() {
        DataSourceDbContext db = CreateInMemoryDb();
        FakePlugin plugin = new(DataSourceType.SystemMetrics);

        var source = new DataSource {
            Name = "Test",
            Type = DataSourceType.SystemMetrics,
            ConfigJson = "{}"
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        DataSourceOrchestrator orchestrator = CreateOrchestrator(db, null, plugin);
        List<DataPointDto> points = await orchestrator.PollAllAsync(CancellationToken.None);

        Assert.NotEmpty(points);
        Assert.Equal(1, plugin.FetchCallCount);
    }

    [Fact]
    public async Task PollAllAsync_Adds_Points_To_Buffer() {
        DataSourceDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();
        FakePlugin plugin = new(DataSourceType.SystemMetrics);

        var source = new DataSource { Id = sourceId, Name = "S", Type = DataSourceType.SystemMetrics, ConfigJson = "{}" };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        DataPointBuffer buffer = new();
        DataSourceOrchestrator orchestrator = CreateOrchestrator(db, buffer, plugin);
        await orchestrator.PollAllAsync(CancellationToken.None);

        List<DataPointDto> buffered = buffer.Get(sourceId);
        Assert.NotEmpty(buffered);
    }
}

internal class FakePlugin(DataSourceType type) : IDataSourcePlugin {
    public DataSourceType Type => type;
    public int FetchCallCount { get; private set; }

    public Task<List<DataPointDto>> FetchAsync(DataSource source, CancellationToken ct) {
        FetchCallCount++;
        return Task.FromResult<List<DataPointDto>>(
        [
            new DataPointDto(source.Id, 42.0, "test", DateTime.UtcNow)
        ]);
    }

    public IAsyncEnumerable<DataPointDto>? StreamAsync(DataSource source, CancellationToken ct) => null;

    public ValidationResultDto ValidateConfig(string configJson) => new(true, null);
}
