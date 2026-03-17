namespace FlexDash.Api.Dtos;

public record AlertRuleDto(Guid Id, Guid DataSourceId, string Name, string? LabelFilter, string Severity, string ConditionType, string ConditionJson, bool IsEnabled, DateTime CreatedAt);
public record CreateAlertRuleDto(Guid DataSourceId, string Name, string? LabelFilter, string Severity, string ConditionType, string ConditionJson);
public record UpdateAlertRuleDto(string Name, string? LabelFilter, string Severity, string ConditionType, string ConditionJson, bool IsEnabled);

public record AlertEventDto(Guid Id, Guid AlertRuleId, string AlertRuleName, string Message, double TriggerValue, bool IsAcknowledged, DateTime TriggeredAt, DateTime? AcknowledgedAt);
