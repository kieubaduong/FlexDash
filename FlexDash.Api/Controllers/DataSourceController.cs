using FlexDash.Api.Data;
using FlexDash.Api.Plugins.Metrics;
using FlexDash.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Controllers;

[ApiController]
[Route("api/datasources")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6960", Justification = "CRUD and Validate both operate on data source configuration")]
public sealed class DataSourceController : ControllerBase {
    private readonly DataSourceDbContext _db;
    private readonly AlertDbContext _alertDb;
    private readonly DataSourceOrchestrator _orchestrator;

    public DataSourceController(DataSourceDbContext db, AlertDbContext alertDb, DataSourceOrchestrator orchestrator) {
        _db = db;
        _alertDb = alertDb;
        _orchestrator = orchestrator;
    }
    [HttpGet]
    public async Task<ActionResult<List<DataSourceDto>>> GetAll() {
        List<DataSource> sources = await _db.DataSources.ToListAsync();
        return Ok(sources.Select(source => source.ToDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DataSourceDto>> GetById(Guid id) {
        DataSource? source = await _db.DataSources.FindAsync(id);
        return source is null ? NotFound() : Ok(source.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<DataSourceDto>> Create(CreateDataSourceDto dto) {
        if (!Enum.TryParse<DataSourceType>(dto.Type, out DataSourceType dsType)) {
            return BadRequest($"Unknown type: {dto.Type}");
        }

        if (dto.AlertRules is null || dto.AlertRules.Count == 0) {
            return BadRequest("At least one alert rule is required.");
        }

        var alertRules = new List<AlertRule>();
        foreach (CreateAlertRuleInput ruleInput in dto.AlertRules) {
            if (!Enum.TryParse<AlertSeverity>(ruleInput.Severity, out AlertSeverity severity)) {
                return BadRequest($"Unknown severity: {ruleInput.Severity}");
            }
            if (!Enum.TryParse<AlertConditionType>(ruleInput.ConditionType, out AlertConditionType condType)) {
                return BadRequest($"Unknown condition type: {ruleInput.ConditionType}");
            }
            alertRules.Add(new AlertRule {
                Name = ruleInput.Name,
                LabelFilter = ruleInput.LabelFilter,
                Severity = severity,
                ConditionType = condType,
                ConditionJson = ruleInput.ConditionJson
            });
        }

        var source = new DataSource {
            Name = dto.Name,
            Type = dsType,
            ConfigJson = dto.ConfigJson,
            PollingIntervalSeconds = dto.PollingIntervalSeconds
        };

        _db.DataSources.Add(source);
        await _db.SaveChangesAsync();

        foreach (AlertRule rule in alertRules) {
            rule.DataSourceId = source.Id;
        }
        _alertDb.AlertRules.AddRange(alertRules);
        await _alertDb.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = source.Id }, source.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DataSourceDto>> Update(Guid id, UpdateDataSourceDto dto) {
        DataSource? source = await _db.DataSources.FindAsync(id);
        if (source is null) {
            return NotFound();
        }

        if (!Enum.TryParse<DataSourceType>(dto.Type, out DataSourceType dsType)) {
            return BadRequest($"Unknown type: {dto.Type}");
        }

        source.Name = dto.Name;
        source.Type = dsType;
        source.ConfigJson = dto.ConfigJson;
        source.PollingIntervalSeconds = dto.PollingIntervalSeconds;
        await _db.SaveChangesAsync();
        return Ok(source.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) {
        DataSource? source = await _db.DataSources.FindAsync(id);
        if (source is null) {
            return NotFound();
        }

        _db.DataSources.Remove(source);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("validate")]
    public ActionResult<ValidationResultDto> Validate(ValidateDataSourceDto dto) {
        ValidationResultDto result = _orchestrator.ValidateConfig(dto.Type, dto.ConfigJson);
        return Ok(result);
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<ValidationResultDto>> TestConnection(TestConnectionDto dto, CancellationToken ct) {
        Result<SshConnectionConfig> parseResult = JsonHelper.TryDeserialize<SshConnectionConfig>(dto.ConfigJson);
        if (!parseResult.IsOk) {
            return Ok(new ValidationResultDto(false, parseResult.GetError()));
        }

        SshConnectionConfig config = parseResult.GetData();
        if (!config.IsRemote) {
            return Ok(new ValidationResultDto(false, "Host is required."));
        }

        string? error = await MetricsCollectorFactory.TestConnectionAsync(config, ct);
        return Ok(error is null
            ? new ValidationResultDto(true, null)
            : new ValidationResultDto(false, error));
    }
}
