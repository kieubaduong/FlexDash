# FlexDash — Project Context for Claude Code

## What is FlexDash?

A real-time monitoring dashboard. It **reads data from external sources** (REST APIs, SQL databases, system metrics, WebSocket streams) and displays them in customizable widgets (charts, gauges, tables). It does NOT create data — it's a consumer/aggregator.

**Internal schema:** All external data gets converted to `DataPoint(DataSourceId, Timestamp, Value, Label?)` via adapter plugins. Every widget speaks DataPoint.

## Tech Stack

- **Backend:** ASP.NET Core Web API, .NET 10, C# 14, EF Core 10, SQLite, SignalR, Serilog
- **Frontend:** Blazor WebAssembly Standalone App, .NET 10 (100% client-side rendering, zero SSR)
- **No shared project** — frontend and backend are fully decoupled, communicate via HTTP REST + SignalR WebSocket
- **Testing:** xUnit (backend), bUnit (frontend)

## Solution Structure (Visual Studio 2026)

```
FlexDash/                         ← solution folder
├── FlexDash.sln
├── FlexDash.Api/                 ← ASP.NET Core Web API project
│   ├── Controllers/
│   │   ├── DashboardController.cs
│   │   ├── DataSourceController.cs
│   │   └── AlertController.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Models/
│   │   ├── Entities.cs            # Dashboard, Widget, DataSource, DataPoint, AlertRule, AlertEvent
│   │   ├── Enums.cs               # WidgetType, DataSourceType, AlertSeverity, AlertConditionType
│   │   └── Dtos.cs                # Request/Response DTOs
│   ├── Services/
│   │   ├── DashboardService.cs
│   │   ├── DataSourceOrchestrator.cs
│   │   └── AlertEngine.cs
│   ├── Plugins/
│   │   ├── IDataSourcePlugin.cs   # The ONE interface — adapter pattern
│   │   ├── RestApiPlugin.cs
│   │   ├── SqlQueryPlugin.cs
│   │   ├── SystemMetricsPlugin.cs
│   │   └── WebSocketStreamPlugin.cs
│   ├── Hubs/
│   │   └── DashboardHub.cs        # SignalR hub for real-time push
│   ├── Workers/
│   │   └── PollingWorker.cs       # BackgroundService — poll → persist → evaluate alerts → push
│   ├── Program.cs
│   ├── appsettings.json
│   └── FlexDash.Api.csproj
│
├── FlexDash.Client/              ← Blazor WebAssembly Standalone App (CSR only)
│   ├── Pages/
│   │   ├── Home.razor
│   │   ├── DashboardView.razor
│   │   ├── DashboardConfig.razor
│   │   └── DataSourceManager.razor
│   ├── Components/
│   │   ├── Widgets/
│   │   │   ├── LineChartWidget.razor
│   │   │   ├── GaugeWidget.razor
│   │   │   ├── StatCardWidget.razor
│   │   │   └── TableWidget.razor
│   │   ├── WidgetGrid.razor       # Drag-drop grid layout
│   │   ├── AlertPanel.razor
│   │   └── Sidebar.razor
│   ├── Services/
│   │   ├── ApiService.cs          # HttpClient calls to backend REST
│   │   └── SignalRService.cs      # HubConnection for real-time data
│   ├── Models/
│   │   ├── Dashboard.cs           # Frontend TypeScript-like interfaces (own copy, not shared)
│   │   ├── DataSource.cs
│   │   └── Alert.cs
│   ├── Layout/
│   │   └── MainLayout.razor
│   ├── wwwroot/
│   │   ├── css/
│   │   └── index.html
│   ├── Program.cs
│   ├── _Imports.razor
│   └── FlexDash.Client.csproj
│
├── tests/
│   └── FlexDash.Tests/
│       ├── Services/
│       ├── Plugins/
│       └── FlexDash.Tests.csproj
│
└── docs/
    ├── ARCHITECTURE.md
    ├── DESIGN-DECISIONS.md
    ├── USER-GUIDE.md
    └── ai-assistance/
```

## Architecture Decisions

1. **No Clean Architecture / no repository pattern** — EF Core IS the repository. Wrapping it adds ceremony without value for this project size.
2. **One interface only: `IDataSourcePlugin`** — Adapter pattern. Each plugin converts external data format into internal `DataPoint`. This is the ONE justified abstraction because data sources are a real, open-ended extension point.
3. **Services use DbContext directly** — DashboardService, AlertEngine inject AppDbContext, no extra layers.
4. **PollingWorker (BackgroundService)** drives the main loop: poll enabled sources → persist DataPoints → evaluate alert rules → push updates via SignalR.
5. **Frontend is a pure SPA** — Blazor WASM standalone, 100% client-side rendering, talks to backend only via HTTP + SignalR. No server-side rendering.

## Key Classes

### Backend

**IDataSourcePlugin** (the one interface):
```csharp
public interface IDataSourcePlugin
{
    DataSourceType Type { get; }
    ValidationResult ValidateConfig(string configJson);
    Task<List<DataPointDto>> FetchAsync(string configJson, CancellationToken ct = default);
    IAsyncEnumerable<DataPointDto>? StreamAsync(string configJson, CancellationToken ct = default);
}
```

**DataPoint** (internal schema — everything converts to this):
```csharp
public class DataPoint
{
    public long Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
}
```

**DataSourceOrchestrator**: Routes requests to correct plugin via `Dictionary<DataSourceType, IDataSourcePlugin>`. Handles polling all enabled sources, persisting results, pruning old data.

**AlertEngine**: Evaluates alert rules (Threshold, RateOfChange, AbsenceOfData) against recent DataPoints. Pure evaluation functions — no side effects in the logic itself.

