# AI Assistance Transparency

## Tools Used

- **Claude Code** (claude-sonnet-4-6 and claude-opus-4-6) via Anthropic's CLI
- **Serena MCP** — semantic code analysis and editing tools integrated with Claude Code

## What Was AI-Generated

All source files in this repository were generated or refined through collaboration with Claude Code following architectural specifications and coding standards authored by the project owner. This includes:

- All C# backend files (models, entities, DTOs, plugins, services, controllers, hub, worker, Program.cs)
- All Blazor/Razor frontend files (components, pages, layouts, services)
- JavaScript Chart.js interop bridge (`chartInterop.js`)
- CSS layout and component styles (`app.css`)
- Test files (xUnit tests — 76 tests across 13 test files)
- Documentation files (TECHNICAL-REPORT.md, ARCHITECTURE.md, DESIGN-DECISIONS.md, USER-GUIDE.md)

## What Was Human-Authored

### Architecture & Vision
- The project specification and product vision
- Technology choices (SQLite, SignalR, Blazor WASM, Chart.js, Serilog, plugin architecture)
- Feature prioritization (e.g., composite design for metrics, LabelFilter for alerts)
- Data storage strategy (in-memory ring buffer for data points, persist only alerts — not raw logs)

### Coding Principles (Enforced by Human Review)

The following coding standards were defined and enforced by the project owner through iterative feedback and code review. The AI was corrected when it violated these rules:

1. **Maximum 3 levels of nesting.** Two techniques to achieve this:
   - **Guard clause pattern** — reject invalid state early with `return`, keeping main logic shallow
   - **Extract smaller functions** — each function has one clear mission (e.g., `TryConnectAsync`, `TryReceiveAsync`, `TryParseValue` extracted from a monolithic `StreamAsync`)

2. **Descriptive variable naming — no abbreviations, no single characters, including LINQ lambdas.** AI-generated code initially used short lambda parameter names (`p => p.Timestamp`, `s => s.ToDto()`, `e => e.TriggeredAt`, `l => l.StartsWith()`, `w => w.ToDto()`). The human reviewer caught these during nitpick code review and required all lambda parameters to use full descriptive names: `point => point.Timestamp`, `source => source.ToDto()`, `alertEvent => alertEvent.TriggeredAt`, `line => line.StartsWith()`, `widget => widget.ToDto()`. This rule applies everywhere — LINQ chains, `ToDictionary(plugin => plugin.Type)`, `GroupBy(point => point.DataSourceId)`, even single-expression lambdas like `.Select(rule => rule.ToDto())`. Class names read as English (`DataPointBuffer`, `MetricsCollectorFactory`).

3. **Define scope/mission clearly for Single Responsibility.** Every class must be describable in one sentence. When a class accumulates responsibilities, they are extracted:
   - `MapRuleToDto`/`MapEventToDto` moved out of `AlertController` → `ToDto()` on model classes
   - Alert evaluators split from `AlertEngine` → `ThresholdEvaluator`, `RateOfChangeEvaluator`, `AbsenceOfDataEvaluator`
   - `WebSocketStreamPlugin.StreamAsync()` split into `TryConnectAsync`, `TryReceiveAsync`, `TryParseValue`

4. **Design for scalability without over-engineering.** The plugin interface supports both polling (`FetchAsync`) and streaming (`StreamAsync`) so that future data sources (gRPC, MQTT, Kafka) can be added by implementing one interface — no modification to existing code.

5. **No primary constructors.** Traditional constructor + `private readonly` field pattern throughout for explicit dependency visibility and debuggability.

6. **Sealed classes by default.** Only `MetricsCollectorBase` is unsealed (it is designed for subclassing via Template Method).

7. **Store only what matters.** Data points are transient (in-memory ring buffer). Only alert events are persisted to the database. A monitoring dashboard is not a logging system.

### Design Decisions (Human-Directed)
- Composite design for unified local/remote metrics (not separate code paths)
- LabelFilter on AlertRule (discovered during testing when memory alerts failed to trigger)
- Dual-mode ingestion (polling + streaming) for flexibility
- Three separate DbContexts for bounded responsibility
- Ring buffer for O(1) bounded-memory data storage

## Human Code Review — Nitpick Level of Detail

The human reviewer treated every AI-generated line of code as a pull request that must pass rigorous code review. Examples of issues caught and corrected:

### LINQ Lambda Parameter Naming
AI initially generated standard short-form lambdas (`s =>`, `r =>`, `e =>`, `p =>`, `w =>`, `l =>`). The human caught every instance across 11+ files and required full descriptive names:

```csharp
// AI-generated (rejected in review):
sources.Select(s => s.ToDto())
events.OrderByDescending(e => e.TriggeredAt)
newPoints.GroupBy(p => p.DataSourceId)
plugins.ToDictionary(p => p.Type)
ring.Any(p => p.Timestamp >= since)

// Human-corrected:
sources.Select(source => source.ToDto())
events.OrderByDescending(alertEvent => alertEvent.TriggeredAt)
newPoints.GroupBy(point => point.DataSourceId)
plugins.ToDictionary(plugin => plugin.Type)
ring.Any(point => point.Timestamp >= since)
```

