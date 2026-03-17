using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace FlexDash.Api.Plugins;

public sealed class WebSocketStreamPlugin : IDataSourcePlugin {
    public DataSourceType Type => DataSourceType.WebSocketStream;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record WebSocketConfig(string Url, string ValuePath, string? Label);

    public Task<List<DataPointDto>> FetchAsync(DataSource source, CancellationToken ct)
        => Task.FromResult<List<DataPointDto>>([]);

    public async IAsyncEnumerable<DataPointDto> StreamAsync(DataSource source, [EnumeratorCancellation] CancellationToken ct) {
        WebSocketConfig? config = JsonSerializer.Deserialize<WebSocketConfig>(source.ConfigJson, JsonOptions);

        if (config is null) {
            yield break;
        }

        await foreach (DataPointDto point in StreamInternalAsync(config, source.Id, ct)) {
            yield return point;
        }
    }

    private static async IAsyncEnumerable<DataPointDto> StreamInternalAsync(
        WebSocketConfig config, Guid sourceId, [EnumeratorCancellation] CancellationToken ct) {
        using var ws = new ClientWebSocket();

        if (!await TryConnectAsync(ws, config.Url, ct)) {
            yield break;
        }

        var buffer = new byte[4096];

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            WebSocketReceiveResult? result = await TryReceiveAsync(ws, buffer, ct);
            if (result is null || result.MessageType == WebSocketMessageType.Close) {
                yield break;
            }

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            double? parsed = TryParseValue(json, config.ValuePath);

            if (parsed.HasValue) {
                yield return new DataPointDto(sourceId, parsed.Value, config.Label, DateTime.UtcNow);
            }
        }
    }

    private static async Task<bool> TryConnectAsync(ClientWebSocket ws, string url, CancellationToken ct) {
        try {
            await ws.ConnectAsync(new Uri(url), ct);
            return true;
        }
        catch (WebSocketException) {
            return false;
        }
    }

    private static async Task<WebSocketReceiveResult?> TryReceiveAsync(
        ClientWebSocket ws, byte[] buffer, CancellationToken ct) {
        try {
            return await ws.ReceiveAsync(buffer, ct);
        }
        catch (WebSocketException) {
            return null;
        }
    }

    private static double? TryParseValue(string json, string valuePath) {
        try {
            using var doc = JsonDocument.Parse(json);
            JsonElement current = doc.RootElement;

            foreach (string part in valuePath.Split('.')) {
                if (!current.TryGetProperty(part, out current)) {
                    return null;
                }
            }

            return current.TryGetDouble(out double v) ? v : null;
        }
        catch (JsonException) {
            return null;
        }
    }

    public ValidationResultDto ValidateConfig(string configJson) {
        try {
            WebSocketConfig? config = JsonSerializer.Deserialize<WebSocketConfig>(configJson, JsonOptions);

            if (config is null) {
                return new ValidationResultDto(false, "Config could not be parsed.");
            }
            if (string.IsNullOrWhiteSpace(config.Url)) {
                return new ValidationResultDto(false, "Url is required.");
            }
            if (!Uri.TryCreate(config.Url, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss")) {
                return new ValidationResultDto(false, "Url must be a valid ws:// or wss:// URI.");
            }

            return new ValidationResultDto(true, null);
        }
        catch (JsonException ex) {
            return new ValidationResultDto(false, $"Invalid JSON: {ex.Message}");
        }
    }
}
