using System.Text.Json;

namespace FlexDash.Api.Services.Alert;

public static class AbsenceOfDataEvaluator {
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record AbsenceConfig(int TimeoutSeconds);

    public static AlertEvent? Evaluate(AlertRule rule, DataPointBuffer buffer) {
        var config = JsonSerializer.Deserialize<AbsenceConfig>(rule.ConditionJson, JsonOptions);
        if (config is null) {
            return null;
        }

        DateTime cutoff = DateTime.UtcNow.AddSeconds(-config.TimeoutSeconds);
        bool hasData = rule.LabelFilter is null
            ? buffer.HasDataSince(rule.DataSourceId, cutoff)
            : buffer.GetSince(rule.DataSourceId, cutoff).Any(point => point.Label == rule.LabelFilter);

        if (hasData) {
            return null;
        }

        return new AlertEvent {
            AlertRuleId = rule.Id,
            Message = $"{rule.Name}: no data received in last {config.TimeoutSeconds}s",
            TriggerValue = 0
        };
    }

}
