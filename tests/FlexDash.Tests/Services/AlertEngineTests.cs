using FlexDash.Api.Core;
using FlexDash.Api.Data;
using FlexDash.Api.Dtos;
using FlexDash.Api.Models;
using FlexDash.Api.Services;
using FlexDash.Api.Services.Alert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlexDash.Tests.Services;

public class AlertEngineTests {
    private static AlertDbContext CreateInMemoryDb() {
        DbContextOptions<AlertDbContext> options = new DbContextOptionsBuilder<AlertDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AlertDbContext(options);
    }

    private static AlertEngine CreateEngine(AlertDbContext db, DataPointBuffer? buffer = null) {
        IServiceScopeFactory scopeFactory = new TestScopeFactory<AlertDbContext>(db);
        return new AlertEngine(scopeFactory, buffer ?? new DataPointBuffer());
    }

    [Theory]
    [InlineData(95.0, true)]
    [InlineData(50.0, false)]
    [InlineData(90.0, false)]
    public async Task Threshold_Triggers_Based_On_Value(double value, bool shouldTrigger) {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "High CPU",
            Severity = AlertSeverity.Warning,
            ConditionType = AlertConditionType.Threshold,
            ConditionJson = """{"operator": ">", "value": 90.0}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        AlertEngine engine = CreateEngine(db);
        var points = new List<DataPointDto> { new(sourceId, value, "cpu", DateTime.UtcNow) };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        if (shouldTrigger) {
            Assert.Single(events);
            Assert.Equal(rule.Id, events[0].AlertRuleId);
        } else {
            Assert.Empty(events);
        }
    }

    [Fact]
    public async Task Threshold_Deduplicates_If_Unacknowledged_Event_Exists() {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "High CPU",
            Severity = AlertSeverity.Critical,
            ConditionType = AlertConditionType.Threshold,
            ConditionJson = """{"operator": ">", "value": 80.0}"""
        };
        db.AlertRules.Add(rule);
        db.AlertEvents.Add(new AlertEvent {
            AlertRuleId = rule.Id,
            Message = "existing",
            TriggerValue = 90,
            IsAcknowledged = false
        });
        await db.SaveChangesAsync();

        AlertEngine engine = CreateEngine(db);
        var points = new List<DataPointDto> { new(sourceId, 95.0, "cpu", DateTime.UtcNow) };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        Assert.Empty(events);
    }

    [Theory]
    [InlineData(20.0, 40.0, 80.0, true)]
    [InlineData(50.0, 50.0, 55.0, false)]
    public async Task RateOfChange_Triggers_Based_On_Percent(
        double percentThreshold, double oldValue, double newValue, bool shouldTrigger) {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "CPU Spike",
            Severity = AlertSeverity.Warning,
            ConditionType = AlertConditionType.RateOfChange,
            ConditionJson = $$"""{"percentChange": {{percentThreshold}}, "windowSeconds": 60}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        DataPointBuffer buffer = new();
        buffer.Add(sourceId,
        [
            new DataPointDto(sourceId, oldValue, "cpu", DateTime.UtcNow.AddSeconds(-50)),
            new DataPointDto(sourceId, newValue, "cpu", DateTime.UtcNow.AddSeconds(-5))
        ]);

        AlertEngine engine = CreateEngine(db, buffer);
        var points = new List<DataPointDto> { new(sourceId, newValue, "cpu", DateTime.UtcNow) };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        if (shouldTrigger)
            Assert.Single(events);
        else
            Assert.Empty(events);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AbsenceOfData_Triggers_Based_On_Data_Presence(bool hasRecentData, bool shouldTrigger) {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "No Data",
            Severity = AlertSeverity.Critical,
            ConditionType = AlertConditionType.AbsenceOfData,
            ConditionJson = """{"timeoutSeconds": 120}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        DataPointBuffer buffer = new();
        if (hasRecentData) {
            buffer.Add(sourceId,
            [
                new DataPointDto(sourceId, 10.0, "cpu", DateTime.UtcNow.AddSeconds(-10))
            ]);
        }

        AlertEngine engine = CreateEngine(db, buffer);
        var points = new List<DataPointDto> { new(sourceId, 0, null, DateTime.UtcNow) };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        if (shouldTrigger)
            Assert.Contains(events, e => e.AlertRuleId == rule.Id);
        else
            Assert.Empty(events);
    }
}

// Minimal test helper to provide IServiceScopeFactory
internal class TestScopeFactory<T>(T db) : IServiceScopeFactory where T : class {
    public IServiceScope CreateScope() => new TestScope<T>(db);
}

internal class TestScope<T>(T db) : IServiceScope where T : class {
    public IServiceProvider ServiceProvider { get; } = new TestServiceProvider<T>(db);
    public void Dispose() { }
}

internal class TestServiceProvider<T>(T db) : IServiceProvider where T : class {
    public object? GetService(Type serviceType) {
        if (serviceType == typeof(T)) {
            return db;
        }

        return null;
    }
}
