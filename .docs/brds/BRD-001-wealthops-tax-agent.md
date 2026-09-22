# BRD-001 — WealthOps Tax Agent

| Field | Value |
|:--|:--|
| **Status** | Draft |
| **Version** | 1.1 |
| **Created** | 2026-09-19 |
| **Owner** | fadi-labs |
| **Scope** | v1 (Milestones M0–M3) |
| **Supersedes** | — |

> **This document contains no personal details by design.** Municipality, church tax membership, household composition, account providers, account numbers, and holdings are **configuration**, not requirements. See §7.3. Do not reintroduce them here, in any `*AGENTS.md`, or in any work task — see `.agents/rules/personal-data-boundary.instructions.md`.

---

## 1. Executive Summary

A locally-run conversational agent that answers questions about Danish personal taxation — both **the rules** (sourced from skat.dk) and **the operator's own financial position** (brokerage transactions, payslips, tax statements, loan schedule).

The agent exists to make a complex, fragmented tax position **legible**: what was earned, what is taxable, under which wrapper, at which rate, and why. It is a *sanity-check and comprehension* tool, not a filing tool and not a licensed advisor.

Two properties define the system and are non-negotiable:

1. **All personal data stays on the operator's machine.** Inference and embedding run locally. No personal financial content is transmitted to any external service.
2. **The language model never computes a monetary figure.** All tax arithmetic is performed by deterministic code. The model interprets questions, selects calculations, retrieves rules, and explains results.

---

## 2. Business Context & Problem Statement

### 2.1 The problem

Danish personal taxation applies **different rules to economically similar assets** depending on the wrapper they sit in and their legal classification:

- Shares in a normal depot are taxed **on realisation**, as aktieindkomst, using the average-cost method (gennemsnitsmetoden).
- The same shares inside an **aktiesparekonto (ASK)** are taxed **annually on the change in value**, at a flat rate, whether or not anything was sold.
- **Investeringsselskaber** (most ETFs and many funds) are taxed **annually on unrealised gains** (lagerbeskatning) — and whether that gain is *aktieindkomst* or *kapitalindkomst* depends on whether the fund appears on SKAT's positive list of share-based investment companies.
- Foreign dividends carry **withholding tax at source**, creditable in Denmark only under specific conditions.

The consequence is that the operator cannot answer basic questions — *what did I actually make last year, after tax?* — without manually reconciling several providers, several tax regimes, and rules published in Danish across many pages.

### 2.2 Why an agent

The information needed already exists, but in incompatible forms: prose rules on skat.dk, semi-structured CSV exports, and PDFs. A retrieval-augmented conversational agent can bridge them, provided the numeric work is done by code rather than inferred by a model.

### 2.3 Secondary driver: confidentiality

The data involved — income, holdings, pension, debt, personal identifiers — is among the most sensitive a person holds. Sending it to a third-party inference provider is not acceptable. This constraint **precedes** capability: a less capable local model is preferred to a more capable remote one.

The same constraint applies to **how the system is built**, not only how it runs. AI coding agents work on this repository continuously; their context is a disclosure surface. A system whose construction leaked the data it was built to protect would be self-defeating. Agents are therefore barred from the real data, work exclusively against synthetic fixtures, and **the project's own documentation is written to contain no personal specifics** (BR-15, BR-16, NFR-8).

---

## 3. Stakeholders

| Role | Interest |
|:--|:--|
| Operator | Understand and sanity-check their own Danish tax position |
| Household members | Modelled as additional taxpayers where Danish rules make the household the unit of assessment |
| SKAT | Authority of record. The årsopgørelse remains authoritative; this system never replaces it |

This is a **single-household, single-operator system**. Multi-tenancy, authentication, and authorisation are explicit non-goals.

---

## 4. Goals & Success Criteria

### 4.1 Goals

| ID | Goal |
|:--|:--|
| G-1 | Answer questions about Danish tax rules with citations to the source material |
| G-2 | Answer questions about the operator's own documents by quoting them |
| G-3 | Report positions, realised results, and dividends across all configured accounts |
| G-4 | Compute the household's annual tax position for a given tax year |
| G-5 | Keep all personal data local, with no external transmission |
| G-6 | Be extensible to deferred features without redesign |

### 4.2 Success criteria

**The system is successful at v1 if it reproduces the operator's most recent completed årsopgørelse within ±1 DKK**, given the same inputs, for every line item in scope — with the single documented exception in AC-6.

---

## 5. Scope

### 5.1 In scope (v1 = M0 → M3)

| Milestone | Capability | Outcome |
|:--|:--|:--|
| **M0** | Foundation | Database, vector store, local model gateway, CLI shell |
| **M1** | Retrieve & quote | Agent answers from skat.dk rules and the operator's own documents, with citations |
| **M2** | Report | Agent reports positions, realised results, and dividends from transaction exports |
| **M3** | Compute | Agent computes the household's tax position for the two configured tax years |

### 5.2 Out of scope (v1)

