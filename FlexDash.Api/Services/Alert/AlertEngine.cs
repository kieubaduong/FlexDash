using FlexDash.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Services.Alert;

public sealed class AlertEngine {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataPointBuffer _buffer;

    public AlertEngine(IServiceScopeFactory scopeFactory, DataPointBuffer buffer) {
        _scopeFactory = scopeFactory;
        _buffer = buffer;
    }

    public async Task<List<AlertEventDto>> EvaluateAsync(List<DataPointDto> newPoints, CancellationToken ct) {
        if (newPoints.Count == 0) {
            return [];
        }

        using var scope = _scopeFactory.CreateScope();
        AlertDbContext db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();

        List<Guid> sourceIds = newPoints.Select(point => point.DataSourceId).Distinct().ToList();
        List<AlertRule> rules = await db.AlertRules
            .Where(alertRule => alertRule.IsEnabled && sourceIds.Contains(alertRule.DataSourceId))
            .ToListAsync(ct);

        var newEvents = new List<AlertEventDto>();

        foreach (AlertRule rule in rules) {
            List<DataPointDto> points = newPoints
                .Where(point => point.DataSourceId == rule.DataSourceId)
                .Where(point => rule.LabelFilter is null || point.Label == rule.LabelFilter)
                .ToList();
            if (points.Count == 0 && rule.ConditionType != AlertConditionType.AbsenceOfData) {
                continue;
            }

            bool hasExisting = await db.AlertEvents
                .AnyAsync(@event => @event.AlertRuleId == rule.Id && !@event.IsAcknowledged, ct);
            if (hasExisting) {
                continue;
            }

            AlertEvent? triggered = rule.ConditionType switch {
                AlertConditionType.Threshold => ThresholdEvaluator.Evaluate(rule, points),
                AlertConditionType.RateOfChange => RateOfChangeEvaluator.Evaluate(rule, _buffer),
                AlertConditionType.AbsenceOfData => AbsenceOfDataEvaluator.Evaluate(rule, _buffer),
                _ => null
            };

            if (triggered is not null) {
                db.AlertEvents.Add(triggered);
                newEvents.Add(triggered.ToDto(rule.Name));
            }
        }

        if (newEvents.Count > 0) {
            await db.SaveChangesAsync(ct);
        }

        return newEvents;
    }
}