**PollingWorker**: `BackgroundService` that runs every 5s — calls Orchestrator.PollAllAsync() → AlertEngine.EvaluateAsync() → pushes results via SignalR hub.

**DashboardHub**: SignalR hub. Clients subscribe to specific data source groups. Worker pushes `WidgetDataUpdate` and `AlertEventDto` to subscribed clients.

### Frontend (Blazor WASM)

**ApiService**: `HttpClient` wrapper calling backend REST endpoints. Returns data to components.

**SignalRService**: Manages `HubConnection` to `DashboardHub`. Exposes real-time data updates and alert events to components.

**Widgets**: Each widget type (LineChart, Gauge, StatCard, Table) is a Razor component that receives `DataPointDto[]` and renders accordingly. Chart rendering via Chart.js JS interop.

## API Endpoints

```
GET    /api/dashboards              — list all dashboards
GET    /api/dashboards/{id}         — get dashboard with widgets
POST   /api/dashboards              — create dashboard
PUT    /api/dashboards/{id}         — update dashboard
DELETE /api/dashboards/{id}         — delete dashboard

POST   /api/dashboards/{id}/widgets — add widget to dashboard
PUT    /api/widgets/{id}/position   — move/resize widget
DELETE /api/widgets/{id}            — remove widget

GET    /api/datasources             — list all data sources
POST   /api/datasources             — create data source
POST   /api/datasources/validate    — validate config before saving
POST   /api/datasources/{id}/fetch  — manual fetch (test)
PUT    /api/datasources/{id}        — update data source
DELETE /api/datasources/{id}        — delete data source

GET    /api/alerts/rules            — list alert rules
POST   /api/alerts/rules            — create alert rule
PUT    /api/alerts/rules/{id}       — update alert rule
DELETE /api/alerts/rules/{id}       — delete alert rule
GET    /api/alerts/events           — recent alert events
POST   /api/alerts/events/{id}/ack  — acknowledge alert

SignalR Hub: /hubs/dashboard
  - SubscribeToSource(sourceId)
  - UnsubscribeFromSource(sourceId)
  - Client receives: "ReceiveData" (WidgetDataUpdate), "ReceiveAlert" (AlertEventDto)
```

## Database Schema (EF Core 10 + SQLite)

```
Dashboard       1 ──── * Widget
DataSource      1 ──── * Widget
DataSource      1 ──── * DataPoint
DataSource      1 ──── * AlertRule
AlertRule       1 ──── * AlertEvent

Dashboard: Id, Name, Description, Columns, RowHeight, CreatedAt, UpdatedAt
Widget: Id, DashboardId(FK), DataSourceId(FK), Type, Title, Config(JSON), X, Y, Width, Height
DataSource: Id, Name, Type(enum), Config(JSON), PollingIntervalSeconds, IsEnabled, CreatedAt
DataPoint: Id(autoincrement), DataSourceId(FK), Timestamp, Value, Label — index on (DataSourceId, Timestamp)
AlertRule: Id, DataSourceId(FK), Name, ConditionType(enum), ConditionConfig(JSON), Severity(enum), IsEnabled
AlertEvent: Id, AlertRuleId(FK), TriggeredAt, Value, Message, Severity(enum), IsAcknowledged
```

## Data Source Plugin Configs (JSON examples)

**REST API:**
```json
{
  "url": "https://api.example.com/metrics",
  "valuePath": "data.cpu.percent",
  "label": "CPU Usage",
  "headers": { "Authorization": "Bearer xxx" }
}
```

**SQL Query:**
```json
{
  "connectionString": "Data Source=mydb.db",
  "query": "SELECT count(*) as cnt FROM orders WHERE date = date('now')",
  "valueColumn": "cnt",
  "labelColumn": null,
  "timestampColumn": null
}
```

**System Metrics:**
```json
{
  "metrics": ["cpu", "memory", "disk"],
  "sourceId": "11111111-1111-1111-1111-111111111111"
}
```

**WebSocket/SSE:**
```json
{
  "url": "wss://stream.example.com/prices",
  "valuePath": "price",
  "protocol": "websocket"
}
```

## Code Style Requirements

- **Max 3 levels of nesting** — use guard clauses and early returns
- **Meaningful names** — `FetchLatestMetricsAsync` not `GetData`
- **Small methods** — under 20 lines each
- **No code duplication** — extract shared logic
- **Pure functions where possible** — mark static, no side effects
- **Immutable DTOs** — use `record` types
- **async/await all the way** — no `.Result` or `.Wait()`
- **CancellationToken propagated** through entire call chain
- **No exceptions for control flow** — return null or result objects for expected failures
- **EF Core directly in services** — no repository wrappers

## NuGet Packages (Backend)

- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.EntityFrameworkCore.Design
- Microsoft.AspNetCore.SignalR
- Serilog.AspNetCore
- Serilog.Sinks.File

## NuGet Packages (Frontend - Blazor WASM)

- Microsoft.AspNetCore.SignalR.Client
- (Chart.js via JS interop from CDN)

## Seed Data

On first run, auto-create a demo dashboard "System Overview" with:
- DataSource: "Local System Metrics" (SystemMetrics plugin, poll every 5s)
- Widget: CPU Usage (LineChart), Memory Usage (Gauge), Disk Usage (StatCard)

## What This Assessment Is For

- Company: Wanzl (retail/logistics)
- Role: .NET Developer
- Philosophy: "You Build, I Buy" — build a real product, not a coding exercise
- Evaluating: UX, performance, documentation, code quality, architecture decisions
- Timeline: 7 days
- Must include: GitHub repo, comprehensive markdown docs, AI assistance transparency in /docs/ai-assistance/
