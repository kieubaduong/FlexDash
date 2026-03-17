# FlexDash Architecture

## Overview

FlexDash is a real-time monitoring dashboard. The backend (ASP.NET Core 10) polls data sources via a plugin system and pushes updates via SignalR. The frontend (Blazor WASM) renders live charts with Chart.js.

## System Diagram

```
                    ┌──────────────────────┐
                    │    Blazor WASM App    │
                    │                      │
                    │  Dashboard  Widgets   │
                    │  Config     Alerts    │
                    └───┬─────────────┬────┘
                   HTTP │             │ SignalR
                        ▼             ▼
               ┌────────────────────────────┐
               │      ASP.NET Core API      │
               │                            │
               │  Controllers   Services    │
               │  SignalR Hub   AlertEngine  │
               └─────────┬─────────────────┘
                         │
            ┌────────────┼────────────┐
            ▼            ▼            ▼
      ┌──────────┐ ┌──────────┐ ┌──────────┐
      │ REST API │ │  System  │ │WebSocket │
      │  Plugin  │ │ Metrics  │ │  Stream  │
      └──────────┘ └────┬─────┘ └──────────┘
                        │
                  ┌─────┴─────┐
                  │ Local/SSH │
                  │  Runner   │
                  └─────┬─────┘
                  ┌─────┴─────┐
                  │ Linux │Win│
                  └───────────┘

  ┌──────────────────────────────────────────┐
  │               Data Pipeline              │
  │                                          │
  │  PollingWorker (5s)                      │
  │       │                                  │
  │       ▼                                  │
  │  Plugin.FetchAsync()                     │
  │       │                                  │
  │       ├──▶ RingBuffer (100 pts/source)   │
  │       ├──▶ AlertEngine.Evaluate()        │
  │       └──▶ SignalR push to clients       │
  └──────────────────────────────────────────┘

  ┌──────────────────────────────────────────┐
  │          SQLite (EF Core)                │
  │                                          │
  │  DataSources │ Widgets │ Alerts/Events   │
  └──────────────────────────────────────────┘
```

## Key Design Patterns

| Pattern | Where | Why |
|---------|-------|-----|
| Strategy | `IDataSourcePlugin` | Pluggable data sources |
| Composite | `ICommandRunner` | Local/SSH command execution |
| Template Method | `MetricsCollectorBase` | OS-specific metric collection |
| Ring Buffer | `DataPointBuffer` | Bounded O(1) in-memory storage |
| Result Monad | `Result<T>` | Error handling without exceptions |

## Technology Choices

| Concern | Choice | Reason |
|---------|--------|--------|
| Database | SQLite + EF Core | Zero-config, embedded |
| Real-time | SignalR | Built into ASP.NET Core |
| Frontend | Blazor WASM | Single .NET stack |
| Charts | Chart.js 4.4.0 | Lightweight, simple interop |
| Logging | Serilog | Structured, rolling file sink |
| SSH | SSH.NET 2024.2.0 | Remote server monitoring |
| Testing | xUnit + Coverlet | Standard .NET with coverage |
