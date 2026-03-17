using FlexDash.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertController : ControllerBase {
    private readonly AlertDbContext _db;

    public AlertController(AlertDbContext db) {
        _db = db;
    }
    [HttpGet("rules")]
    public async Task<ActionResult<List<AlertRuleDto>>> GetRules([FromQuery] Guid? dataSourceId = null) {
        IQueryable<AlertRule> query = _db.AlertRules;
        if (dataSourceId.HasValue) {
            query = query.Where(rule => rule.DataSourceId == dataSourceId.Value);
        }
        List<AlertRule> rules = await query.ToListAsync();
        return Ok(rules.Select(rule => rule.ToDto()).ToList());
    }

    [HttpGet("rules/{id:guid}")]
    public async Task<ActionResult<AlertRuleDto>> GetRule(Guid id) {
        AlertRule? rule = await _db.AlertRules.FindAsync(id);
        return rule is null ? NotFound() : Ok(rule.ToDto());
    }

    [HttpPost("rules")]
    public async Task<ActionResult<AlertRuleDto>> CreateRule(CreateAlertRuleDto dto) {
        if (!Enum.TryParse<AlertSeverity>(dto.Severity, out AlertSeverity severity)) {
            return BadRequest($"Unknown severity: {dto.Severity}");
        }
        if (!Enum.TryParse<AlertConditionType>(dto.ConditionType, out AlertConditionType condType)) {
            return BadRequest($"Unknown condition type: {dto.ConditionType}");
        }

        var rule = new AlertRule {
            DataSourceId = dto.DataSourceId,
            Name = dto.Name,
            LabelFilter = dto.LabelFilter,
            Severity = severity,
            ConditionType = condType,
            ConditionJson = dto.ConditionJson
        };

        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule.ToDto());
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<ActionResult<AlertRuleDto>> UpdateRule(Guid id, UpdateAlertRuleDto dto) {
        AlertRule? rule = await _db.AlertRules.FindAsync(id);
        if (rule is null) {
            return NotFound();
        }

        if (!Enum.TryParse<AlertSeverity>(dto.Severity, out AlertSeverity severity)) {
            return BadRequest($"Unknown severity: {dto.Severity}");
        }
        if (!Enum.TryParse<AlertConditionType>(dto.ConditionType, out AlertConditionType condType)) {
            return BadRequest($"Unknown condition type: {dto.ConditionType}");
        }

        rule.Name = dto.Name;
        rule.LabelFilter = dto.LabelFilter;
        rule.Severity = severity;
        rule.ConditionType = condType;
        rule.ConditionJson = dto.ConditionJson;
        rule.IsEnabled = dto.IsEnabled;
        await _db.SaveChangesAsync();
        return Ok(rule.ToDto());
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id) {
        AlertRule? rule = await _db.AlertRules.FindAsync(id);
        if (rule is null) {
            return NotFound();
        }

        _db.AlertRules.Remove(rule);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("events")]
    public async Task<ActionResult<List<AlertEventDto>>> GetEvents([FromQuery] int limit = 100) {
        List<AlertEvent> events = await _db.AlertEvents
            .Include(e => e.AlertRule)
            .OrderByDescending(alertEvent => alertEvent.TriggeredAt)
            .Take(limit)
            .ToListAsync();

        return Ok(events.Select(alertEvent => alertEvent.ToDto(alertEvent.AlertRule.Name)).ToList());
    }

    [HttpPost("events/{id:guid}/ack")]
    public async Task<IActionResult> AcknowledgeEvent(Guid id) {
        AlertEvent? alertEvent = await _db.AlertEvents.FindAsync(id);
        if (alertEvent is null) {
            return NotFound();
        }

        alertEvent.IsAcknowledged = true;
        alertEvent.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
