using Renci.SshNet;

namespace FlexDash.Api.Plugins.Metrics;

public sealed class SshCommandRunner : ICommandRunner {
    private readonly SshClient _client;

    public SshCommandRunner(SshClient client) {
        _client = client;
    }

    public async Task<string> RunAsync(string command, CancellationToken ct) {
        if (!_client.IsConnected) {
            await Task.Run(() => _client.Connect(), ct);
        }

        return await Task.Run(() => {
            ct.ThrowIfCancellationRequested();
            using SshCommand cmd = _client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(10);
            return cmd.Execute();
        }, ct);
    }

    public ValueTask DisposeAsync() {
        if (_client.IsConnected) {
            _client.Disconnect();
        }

        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
