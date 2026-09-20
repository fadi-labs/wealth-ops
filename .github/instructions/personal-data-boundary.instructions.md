---
description: 'Personal data boundary — AI coding agents must never read, embed, query, or reproduce the user''s real financial data or its embeddings while doing development work'
globs: "**"
paths:
  - "**"
applyTo: '**'
alwaysApply: true
---

# Personal Data Boundary

**AI coding agents build the system that processes personal financial data. They must never access that data.** Updated: 2026-09-19

## Absolute Rule

The user's real financial data — payslips, årsopgørelser, broker exports, loan schedules, the derived database rows, and the vector embeddings computed from any of them — is **out of bounds to every coding agent at all times**, including during debugging, verification, and test-failure investigation.

There is no development task that requires it. If a task appears to require it, the task is wrong.

| Situation | Action |
|-----------|--------|
| A test fails and real data would explain why | **Reproduce with a synthetic fixture.** Never open the real file |
| Ingestion produces an unexpected result | **Ask the user for a redacted or synthetic sample** that reproduces the shape |
| A parser must handle a real-world quirk | **Ask the user to describe or anonymise the quirk.** Encode it as a synthetic fixture |
| Retrieval quality looks wrong | Evaluate against the **rules corpus** or synthetic personal fixtures only |
| The user pastes personal data into the conversation | Use it to answer **that** question; never write it to a file, a test, a commit, or a document |

## What Is Out Of Bounds

**Files** — anything under the configured personal data directory (`WEALTHOPS_PERSONAL_DATA_DIR`, default `~/.wealthops/personal/`), anything under `.local/`, and any real payslip, tax statement, broker export, bank statement, or loan schedule wherever it happens to sit.

**Database** — the `Personal` corpus and every table holding transactions, positions, annual facts, or reconciliation reference values. Do not run ad-hoc SQL against the development database to inspect content. Schema inspection via EF Core model definitions and migrations is fine; **row content is not**.

**Embeddings** — vectors derived from personal documents are personal data. An embedding is not anonymisation.

**Derived output** — computed tax figures, reconciliation reports, and agent answers about the user's position are personal data and are subject to the same rule.

## Documentation Carries No Personal Specifics

The boundary covers what agents **write**, not only what they read. No document in this repository — `*AGENTS.md`, BRD, HLD, ADR, work task, README, code comment, commit message, or PR description — may name:

- a municipality, church tax membership, marital status, or household composition
- a bank, broker, or other financial provider
- an account number, depot number, or customer identifier
- a real ISIN, ticker, instrument name, or holding
- a real amount, salary, balance, or tax figure

**Requirements describe capabilities; configuration supplies particulars.** Write "the configured municipality", not the municipality. Write "Format A", not the provider whose export it is. Where a concrete example genuinely aids understanding, invent one and mark it synthetic.

This is not cosmetic. Documentation is committed, pushed, mirrored to `.github/instructions/`, read by several vendors' agents, and may be shared or published. It is the least controlled surface in the repository, so it holds the least.

A configuration key is the correct home for every one of the items above. If a fact does not fit a configuration key, ask whether the system needs it at all.

## Why

Three reasons, in order of importance:

1. **The product promise is data locality** (BRD-001, BR-1). A system whose own construction leaked the data it was built to protect would be self-defeating.
2. **Agent context is not a private place.** Anything an agent reads enters a transcript, may be summarised, may be sent to a model endpoint, and may be persisted by the harness. Reading a payslip into context is a disclosure, not a lookup.
3. **Real data in a test is a permanent leak.** Fixtures are committed, shared, and copied forward. A single real figure pasted into a test outlives every intention behind it.

## Structural Enforcement

Policy alone is insufficient, so the boundary is structural:

- **Personal data lives outside the repository**, under `WEALTHOPS_PERSONAL_DATA_DIR`. The repository never contains a path to it, only a configuration key.
- **`.local/` is the sole in-repo escape hatch**, is gitignored, and is denied to agents. Anything that must sit near the repository goes there and stays unreadable.
- **`.agents/settings.json` denies** agent read, edit, and write access to those paths and denies direct database clients (`psql`, `pg_dump`). Deny rules take precedence over allow rules and over `bypassPermissions`.
- **Application logging must never emit personal content** — no transaction rows, no document text, no chunk text, no computed figures. Log identifiers, counts, and durations. See `backend/backend-logging-conventions.instructions.md`.

## Test Fixtures

Every fixture is **synthetic and hand-written** to exercise a named characteristic (see BRD-001 §7.1). Synthetic means invented, not anonymised — an anonymised real export still carries real amounts, dates, and holdings, and is treated as personal data.

Fixture amounts should be obviously artificial (round numbers, recognisable patterns) so that a real value pasted by mistake is visible on sight in review.

## If The Boundary Is Crossed

Stop. Tell the user plainly what was accessed and where it may have landed — context, a file, a test, a commit. Do not quietly continue, and do not attempt to clean it up silently. A disclosed mistake is recoverable; an undisclosed one is not.

## Changelog

> AI loading note: Skip this section during routine task execution. Use it only when updating this rule file.

| Date | Change |
|:-----|:-------|
| 2026-09-19 | Initial version. Introduced with BRD-001 (WealthOps Tax Agent). |
| 2026-09-19 | Added "Documentation Carries No Personal Specifics" — the boundary covers what agents write, not only what they read. Personal particulars belong in configuration (BRD-001 BR-16, AC-14). |
