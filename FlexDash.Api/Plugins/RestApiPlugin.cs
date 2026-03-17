using System.Text.Json;

namespace FlexDash.Api.Plugins;

public sealed class RestApiPlugin : IDataSourcePlugin {
    private readonly IHttpClientFactory _httpClientFactory;

    public RestApiPlugin(IHttpClientFactory httpClientFactory) {
        _httpClientFactory = httpClientFactory;
    }
    public DataSourceType Type => DataSourceType.RestApi;

    private sealed record RestApiConfig(string Url, string ValuePath, string? Label, Dictionary<string, string>? Headers);

    public async Task<List<DataPointDto>> FetchAsync(DataSource source, CancellationToken ct) {
        Result<RestApiConfig> configResult = JsonHelper.TryDeserialize<RestApiConfig>(source.ConfigJson);
        if (configResult.IsError) {
            return [];
        }

        RestApiConfig config = configResult.GetData();
        HttpClient client = _httpClientFactory.CreateClient();

        if (config.Headers is not null) {
            foreach (var (key, value) in config.Headers) {
                client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }
        }

        using HttpResponseMessage response = await client.GetAsync(config.Url, ct);
        if (!response.IsSuccessStatusCode) {
            return [];
        }

        using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        Result<double> valueResult = ExtractValueByPath(doc.RootElement, config.ValuePath);
        if (valueResult.IsError) {
            return [];
        }

        return [new DataPointDto(source.Id, valueResult.GetData(), config.Label, DateTime.UtcNow)];
    }

    public IAsyncEnumerable<DataPointDto>? StreamAsync(DataSource source, CancellationToken ct) => null;

    public ValidationResultDto ValidateConfig(string configJson) {
        Result<RestApiConfig> configResult = JsonHelper.TryDeserialize<RestApiConfig>(configJson);
        if (configResult.IsError) {
            return new ValidationResultDto(false, configResult.GetError());
        }

        RestApiConfig config = configResult.GetData();

        if (string.IsNullOrWhiteSpace(config.Url)) {
            return new ValidationResultDto(false, "Url is required.");
        }
        if (string.IsNullOrWhiteSpace(config.ValuePath)) {
            return new ValidationResultDto(false, "ValuePath is required.");
        }
        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out _)) {
            return new ValidationResultDto(false, "Url must be a valid absolute URI.");
        }

        return new ValidationResultDto(true, null);
    }

    private static Result<double> ExtractValueByPath(JsonElement root, string path) {
        string[] parts = path.Split('.');
        JsonElement current = root;
        foreach (string part in parts) {
            if (!current.TryGetProperty(part, out current)) {
                return Result<double>.Err($"Path segment '{part}' not found.");
            }
        }

        if (!current.TryGetDouble(out double value)) {
            return Result<double>.Err($"Value at path '{path}' is not a number.");
        }

        return Result<double>.Ok(value);
    }

}
