# FlexDash User Guide

## Prerequisites

- .NET 10 SDK
- (Optional) SQLite CLI for inspecting the database

## Running the Application

### 1. Start the API

```bash
cd FlexDash.Api
dotnet run
```

The API starts at `https://localhost:7095`. On first run it creates `flexdash.db` and seeds a "Local System Metrics" data source with CPU, Memory, and Disk widgets plus alert rules.

OpenAPI docs: `https://localhost:7095/scalar/v1`

### 2. Start the Blazor Client

```bash
cd FlexDash.Client
dotnet run
```

The client starts at `https://localhost:7123`. Your browser opens automatically and redirects to the dashboard.

### Quick Start (Both Together)

Use the solution launch profile in Visual Studio: select **"API + Client"** from the launch dropdown.

---

## Creating a Data Source

1. Navigate to **Data Sources** in the sidebar.
2. Click **Add Data Source**.
3. Choose a **Type** and fill in the configuration:

| Type | Description | Config Fields |
|---|---|---|
| `RestApi` | Poll a REST endpoint for a JSON value | URL, ValuePath (e.g., `data.value`), Label, Headers |
| `SystemMetrics` | Monitor CPU, memory, and disk usage | Leave empty for local; fill SSH fields for remote |
| `WebSocketStream` | Stream real-time values from a WebSocket | WebSocket URL (ws:// or wss://), ValuePath, Label |

4. For **SystemMetrics with remote monitoring**:
   - Enter SSH Host, Port, Username
   - Provide either Password or Private Key Path
   - Click **Test Connection** to verify SSH connectivity
   - Supports both Linux and Windows remote machines (auto-detected)

5. Add at least one **Alert Rule** per data source:
   - **Name**: Descriptive name (e.g., "High CPU")
   - **Label Filter**: Optional — filter by data point label (e.g., "cpu", "memory", "disk")
   - **Severity**: Info, Warning, or Critical
   - **Condition Type**: Threshold, RateOfChange, or AbsenceOfData
   - **Condition Config**: JSON for the condition parameters

6. Click **Save**.

---

## Alert Condition Types

| Type | Config | Description |
|---|---|---|
| `Threshold` | `{"operator":">","value":90.0}` | Triggers when latest value crosses the threshold. Operators: `>`, `>=`, `<`, `<=`, `==` |
| `RateOfChange` | `{"percentChange":20.0,"windowSeconds":60}` | Triggers when percent change exceeds limit within a time window |
| `AbsenceOfData` | `{"timeoutSeconds":120}` | Triggers when no data is received within the timeout period |

### Label Filtering

When a data source produces multiple labeled metrics (like SystemMetrics: cpu, memory, disk), set the **Label Filter** on the alert rule to evaluate only the relevant metric. Without a label filter, the rule evaluates all data points from that source.

---

## Dashboard

The dashboard displays widgets in a responsive grid layout:

- **Line Chart** — Time-series plot of recent data points
- **Gauge** — Semi-circular gauge showing current percentage (0-100%)
- **Stat Card** — Large display of the latest value
- **Table** — Tabular view of recent data points

### Real-Time Updates

Widgets update automatically as new data arrives via SignalR. The sidebar shows an alert badge with the count of unacknowledged alerts. Click **Ack** on any alert to dismiss it.

### Widget Configuration

Navigate to **Configure Widgets** to add or remove widgets. Each widget is tied to a data source and can optionally filter by label.

### Drag-and-Drop

Widgets can be repositioned by dragging. Position changes are automatically saved.

---

## Running Tests

```bash
dotnet test tests/FlexDash.Tests
```

With coverage report:

```bash
dotnet test tests/FlexDash.Tests --collect:"XPlat Code Coverage"
```

**Current status:** 76 tests, 100% passing, 32.3% line coverage.

---

## Configuration

`FlexDash.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=flexdash.db"
  },
  "FlexDash": {
    "DataRetentionHours": 24,
    "PollingIntervalSeconds": 5
  }
}
```

Logs are written to `FlexDash.Api/logs/flexdash-YYYYMMDD.log` (rolling daily via Serilog).
