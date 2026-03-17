using System.Diagnostics;

namespace FlexDash.Api.Plugins.Metrics;

public sealed class LocalCommandRunner : ICommandRunner {
    public async Task<string> RunAsync(string command, CancellationToken ct) {
        string fileName;
        string arguments;

        if (OperatingSystem.IsWindows()) {
            fileName = "cmd.exe";
            arguments = $"/C {command}";
        }
        else {
            fileName = "/bin/sh";
            arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
        }

        using var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