| ID | Excluded | Rationale |
|:--|:--|:--|
| OOS-1 | **Scenario simulation and advice** | Deferred to v2 with pension; without pension the main lever is absent |
| OOS-2 | **Pension** — contributions, account values, PAL-skat, contribution headroom | Deferred. See BR-9 for the correctness consequence |
| OOS-3 | **PDF field extraction** | Documents are retrievable and quotable; numeric facts are entered manually (see BR-8) |
| OOS-4 | **Cross-border taxation** — foreign employment, non-residency, special expatriate schemes | Not applicable to the configured household |
| OOS-5 | **Corporate actions** — splits, mergers, spin-offs, ticker changes | Not present in current data; data model accommodates them |
| OOS-6 | **Independent gennemsnitsmetode recomputation** | Provider-supplied cost basis is reconciled instead (see BR-7) |
| OOS-7 | **Foreign dividend withholding tax and Danish credit relief** | Dividends recorded gross in v1 |
| OOS-8 | **Market price feeds** | Year-end valuations come from annual statements |
| OOS-9 | **Export formats beyond the two specified in §7** | Only these are required; EP-1 accommodates more |
| OOS-10 | **Cloud or multi-provider inference** | Contradicts G-5 in v1 |
| OOS-11 | **Web or mobile interface** | CLI only |
| OOS-12 | **Filing to SKAT / TastSelv automation** | Permanent non-goal |
| OOS-13 | **Trade execution, live market data** | Permanent non-goal |
| OOS-14 | **Regulated financial advice** | Permanent non-goal — the system informs, it does not advise |
| OOS-15 | **Crypto assets** | Not required |
| OOS-16 | **Multi-user, authentication, authorisation** | Single-household system |

---

## 6. Business Requirements

| ID | Requirement | Priority |
|:--|:--|:--|
| **BR-1** | All personal financial data — documents, transactions, computed results, embeddings — **must remain on the local machine**. No component may transmit personal content to an external service. | MUST |
| **BR-2** | The language model **must not produce monetary figures from its own reasoning**. Every DKK amount presented to the operator must originate from deterministic code or be a verbatim quotation from a source document. | MUST |
| **BR-3** | Every answer about tax rules **must cite its source** (document and location). Every answer about the operator's position must identify the account, tax year, and calculation used. | MUST |
| **BR-4** | Tax rates, thresholds, and allowances **must be year-keyed reference data**, never constants in code. The system must support two adjacent tax years concurrently, since bracket structures differ between them. | MUST |
| **BR-5** | The household **may contain more than one taxpayer**, married. Danish rules that make the household the unit of assessment must be modelled: **transfer of unused personfradrag** between spouses, and the **doubled aktieindkomst progression threshold** for married couples. The model must tolerate a taxpayer with zero income and no assets, and must not assume it. | MUST |
| **BR-6** | The system **must reconcile against a completed årsopgørelse** as its correctness gate. | MUST |
| **BR-7** | Realised gains **must be reconciled against provider-supplied cost basis and result** rather than independently recomputed. Discrepancies must be surfaced, not silently resolved. | MUST |
| **BR-8** | Numeric inputs not derivable from transaction exports **must come from an explicit, operator-maintained annual facts record** — never from inferred PDF extraction. | MUST |
| **BR-9** | Taxable income must be sourced as **reported A-indkomst / AM-bidrag-pligtig indkomst**, not gross salary. Employer pension contributions are excluded from taxable income at source; using gross salary would overstate tax. | MUST |
| **BR-10** | The system **must state when it does not know**, rather than approximating. Unclassified instruments, missing facts, and unsupported transaction types must be reported explicitly. | MUST |
| **BR-11** | Ingestion **must be idempotent** — re-ingesting the same export must not duplicate or alter results. | MUST |
| **BR-12** | **No real personal financial data may be committed to the repository.** All test fixtures are synthetic. | MUST |
| **BR-13** | The system must run on a CPU-only development machine and on Apple Silicon, **without code changes** — only configuration. | MUST |
| **BR-14** | Deferred capabilities (§12) must be addable **without redesign**, via the extension points named in §10. | SHOULD |
| **BR-15** | **AI coding agents must never access real financial data — or embeddings derived from it — while performing development work.** This covers files, database rows, vectors, and derived output, and applies during debugging as well as feature work. See `.agents/rules/personal-data-boundary.instructions.md`. | MUST |
| **BR-16** | **Project documentation must contain no personal specifics.** Municipality, church tax membership, household composition, account providers, account numbers, instrument identifiers, and holdings are **configuration values**, never document content. Requirements describe capabilities; configuration supplies particulars. | MUST |

---

## 7. Data Sources

| Source | Form | Acquisition | Use |
|:--|:--|:--|:--|
| **skat.dk** tax rules | Web pages (Danish) | Manual download / fetch | Rules corpus — retrieval and citation |
| **Transaction export, Format A** | CSV, tab-delimited, 30 named columns, header row | Manual export | Transactions for realisation-taxed accounts |
| **Transaction export, Format B** | CSV, semicolon-delimited, 26 named columns + trailing empty field | Manual export | Transactions for the ASK |
| **Payslips** | PDF, consistent layout | Manual download | Retrieval and quotation only |
| **Årsopgørelse / forskudsopgørelse** | PDF | Manual download | Retrieval, quotation, and reconciliation target |
| **Annual account statements** | PDF | Manual download | Source of year-end valuations and the ASK taxable base |
| **Loan amortisation schedule** | PDF, fixed rate, full term | Already held | Source of annual interest paid |
| **Instrument classification** | Operator-maintained table | Manual | ISIN → tax treatment mapping |

