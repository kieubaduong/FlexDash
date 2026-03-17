using System.Text.Json;

namespace FlexDash.Api.Services.Alert;

public static class ThresholdEvaluator {
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record ThresholdConfig(string Operator, double Value);

    public static AlertEvent? Evaluate(AlertRule rule, List<DataPointDto> points) {
        var config = JsonSerializer.Deserialize<ThresholdConfig>(rule.ConditionJson, JsonOptions);
        if (config is null || points.Count == 0) {
            return null;
        }

        DataPointDto latest = points.OrderByDescending(point => point.Timestamp).ToList()[0];

        bool triggered = config.Operator switch {
            ">" => latest.Value > config.Value,
            ">=" => latest.Value >= config.Value,
            "<" => latest.Value < config.Value,
            "<=" => latest.Value <= config.Value,
            "==" => Math.Abs(latest.Value - config.Value) < 0.0001,
            _ => false
        };

        if (!triggered) {
            return null;
        }

        return new AlertEvent {
            AlertRuleId = rule.Id,
            Message = $"{rule.Name}: value {latest.Value} {config.Operator} {config.Value}",
            TriggerValue = latest.Value
        };
    }

}
