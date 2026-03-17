namespace FlexDash.Api.Models;

public enum AlertSeverity {
    Info,
    Warning,
    Critical
}

public enum AlertConditionType {
    Threshold,
    RateOfChange,
    AbsenceOfData
}

public sealed class AlertRule {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DataSourceId { get; set; }
    public string Name { get; set; } = "";
    public AlertSeverity Severity { get; set; }
    public AlertConditionType ConditionType { get; set; }
    public string? LabelFilter { get; set; }
    public string ConditionJson { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<AlertEvent> Events { get; set; } = [];

    public AlertRuleDto ToDto() => new(
        Id, DataSourceId, Name, LabelFilter, Severity.ToString(),
        ConditionType.ToString(), ConditionJson, IsEnabled, CreatedAt);
}
