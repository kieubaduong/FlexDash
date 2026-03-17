namespace FlexDash.Api.Plugins.Metrics;

public sealed class LinuxMetricsCollector : MetricsCollectorBase {
    public LinuxMetricsCollector(ICommandRunner runner) : base(runner) { }

    protected override string CpuCommand => "cat /proc/stat";
    protected override string MemoryCommand => "cat /proc/meminfo";
    protected override string DiskCommand => "df --total --output=size,avail | tail -1";

    protected override double ParseCpu(string output) {
        try {
            string? cpuLine = output.Split('\n').FirstOrDefault(line => line.StartsWith("cpu "));
            if (cpuLine is null) {
                return 0;
            }

            string[] parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long iowait = long.Parse(parts[5]);

            long idleTime = idle + iowait;
            long totalTime = user + nice + system + idle + iowait;

            if (!HasBaseline) {
                PrevIdleTime = idleTime;
                PrevTotalTime = totalTime;
                HasBaseline = true;
                return 0;
            }

            long deltaIdle = idleTime - PrevIdleTime;
            long deltaTotal = totalTime - PrevTotalTime;
            PrevIdleTime = idleTime;
            PrevTotalTime = totalTime;

            return deltaTotal == 0 ? 0 : Math.Round((1.0 - (double)deltaIdle / deltaTotal) * 100, 1);
        }
        catch {
            return 0;
        }
    }

    protected override double ParseMemory(string output) {
        try {
            string[] lines = output.Split('\n');
            long total = ParseMemInfoKb(lines, "MemTotal:");
            long available = ParseMemInfoKb(lines, "MemAvailable:");
            return total == 0 ? 0 : Math.Round((double)(total - available) / total * 100, 1);
        }
        catch {
            return 0;
        }
    }

    protected override double ParseDisk(string output) {
        try {
            string[] parts = output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) {
                return 0;
            }

            long size = long.Parse(parts[0]);
            long avail = long.Parse(parts[1]);
            return size == 0 ? 0 : Math.Round((double)(size - avail) / size * 100, 1);
        }
        catch {
            return 0;
        }
    }

    private static long ParseMemInfoKb(string[] lines, string key) {
        string? line = lines.FirstOrDefault(entry => entry.StartsWith(key));
        if (line is null) {
            return 0;
        }

        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out long kb) ? kb : 0;
    }
}
