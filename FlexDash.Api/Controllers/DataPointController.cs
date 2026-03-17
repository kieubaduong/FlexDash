using FlexDash.Api.Data;
using FlexDash.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexDash.Api.Controllers;

[ApiController]
[Route("api/datasources/{sourceId:guid}")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6960", Justification = "Fetch and GetDataPoints both operate on data points for a single source")]
public sealed class DataPointController : ControllerBase {
    private readonly DataSourceDbContext _db;
    private readonly DataSourceOrchestrator _orchestrator;
    private readonly DataPointBuffer _buffer;

    public DataPointController(DataSourceDbContext db, DataSourceOrchestrator orchestrator, DataPointBuffer buffer) {
        _db = db;
        _orchestrator = orchestrator;
        _buffer = buffer;
    }

    [HttpPost("fetch")]
    public async Task<ActionResult<List<DataPointDto>>> Fetch(Guid sourceId, CancellationToken ct) {
        DataSource? source = await _db.DataSources.FindAsync(sourceId);
        if (source is null) {
            return NotFound();
        }

        List<DataPointDto> points = await _orchestrator.FetchForSourceAsync(sourceId, ct);
        return Ok(points);
    }

    [HttpGet("datapoints")]
    public ActionResult<List<DataPointDto>> GetDataPoints(Guid sourceId, [FromQuery] int limit = 100) {
        List<DataPointDto> points = _buffer.Get(sourceId).TakeLast(limit).ToList();
        return Ok(points);
    }
}
