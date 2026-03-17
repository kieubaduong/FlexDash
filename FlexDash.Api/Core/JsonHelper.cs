using System.Text.Json;

namespace FlexDash.Api.Core;

public static class JsonHelper {
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public static Result<T> TryDeserialize<T>(string json) {
        try {
            T? result = JsonSerializer.Deserialize<T>(json, CaseInsensitive);

            if (result is null) {
                return Result<T>.Err("Config could not be parsed.");
            }

            return Result<T>.Ok(result);
        }
        catch (JsonException ex) {
            return Result<T>.Err($"Invalid JSON: {ex.Message}");
        }
    }
}
