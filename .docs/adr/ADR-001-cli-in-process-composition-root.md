# ADR-001 — `WealthOps.Cli` is a direct in-process composition root

| Field | Value |
|:--|:--|
| **Status** | Accepted |
| **Date** | 2026-09-20 |
| **Ref** | WT-001 (M0), BRD-001 BR-1, OOS-10, OOS-11 |

## Context

Root `AGENTS.md` described a `WealthOps.ChatHost` project as a "standalone LLM microservice" that "owns the Anthropic SDK" and "talks to Host via HTTP only".

Three things were wrong with that description when M0 started:

1. **The project did not exist.** Nothing in `src/` or `WealthOps.slnx` referenced it.
2. **v1 has no Anthropic dependency.** BR-1 requires all personal data to stay on the local machine and OOS-10 excludes cloud inference. Inference runs against a local OpenAI-compatible endpoint.
3. **v1 has no web interface** (OOS-11). The only client is a local terminal.

M0 needs a composition root from which the operator can hold a conversation and run `ingest` / `status`. The question is whether that root talks to `WealthOps.Host` over HTTP or composes the application in-process.

## Decision

Create `src/WealthOps.Cli`, a console application that references Application, Infrastructure and Domain and dispatches Mediator requests **in-process**.

`WealthOps.Host` remains a separate composition root over the same layers, reduced in v1 to a health probe.

The inaccurate `WealthOps.ChatHost` description is removed from root `AGENTS.md`.

## Rejected alternative — CLI as an HTTP client of Host

Rejected because it buys nothing and costs several things:

- It introduces a serialisation boundary between a terminal and a local database, for no consumer.
- It requires a second process to be running before the CLI is usable, which makes `status` harder to interpret rather than easier: `status` would report on *Host's* view of the model endpoint and database, not on the process the operator is actually running.
- It creates an HTTP surface carrying personal content. Nothing in v1 needs that surface to exist, and NFR-1 is easier to defend when it does not.
- It fixes nothing about testability. The Application layer is already reachable from a test without either host.

## Consequences

- **Two composition roots must stay configuration-compatible.** Both bind the identical `WealthOps:*` surface through one shared `AddApplication()` / `AddInfrastructure()` pair, so a key added later cannot drift between them.
- **Host is nearly empty in v1.** It maps `GET /health` and nothing else. `GET /` deliberately stays unmapped, because the Host integration smoke test asserts a 404 there as proof the pipeline booted.
- **Revisit if FS-22 (web interface) lands.** Host then gains real endpoints and the CLI may become one of several clients — but it should still compose in-process rather than call itself over HTTP.
