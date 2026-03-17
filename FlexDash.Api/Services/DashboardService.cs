using FlexDash.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Services;

public sealed class DashboardService {
    private readonly WidgetDbContext _db;

    public DashboardService(WidgetDbContext db) {
        _db = db;
    }

    public async Task<List<WidgetDto>> GetWidgetsAsync() {
        List<Widget> widgets = await _db.Widgets.ToListAsync();
        return widgets.Select(widget => widget.ToDto()).ToList();
    }

    public async Task<WidgetDto?> AddWidgetAsync(CreateWidgetDto dto) {
        if (!Enum.TryParse<WidgetType>(dto.Type, out WidgetType widgetType)) {
            return null;
        }

        var widget = new Widget {
            DataSourceId = dto.DataSourceId,
            Title = dto.Title,
            Type = widgetType,
            GridColumn = dto.GridColumn,
            GridRow = dto.GridRow,
            GridWidth = dto.GridWidth,
            GridHeight = dto.GridHeight,
            LabelFilter = dto.LabelFilter
        };

        _db.Widgets.Add(widget);
        await _db.SaveChangesAsync();
        return widget.ToDto();
    }

    public async Task<WidgetDto?> UpdateWidgetPositionAsync(Guid widgetId, UpdateWidgetPositionDto dto) {
        Widget? widget = await _db.Widgets.FindAsync(widgetId);
        if (widget is null) {
            return null;
        }

        widget.GridColumn = dto.GridColumn;
        widget.GridRow = dto.GridRow;
        widget.GridWidth = dto.GridWidth;
        widget.GridHeight = dto.GridHeight;
        await _db.SaveChangesAsync();
        return widget.ToDto();
    }

    public async Task<bool> RemoveWidgetAsync(Guid widgetId) {
        Widget? widget = await _db.Widgets.FindAsync(widgetId);
        if (widget is null) {
            return false;
        }

        _db.Widgets.Remove(widget);
        await _db.SaveChangesAsync();
        return true;
    }
}
