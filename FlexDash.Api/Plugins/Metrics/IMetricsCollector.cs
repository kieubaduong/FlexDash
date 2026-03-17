namespace FlexDash.Api.Plugins.Metrics;

public record MetricsSnapshot(double CpuPercent, double MemoryPercent, double DiskPercent);

public interface IMetricsCollector : IAsyncDisposable {
    Task<MetricsSnapshot> CollectAsync(CancellationToken ct);
}