**Result:** Zero single-character lambda parameters in the entire codebase.

### Primary Constructor Removal
AI used C# 12 primary constructors across 16 classes. The human reviewed and explicitly directed removal of every single one, requiring traditional constructor + `private readonly` field pattern for debuggability and explicitness.

### Sonar Violations
Human ran SonarQube analysis and directed fixes for specific rules:
- S3776: Cognitive complexity in `WebSocketStreamPlugin.StreamAsync()` → extracted 3 methods
- S108: Empty catch block → added structured logging
- S4487: Unused `_config` field in `SshCommandRunner` → removed

### N+1 Query Problem in AlertEngine
The initial AI-generated AlertEngine had a classic **N+1 query problem**. For each incoming data point, it queried the database to fetch matching alert rules:

```csharp
// AI-generated (N+1 — rejected in review):
foreach (DataPointDto point in newPoints) {
    List<AlertRule> rules = await db.AlertRules
        .Where(rule => rule.IsEnabled && rule.DataSourceId == point.DataSourceId)
        .ToListAsync(ct);
    // evaluate each rule against this point...
}
```

With SystemMetrics producing 3 data points per poll (cpu, memory, disk) and potentially dozens of data sources, this would fire **N database queries per polling cycle** (every 5 seconds). At 10 data sources x 3 points each = 30 queries per cycle, 360 queries per minute — for what should be a single query.

**Human-directed fix:** Batch-load all relevant rules in a single query, then filter in-memory:

```csharp
// Human-corrected (single query):
List<Guid> sourceIds = newPoints.Select(point => point.DataSourceId).Distinct().ToList();
List<AlertRule> rules = await db.AlertRules
    .Where(alertRule => alertRule.IsEnabled && sourceIds.Contains(alertRule.DataSourceId))
    .ToListAsync(ct);

foreach (AlertRule rule in rules) {
    List<DataPointDto> points = newPoints
        .Where(point => point.DataSourceId == rule.DataSourceId)
        .Where(point => rule.LabelFilter is null || point.Label == rule.LabelFilter)
        .ToList();
    // evaluate...
}
```

**Result:** Exactly **1 database query** per polling cycle regardless of how many data sources or points. The in-memory filtering of `newPoints` against rules is O(N*M) but on tiny datasets (typically <100 points, <20 rules) this is negligible.

### Alert Engine Bug
Human discovered on the running dashboard that "High Memory > 85%" alert didn't trigger despite memory at 86%. Root cause: the engine evaluated the latest point from *any* label, not just memory. Human directed the `LabelFilter` feature addition and verified the fix.

### Architecture Direction
Human rejected the initial separate-class approach for local vs remote metrics and directed the composite design through iterative discussion:
- "liệu ta có cần phải tách ra 2 class local và remote không?" (Do we need separate local and remote classes?)
- "tôi nghĩ ta phải thống nhất" (I think we need to unify)
- This led to the `ICommandRunner` abstraction — same collectors work for both local and SSH

## Iterative Development Process

The project was built through multiple conversational iterations, not in a single generation. Key iterative cycles:

1. **Initial scaffold** — Core architecture, plugin system, basic CRUD
2. **Alert engine fix** — Discovered LabelFilter was needed when memory alerts didn't trigger despite high values (human noticed the bug on the dashboard)
3. **SSH remote monitoring** — Evolved from separate local/remote classes → unified composite design through human-directed discussion ("liệu ta có cần phải tách ra 2 class local và remote không?")
4. **Test coverage expansion** — Went from initial tests to 76 tests covering core, plugins, services, and metrics collectors
5. **Sonar compliance** — Addressed S3776 (cognitive complexity via extract method), S108 (empty catch → logged), S4487 (unused fields removed)
6. **Code style enforcement** — Human explicitly directed: remove all primary constructors (16 classes), add sealed modifiers, enforce braces on single-line control statements

## Review and Validation

The generated code has been reviewed against the specification for:

- Correct implementation of the plugin interface contract
- Proper singleton/scoped lifetime separation (IServiceScopeFactory pattern)
- CORS + SignalR `AllowCredentials()` configuration
- Alert deduplication logic and LabelFilter correctness
- Thread safety (RingBuffer with Lock, ConcurrentDictionary, SemaphoreSlim)
- AbsenceOfData edge case (must evaluate even when no matching points arrive)
- Cross-platform metrics parsing (Linux /proc vs Windows PowerShell)
- Nesting depth compliance (max 3 levels)
- Naming conventions (no abbreviations, no single-char variables)
- All 76 tests passing with 32.3% line coverage

## Notes

- The code uses idiomatic C# 12/.NET 10 features: collection expressions (`[]`), record types, file-scoped namespaces
- Traditional constructors are used throughout (no primary constructors) per explicit project convention
- All classes are sealed unless designed for inheritance (only `MetricsCollectorBase` is unsealed)
- No hallucinated NuGet packages were used; all packages are real and at the specified versions
- The `SystemMetricsPlugin` CPU baseline behavior (first call returns 0%) is intentional — delta calculation requires two readings
