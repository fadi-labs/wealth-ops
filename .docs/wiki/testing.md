# Testing Strategy

## Test Levels

| Level | Label | Projects | Containers? | Description |
|---|---|---|---|---|
| L0 | Unit | `*.UnitTest` | None | Isolated logic, no I/O — pure in-process |
| L1 | Component | `Application.ComponentTest`, `Infrastructure.ComponentTest` | PostgreSQL + WireMock | End-to-end within a layer; real DB and HTTP stubs via Aspire |
| L2 | Integration | `Host.IntegrationTest` | PostgreSQL + WireMock | Full stack via `WebApplicationFactory` + Aspire containers |

## Test Infrastructure

Shared fixtures live in `tests/WealthOps.TestFramework/`. Container orchestration (PostgreSQL, WireMock) lives in `tests/WealthOps.TestFramework.Aspire/`.

### AspireFixture

`AspireFixture` provisions and shares test containers across all test assemblies in a process. It tries three strategies in order:

1. **Reuse** — if another fixture in the same process already initialised, adopt the shared state
2. **Fixed endpoints** — probe `127.0.0.1:15432` (Postgres) and `127.0.0.1:19091` (WireMock) — succeeds if containers are pre-warmed (CI or local `dotnet run --project tests/WealthOps.TestFramework.Aspire`)
3. **Docker port discovery** — query `docker`/`podman port` for the persistent named containers (`project-test-postgres`, `project-test-wiremock`)
4. **Start Aspire host** — provision fresh containers (takes ~30s on first run)

Container lifetimes are `Persistent` — they survive test runs and are reused on subsequent runs.

### WebAppFixture&lt;T&gt;

Base class for L2 integration tests. Initialises `AspireFixture`, then boots `WebApplicationFactory<TProgram>`. Override hooks:

- `EnrichConfigurationAsync(overrides)` — inject connection strings, WireMock base URL, etc.
- `PostInitializeAsync()` — run post-boot setup (e.g. trigger a sync cycle)
- `RecreateDatabaseOnInitialize` — set `true` to drop/recreate the database before the fixture starts
- `DatabaseName` — default is a Guid-suffixed name for isolation; override for deterministic names

### ProjectTestDatabase

Factory for per-test isolated databases in L1 Infrastructure tests. Drops/recreates a named database and returns a connection string handle. When EF Core migrations are added, extend `CreateAsync` to run migrations before returning.

### DatabaseResetter

Wraps Respawn for fast between-test data wipes without drop/recreate. Use in `IAsyncLifetime.DisposeAsync()` or an `AfterTest` hook.

### WireMockAdminClient

HTTP client for the WireMock admin API. Obtain via `AspireFixture.CreateWireMockAdminClient()`:

```csharp
await using var admin = aspire.CreateWireMockAdminClient();
await admin.StubJsonResponseAsync("GET", "/api/users", new[] { ... });
// ... run test ...
await admin.ResetAsync(); // clear stubs between tests
```

## Container Port Map

| Container | Local Port | Service |
|---|---|---|
| `project-test-postgres` | 15432 | PostgreSQL — image `pgvector/pgvector:pg17` |
| `project-test-wiremock` | 19091 | WireMock HTTP admin + stubbed endpoints |

> **The Postgres image must be pgvector-enabled.** The application's initial migration issues
> `CREATE EXTENSION vector`, and the L1 persistence tests round-trip a real `vector` column.
>
> If you pre-warmed `project-test-postgres` before the image changed from stock `postgres`, the
> stale container is still reachable on the fixed port and will be reused — migrations then fail on
> the missing extension. Recreate it once:
>
> ```bash
> docker rm -f project-test-postgres   # or: podman rm -f project-test-postgres
> ```
>
> **Ports are requested, not guaranteed.** Some runtimes (Rancher Desktop among them) publish a
> random host port regardless of the requested one; `AspireFixture` falls back to discovering it.
> When connecting by hand, ask rather than assume:
>
> ```bash
> docker port project-test-postgres 5432/tcp
> ```

## Collection Fixture Pattern

The `"Aspire"` xUnit collection shares one `AspireFixture` instance across all tests in a given assembly. Each L1/L2 test assembly must re-declare the collection:

```csharp
// AspireCollection.cs (in each L1/L2 test project)
[CollectionDefinition("Aspire")]
public sealed class AspireCollection : ICollectionFixture<AspireFixture>;
```

Tests opt in via `[Collection("Aspire")]` and receive `AspireFixture` via constructor injection.

## Running Tests

```bash
# All tests (L0 + L1 + L2) — requires Docker
dotnet test WealthOps.slnx

# L0 only — no containers required
dotnet test tests/WealthOps.Domain.UnitTest
dotnet test tests/WealthOps.Application.UnitTest
dotnet test tests/WealthOps.Infrastructure.UnitTest
dotnet test tests/WealthOps.Host.UnitTest

# L1 component tests
dotnet test tests/WealthOps.Application.ComponentTest
dotnet test tests/WealthOps.Infrastructure.ComponentTest

# L2 integration tests
dotnet test tests/WealthOps.Host.IntegrationTest

# Pre-warm containers (speeds up first test run)
dotnet run --project tests/WealthOps.TestFramework.Aspire
```
