# Design Decisions

## 1. Plugin Pattern for Data Sources

**Decision:** `IDataSourcePlugin` adapter interface with one implementation per source type.

**Rationale:** Data sources are fundamentally heterogeneous (HTTP, SQL, OS APIs, WebSockets). Treating them uniformly behind a single interface allows the orchestrator to poll all sources with identical logic. Adding a new source type requires only implementing the interface — no changes to the orchestrator, controller, or worker.

**Trade-off:** Plugins are registered as singletons, so they must be thread-safe. `SystemMetricsPlugin` uses `SemaphoreSlim` + `ConcurrentDictionary` for thread-safe collector caching.

---

## 2. Ring Buffer for In-Memory Data

**Decision:** Use a fixed-capacity circular buffer (`RingBuffer<T>`) wrapped in `DataPointBuffer` instead of persisting every data point to SQLite.

**Rationale:**
- O(1) amortized writes — no database I/O on every poll cycle
- Bounded memory — fixed capacity (100 per source) prevents unbounded growth
- Automatic eviction — oldest data is naturally overwritten, no cleanup jobs needed
- Fast alert evaluation — `GetSince()` and `HasDataSince()` operate purely in-memory

**Trade-off:** Data is lost on restart. For a monitoring dashboard focused on real-time visibility, this is acceptable. Historical trend analysis beyond the buffer window would require a time-series database.

---

## 3. Three Separate DbContexts

**Decision:** Split EF Core into `WidgetDbContext`, `DataSourceDbContext`, and `AlertDbContext` rather than a single monolithic context.

**Rationale:** Each context has a clear, bounded responsibility. This prevents accidental cross-domain navigation properties and keeps each context's `OnModelCreating` focused. It also allows independent evolution — adding a new alert feature doesn't risk impacting widget persistence.

**Trade-off:** Slightly more configuration in `Program.cs`. All contexts point to the same SQLite file, so there is no data isolation at the storage level.

---

## 4. Singleton Services with Scoped DbContext via IServiceScopeFactory

**Decision:** `DataSourceOrchestrator` and `AlertEngine` are singletons but never hold a `DbContext` as a field. Instead, they accept `IServiceScopeFactory` and create a new scope (and therefore a new `DbContext`) per operation.

**Rationale:** `DbContext` is not thread-safe and must be scoped. The orchestrator needs to be a singleton because it holds the plugin dictionary and data point buffer for the application lifetime. `IServiceScopeFactory` bridges these conflicting lifetime requirements correctly.

**Trade-off:** Slightly more boilerplate than injecting DbContext directly, but correctness is non-negotiable here.

---

## 5. SignalR for Real-Time Push

**Decision:** Use SignalR with group-based subscriptions (`SourceId` → group name).

**Rationale:** SignalR is built into ASP.NET Core — no extra infrastructure. Group subscriptions mean a client only receives data for the sources its active dashboard needs, keeping bandwidth low. The hub methods `SubscribeToSource` / `UnsubscribeFromSource` give the client explicit control over what it receives.

**Trade-off:** `AllowCredentials()` in CORS is required for SignalR, which means the CORS policy must list explicit origins (no wildcard). Configured for Blazor dev ports `7123` / `5230`.

---

## 6. Composite Design for Metrics Collection

**Decision:** Unify local and remote metrics collection through `ICommandRunner` abstraction rather than separate code paths for local vs SSH.

**Rationale:** The parsing logic for CPU, memory, and disk is identical regardless of whether the command runs locally or over SSH. The only difference is *how* the command is executed. By abstracting command execution behind `ICommandRunner`, the same `LinuxMetricsCollector` and `WindowsMetricsCollector` work for both local and remote machines.

**Patterns applied:**
- **Composite/Strategy** — `ICommandRunner` with `LocalCommandRunner` and `SshCommandRunner`
- **Template Method** — `MetricsCollectorBase` defines the collection algorithm; subclasses provide OS-specific commands and parsers
- **Factory** — `MetricsCollectorFactory` creates the correct collector based on config and OS detection

**Trade-off:** The OS detection for remote machines (`uname -s`) adds one extra SSH command on first connection. This is acceptable as it happens once per collector lifecycle.

---

## 7. Blazor WASM (Standalone) Frontend

**Decision:** Pure client-side Blazor WebAssembly — no Blazor Server, no SSR hybrid.

**Rationale:** A monitoring dashboard is inherently stateful on the client (live chart data, SignalR connection). Client-side WASM means the server only serves the initial bundle and then the API — there is no persistent server-side Blazor circuit to maintain. The frontend can be deployed as a static bundle to any CDN or file host independent of the API.

