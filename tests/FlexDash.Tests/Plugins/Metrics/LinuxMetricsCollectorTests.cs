using FlexDash.Api.Plugins.Metrics;
using Xunit;

namespace FlexDash.Tests.Plugins.Metrics;

public class LinuxMetricsCollectorTests {
    [Fact]
    public async Task CollectAsync_Parses_Linux_Output() {
        var runner = new FakeCommandRunner(new Dictionary<string, string> {
            ["cat /proc/stat"] = "cpu  1000 200 300 5000 100 0 0 0\ncpu0  500 100 150 2500 50 0 0 0\n",
            ["cat /proc/meminfo"] = "MemTotal:       16384000 kB\nMemFree:         2000000 kB\nMemAvailable:    4096000 kB\n",
            ["df --total --output=size,avail | tail -1"] = "  500000000   200000000\n"
        });

        var collector = new LinuxMetricsCollector(runner);

        // First call: baseline — CPU returns 0
        MetricsSnapshot snap1 = await collector.CollectAsync(CancellationToken.None);
        Assert.Equal(0, snap1.CpuPercent);
        Assert.True(snap1.MemoryPercent > 0);
        Assert.True(snap1.DiskPercent > 0);

        // Memory: (16384000 - 4096000) / 16384000 * 100 = 75%
        Assert.Equal(75.0, snap1.MemoryPercent);

        // Disk: (500000000 - 200000000) / 500000000 * 100 = 60%
        Assert.Equal(60.0, snap1.DiskPercent);
    }

    [Fact]
    public async Task CollectAsync_Cpu_Delta_On_Second_Call() {
        int callCount = 0;
        var runner = new FakeCommandRunner(cmd => {
            if (cmd.StartsWith("cat /proc/stat")) {
                callCount++;
                // First: idle=5000, total=6600; Second: idle=5100, total=6900
                return callCount <= 1
                    ? "cpu  1000 200 300 5000 100 0 0 0\n"
                    : "cpu  1100 200 400 5100 100 0 0 0\n";
            }
            if (cmd.StartsWith("cat /proc/meminfo")) {
                return "MemTotal: 8000000 kB\nMemAvailable: 4000000 kB\n";
            }

            return "1000000 500000\n";
        });

        var collector = new LinuxMetricsCollector(runner);

        await collector.CollectAsync(CancellationToken.None); // baseline
        MetricsSnapshot snap2 = await collector.CollectAsync(CancellationToken.None);

        // delta idle = 5100-5000=100, delta total = 6900-6600=300
        // cpu = (1 - 100/300) * 100 = 66.7
        Assert.Equal(66.7, snap2.CpuPercent);
    }

    [Fact]
    public async Task CollectAsync_Handles_Empty_Output() {
        var runner = new FakeCommandRunner(new Dictionary<string, string> {
            ["cat /proc/stat"] = "",
            ["cat /proc/meminfo"] = "",
            ["df --total --output=size,avail | tail -1"] = ""
        });

        var collector = new LinuxMetricsCollector(runner);
        MetricsSnapshot snap = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(0, snap.CpuPercent);
        Assert.Equal(0, snap.MemoryPercent);
        Assert.Equal(0, snap.DiskPercent);
    }
}

internal class FakeCommandRunner : ICommandRunner {
    private readonly Func<string, string> _handler;

    public FakeCommandRunner(Dictionary<string, string> responses) {
        _handler = cmd => responses.TryGetValue(cmd, out string? val) ? val : "";
    }

    public FakeCommandRunner(Func<string, string> handler) {
        _handler = handler;
    }

    public Task<string> RunAsync(string command, CancellationToken ct) {
        return Task.FromResult(_handler(command));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
