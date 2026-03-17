namespace FlexDash.Api.Plugins.Metrics;

public sealed class SshConnectionConfig {
    public string? Host { get; set; }
    public int Port { get; set; } = 22;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }

    public bool IsRemote => !string.IsNullOrWhiteSpace(Host);
}