**Format A** and **Format B** are the two export shapes the parsers must handle. They are identified by their technical shape, not by their provider: which account uses which format is configuration (§7.3).

### 7.1 Known format characteristics

These are **correctness-critical** and must be handled explicitly.

**Both formats carry a header row of named columns.** Both parsers therefore bind **by column name, never by position**. A missing, reordered, renamed, or otherwise unexpected header is a **hard failure at ingest** — never a fallback to positional binding, and never a silent partial parse. This makes the parsers resilient to a provider adding or reordering columns, which is the most likely future breakage.

| ID | Characteristic | Consequence |
|:--|:--|:--|
| DS-1 | Both formats use the **Danish decimal comma**; larger amounts use `.` as thousands separator | Parse with `da-DK`, never invariant culture |
| DS-2 | Format A is **tab-delimited**, 30 columns; Format B is **semicolon-delimited**, 26 columns plus a trailing empty field (27 on split) | Assert the column count on every row |
| DS-3 | **Handelsdag** (Format A) / **Handelsdato** (Format B) determines the tax year. Format A also carries `Bogføringsdag` and `Valørdag`; Format B also carries `Valørdato` | Using booking or settlement date silently moves income between years |
| DS-4 | Date formats differ: Format A uses ISO `yyyy-MM-dd`; Format B uses `dd-MM-yyyy` | Two parsers, one normalised model |
| DS-5 | Format B's `Valørdato` is a raw Java `Date.toString()` (`EEE MMM dd HH:mm:ss zzz yyyy`) | Parse components explicitly; pin to Europe/Copenhagen; never trust the zone token |
| DS-6 | Format A's `Beløb` is **signed** (negative for a purchase); Format B is **unsigned**, with direction in `Køb / Salg` | Normalise sign convention on ingest |
| DS-7 | **Format B pairs every amount across two currency bases** — `Markedsværdi i handelsvaluta` / `i afregningsvaluta`, and `Afregningsbeløb i DKK` / `i valuta` — each with its own currency column. **Only one member of a pair is populated**, and observed rows populate the `i afregningsvaluta` / `i valuta` side even for a DKK-denominated trade | Read whichever member is present; cross-validate when both are; **fail when neither is**. A parser that reads only `Afregningsbeløb i DKK` silently gets nothing |
| DS-8 | In Format B, `Markedsværdi` is **gross** (quantity × price) while `Afregningsbeløb` is **net of `Kurtage` and `Andre omkostninger`** | They are not duplicates; the difference is the cost of the trade |
| DS-9 | Format A supplies `Indkøbsværdi` (cost basis) and `Resultat` (realised result). **Format B supplies neither** | BR-7 reconciliation applies to Format A only — acceptable because the Format B account is lager-taxed, so realised gains are not part of its assessment |
| DS-10 | Format A supplies `Totalt antal` and `Saldo` (running position and cash). **Format B supplies neither** | The replay integrity check is Format A only |
| DS-11 | Format B's only available integrity check is internal arithmetic: `Markedsværdi` = `Stk. / Nom.` × `Kurs`, and `Afregningsbeløb` = `Markedsværdi` ± `Kurtage` ± `Andre omkostninger` per direction | Assert both on every row; this is the substitute for DS-10 |
| DS-12 | Format A has a `Makuleringsdato` column marking cancelled transactions; **none are expected**. **Format B has no equivalent column** | Assert empty on Format A and fail loudly if ever populated; no cancellation check is available on Format B |
| DS-13 | Format B carries **both `Konto` and `Depot`** as separate columns | Do not assume they are equal, even when they coincide |
| DS-14 | Format B account identifiers contain spaces | Normalise on ingest; retain raw for audit |
| DS-15 | **ISIN is the instrument join key**, not ticker | Tickers are reused and renamed; both formats carry ISIN |
| DS-16 | The FX rate is printed on the transaction — `Vekslingskurs` / `Middelkurs` (Format A), `Valutakurs` (Format B) | No external FX rate source is required |
| DS-17 | Transactions alone **cannot** produce a lagerbeskatning base — market values at the year boundary are required. This applies at **two different granularities**: the **ASK at account level** (the whole wrapper is lager-taxed), and **each lager-classified instrument held in a realisation-taxed account** (lagerbeskatning follows the instrument, not the wrapper). Realisation-taxed shares need no valuation at all | Sourced from annual statements (primary) or manual entry (fallback). The general form `gain = (closing value + disposals) − (opening value + acquisitions)` takes disposals and acquisitions from the transaction export, so only **opening and closing market value** are supplied manually — and only for holdings that straddle a year boundary |

#### 7.1.1 Confirmed column headers

Header rows carry no personal data (see OI-2), so the exact column names are recorded here verbatim. Both counts match DS-2 exactly.

