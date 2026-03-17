namespace FlexDash.Api.Models;

public enum DataSourceType {
    RestApi,
    SystemMetrics,
    WebSocketStream
}

public sealed class DataSource {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DataSourceType Type { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public int PollingIntervalSeconds { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DataSourceDto ToDto() => new(
        Id, Name, Type.ToString(), ConfigJson, PollingIntervalSeconds, CreatedAt);
}
