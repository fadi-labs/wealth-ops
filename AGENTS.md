# AGENTS.md

This file provides guidance for AI coding agents working in the WealthOps repository.

## Project Overview

WealthOps is a personal wealth and portfolio operations platform, built on an AI-spec-driven, AI-agnostic development foundation. It documents reusable patterns, blueprints, and component specifications that guide automated and AI-assisted software delivery.

**Tech stack:** .NET 10 · ASP.NET Core · Clean Architecture (Domain / Application / Infrastructure / Host) · EF Core + PostgreSQL · Mediator (source-gen CQRS) · xunit.v3

## Personal Data Boundary (READ FIRST)

**This repository builds software that processes the owner's real personal financial data. AI coding agents must never access that data.** Full rule: `.agents/rules/personal-data-boundary.instructions.md` (auto-loaded every session).

Out of bounds at all times — including while debugging a failing test:

- **Files** — anything under `WEALTHOPS_PERSONAL_DATA_DIR` (default `~/.wealthops/personal/`) or `.local/`; any real payslip, årsopgørelse, broker export, bank statement, or loan schedule.
- **Database rows** — transactions, positions, annual facts, reconciliation reference values, and the `Personal` document corpus. Inspect the EF Core model and migrations, never row content. No ad-hoc SQL clients.
- **Embeddings** of personal documents — an embedding is not anonymisation.
- **Derived output** — computed tax figures and reconciliation reports.

The boundary also covers what you **write**. **No document, test, fixture, comment, or commit message may name** a municipality, church tax membership, household composition, financial provider, account number, real ISIN or holding, or a real amount. Requirements describe capabilities; **configuration supplies particulars** — write "the configured municipality", not the municipality; "Format A", not the provider whose export it is.

No development task requires this data; if one appears to, the task is wrong. Reproduce with **synthetic** fixtures — invented, not anonymised. `.agents/settings.json` denies access structurally (deny rules override `bypassPermissions`), but the rule holds regardless of whether a tool enforces it.

## AI Context Files

