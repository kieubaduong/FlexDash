using FlexDash.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexDash.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase {
    private readonly DashboardService _service;

    public DashboardController(DashboardService service) {
        _service = service;
    }

    [HttpGet("widgets")]
    public async Task<ActionResult<List<WidgetDto>>> GetWidgets()
        => Ok(await _service.GetWidgetsAsync());

    [HttpPost("widgets")]
    public async Task<ActionResult<WidgetDto>> AddWidget(CreateWidgetDto dto) {
        WidgetDto? result = await _service.AddWidgetAsync(dto);
        return result is null ? BadRequest() : Ok(result);
    }

    [HttpPut("widgets/{widgetId:guid}/position")]
    public async Task<ActionResult<WidgetDto>> UpdateWidgetPosition(Guid widgetId, UpdateWidgetPositionDto dto) {
        WidgetDto? result = await _service.UpdateWidgetPositionAsync(widgetId, dto);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("widgets/{widgetId:guid}")]
    public async Task<IActionResult> RemoveWidget(Guid widgetId) {
        bool removed = await _service.RemoveWidgetAsync(widgetId);
        return removed ? NoContent() : NotFound();
    }
}
