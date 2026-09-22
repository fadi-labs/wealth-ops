# TEST_FRAMEWORK_AGENTS.md

## TL;DR

Shared xunit.v3 test fixtures and helpers reused across the L0/L1/L2 test projects. This is a library (`IsTestProject=false`) — it contains no tests.

## Non-Negotiables

- **Keep it generic and domain-agnostic.** No references to feature code or concrete domain types; fixtures are reusable scaffolding only.
- **No `[Fact]`/`[Theory]` here.** `IsTestProject` is `false`; tests live in the `*.UnitTest` / `*.ComponentTest` / `*.IntegrationTest` projects that reference this one.

## Key Behaviors

- **`WebAppFixture<TProgram>`** wraps `WebApplicationFactory<TProgram>` and is generic over a Host's entry point. Because xunit.v3 compiles test assemblies as executables (each gets its own auto-generated `Program`), an integration test must reference the Host with `Aliases="HostApp"` and close the fixture as `WebAppFixture<HostApp::Program>` to avoid an ambiguous `Program`.
- **`ServiceProviderFixture`** builds an isolated `IServiceCollection`/`IServiceProvider` for L0/L1 tests and routes logging to the test output via `XUnitLoggerFactory`.
- **`XUnitLogger*`** bridges `ILogger` to xunit's `ITestOutputHelper`, with optional per-category minimum levels.
- **`PriorityOrderer` + `[TestPriority]`** order test cases when sequencing matters; opt in with `[TestCaseOrderer(typeof(PriorityOrderer))]` on the test class.
- **`WealthOpsTestDatabase.CreateAsync` takes an `initialize` callback rather than running migrations itself.** This project is referenced by *every* test project including `Domain.UnitTest`, whose whole point is having no persistence dependency — a project reference on Infrastructure here would drag EF Core and Npgsql into it. The caller supplies schema creation; see `PersistenceTestContext` in `Infrastructure.ComponentTest`.
- **The test PostgreSQL image is `pgvector/pgvector:pg17`**, not stock `postgres` — the application's initial migration issues `CREATE EXTENSION vector`. Container name and port are unchanged so CI and the wiki stay valid. **Gotcha:** a persistent container pre-warmed before that change lacks the extension and will be silently reused by the fixed-endpoint probe; `WealthOpsTestDatabase` detects the resulting PostgreSQL error and tells the developer to run `docker rm -f project-test-postgres`.
- **`AspireFixture` discovers the actual mapped host port** via `docker port` when the fixed port is not reachable. Some container runtimes (Rancher Desktop, for one) assign a random host port despite the requested `port:` — so never assume `15432` when connecting by hand; ask `docker port project-test-postgres 5432/tcp`.

## Changelog

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-05-30 | Created — lean fixtures (`ServiceProviderFixture`, `WebAppFixture<TProgram>`), xunit output logging, and test-case ordering helpers. | — |
| 2026-09-20 | Test PostgreSQL image → `pgvector/pgvector:pg17`. `WealthOpsTestDatabase` gained an `initialize` callback (replacing the migration TODO) and a named error for a stale non-pgvector container. | WT-001 |