`AGENTS.md` and `*AGENTS.md` are **AI-coder contextual knowledge documents**. Read them like `CLAUDE.md` (or your agent's equivalent standard context file): they are first-class, authoritative context — not optional reference material. Before changing code, treat any `AGENTS.md` / `*AGENTS.md` in scope as required reading.

These documents capture the **functional requirements and intent behind the code** — the "why", constraints, boundaries, and non-obvious behaviors that source code alone does not communicate. Use them to understand what the code is supposed to do before you change how it does it.

Contextual knowledge is layered, and applies at **multiple levels** — read every level that governs the code you touch (specific overrides general):

- **Domain** — the broad business/functional area.
- **Sub-domain** — a bounded slice within a domain.
- **Feature** — a specific capability or vertical slice.
- **Technology** — cross-cutting technical concerns (persistence, messaging, logging, etc.).

This root `AGENTS.md` is the top-level document; nested `*AGENTS.md` files inherit from it and add local context closest to the code. When working in a folder, the nearest `*AGENTS.md` is the most authoritative for that code.

Keep `*AGENTS.md` files synchronised with code and documentation changes. Functional `*AGENTS.md` files in feature folders are auto-loaded by the `load-agents-context` PostToolUse hook on the first Read/Edit in their directory tree — no manual registration required.

### Required Maintenance

- Every PR should create or update at least one `*AGENTS.md` file.
- Update the closest context file to the code you change. Prefer local context over adding more content to this root file.
- When domain model or structural shape changes, also update the relevant implementation or architecture context.

### Placement Rules

- Functional feature context belongs close to the feature code.
- Cross-cutting concerns belong under `.docs/hlds/02-nfrs/` or the nearest `*AGENTS.md`.
- Avoid creating duplicate context files that restate the same plan at multiple levels without adding new information.

## Implementation Docs

All planned work is tracked as worktasks under `.context/work-tasks/` (gitignored — local only). Use the `create-worktask` skill (`/create-worktask <kebab-slug>`) to scaffold a new one.

## Repository Layout (Navigation)

| Layer | Path | Purpose |
|---|---|---|
| Domain | `src/WealthOps.Domain/` | Core entities, value objects — no external deps |
| Application | `src/WealthOps.Application/` | Vertical-slice use cases via Mediator — `Features/<Name>/`, shared code in `Common/` |
| Infrastructure | `src/WealthOps.Infrastructure/` | EF Core + PostgreSQL (`Persistence/`), HTTP clients (`Clients/`) |
| Host | `src/WealthOps.Host/` | ASP.NET Core Web API, Serilog, Scalar OpenAPI |
| ChatHost | `src/WealthOps.ChatHost/` | Standalone LLM microservice — owns Anthropic SDK; talks to Host via HTTP only |

Detailed backend coding rules are maintained in `.agents/rules/backend/` and scoped per-file via frontmatter (see Rules section).

## Rules

All rules live under `.agents/rules/` as `*.instructions.md` files and are auto-loaded every session by Claude Code, Cursor, Copilot, and Codex via the symlinks/path-references documented in `.agents/AI_DEVELOPMENT_AGENTS.md`. Applicability is scoped **per-file** via frontmatter (`paths` for Claude, `globs`+`alwaysApply` for Cursor, `applyTo` for Copilot) — e.g. backend rules carry `**/*.cs` so they attach when a C# file is opened. Rules are organized into category subfolders for navigation; the folder is organizational only and does not change loading. One exception to "auto-loaded every session": prompt-scoped rules may be **deferred for Claude** and re-injected on demand by a `UserPromptSubmit` hook (e.g. `code-review-standards` loads only on review prompts via `.agents/hooks/code-review-standards-context.sh`; Cursor/Copilot still load it always). See `.agents/rules/meta/rules.instructions.md` ("Hook-deferred rules") for the file convention and `.agents/skills/manage-rule-system/SKILL.md` for the directory contract.

### Rule Categories

| Category | Folder | Contents |
|----------|--------|----------|
| _(cross-cutting)_ | `.agents/rules/` (flat) | `ai-workflow-rules`, `code-review-standards` (Claude: hook-deferred to review prompts), `personal-data-boundary` (**agents must never read the owner's real financial data or its embeddings**), `project-overview`, `skill-secret-handling` |
| git | `.agents/rules/git/` | `git-policy`, `pr-standards` |
| meta | `.agents/rules/meta/` | `rules` (file convention), `knowledge-conventional-contexts-quality` (AGENTS.md quality) |
| backend (`**/*.cs`) | `.agents/rules/backend/` | `api-mediator-validation` (Minimal API + Mediator + FluentValidation fail-fast); `architecture-slices` (clean-architecture boundaries, vertical-slice Features); `backend-logging-conventions` (Information vs Debug levels); `external-api-clients` (Refit list vs singular client split, HybridCache adapter); `migrations` (`[ExcludeFromCodeCoverage]` requirement); `wiremock-stubbing` (TestFramework.Aspire single-source stub helper) |

## Build / Test Commands

```bash
dotnet build WealthOps.slnx                    # build
dotnet test  WealthOps.slnx                    # run all tests
dotnet run --project src/WealthOps.AppHost      # dev Aspire AppHost
dotnet run --project src/WealthOps.ChatHost     # ChatHost standalone (separate process from the API Host)
```

Target a single test project directly when needed (e.g. `dotnet test tests/WealthOps.Domain.UnitTest`); `ls tests/` lists them — no Trait annotations required. **Gotcha:** the dev Aspire dashboard runs at `http://localhost:15278`; when started from a terminal, use the printed `/login?t=...` URL on first browser visit.

Container builds intentionally mirror the repo-root layout inside the SDK stage (`src/WealthOps.*` under a non-`/src` working directory — the root `Dockerfile` uses `WORKDIR /build`, so `dotnet restore` logs clean `src/WealthOps.*` paths, never `/src/src/WealthOps.*`). When editing `Dockerfile`, keep `WealthOps.slnx`, `Directory.*.props`, `NuGet.Config`, and `src/` in their repo-root-relative positions so solution/project references and central package props continue to resolve. `.github/workflows/publish-image.yml` pushes the GHCR image from the repo-root `Dockerfile` (push to `main` → `:latest`, `v*` tags → semver tags, `workflow_dispatch` → a supplied pre-release version, never `:latest`). **Gotcha:** `workflow_dispatch` inputs support only `description`/`type`/`required`/`default`/`options` — a `pattern:` key makes GitHub reject the whole workflow file, so the semver shape of `inputs.version` is enforced by a fail-fast `Validate dispatch version` step instead; see `.docs/wiki/ci.md`.

## Test Framework

xunit.v3 · Shouldly · Bogus · Respawn. Three tiers (the distinction is non-obvious and drives where a test belongs):

- **L0** `*.UnitTest` — no I/O, all in-process.
- **L1** component — `Application.ComponentTest` uses in-memory EF Core; `Infrastructure.ComponentTest` uses a real isolated DB + Respawn.
- **L2** `*.IntegrationTest` — full stack, real PostgreSQL.

Shared fixtures live in `tests/WealthOps.TestFramework/`; the Aspire dependency host (PostgreSQL + WireMock containers) in `tests/WealthOps.TestFramework.Aspire/`. See `.docs/wiki/testing.md`.

## Style and Dependencies

Authoritative stack and coding conventions for AI coders are in `.agents/rules/project-overview.instructions.md` and backend-specific rules under `.agents/rules/backend/` (scoped per-file via `**/*.cs` frontmatter).

## Architecture Decisions (NFRs)

Human-facing reviewer documentation lives in `.docs/wiki/`. Detailed high-level designs, non-functional requirements, and lightweight architecture decision records live under `.docs/hlds/`. Business requirement documents live under `.docs/brds/` — start with `BRD-001-wealthops-tax-agent.md`, the authoritative scope for the tax agent (v1 milestones M0–M3, tracked as `WT-001` … `WT-004` under `.context/work-tasks/`).

## CI/CD

PR gate — `.github/workflows/pr-gate.yml` (triggers: `pull_request` → `main`, `push` → `main`, `workflow_dispatch`): restore → build (Release) → Aspire-backed test with coverage via the local action `.github/actions/aspire-test-with-coverage`, then publish + upload the coverage report. Full step list, service ports, timing, and local .NET tools: `.docs/wiki/ci.md`.

Skills that need a secret must follow `.github/instructions/skill-secret-handling.instructions.md` (read it from the runtime env via a script; never embed the value).

## Git Constraints

This repository is hosted on **GitHub** at `https://github.com/fadi-labs/wealth-ops`.

- **CLI tool:** Use `gh` (GitHub CLI) for PR and repository operations.
- **PR template:** `.github/pull_request_template.md`
- **Code owners:** `.github/CODEOWNERS` — all files owned by `@fadi-labs`

## Glossary

<!-- TODO: Add domain-specific terms and abbreviations as the project evolves. -->

| Term | Description |
|---|---|
| Blueprint | A reusable, parameterised specification for a component or service |
| Catalogue | The collection of all blueprints and templates in this repository |
| Spec-driven | Development approach where machine-readable specifications are the source of truth |