**Format A** — tab-delimited, 30 columns:

```
Id  Bogføringsdag  Handelsdag  Valørdag  Depot  Transaktionstype  Værdipapirer  ISIN  Antal  Kurs
Rente  Samlede afgifter  Valuta  Beløb  Valuta  Indkøbsværdi  Valuta  Resultat  Valuta  Totalt antal
Saldo  Vekslingskurs  Transaktionstekst  Makuleringsdato  Notanummer  Verifikationsnummer  Kurtage
Valuta  Middelkurs  Oprindelig rente
```

**Format B** — semicolon-delimited, 26 columns plus a trailing empty field (27 on split):

```
Hovedordrenr.;Køb / Salg;Navn;ISIN kode;Ticker kode;Børs;Handelsdato;Valørdato;Stk. / Nom.;Kurs;Valuta;
Valutakurs;Markedsværdi i handelsvaluta;Valuta;Markedsværdi i afregningsvaluta;Valuta;Kurtage;Valuta;
Andre omkostninger;Valuta;Afregningsbeløb i DKK;Valuta;Afregningsbeløb i valuta;Valuta;Konto;Depot;
```

Observations from the confirmed headers:

- **OI-2 (partially answered):** Format A's header carries no column dedicated to dividend withholding tax. `Samlede afgifter` ("total duties/fees") is the only fee-like column outside `Kurtage`; whether withholding tax is folded into it, into `Rente`, or is simply absent from dividend rows is still open — needs a dividend row's `Transaktionstype` value and which columns are populated to settle.
- Format A additionally carries `Værdipapirer` (instrument name, alongside `ISIN` — DS-15 still applies, ISIN is the join key) and `Oprindelig rente` (unexplained by the DS list so far; likely relevant only to interest-bearing instruments).
- Both formats repeat a bare `Valuta` header once per amount column, as DS-7 anticipates for Format B; Format A does the same for `Beløb`, `Indkøbsværdi`, `Resultat`, and `Kurtage`/`Middelkurs` — confirm during parsing which `Valuta` occurrence pairs with which amount, since they are positional despite the "bind by name" rule (the repeated header text is itself the exception the parser must special-case).

### 7.2 Acquisition and file layout

Two regimes, deliberately separated so the locality boundary is structural rather than procedural.

**Rules are fetched; personal data is dropped.** The rules corpus is public, so acquisition is automated and the cache is **agent-readable** — retrieval quality can be debugged against it. Personal data is manual and **agent-denied**. Separate roots are what make that distinction enforceable.

#### Rules corpus — automated

A single re-runnable command fetches a curated list of skat.dk URLs (the list is committed; URLs are not personal), converts each page to text, and caches it with its source URL and retrieval date. Re-run annually when rates change. **This is the only outbound network traffic in the system**, and it carries no personal content (NFR-1).

#### Personal data — operator-supplied

A directory tree outside the repository, rooted at `WealthOps:PersonalDataDirectory`:

```
<personal data root>/
├── transactions/
│   ├── format-a/              realisation-taxed account exports
│   └── format-b/              ASK exports
├── documents/
│   ├── payslips/
│   ├── tax-statements/        årsopgørelse, forskudsopgørelse
│   ├── annual-statements/     year-end valuations, ASK taxable base
│   └── loan/                  amortisation schedule
├── reference/
│   ├── instruments.csv        ISIN → classification (operator-maintained)
│   ├── valuations-<year>.csv  opening/closing market value per lager-classified ISIN
│   └── facts-<year>.json      annual facts, incl. the ASK taxable base
└── reconciliation/
    └── arsopgorelse-<year>.json   line values for the correctness gate
```

Ingestion walks the tree, routes by folder, and deduplicates by content hash — so re-dropping an unchanged file is a no-op and the operator never tracks what has already been loaded (BR-11, AC-1).

| Input | Source | Cadence | Notes |
|:--|:--|:--|:--|
| Transaction exports | Provider export function | Annually or on demand | **Full history, not a single tax year** — position replay and running-total validation need continuity from account opening |
| Payslips | Employer portal / digital post | Monthly or yearly batch | Retrieval and quotation only; no figures are extracted (OOS-3) |
| Årsopgørelse, forskudsopgørelse | Tax authority self-service | Annually | Also the reconciliation target (BR-6) |
| Annual account statements | **Each provider, not only the ASK provider** | Annually | **Load-bearing** — the sole v1 source of year-boundary market values (DS-17), needed at *account* level for the ASK and at *per-ISIN* level for lager-classified instruments held in realisation-taxed accounts. Without both, lagerbeskatning cannot be computed |
| Loan amortisation schedule | Already held | Once | Fixed rate, so one file covers the full term |
| `instruments.csv` | Operator | When a new ISIN appears | The agent reports which ISINs are unclassified; the operator resolves each against the positive list (FR-M2-8, AC-7) |
| `facts-<year>.json` | Operator, from the documents above | Annually | The deliberate substitute for PDF extraction (BR-8, EP-5) |
| `reconciliation/` | Operator, from the årsopgørelse | Annually | Required only to run the correctness gate |

