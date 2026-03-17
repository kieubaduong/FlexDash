namespace FlexDash.Api.Dtos;

public record DataSourceDto(Guid Id, string Name, string Type, string ConfigJson, int PollingIntervalSeconds, DateTime CreatedAt);
public record CreateDataSourceDto(string Name, string Type, string ConfigJson, int PollingIntervalSeconds, List<CreateAlertRuleInput> AlertRules);
public record CreateAlertRuleInput(string Name, string? LabelFilter, string Severity, string ConditionType, string ConditionJson);
public record UpdateDataSourceDto(string Name, string Type, string ConfigJson, int PollingIntervalSeconds);

public record DataPointDto(Guid DataSourceId, double Value, string? Label, DateTime Timestamp);

public record ValidateDataSourceDto(string Type, string ConfigJson);
public record TestConnectionDto(string ConfigJson);
public record ValidationResultDto(bool IsValid, string? ErrorMessage);
