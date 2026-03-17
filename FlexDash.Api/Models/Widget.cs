namespace FlexDash.Api.Models;

public enum WidgetType {
    LineChart,
    Gauge,
    StatCard,
    Table
}

public sealed class Widget {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DataSourceId { get; set; }
    public string Title { get; set; } = "";
    public WidgetType Type { get; set; }
    public int GridColumn { get; set; }
    public int GridRow { get; set; }
    public int GridWidth { get; set; } = 1;
    public int GridHeight { get; set; } = 1;
    public string? LabelFilter { get; set; }

    public WidgetDto ToDto() => new(
        Id, DataSourceId, Title,
        Type.ToString(), GridColumn, GridRow, GridWidth, GridHeight, LabelFilter);
}