Expected effort: one setup session, then annual upkeep at årsopgørelse time. The two operator-maintained files are the only recurring manual work, and both exist because they are the honest alternative to inference — `instruments.csv` is where ETF tax treatment is decided, and `facts-<year>.json` supplies the numbers every calculation depends on.

### 7.3 Personal specifics are configuration

Per BR-16, every particular of the operator's situation is supplied through `appsettings` (or an environment-specific override kept outside the repository), never through documentation or code. The configuration surface:

| Key | Holds |
|:--|:--|
| `WealthOps:PersonalDataDirectory` | Root path for source documents and exports. Lives outside the repository |
| `WealthOps:Taxation:Household:Taxpayers[]` | One entry per taxpayer: identifier, municipality, church tax membership, marital status, spouse reference |
| `WealthOps:Taxation:TaxYears[]` | The tax years in scope |
| `WealthOps:Accounts[]` | One entry per account: identifier, wrapper type (`Aktiesparekonto` \| `FrieMidler`), export format (`FormatA` \| `FormatB`), owning taxpayer |
| `WealthOps:Instruments` | Path to the operator-maintained ISIN classification table |
| `WealthOps:RulesCacheDirectory` | Rules corpus cache. **Separate from the personal root** — public content, agent-readable |
| `WealthOps:Models:*` | Endpoint, chat model id, embedding model id, embedding dimensions |

Configuration containing real values is **gitignored and denied to agents**. The repository carries an example file with placeholder values only.

---

## 8. Functional Requirements

### M0 — Foundation

| ID | Requirement |
|:--|:--|
| FR-M0-1 | PostgreSQL with the `pgvector` extension, orchestrated for local development |
| FR-M0-2 | A model gateway addressing a configurable OpenAI-compatible endpoint, with chat and embedding model identifiers supplied by configuration |
| FR-M0-3 | An interactive CLI providing a chat loop and an `ingest` command |
| FR-M0-4 | A document store recording each ingested file with a content hash, enabling idempotent re-ingestion |

### M1 — Retrieve & Quote

| ID | Requirement |
|:--|:--|
| FR-M1-1 | Ingest PDFs: extract text, chunk, embed locally, store with corpus = `personal` |
| FR-M1-2 | Ingest skat.dk rule pages: chunk, embed, store with corpus = `rules` |
| FR-M1-3 | Retrieval is **partitioned by corpus**; no cross-corpus similarity search |
| FR-M1-4 | Queries are asked in **English** against **Danish** source material (cross-lingual retrieval) |
| FR-M1-5 | The agent answers rules questions with citations, and personal-document questions by quotation |
| FR-M1-6 | Every stored vector records the embedding model identifier and dimension |

### M2 — Report

| ID | Requirement |
|:--|:--|
| FR-M2-1 | Parse a Format A export into the normalised transaction model |
| FR-M2-2 | Parse a Format B export into the same model |
| FR-M2-3 | Validate on ingest: replayed position and cash must match provider running totals; the cancellation column must be empty; every transaction type must be recognised |
| FR-M2-4 | Maintain an ISIN classification table (tax treatment and income type), operator-populated |
| FR-M2-5 | Report current positions per account |
| FR-M2-6 | Report realised results for a tax year, reconciled against provider figures, by **Handelsdag** |
| FR-M2-7 | Report dividends for a tax year (gross) |
| FR-M2-8 | Report which ISINs remain unclassified, blocking any computation that depends on them |

### M3 — Compute

| ID | Requirement |
|:--|:--|
| FR-M3-1 | Year-keyed tax parameter reference data for both configured tax years |
| FR-M3-2 | Annual facts record per taxpayer per year, operator-maintained |
| FR-M3-3 | Personal income tax calculation: AM-bidrag, the year's bracket structure, kommuneskat at the configured municipal rate, church tax where the configured taxpayer is a member, personfradrag including spousal transfer, employment allowances, and the tax ceiling |
| FR-M3-4 | Share income (aktieindkomst) calculation with the two-tier rate and the married-doubled threshold |
| FR-M3-5 | Aktiesparekonto calculation: flat-rate tax on the annual value change, with loss carry-forward within the account |
| FR-M3-6 | Capital income calculation including deductible loan interest |
| FR-M3-7 | Lagerbeskatning for lager-classified instruments held in realisation-taxed accounts, computed **per ISIN** from year-boundary market values plus the year's acquisitions and disposals, and routed to share or capital income per the ISIN classification. Distinct from FR-M3-5, which computes the ASK at **account** level (DS-17) |
| FR-M3-8 | A consolidated household annual tax summary |
| FR-M3-9 | Each calculation is exposed as an agent tool with a typed contract |
| FR-M3-10 | A reconciliation report comparing computed figures against recorded årsopgørelse values, itemised by line |

---

## 9. Non-Functional Requirements

