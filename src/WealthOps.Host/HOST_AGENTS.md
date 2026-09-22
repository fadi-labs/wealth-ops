# HOST_AGENTS.md

## TL;DR

ASP.NET Core composition root (Minimal API). In v1 it maps `GET /health` and nothing else — the CLI is the operator surface (ADR-001).

## Non-Negotiables

- **Keep business logic out of Host.** Endpoints translate HTTP to a Mediator request and back; they contain no domain or orchestration logic.
- **One endpoint per use case** under `Endpoints/`; cross-cutting composition (DI, middleware, observability, problem-details) lives in `Configuration/`.
- **`Program` ends with `public partial class Program { }`** so integration tests can target it via `WebApplicationFactory<Program>`.
- **References Application, Domain, and Infrastructure** — it is the only ASP.NET project that composes all layers.
- **Do not map `GET /`.** The integration smoke test asserts a 404 there as proof the pipeline booted. Mapping it silently removes that signal.
- **Compose only through `AddWealthOps()`** (`AddApplication` + `AddInfrastructure`) — the same pair the CLI calls. Registering a service directly here lets the two composition roots drift.

## Key Behaviors

- **`/health` reports database state only.** The model endpoint is deliberately excluded: local inference is an external host process the Host neither owns nor uses, so folding it in would report unhealthy for something irrelevant to this process. The full picture is `wealthops status`.
- **`/health` returns 503 with the same payload shape when unhealthy**, so a probe can branch on status code and a human can read the reason from one response.
- **Host does not run migrations.** Only the CLI does (`chat`/`ingest`). A Host booted against an unmigrated database reports `pendingMigrations > 0` from `/health` rather than mutating the schema.
- **Booting the Host requires complete, valid `WealthOps:*` configuration**, because `AddApplication` validates on start (ADR-003). That is why `HostWebAppFixture` supplies the whole surface — and it doubles as a standing check that a key added for the CLI was not forgotten here.

## Test References

L2 — `tests/WealthOps.Host.IntegrationTest/`: `SmokeTests` (un-routed 404, `/health` against a migrated database, `/health` independent of the model endpoint), `HostWebAppFixture` (real PostgreSQL via `AspireFixture`, synthetic configuration, migration applied in `PostInitializeAsync`).

## Migration Plans

Host stays minimal until a real consumer exists. FS-22 (web interface) is the trigger for real endpoints; at that point the CLI should still compose in-process rather than call Host over HTTP (ADR-001).

## Changelog

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-05-30 | Created — minimal runnable Host (`Program.cs`, `appsettings(.Development).json`, `Properties/launchSettings.json`) with empty `Configuration/`, `Endpoints/`, `HealthChecks/`, `Workers/`. | — |
| 2026-09-20 | Composed via `AddWealthOps()` and mapped `GET /health` (database reachability, pgvector presence, pending migration count; 503 when unhealthy). `GET /` remains unmapped by design. | WT-001 |
