namespace FlexDash.Api.Plugins.Metrics;

public sealed class WindowsMetricsCollector : MetricsCollectorBase {
    public WindowsMetricsCollector(ICommandRunner runner) : base(runner) { }

    protected override string CpuCommand =>
        "powershell -NoProfile -Command \"Get-CimInstance Win32_Processor | Select-Object -ExpandProperty LoadPercentage\"";

    protected override string MemoryCommand =>
        "powershell -NoProfile -Command \"$os = Get-CimInstance Win32_OperatingSystem; Write-Output \\\"$($os.TotalVisibleMemorySize) $($os.FreePhysicalMemory)\\\"\"";

    protected override string DiskCommand =>
        "powershell -NoProfile -Command \"Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object { Write-Output \\\"$($_.Size) $($_.FreeSpace)\\\" }\"";

    protected override double ParseCpu(string output) {
        try {
            double[] values = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => double.TryParse(line, out _))
                .Select(double.Parse)
                .ToArray();
            return values.Length == 0 ? 0 : Math.Round(values.Average(), 1);
        }
        catch {
            return 0;
        }
    }

    protected override double ParseMemory(string output) {
        try {
            string[] parts = output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) {
                return 0;
            }

            long total = long.Parse(parts[0]);
            long free = long.Parse(parts[1]);
            return total == 0 ? 0 : Math.Round((double)(total - free) / total * 100, 1);
        }
        catch {
            return 0;
        }
    }

    protected override double ParseDisk(string output) {
        try {
            long totalSize = 0;
            long totalFree = 0;
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) {
                    continue;
                }

                if (long.TryParse(parts[0], out long size) && long.TryParse(parts[1], out long free)) {
                    totalSize += size;
                    totalFree += free;
                }
            }
            return totalSize == 0 ? 0 : Math.Round((double)(totalSize - totalFree) / totalSize * 100, 1);
        }
        catch {
            return 0;
        }
    }
}
