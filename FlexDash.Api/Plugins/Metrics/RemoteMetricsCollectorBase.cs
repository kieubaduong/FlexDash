namespace FlexDash.Api.Plugins.Metrics;

/// <summary>
/// Base class for OS-specific metric collection.
/// Uses an ICommandRunner to execute commands — works for both local and remote (SSH).
/// </summary>
public abstract class MetricsCollectorBase : IMetricsCollector {
    private readonly ICommandRunner _runner;

    protected long PrevIdleTime;
    protected long PrevTotalTime;
    protected bool HasBaseline;

    protected MetricsCollectorBase(ICommandRunner runner) {
        _runner = runner;
    }

    protected abstract string CpuCommand { get; }
    protected abstract string MemoryCommand { get; }
    protected abstract string DiskCommand { get; }

    protected abstract double ParseCpu(string output);
    protected abstract double ParseMemory(string output);
    protected abstract double ParseDisk(string output);

    public async Task<MetricsSnapshot> CollectAsync(CancellationToken ct) {
        string cpuOutput = await _runner.RunAsync(CpuCommand, ct);
        string memOutput = await _runner.RunAsync(MemoryCommand, ct);
        string diskOutput = await _runner.RunAsync(DiskCommand, ct);

        return new MetricsSnapshot(
            ParseCpu(cpuOutput),
            ParseMemory(memOutput),
            ParseDisk(diskOutput));
    }

    public ValueTask DisposeAsync() => _runner.DisposeAsync();
}