| ID | Requirement |
|:--|:--|
| NFR-1 | **Locality** — no network egress carrying personal content. Rules-corpus acquisition is the only outbound traffic, and carries no personal data |
| NFR-2 | **Portability** — CPU-only and Apple Silicon targets; differences confined to configuration |
| NFR-3 | **Testability** — no automated test may require a running language model. Model-dependent behaviour is tested against a stub |
| NFR-4 | **Determinism** — identical inputs produce identical monetary outputs, independent of model, temperature, or phrasing |
| NFR-5 | **Auditability** — every computed figure is traceable to its inputs; every ingested row retains its source file and raw line |
| NFR-6 | **Data volume** — low: hundreds of transactions, tens of documents. Performance is not a design constraint |
| NFR-7 | **Failure posture** — the system fails loudly on unrecognised input rather than guessing (see BR-10) |
| NFR-8 | **Development-time confidentiality** — the locality guarantee (NFR-1) extends to how the system is built. Agent context is a disclosure surface. Application logging must never emit personal content — identifiers, counts, and durations only. Documentation must carry no personal specifics (BR-16) |

---

## 10. Extension Points

Each deferred capability in §12 has a named seam. These exist in v1 even where only one implementation is present.

| ID | Seam | v1 implementation | Enables |
|:--|:--|:--|:--|
| EP-1 | Transaction export parser, selected by configured format | Format A, Format B | Additional formats and providers |
| EP-2 | Normalised transaction model with unpopulated columns (corporate-action reference, lot identifier, source-document identifier) | Nullable, unused | Corporate actions, lot tracking, provenance — without migration |
| EP-3 | Chat and embedding model gateway | Local OpenAI-compatible endpoint | Different models, different machines, remote providers |
| EP-4 | Corpus discriminator on the vector store | `rules`, `personal` | Additional corpora; enforcement of locality per corpus |
| EP-5 | **Facts provider** | Manual annual record | PDF extraction and transaction-derived facts as alternative implementations, with manual retained as override |
| EP-6 | Year-end valuation source | Annual statement figure | Historical price feed |
| EP-7 | Instrument classifier | Operator-maintained table | SKAT positive-list ingestion, with manual entries as overrides |
| EP-8 | Tax calculator registry | The M3 calculators | New calculators register without touching the agent |

**EP-5 is load-bearing.** It is what makes the deferral of PDF extraction a deferral rather than a dead end: calculators request a fact by name and tax year, and never learn whether it was typed, extracted, or derived.

---

## 11. Acceptance Criteria

| ID | Criterion |
|:--|:--|
| AC-1 | Re-ingesting an unchanged export produces no change in stored data |
| AC-2 | Replayed positions and cash balances match provider running totals at every Format A row |
| AC-3 | A transaction traded on the last business day of a tax year and booked in the next is attributed to the earlier year |
| AC-4 | A rules question returns an answer with a citation resolving to the source page |
| AC-5 | A question about a payslip returns a quotation from the correct document and period |
| AC-6 | Computed figures match the årsopgørelse within ±1 DKK for every in-scope line, **except** aktieindkomst where foreign dividends are held, which is expected to be **gross** and is recorded as a known variance with its magnitude stated |
| AC-7 | An unclassified ISIN blocks dependent computation with a named, actionable error |
| AC-8 | A missing annual fact blocks dependent computation with a named, actionable error |
| AC-9 | The full test suite passes with no language model running |
| AC-10 | No monetary figure in any answer originates from model generation — every figure is tool output or a document quotation |
| AC-11 | The repository contains no real personal financial data |
| AC-12 | Switching chat or embedding model is a configuration change only |
| AC-13 | Agent configuration denies read, edit, and write access to personal data paths and to direct database clients; personal data paths and real configuration are gitignored; no log statement emits personal content |
| AC-14 | No document in `.docs/`, `.context/`, or any `*AGENTS.md` names a municipality, a provider, an account, an instrument, or a household member's circumstances |

---

## 12. Future Scope

Deferred, with the extension point that accommodates each.

### 12.1 Next (v2)

| ID | Capability | Seam |
|:--|:--|:--|
| FS-1 | **Pension** — contributions, ratepension headroom, PAL-skat, provider statements | EP-5 |
| FS-2 | **Scenario simulation** — re-run a tax year under adjustments; solve for a target | EP-8 |
| FS-3 | **Advisory answers** — e.g. contribution sizing against a bracket threshold, grounded in FS-2 | EP-8 |
| FS-4 | **Household optimisation** — quantify reallocating assets between spouses to use an otherwise-idle personal allowance, share-income bracket, or ASK ceiling | EP-8 |

### 12.2 Data & ingest

| ID | Capability | Seam |
|:--|:--|:--|
| FS-5 | PDF field extraction for payslips and tax statements | EP-5 |
| FS-6 | Additional export formats and providers | EP-1 |
| FS-7 | Corporate actions — splits, mergers, spin-offs, ticker changes | EP-2 |
| FS-8 | Cancelled-transaction netting beyond assertion | EP-2 |
| FS-9 | Historical price feed by ISIN for year-end valuation | EP-6 |
| FS-10 | Pension provider statements | EP-5 |

### 12.3 Tax engine

