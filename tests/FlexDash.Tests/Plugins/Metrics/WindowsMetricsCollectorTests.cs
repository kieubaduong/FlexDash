using FlexDash.Api.Plugins.Metrics;
using Xunit;

namespace FlexDash.Tests.Plugins.Metrics;

public class WindowsMetricsCollectorTests {
    [Fact]
    public async Task CollectAsync_Parses_Windows_Output() {
        var runner = new FakeCommandRunner(cmd => {
            if (cmd.Contains("Win32_Processor")) {
                return "45\r\n";
            }

            if (cmd.Contains("Win32_OperatingSystem")) {
                return "16000000 4000000\r\n";
            }

            if (cmd.Contains("Win32_LogicalDisk")) {
                return "500000000000 200000000000\r\n";
            }

            return "";
        });

        var collector = new WindowsMetricsCollector(runner);
        MetricsSnapshot snap = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(45.0, snap.CpuPercent);
        // Memory: (16000000 - 4000000) / 16000000 * 100 = 75%
        Assert.Equal(75.0, snap.MemoryPercent);
        // Disk: (500B - 200B) / 500B * 100 = 60%
        Assert.Equal(60.0, snap.DiskPercent);
    }

    [Fact]
    public async Task CollectAsync_Multi_Cpu_Averages() {
        var runner = new FakeCommandRunner(cmd => {
            if (cmd.Contains("Win32_Processor")) {
                return "40\r\n60\r\n";
            }

            if (cmd.Contains("Win32_OperatingSystem")) {
                return "8000000 4000000\r\n";
            }

            if (cmd.Contains("Win32_LogicalDisk")) {
                return "100 50\r\n";
            }

            return "";
        });

        var collector = new WindowsMetricsCollector(runner);
        MetricsSnapshot snap = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(50.0, snap.CpuPercent);
    }

    [Fact]
    public async Task CollectAsync_Multiple_Disks() {
        var runner = new FakeCommandRunner(cmd => {
            if (cmd.Contains("Win32_Processor")) {
                return "10\r\n";
            }

            if (cmd.Contains("Win32_OperatingSystem")) {
                return "8000000 4000000\r\n";
            }

            if (cmd.Contains("Win32_LogicalDisk")) {
                return "1000000000 400000000\r\n500000000 100000000\r\n";
            }

            return "";
        });

        var collector = new WindowsMetricsCollector(runner);
        MetricsSnapshot snap = await collector.CollectAsync(CancellationToken.None);

        // Total: 1.5B, Free: 0.5B, Used: 1B → 66.7%
        Assert.Equal(66.7, snap.DiskPercent);
    }

    [Fact]
    public async Task CollectAsync_Handles_Empty_Output() {
        var runner = new FakeCommandRunner(_ => "");

        var collector = new WindowsMetricsCollector(runner);
        MetricsSnapshot snap = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(0, snap.CpuPercent);
        Assert.Equal(0, snap.MemoryPercent);
        Assert.Equal(0, snap.DiskPercent);
    }
}
