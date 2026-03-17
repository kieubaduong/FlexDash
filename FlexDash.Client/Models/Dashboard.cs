namespace FlexDash.Client.Models;

public record WidgetDto(Guid Id, Guid DataSourceId, string Title, string Type, int GridColumn, int GridRow, int GridWidth, int GridHeight, string? LabelFilter);
public record CreateWidgetDto(Guid DataSourceId, string Title, string Type, int GridColumn, int GridRow, int GridWidth, int GridHeight, string? LabelFilter);
public record UpdateWidgetPositionDto(int GridColumn, int GridRow, int GridWidth, int GridHeight);
