using Renci.SshNet;

namespace FlexDash.Api.Plugins.Metrics;

public static class MetricsCollectorFactory {
    /// <summary>
    /// Creates a metrics collector. If config has SSH host, connects remotely and detects OS.
    /// Otherwise, creates a local collector based on current OS.
    /// </summary>
    public static async Task<IMetricsCollector> CreateAsync(
        SshConnectionConfig config, CancellationToken ct) {
        if (!config.IsRemote) {
            ICommandRunner localRunner = new LocalCommandRunner();
            return OperatingSystem.IsLinux()
                ? new LinuxMetricsCollector(localRunner)
                : new WindowsMetricsCollector(localRunner);
        }

        SshClient client = CreateSshClient(config);
        await Task.Run(() => client.Connect(), ct);

        ICommandRunner sshRunner = new SshCommandRunner(client);
        bool isLinux = await DetectLinuxAsync(sshRunner, ct);
        return isLinux
            ? new LinuxMetricsCollector(sshRunner)
            : new WindowsMetricsCollector(sshRunner);
    }

    /// <summary>
    /// Tests SSH connectivity. Returns null on success, error message on failure.
    /// </summary>
    public static async Task<string?> TestConnectionAsync(
        SshConnectionConfig config, CancellationToken ct) {
        SshClient? client = null;
        try {
            client = CreateSshClient(config);
            await Task.Run(() => client.Connect(), ct);

            using SshCommand cmd = client.CreateCommand("echo ok");
            cmd.CommandTimeout = TimeSpan.FromSeconds(10);
            await Task.Run(() => cmd.Execute(), ct);

            return null;
        }
        catch (Exception ex) {
            return ex.Message;
        }
        finally {
            if (client is not null) {
                if (client.IsConnected) {
                    client.Disconnect();
                }

                client.Dispose();
            }
        }
    }

    internal static SshClient CreateSshClient(SshConnectionConfig config) {
        if (string.IsNullOrWhiteSpace(config.Host)) {
            throw new ArgumentException("Host is required for remote connection.");
        }

        if (string.IsNullOrWhiteSpace(config.Username)) {
            throw new ArgumentException("Username is required for remote connection.");
        }

        Renci.SshNet.ConnectionInfo connectionInfo;
        if (!string.IsNullOrWhiteSpace(config.PrivateKeyPath)) {
            var keyFile = new PrivateKeyFile(config.PrivateKeyPath);
            connectionInfo = new Renci.SshNet.ConnectionInfo(config.Host, config.Port, config.Username,
                new PrivateKeyAuthenticationMethod(config.Username, keyFile));
        }
        else if (!string.IsNullOrWhiteSpace(config.Password)) {
            connectionInfo = new Renci.SshNet.ConnectionInfo(config.Host, config.Port, config.Username,
                new PasswordAuthenticationMethod(config.Username, config.Password));
        }
        else {
            throw new ArgumentException("Either Password or PrivateKeyPath is required.");
        }

        connectionInfo.Timeout = TimeSpan.FromSeconds(10);
        return new SshClient(connectionInfo);
    }

    private static async Task<bool> DetectLinuxAsync(ICommandRunner runner, CancellationToken ct) {
        try {
            string result = await runner.RunAsync("uname -s", ct);
            return result.Trim().Equals("Linux", StringComparison.OrdinalIgnoreCase);
        }
        catch {
            return false;
        }
    }
}