| ID | Capability | Seam |
|:--|:--|:--|
| FS-11 | Independent gennemsnitsmetode computation as a check on provider figures | EP-8 |
| FS-12 | Foreign dividend withholding and Danish credit relief | EP-8 |
| FS-13 | Loss carry-forward tracking — kildeartsbegrænsede share losses and ASK internal losses | EP-8 |
| FS-14 | Deduction sweep — befordringsfradrag, servicefradrag, donations, A-kasse | EP-5, EP-8 |
| FS-15 | Property taxation — ejendomsværdiskat, grundskyld | EP-8 |
| FS-16 | Virksomhedsordning | EP-8 |

### 12.4 Rules corpus

| ID | Capability | Seam |
|:--|:--|:--|
| FS-17 | Den juridiske vejledning and the underlying statutes | EP-4 |
| FS-18 | SKAT positive-list ingestion as structured data | EP-7 |
| FS-19 | Automated annual refresh with change detection | EP-4 |

### 12.5 Platform

| ID | Capability | Seam |
|:--|:--|:--|
| FS-20 | Apple Silicon deployment | EP-3 |
| FS-21 | Optional remote provider with redaction and explicit consent | EP-3, EP-4 |
| FS-22 | Web interface and mobile access | — |
| FS-23 | Scheduled ingestion | — |

---

## 13. Open Items

| ID | Item | Blocks | Resolution path |
|:--|:--|:--|:--|
| OI-1 | Exact tax parameters for both configured years — bracket rates and thresholds, personfradrag, employment allowances, tax ceiling, and the municipal rate. The two years differ materially following the bracket restructuring | FR-M3-1 | Source from skat.dk during M3; each value carries its source reference. Municipal rate resolved from configuration |
| OI-2 | Whether Format A dividend rows carry a withholding-tax column | FS-12, magnitude of the AC-6 variance | **Partially resolved (§7.1.1):** the header carries no column named for withholding tax; still open whether it is folded into `Samlede afgifter` or absent from dividend rows — settle with a dividend row's populated columns |
| OI-3 | Complete transaction-type vocabulary in both formats | FR-M2-3 | Operator reports the distinct values from a full-year export during M2; each unhandled type is a loud failure |
| OI-4 | For **each** provider: whether the annual statement supplies the ASK taxable base directly, and whether it lists **per-ISIN year-boundary market values** for lager-classified holdings in realisation-taxed accounts (DS-17) | FR-M3-5, FR-M3-7 | Operator checks the available statements from both providers; manual entry via `valuations-<year>.csv` is the fallback |
| OI-5 | Whether any loan prepayments have been made, which would invalidate the remaining amortisation schedule | FR-M3-6 | Operator confirms; an annual statement figure is the fallback |
| OI-6 | On a **non-DKK** trade, which member of Format B's paired currency-base columns is populated (DS-7). The observed DKK row fills the `i afregningsvaluta` / `i valuta` side, which is the counterintuitive one | FR-M2-2 | One non-DKK row settles it. Until then the parser reads whichever member is present and fails when neither is — safe but blind |
| OI-7 | Whether Format A's `Resultat` is computed **per Danish tax rules (gennemsnitsmetoden)** or as a plain book P&L (FIFO or similar) | **BR-7 rests entirely on this** | Ask the provider. If it is not the tax-rule figure, reconcile-not-recompute is unsound and FS-11 (independent computation) is promoted into v1 |
| OI-8 | Whether the Format B export includes **cash deposits and withdrawals** to the ASK, or only securities trades | FR-M3-5 | The ASK base is `(closing + withdrawals) − (opening + deposits)`. If cash movements are absent, the base cannot be computed independently at all and the statement figure becomes the only source, not merely the preferred one |
| OI-9 | Whether either export can be produced **from account opening**, and any row-count or date-range cap | AC-2, position accuracy | Replay validation and correct positions need continuity from the first transaction. A capped export needs a manual opening-balance mechanism |
| OI-10 | How each provider represents **corporate actions** in its export, and under which transaction type | FS-7 (deferred) | Not needed for v1, but knowing the shape now avoids a model change later (EP-2) |

### 13.1 Provider information requests

A single checklist to take to each provider. None of these questions disclose personal data — they are about **file structure and statement content**, so answers can be pasted back directly.

**Provider of the realisation-taxed account (Format A)**

- [ ] **OI-7 — how is `Resultat` calculated?** Danish tax rules (gennemsnitsmetoden) or a book P&L? *Highest-value question in this list: BR-7 and the whole v1 reconciliation approach depend on the answer.*
- [ ] **OI-4a** — does the annual tax statement list **per-ISIN market values at the year boundary** for lager-taxed funds, or only realised transactions?
- [ ] **OI-2** — do dividend rows carry a **withholding-tax** column?
- [ ] **OI-3a** — what is the complete set of values `Transaktionstype` can take?
- [ ] **OI-9a** — can the export cover **full history from account opening**? Any cap?
- [ ] **OI-10a** — how do corporate actions appear?

**Provider of the ASK (Format B)**

