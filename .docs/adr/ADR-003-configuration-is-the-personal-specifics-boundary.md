# ADR-003 — Configuration is the sole carrier of personal specifics

| Field | Value |
|:--|:--|
| **Status** | Accepted |
| **Date** | 2026-09-20 |
| **Ref** | WT-001 (M0), BRD-001 BR-12, BR-15, BR-16, NFR-8, AC-11, AC-13, AC-14 |

## Context

BR-16 requires that no document, and no line of code, name a municipality, a church tax membership, a household composition, a financial provider, an account number, a real instrument, or a real amount. Requirements describe capabilities; configuration supplies particulars.

That is a statement of intent. It needs a mechanism, or it decays the first time a hard-coded default is convenient.

## Decision

**Every particular of the operator's situation binds from configuration, and the binding is validated at startup.**

| Key | Holds |
|:--|:--|
| `WealthOps:PersonalDataDirectory` | Root for source documents and exports — outside the repository |
| `WealthOps:Taxation:Household:Taxpayers[]` | Per taxpayer: identifier, municipality, church tax membership, marital status, spouse reference |
| `WealthOps:Taxation:TaxYears[]` | Tax years in scope |
| `WealthOps:Accounts[]` | Per account: identifier, wrapper, export format, owning taxpayer |
| `WealthOps:Instruments` | Path to the operator-maintained ISIN classification table |
| `WealthOps:RulesCacheDirectory` | Rules corpus cache — a separate root from personal data: public, agent-readable |
| `WealthOps:Models:{Endpoint,Chat:Id,Embedding:Id,Embedding:Dimensions}` | Model gateway |

Supporting rules:

- **`appsettings.Example.json` is committed with placeholder values only.** It documents the shape; it names nothing real.
- **Real values live in `appsettings.Local.json`** (gitignored) or `WEALTHOPS_*` environment variables.
- **`WEALTHOPS_PERSONAL_DATA_DIR` supplies the ingestion root**, defaulting to `~/.wealthops/personal`. The repository holds a configuration key, never a path to real data.
- **Validation is where the rules are stated**, not documentation: a taxpayer must have a municipality; an account must reference a configured taxpayer; a married taxpayer's spouse reference must resolve to another configured taxpayer and must be mutual.

## Startup-failure boundary

Shape and referential integrity **fail at startup**: unknown taxpayer on an account, unresolved or non-mutual spouse reference, empty tax years, blank municipality, duplicate identifiers, non-absolute endpoint URI, non-positive embedding dimensions.

Directory **existence does not fail startup**. It is reported by `status`.

The reason is diagnosability. A machine with a mistyped personal-data path must still be able to run `status` and be told so. Refusing to boot would leave the operator holding a stack trace and no instrument.

## Consequences

- No test fixture, comment, or commit message needs a real value, because no code path has a real default to compare against.
- Adding a personal particular later means adding a configuration key and a validator rule. There is no other place to put it that will pass review.
- `status` becomes the single reconciliation point between configuration, the model endpoint and the database, and therefore the Definition-of-Done probe for M0.
