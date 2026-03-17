using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Data;

public static class SeedData {
    private static readonly Guid SystemMetricsSourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TemperatureSourceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StockPriceSourceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid HeartRateSourceId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static async Task SeedAsync(DataSourceDbContext dataSourceDb, WidgetDbContext widgetDb, AlertDbContext alertDb) {
        if (await widgetDb.Widgets.AnyAsync()) {
            return;
        }

        // --- Data Sources ---

        dataSourceDb.DataSources.AddRange(
            new DataSource {
                Id = SystemMetricsSourceId,
                Name = "Local System Metrics",
                Type = DataSourceType.SystemMetrics,
                ConfigJson = "{}",
                PollingIntervalSeconds = 5
            },
            new DataSource {
                Id = TemperatureSourceId,
                Name = "Temperature Sensor (REST)",
                Type = DataSourceType.RestApi,
                ConfigJson = """{"Url":"http://localhost:5190/api/fake/temperature","ValuePath":"reading.temperature","Label":"temperature"}""",
                PollingIntervalSeconds = 5
            },
            new DataSource {
                Id = StockPriceSourceId,
                Name = "Stock Price (REST)",
                Type = DataSourceType.RestApi,
                ConfigJson = """{"Url":"http://localhost:5190/api/fake/stockprice","ValuePath":"data.price","Label":"price"}""",
                PollingIntervalSeconds = 3
            },
            new DataSource {
                Id = HeartRateSourceId,
                Name = "Heart Rate (WebSocket)",
                Type = DataSourceType.WebSocketStream,
                ConfigJson = """{"Url":"ws://localhost:5190/ws/fake/heartrate","ValuePath":"metric.heartRate","Label":"heartRate"}""",
                PollingIntervalSeconds = 0
            }
        );
        await dataSourceDb.SaveChangesAsync();

        // --- Alert Rules ---

        alertDb.AlertRules.AddRange(
            new AlertRule {
                DataSourceId = SystemMetricsSourceId,
                Name = "High CPU",
                LabelFilter = "cpu",
                Severity = AlertSeverity.Warning,
                ConditionType = AlertConditionType.Threshold,
                ConditionJson = """{"Operator":">","Value":90}"""
            },
            new AlertRule {
                DataSourceId = SystemMetricsSourceId,
                Name = "High Memory",
                LabelFilter = "memory",
                Severity = AlertSeverity.Warning,
                ConditionType = AlertConditionType.Threshold,
                ConditionJson = """{"Operator":">","Value":85}"""
            },
            new AlertRule {
                DataSourceId = SystemMetricsSourceId,
                Name = "No Data",
                Severity = AlertSeverity.Critical,
                ConditionType = AlertConditionType.AbsenceOfData,
                ConditionJson = """{"TimeoutSeconds":30}"""
            },
            new AlertRule {
                DataSourceId = TemperatureSourceId,
                Name = "High Temperature",
                LabelFilter = "temperature",
                Severity = AlertSeverity.Warning,
                ConditionType = AlertConditionType.Threshold,
                ConditionJson = """{"Operator":">","Value":33}"""
            },
            new AlertRule {
                DataSourceId = HeartRateSourceId,
                Name = "Elevated Heart Rate",
                LabelFilter = "heartRate",
                Severity = AlertSeverity.Warning,
                ConditionType = AlertConditionType.Threshold,
                ConditionJson = """{"Operator":">","Value":100}"""
            }
        );
        await alertDb.SaveChangesAsync();

        // --- Widgets ---
        // Row 0: System metrics
        // Row 1: Demo REST + WebSocket sources

        widgetDb.Widgets.AddRange(
            // Row 0 — System Metrics
            new Widget {
                DataSourceId = SystemMetricsSourceId,
                Title = "CPU Usage",
                Type = WidgetType.LineChart,
                GridColumn = 0,
                GridRow = 0,
                GridWidth = 2,
                GridHeight = 1,
                LabelFilter = "cpu"
            },
            new Widget {
                DataSourceId = SystemMetricsSourceId,
                Title = "Memory Usage",
                Type = WidgetType.Gauge,
                GridColumn = 2,
                GridRow = 0,
                GridWidth = 1,
                GridHeight = 1,
                LabelFilter = "memory"
            },
            new Widget {
                DataSourceId = SystemMetricsSourceId,
                Title = "Disk Usage",
                Type = WidgetType.StatCard,
                GridColumn = 3,
                GridRow = 0,
                GridWidth = 1,
                GridHeight = 1,
                LabelFilter = "disk"
            },

            // Row 1 — Demo REST + WebSocket
            new Widget {
                DataSourceId = TemperatureSourceId,
                Title = "Temperature",
                Type = WidgetType.Gauge,
                GridColumn = 0,
                GridRow = 1,
                GridWidth = 1,
                GridHeight = 1,
                LabelFilter = "temperature"
            },
            new Widget {
                DataSourceId = StockPriceSourceId,
                Title = "Stock Price (DEMO)",
                Type = WidgetType.LineChart,
                GridColumn = 1,
                GridRow = 1,
                GridWidth = 2,
                GridHeight = 1,
                LabelFilter = "price"
            },
            new Widget {
                DataSourceId = HeartRateSourceId,
                Title = "Heart Rate (Live)",
                Type = WidgetType.LineChart,
                GridColumn = 3,
                GridRow = 1,
                GridWidth = 1,
                GridHeight = 1,
                LabelFilter = "heartRate"
            }
        );
        await widgetDb.SaveChangesAsync();
    }
}