- [ ] **OI-8 — does the export include cash deposits and withdrawals**, or only securities trades? *Second-highest value: it decides whether the ASK base is computable at all from the export.*
- [ ] **OI-4b** — does the annual statement state the **ASK taxable base** directly, or only opening and closing valuations?
- [ ] **OI-6** — on a non-DKK trade, which of the paired currency-base columns is populated?
- [ ] **OI-3b** — what is the complete set of values `Køb / Salg` can take (dividends, deposits, withdrawals, fees)?
- [ ] **OI-9b** — can the export cover full history? Any cap?
- [ ] Does the statement report the **loss carry-forward balance** held within the ASK?
- [ ] **OI-10b** — how do corporate actions appear?

**Not provider questions** — resolved by the operator or from public sources:

- [ ] **OI-1** — tax parameters for both years, from skat.dk (during M3)
- [ ] **OI-5** — whether any loan prepayments have been made

**None of these block starting.** M0 and M1 are entirely independent of them; M2 can be built against the documented structure with fixtures. OI-7 and OI-8 are the two that could change design rather than merely fill a blank, so they are worth asking first.

---

## 14. Glossary

| Term | Meaning |
|:--|:--|
| **Aktieindkomst** | Share income — dividends and realised share gains, taxed at two progressive rates |
| **Aktiesparekonto (ASK)** | Share savings account, taxed annually at a flat rate on the change in value, with its own contribution ceiling and internal loss carry-forward |
| **AM-bidrag** | Labour market contribution, levied on earned income before income tax |
| **Årsopgørelse** | The annual tax statement issued by SKAT — the authoritative record |
| **Bundskat / mellemskat / topskat** | The progressive state income tax brackets; restructured in 2026 |
| **Forskudsopgørelse** | The preliminary income assessment governing withholding during the year |
| **Frie midler** | A normal, unwrapped investment account ("depot") |
| **Gennemsnitsmetoden** | Average acquisition cost method for computing realised share gains |
| **Investeringsselskab** | Investment company — most ETFs; taxed on unrealised annual gains |
| **Kapitalindkomst** | Capital income — interest, and gains on non-share-based investment companies |
| **Kildeartsbegrænset tab** | A loss usable only against the same category of income |
| **Kirkeskat** | Church tax — applies only to members of folkekirken. Membership is per-taxpayer configuration |
| **Kommuneskat** | Municipal income tax, rate varying by municipality. The municipality is configuration |
| **Lagerbeskatning** | Taxation on the annual change in value, whether or not anything was sold |
| **PAL-skat** | Pension return tax, levied annually on pension investment returns |
| **Personfradrag** | Personal allowance; unused portion is transferable between spouses |
| **Rentefradrag** | Deduction for interest paid |
| **Realisationsbeskatning** | Taxation only when an asset is sold |
| **Skatteloft** | The ceiling on combined marginal income tax |

---

## Changelog

| Date | Change | Ref |
|:--|:--|:--|
| 2026-09-19 | Created from braindump session. v1 scoped to M0–M3; pension, simulation, and advisory deferred to v2. | — |
| 2026-09-19 | Added BR-15, NFR-8, AC-13: AI coding agents may not access real personal data or its embeddings. Enforced via `.agents/rules/personal-data-boundary.instructions.md`, `.agents/settings.json` deny rules, and `.gitignore`. | — |
| 2026-09-19 | **Removed all personal specifics** (v1.1). Providers replaced by neutral export format identifiers (Format A/B); municipality, church tax membership, household composition, accounts, and instrument identifiers moved to configuration (§7.3). Added BR-16 and AC-14. | — |
| 2026-09-20 | **Format specs corrected from actual headers** (v1.2). Both formats confirmed to carry header rows → binding is by column name, unexpected header is a hard failure, positional fallback removed. §7.1 rewritten and extended to DS-1…DS-17, adding Format B's paired currency-base columns (DS-7, the principal trap), gross-vs-net `Markedsværdi`/`Afregningsbeløb` (DS-8), and the absence of cost basis, result, running totals, and a cancellation column in Format B (DS-9, DS-10, DS-12) — so BR-7 reconciliation and the replay check are Format A only, with an internal arithmetic check (DS-11) as Format B's substitute. Added §7.2 acquisition and file layout. Clarified DS-17: lagerbeskatning valuations are needed at **account** level for the ASK **and per ISIN** for lager-classified instruments in realisation-taxed accounts; FR-M3-7 and OI-4 updated accordingly. Added `WealthOps:RulesCacheDirectory`. | — |
| 2026-09-20 | Added OI-6…OI-10 and §13.1 **Provider information requests** — a per-provider checklist of structural questions pending answers. Two are design-affecting rather than blank-filling: **OI-7** (whether Format A's `Resultat` follows gennemsnitsmetoden — BR-7 rests on it) and **OI-8** (whether the Format B export carries ASK cash movements — decides whether the ASK base is independently computable). | — |
| 2026-09-22 | Added §7.1.1 with the **confirmed verbatim column headers** for both formats (header text carries no personal data, per OI-2). Both counts match DS-2 exactly. Partially resolves **OI-2**: no column is named for dividend withholding tax; still open whether it is folded into `Samlede afgifter` or absent. Noted two previously undocumented Format A columns (`Værdipapirer`, `Oprindelig rente`) and that the repeated `Valuta` header is positional, not nameable — the one exception to "bind by column name" that the parser must special-case. | — |
