# DOMAIN_AGENTS.md

## TL;DR

Pure domain model — entities, aggregate roots, and value objects. Zero external dependencies and no I/O.

## Non-Negotiables

- **No outward dependencies.** Domain references no other project and no infrastructure packages (EF Core, ASP.NET, HTTP, serialization). It is the innermost Clean Architecture layer — everything depends on it, it depends on nothing.
- **No I/O or framework concerns.** No persistence, network, logging, or DI registration here — those belong in Infrastructure/Host.
- **Enforce invariants at construction.** Guard required state in constructors/factory methods so an entity cannot exist in an invalid state. Value objects are immutable and compared by value.
- **`EmbeddingVector` stays a plain float array.** The database representation is a `pgvector` column, but that type belongs to Infrastructure — importing `Pgvector` here would put a persistence package inside the innermost layer. The EF value converter bridges the two.
- **No personal particular may be named here.** Enum members describe *categories* — `FormatA`, not the provider whose export it is; `Aktiesparekonto`, not an account number (BR-16).

## Key Behaviors

- **`ContentHash` is identity, path is not.** The same file moved or re-downloaded under a new name is the same document. This is what makes re-ingestion idempotent (BR-11, AC-1) and why `Document.RelocateTo` exists instead of a second row. Canonical form is 64 **lowercase** hex characters — `Parse` rejects uppercase, because two spellings of one hash would defeat the unique index that the idempotence guarantee rests on.
- **A `DocumentChunk` takes its corpus from its `Document`, not from a parameter.** `DocumentChunk.Create` accepts the parent entity precisely so there is no overload that lets a caller pair a chunk with the wrong corpus. Corpus-scoped search (FR-M1-3) is therefore an invariant rather than a convention.
- **`EmbeddingVector` rejects empty and non-finite components.** A `NaN` or zero-width vector indicates a broken embedding response, not an unusual one; stored, it would poison every later similarity search while still returning plausible-looking neighbours (NFR-7).
- **Each chunk records its own `EmbeddingModelId` and `Dimensions`.** Vectors from different models are not comparable. Storing the model per row is what makes a mixed-model corpus *detectable* instead of silently wrong (ADR-002, FR-M1-6).
- **`TaxYear` is wrapped rather than a bare `int`.** Tax years and ordinals are both small integers flowing through the same signatures; swapping them produces a plausible wrong answer. The validity window is wide enough for any real assessment and narrow enough to catch a two-digit year or an ordinal.
- **`Corpus` exists from M0 although v1 has only two members and no remote path.** It is the enforcement point for data locality (BR-1, EP-4) — `Rules` content is public and may be debugged freely, `Personal` content may not leave the machine.

## Test References

L0 — `tests/WealthOps.Domain.UnitTest/`: `ValueObjects/` (`ContentHashTests`, `EmbeddingVectorTests`, `TaxYearTests`), `Entities/` (`DocumentTests`, `DocumentChunkTests`).

## Changelog

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-05-30 | Created — empty Clean Architecture domain skeleton (`Entities/`, `ValueObjects/`). | — |
| 2026-09-20 | Added `Enums/` (`Corpus`, `DocumentType`, `MaritalStatus`, `AccountWrapper`, `TransactionExportFormat`), `ValueObjects/` (`ContentHash`, `EmbeddingVector`, `TaxYear`), `Entities/` (`Document`, `DocumentChunk`). `EmbeddingVector.Create` has an array overload as the primary because EF value converters are expression trees and cannot carry a `ReadOnlySpan<T>`. | WT-001 |
