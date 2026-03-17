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

public class AlertEngineLabelFilterTests {
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
    [InlineData("memory", 86.0, true)]
    [InlineData("cpu", 15.0, false)]
    public async Task Threshold_With_LabelFilter_Evaluates_Matching_Points(
        string labelFilter, double matchingValue, bool shouldTrigger) {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "Alert",
            LabelFilter = labelFilter,
            Severity = AlertSeverity.Warning,
            ConditionType = AlertConditionType.Threshold,
            ConditionJson = """{"operator": ">", "value": 85.0}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        AlertEngine engine = CreateEngine(db);

        var points = new List<DataPointDto> {
            new(sourceId, 15.0, "cpu", DateTime.UtcNow),
            new(sourceId, matchingValue, "memory", DateTime.UtcNow),
            new(sourceId, 45.0, "disk", DateTime.UtcNow)
        };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        if (shouldTrigger)
            Assert.Single(events);
        else
            Assert.Empty(events);
    }

    [Fact]
    public async Task Threshold_Without_LabelFilter_Uses_All_Points() {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "Generic Alert",
            LabelFilter = null,
            Severity = AlertSeverity.Warning,
            ConditionType = AlertConditionType.Threshold,
            ConditionJson = """{"operator": ">", "value": 90.0}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        AlertEngine engine = CreateEngine(db);
        var points = new List<DataPointDto> {
            new(sourceId, 95.0, "cpu", DateTime.UtcNow)
        };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        Assert.Single(events);
    }

    [Fact]
    public async Task RateOfChange_With_LabelFilter_Only_Uses_Matching_Buffer_Points() {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "Memory Spike",
            LabelFilter = "memory",
            Severity = AlertSeverity.Warning,
            ConditionType = AlertConditionType.RateOfChange,
            ConditionJson = """{"percentChange": 20.0, "windowSeconds": 60}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        DataPointBuffer buffer = new();
        buffer.Add(sourceId, [
            new DataPointDto(sourceId, 10.0, "cpu", DateTime.UtcNow.AddSeconds(-50)),
            new DataPointDto(sourceId, 90.0, "cpu", DateTime.UtcNow.AddSeconds(-5)),
            new DataPointDto(sourceId, 40.0, "memory", DateTime.UtcNow.AddSeconds(-50)),
            new DataPointDto(sourceId, 80.0, "memory", DateTime.UtcNow.AddSeconds(-5))
        ]);

        AlertEngine engine = CreateEngine(db, buffer);
        var points = new List<DataPointDto> { new(sourceId, 80.0, "memory", DateTime.UtcNow) };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        Assert.Single(events);
        Assert.Contains("Memory Spike", events[0].Message);
    }

    [Fact]
    public async Task AbsenceOfData_With_LabelFilter_Checks_Only_Matching_Label() {
        AlertDbContext db = CreateInMemoryDb();
        Guid sourceId = Guid.NewGuid();

        var rule = new AlertRule {
            DataSourceId = sourceId,
            Name = "No Memory Data",
            LabelFilter = "memory",
            Severity = AlertSeverity.Critical,
            ConditionType = AlertConditionType.AbsenceOfData,
            ConditionJson = """{"timeoutSeconds": 30}"""
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();

        DataPointBuffer buffer = new();
        buffer.Add(sourceId, [
            new DataPointDto(sourceId, 50.0, "cpu", DateTime.UtcNow.AddSeconds(-5))
        ]);

        AlertEngine engine = CreateEngine(db, buffer);
        var points = new List<DataPointDto> { new(sourceId, 50.0, "cpu", DateTime.UtcNow) };

        List<AlertEventDto> events = await engine.EvaluateAsync(points, CancellationToken.None);

        Assert.Single(events);
        Assert.Contains("No Memory Data", events[0].Message);
    }
}
