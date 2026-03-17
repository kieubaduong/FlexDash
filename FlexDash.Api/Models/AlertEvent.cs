namespace FlexDash.Api.Models;

public sealed class AlertEvent {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertRuleId { get; set; }
    public string Message { get; set; } = "";
    public double TriggerValue { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public AlertRule AlertRule { get; set; } = null!;

    public AlertEventDto ToDto(string ruleName) => new(
        Id, AlertRuleId, ruleName, Message, TriggerValue,
        IsAcknowledged, TriggeredAt, AcknowledgedAt);
}
