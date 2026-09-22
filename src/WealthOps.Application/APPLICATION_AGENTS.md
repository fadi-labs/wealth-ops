# APPLICATION_AGENTS.md

## TL;DR

Vertical-slice use cases dispatched via the Mediator source generator — one folder per feature under `Features/`, never a horizontal `Commands/`/`Queries/` split.

## Non-Negotiables

- **No `Commands/` or `Queries/` folders.** Each use case lives in `Features/<FeatureName>/` with its request, response, validator, and handler colocated.
- **No domain logic here.** Application orchestrates Domain and infrastructure abstractions; business rules live in Domain.
- **Define infrastructure contracts as interfaces here.** Persistence store contracts go in `Common/Persistence/`, external client contracts in `Common/Clients/`. Application depends on `IFoo`, never on an Infrastructure concrete type.
- **Use `Mediator` (martinothamar), not `MediatR`.** Register handlers as `Scoped` (`MediatorOptions.ServiceLifetime = ServiceLifetime.Scoped`) so they can consume scoped collaborators such as `DbContext`; the generator defaults to Singleton, which fails DI scope validation.
- **FluentValidation runs fail-fast** in a Mediator pipeline behavior under `Common/Pipelines/`, before the handler.
- **References Domain only** — never Infrastructure or Host.
- **`IVectorStore.SearchAsync` always takes a corpus.** `CorpusQuery` carries it as a required positional member, so FR-M1-3 ("no cross-corpus similarity search") is enforced by the compiler. Never add an overload that omits it or defaults it.
- **No model identifier in code.** `IChatModel.ModelId` and `IEmbeddingModel.ModelId` expose it from configuration so nothing outside Infrastructure has to name a model (BR-13, AC-12).

## Slice shape (`Features/<Name>/<UseCase>.cs`)

```text
Features/
  <FeatureName>/
    <UseCase>.cs
      ├ Request    : IRequest<Response>
      ├ Response
      ├ Validator  : AbstractValidator<Request>
      └ Handler    : IRequestHandler<Request, Response>
```

## Architecture Decisions

**LADR-001: `CorpusQuery` carries the query text although v1 never reads it**
- *Date:* 2026-09-20 · *Status:* Accepted
- *Context:* v1 retrieval is dense-only, which is right for FR-M1-4 — queries are asked in English against Danish source material, where lexical matching fails outright. But the highest-value tokens in this domain are exact strings embeddings handle poorly: ISINs, statutory references, Danish term names.
- *Decision:* `SearchAsync` takes a `CorpusQuery` record carrying corpus, embedding **and** the original query text. The M0 implementation ignores the text.
- *Rejected:* `SearchAsync(Corpus, EmbeddingVector, int)`. It forecloses hybrid search — adding a lexical leg later would mean changing an Application contract that M1–M3 have already been built against.
- *Consequences:* Adding BM25/`tsvector` fusion or a rerank stage becomes one Infrastructure class and one migration. Costs one unused field until then.

**LADR-002: Options validation fails at startup; directory *existence* does not**
- *Date:* 2026-09-20 · *Status:* Accepted
- *Context:* ADR-003 makes configuration the sole carrier of personal particulars, so its correctness is load-bearing.
- *Decision:* Shape and referential integrity fail at startup via `ValidateOnStart` — unknown taxpayer on an account, unresolved or non-mutual spouse reference, empty tax years, blank municipality, duplicate ids. A missing directory does **not**.
- *Consequences:* A machine with a mistyped personal-data path can still run `status` and be told so. Refusing to boot would leave the operator with a stack trace and no instrument.

## Key Behaviors

- **`WealthOpsOptionsStartupValidator` reports every failure at once**, not the first. An operator configuring this for the first time should see everything wrong in one pass rather than fixing one key and rebooting to find the next.
- **A spouse reference must be mutual.** A one-directional link would make personfradrag transfer (BR-5) compute differently depending on which taxpayer was assessed first. A `Single` taxpayer naming a spouse is also rejected — that is a contradiction, not a harmless extra.
- **The rules cache may not sit inside the personal data root.** Nesting them would put agent-readable public content behind a denied path and risk treating personal content as public. Two separate roots are the enforcement mechanism, so the validator checks it.
- **`PersonalDataRouter` routes by folder position, never by sniffing content.** Content-sniffing would mean an agent-facing component reading personal material to decide what it is. An unrecognised position becomes `DocumentType.Unclassified` and is *reported*, never guessed at or dropped (BR-10, NFR-7).
- **`IngestPath` refuses a path outside both configured roots** — every ingested file must belong to exactly one corpus, and a path under neither has no safe default.
- **`SendChatMessage` sends no tool definitions in M0.** Offering a tool the system cannot service invites the model to fabricate a call. Tools arrive with the M3 calculators (FR-M3-9).
- **Handlers log counts and durations only** — never message content, chunk text, file names, or figures (NFR-8).

## Test References

- L0 — `tests/WealthOps.Application.UnitTest/`: `Common/Configuration/` (binding + every validator rejection), `Common/Pipelines/` (fail-fast ordering), `Features/Ingestion/PersonalDataRouterTests`, `Features/Chat/SendChatMessageTests`.
- L1 — `tests/WealthOps.Application.ComponentTest/`: `Features/Ingestion/IngestPathTests` (real temp-directory walk, idempotence, corpus resolution), `CompositionTests` (guards the Scoped Mediator lifetime via `ValidateOnBuild` + `ValidateScopes`).

## Quality Constraints

**No automated test may require a running language model** (NFR-3, AC-9). `IChatModel` and `IEmbeddingModel` are always stubbed.

## Packages

`Mediator.Abstractions` + `Mediator.SourceGenerator`, `FluentValidation.DependencyInjectionExtensions`, `Microsoft.Extensions.{Configuration,Options,Options.ConfigurationExtensions,DependencyInjection.Abstractions,Logging.Abstractions}` — all versioned centrally in `Directory.Packages.props`.

## Changelog

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-05-30 | Created — empty vertical-slice skeleton (`Features/`, `Common/{Clients,Exceptions,Models,Persistence,Pipelines}/`, `Extensions/`). | — |
| 2026-09-20 | Added the M0 contract surface: `Common/Clients/` (`IChatModel`, `IEmbeddingModel`, `IModelEndpointProbe` + message/tool records), `Common/Persistence/` (`IVectorStore` with `CorpusQuery`, `IDocumentStore`, `IPersistenceDiagnostics`), `Common/Configuration/` (full `WealthOps:*` graph + validators), `Common/Exceptions/`, `Common/Pipelines/ValidationPipelineBehavior`, `Extensions/AddApplication`. Slices: `Features/{Chat,Ingestion,Diagnostics}/`. LADR-001 (`CorpusQuery` carries query text), LADR-002 (startup-failure boundary). | WT-001 |
