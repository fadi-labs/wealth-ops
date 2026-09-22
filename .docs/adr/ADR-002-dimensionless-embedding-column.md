# ADR-002 — The embedding column carries no declared dimension

| Field | Value |
|:--|:--|
| **Status** | Accepted |
| **Date** | 2026-09-20 |
| **Ref** | WT-001 (M0), BRD-001 EP-3, FR-M1-6, NFR-6, NFR-7, AC-12 |

## Context

`pgvector` columns are normally declared with a fixed dimension — `vector(768)`. That dimension must be known when the migration is written.

But the embedding model is **configuration** (EP-3), and AC-12 requires that switching chat or embedding model be a configuration change only. A model with a different output dimension is a routine switch, not a schema event.

Declaring `vector(n)` would make `n` a migration constant, so changing the embedding model would require a migration — breaking AC-12 — and would make the dimension a property of the schema rather than of the data.

## Decision

Declare the column as `vector`, with **no dimension**.

Record the dimension where it actually belongs — on the row:

- `DocumentChunk.Dimensions` — the dimension of the stored vector.
- `DocumentChunk.EmbeddingModelId` — the model that produced it (FR-M1-6).

A chunk whose dimension does not match the configured embedding model's dimension is **rejected at upsert** with a named exception. `status` reports the stored `(model id, dimensions, count)` profiles against the configured pair, so a mismatch is visible before it corrupts a retrieval result.

## Rejected alternatives

- **`vector(n)` with `n` read from configuration at migration time.** Makes changing the embedding model a migration, contradicting AC-12. It also invites two databases with identical migration history and different schemas.
- **Silently truncating or zero-padding a mismatched vector.** Forbidden by NFR-7 (fail loudly on unrecognised input), and worse than useless in practice: a truncated embedding still returns confident-looking neighbours.

## Consequences

- **No ANN index in v1.** `pgvector` requires a fixed dimension to build `ivfflat` or `hnsw`. Retrieval is a sequential scan with cosine distance.

  This is the right trade at v1 scale: NFR-6 puts the corpus in the hundreds of chunks. An exact scan is also *more* correct than an approximate index — there is no recall loss.

  If volume ever justifies an index, the path is additive: a later migration pins a dimension-typed column once a single embedding model has settled.
- **Mixed-model corpora are detectable rather than silent.** Two models' vectors can coexist in the table; they are never comparable, and the per-row model id is what makes that state diagnosable instead of mysterious.
- **Cosine is the distance metric**, not L2 or inner product — it stays correct if a future configured model does not emit L2-normalised vectors.