**Trade-off:** Initial load is slower (downloading the .NET runtime). Not suitable for SEO-sensitive content. Acceptable for a dashboard accessed by authenticated operators.

---

## 8. LabelFilter on Alert Rules

**Decision:** Add optional `LabelFilter` to `AlertRule` so each rule evaluates only data points matching a specific label.

**Rationale:** A single SystemMetrics data source produces three labeled data points per poll: `cpu`, `memory`, `disk`. Without filtering, a "High Memory > 85%" rule would evaluate the latest point from *any* label — it might pick up `cpu=15%` and not trigger, even when memory is at 90%. `LabelFilter = "memory"` ensures the rule only evaluates memory data points.

**Trade-off:** Alert rules created before this feature have null LabelFilter and evaluate all points (backward-compatible). New rules should set LabelFilter for multi-series data sources.

---

## 9. Result<T> Monad for Error Handling

**Decision:** Use a value-type `Result<T>` struct for operations that can fail, instead of throwing exceptions.

**Rationale:** JSON deserialization, config validation, and value extraction are expected to fail on bad input — these are not exceptional conditions. `Result<T>` makes the success/failure path explicit in the type system, eliminating forgotten catch blocks and making the control flow visible.

**Trade-off:** Callers must check `IsOk` before calling `GetData()`. This is intentional — it forces handling the error case. The struct is a readonly value type, so there is zero allocation overhead.

---

## 10. Sealed Classes by Default

**Decision:** Mark all classes as `sealed` unless they are explicitly designed for inheritance.

**Rationale:** Sealed classes communicate design intent clearly: "this class is not an extension point." They also enable minor JIT optimizations (devirtualization). Only `MetricsCollectorBase` is left unsealed as it is specifically designed for subclassing via Template Method.

**Trade-off:** If a class later needs to be subclassed, the `sealed` modifier must be removed. This is a deliberate friction that forces the developer to make inheritance an intentional decision.

---

## 11. Dual-Mode Data Ingestion: Polling + Streaming

**Decision:** `IDataSourcePlugin` supports both `FetchAsync()` (polling) and `StreamAsync()` (real-time push), rather than polling only.

**Rationale:** Not all data sources are poll-friendly. WebSocket feeds push data continuously — polling would miss events between intervals. Conversely, system metrics are naturally polled. Each plugin chooses the appropriate ingestion mode. This also makes the system extensible: a future gRPC or MQTT plugin simply implements one or both methods.

**Trade-off:** Plugins that don't support streaming return `null` from `StreamAsync()`. The orchestrator checks for this and skips streaming. This is a minor null check, not a design burden.

---

## 12. No Raw Data Persistence — Only Alert Events

**Decision:** Raw data points are stored only in the in-memory RingBuffer (100 per source). They are intentionally not persisted to the database. Only alert events are persisted.

**Rationale:**
- A monitoring dashboard is not a logging system. Storing every data point from every source at 5-second intervals would produce ~17,000 rows/source/day — growing indefinitely for no operational value.
- The dashboard only displays the most recent data. The ring buffer provides exactly this with O(1) insertion and bounded memory.
- Alert events are the valuable artifact. When something goes wrong, what matters is: what triggered, when, at what value, and was it acknowledged?
- If long-term metrics are needed, that is the job of a dedicated time-series database (InfluxDB, Prometheus) — not an application database.

**Trade-off:** No historical trend analysis beyond the buffer window. This is acceptable for a real-time dashboard. The system can coexist with external storage systems.

---

## 13. Designed for Extension — Future-Proof Without Over-Engineering

**Decision:** All extension points use interfaces or abstract classes. Adding new capabilities never requires modifying existing code.

**Rationale:** The codebase is designed for real-world evolution:
- New data source type (gRPC, MQTT, Kafka) → implement `IDataSourcePlugin` (1 class + 1 DI registration)
- New OS for metrics (macOS) → subclass `MetricsCollectorBase` (1 class + 1 factory branch)
- New alert condition (anomaly detection) → create evaluator (1 class + 1 switch case)
- New command transport (WinRM) → implement `ICommandRunner` (1 class + 1 factory branch)
- New widget type (heatmap) → add Blazor component (1 file + 1 conditional in WidgetGrid)

**Trade-off:** The interfaces and abstractions exist today even though not all extension points are used yet. This is a small cost compared to the refactoring needed if the codebase were not designed for extension.
