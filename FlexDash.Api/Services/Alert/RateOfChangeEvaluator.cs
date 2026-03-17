using System.Text.Json;

namespace FlexDash.Api.Services.Alert;

public static class RateOfChangeEvaluator {
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record RateOfChangeConfig(double PercentChange, int WindowSeconds);

    public static AlertEvent? Evaluate(AlertRule rule, DataPointBuffer buffer) {
        var config = JsonSerializer.Deserialize<RateOfChangeConfig>(rule.ConditionJson, JsonOptions);
        if (config is null) {
            return null;
        }

        DateTime windowStart = DateTime.UtcNow.AddSeconds(-config.WindowSeconds);
        List<DataPointDto> historical = buffer.GetSince(rule.DataSourceId, windowStart)
            .Where(point => rule.LabelFilter is null || point.Label == rule.LabelFilter)
            .OrderBy(point => point.Timestamp)
            .ToList();

        if (historical.Count < 2) {
            return null;
        }

        double first = historical[0].Value;
        double last = historical[^1].Value;
        if (Math.Abs(first) < 0.0001) {
            return null;
        }

        double percentChange = Math.Abs((last - first) / first * 100);
        if (percentChange < config.PercentChange) {
            return null;
        }

        return new AlertEvent {
            AlertRuleId = rule.Id,
            Message = $"{rule.Name}: rate of change {percentChange:F1}% exceeds threshold {config.PercentChange}%",
            TriggerValue = percentChange
        };
    }

}
