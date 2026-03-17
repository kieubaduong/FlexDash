namespace FlexDash.Api.Plugins.Metrics;

public interface ICommandRunner : IAsyncDisposable {
    Task<string> RunAsync(string command, CancellationToken ct);
}
