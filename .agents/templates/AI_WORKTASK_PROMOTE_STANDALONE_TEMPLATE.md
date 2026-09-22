# Task: [one-liner task description]

## Execution Profile
- **Recommended session model:** [model]   <!-- human action: set with /model BEFORE starting; can be switched at a gate -->
- **Path:** Lightweight (0→1→6→7→8) | Full (0→1→2→3→4→5→6→7→8)
- **Subagents:** none | Explore (read-only fan-out) | worktree-isolated writers
- **Commits during execution:** not allowed | allowed via the git-commit skill
- **Push:** never

## Contexts
- [ ] list relevant context documents, domain files, or knowledge sources
- [ ] if no context document exists for this feature, add one here and include "create context document" in Instructions

## Instructions
- [ ] list specific requirements and acceptance criteria

## Constraints

### Context Loading (Phase 0 — MANDATORY FIRST)
- **Load domain/feature context BEFORE asking clarifying questions** — you cannot ask intelligent questions without understanding existing patterns
- Use the `context-load-context` skill with `[domain]` to find/create functional context, or rely on the `load-agents-context` PostToolUse hook which auto-injects ancestor `*AGENTS.md` on first Read/Edit

### Git Behavior

Commits are governed by the **Commits during execution** field in the Execution Profile above.
The default is **not allowed**. This template grants no override of `git-policy.instructions.md`.

- **Commits: per the Execution Profile field.** Default **not allowed** — report the change set and stop. When the field allows commits, make them by invoking the **`git-commit` skill**, so conventional-format validation runs; never compose a commit message inline
- **Push: never** — manual review is always required before push

### Phase Output Rules (MANDATORY — no exceptions)

1. **Label every phase** — Output `## Phase N: Name` as a visible header before executing each phase
2. **Label every skip** — If skipping a phase, output: `## Phase N: Name — Skipped: [one-line reason]`
3. **Sequential execution** — Phases MUST execute in declared order. Never reorder, combine, or nest (e.g., doing Phase 5 work inside Phase 6 is a violation)
4. **No silent phases** — Every phase in your chosen path MUST appear in output. If the user can't see it, it didn't happen

> **FALLBACK copy.** `ai-workflow-rules.instructions.md` is authoritative. This copy exists for runners that do not auto-load `.agents/rules/` (e.g. Codex) and MUST be kept in sync with the rule on every workflow change.

### Execution Phases

Follow in order. **Do not skip phases without outputting the skip reason. Do not proceed without explicit user confirmation at gates.**

| Phase | Name | Gate? | Purpose | Who |
|-------|------|-------|---------|-----|
| 0 | Context Load | 🛑 MANDATORY | Read documents from **Contexts** section above | session |
| 1 | Discovery | 🛑 GATE | Clarify domain, technical and test scope | session |
| 2 | Analysis | | Analyze tech stack, determine tech requirements, identify patterns | session (+ `Explore` fan-out, optional) |
| 3 | Specification | | Create technical specification with architectural decisions | session |
| 4 | Planning | 🛑 GATE | Present implementation plan + module breakdown | session |
| 5 | Documentation | | Record the approved plan in the nearest `*AGENTS.md`, in its existing sections | session |
| 6 | Implementation | | 🔨 YOLO MODE — implement autonomously | session (worktree-isolated writers only if justified) |
| 7 | Verification | | Quality gate — spec/implementation sync check | session + `/code-review` |
| 8 | Handover | 🛑 MANDATORY | Document actual implementation; stop before committing | session |

**Lightweight path** (trivial tasks): Phase 0 → 1 → 6 → 7 → 8
Use when: single file, no architectural decisions, clear requirements.

**Full path** (non-trivial tasks): Phase 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8
Use when: 3+ files, new patterns, cross-cutting concerns, or ambiguous scope.

### Subagents

**Default: none.** The session already holds the loaded context, the conversation history and the
file state. Every subagent starts cold, re-derives that context, and returns only text — so a
subagent must buy something the session cannot do itself.

Three justified shapes, and no others:

| Shape | When | Why it earns the cost |
|-------|------|-----------------------|
| **`Explore` read-only fan-out** (Phase 2) | A broad sweep across many files/directories where only the conclusion is needed | Keeps large file dumps out of the session's context |
| **Worktree-isolated parallel writers** (Phase 6) | Genuinely independent modules, each in its own git worktree, with an **explicit merge step** | Real wall-clock parallelism without write collisions |
| **Independent review** (Phase 7) | Verification, via the existing `/code-review` skill | A reviewer that has not just written the code |

Anything else — "a security agent alongside QA", "a documentation agent alongside implementation" —
is one session applying another lens, in sequence, from context it already holds.

**Parallelism means batching tool calls, not spawning agents.** Issue N independent reads, greps
or builds in a single message. See Phase 6.

---

## Phase 1: Discovery

Ask clarifying questions about requirements, constraints and business logic. Decide technical
implementation details autonomously — those are not discovery questions.

Apply three lenses **in sequence, from the context already loaded in Phase 0** — this is one
session changing focus, not three agents:

**Product lens:**
- Business intent and value proposition
- Acceptance criteria and success metrics
- User stories and edge cases
- Scope boundaries (what's IN, what's OUT)
- Related business context or dependencies

**Architecture lens:**
- Technical feasibility and constraints
- System integration points and dependencies
- Performance, scalability, or security implications
- Trade-offs between proposed approaches
- Impact on existing architecture

**Test lens:**
- Testing scope and coverage expectations
- Acceptance criteria validation (are they testable?)
- Test tiers required: L0 (unit), L1 (component), L2 (integration)
- Known edge cases or failure scenarios to cover
- Regression test impact

### Output & Gate

```markdown
## Phase 1: Discovery

### Business Clarifications
- [findings / questions]

### Technical Clarifications
- [findings / questions]

### Test Scope Clarifications
- [findings / questions]

---

**Gate Question:**
Does this understanding capture your intent?
Reply 'approved' to proceed, or provide corrections.
```

**If requirements are already clear from the task description, state your understanding and proceed
without waiting.** Otherwise, wait for the user's response before proceeding.

---

## Phase 2: Analysis

**Prerequisite:** Phase 1 clarifications resolved.

1. **Determine technology stack requirements** — This output directly informs Phase 4 (Planning):
   - Backend required? (Database changes, API endpoints, server-side logic, service integrations, message queues)
   - Frontend required? (User-interface changes, client-side state, client-side validation, presentation assets)
   - Infrastructure/DevOps required? (Deployment, configuration, CI/CD, security)
   - **Output**: Clear YES/NO for each, with justification

2. **Analyze existing patterns and conventions**:
   - Similar features already implemented — what patterns do they follow?
   - Existing architecture decisions (ADRs) that apply to this task
   - Code organization, naming conventions, testing patterns
   - Technology stack baseline (what's already in use)

3. **Identify architectural constraints and dependencies**:
   - How does this task integrate with existing systems?
   - Breaking changes or migration paths needed?
   - Performance or scalability constraints
   - Security/compliance considerations

**Optional read-only fan-out.** When step 2 requires sweeping many directories or naming
conventions and only the conclusion is needed, dispatch an `Explore` (read-only) subagent rather
than reading everything into the session. Model hint at that spawn point:

```yaml
models:
  claude: sonnet
  copilot: auto
  codex: gpt-5.4
```

**Output format:**

```markdown
## Phase 2: Analysis

### Technology Stack Requirements
- **Backend**: YES — [justification: DB changes, API endpoints, etc.]
- **Frontend**: NO — [justification: no user-interface changes]
- **Infrastructure**: NO — [justification: no deployment changes]

### Existing Patterns & Conventions
- Similar features: [list and reference]
- Relevant ADRs: [which decisions from code-review-standards.instructions.md apply]
- Code organization: [where should new code go]
- Testing patterns: [what test structure to follow]

### Architectural Constraints
- Integration points: [where this connects]
- Performance considerations: [if any]
- Security implications: [if any]
- Migration/breaking changes: [if any]

**Ready for Phase 3.**
```

**Exit check**: proceed to Phase 3 only if tech stack requirements are clear.

---

## Phase 3: Specification

**Prerequisite:** Phase 2 analysis complete (tech stack determined).

1. **Create technical specification** with architectural decisions:
   - Architecture diagram or data model changes (if applicable)
   - API contracts, database schema changes (if backend)
   - Component/presentation structure (if frontend)
   - Error handling and logging strategy (following backend-logging-conventions.instructions.md)

2. **Document architectural decisions** — Use LADR format:
   - Decision title
   - Context (why this decision is needed)
   - Decision (what was chosen)
   - Consequences (what changes as a result)
   - Reference to code-review-standards.instructions.md ADRs if applicable

3. **Define testing approach**:
   - Test tiers and coverage expectations
   - Edge cases and failure scenarios
   - Integration test requirements (if applicable)

4. **Specify constraints and non-functional requirements**:
   - Performance targets
   - Security requirements
   - Scalability considerations
   - Compliance/audit trail needs

**Output format:**

```markdown
## Phase 3: Specification

### Technical Specification

[Include architecture diagrams, data model, API contracts, etc.]

### Architectural Decisions

**LADR-XXX: [Decision Title]**
- Context: [why this decision]
- Decision: [what was chosen]
- Consequences: [what changes]
- Related ADRs: [links to code-review-standards.instructions.md decisions]

[Additional LADRs if needed]

### Testing Approach
- Test tiers: L0 (unit) / L1 (component) / L2 (integration) requirements
- Coverage targets: [e.g., >80% for critical paths]
- Edge cases: [high-risk scenarios identified in Discovery]

### Non-Functional Requirements
- Performance: [targets or constraints]
- Security: [requirements from clarifications]
- Scalability: [expectations for growth]

**Ready for Phase 4.**
```

**Exit check**: proceed to Phase 4 only if the specification is complete and aligns with Phase 1 clarifications.

---

## Phase 4: Planning

**Prerequisite:** Phase 3 specification complete.

1. **Create the overall implementation plan**:
   - Ordered list of implementation steps
   - Module/component breakdown
   - File-level changes and dependencies
   - Risk mitigation strategies
   - Estimated scope (files touched, effort)

2. **Add a per-stack plan for each stack the Phase 2 YES/NO selected.** This is the same session
   applying the relevant engineering lens per stack, in sequence — one section per YES.

3. **Reconcile the per-stack plans**: integration points, contract mismatches and ordering
   conflicts between them. Adjudicate conflicts before presenting.

**Output format:**

```markdown
## Phase 4: Planning

### Overall Implementation Strategy
- Step 1: [implementation step] (File: X)
- Step 2: [implementation step] (File: Y)
- Dependencies: [what must complete before what]
- Risks: [identified risks and mitigation]

### Backend Plan (if Phase 2 said YES)
- Data/schema changes: [migrations]
- API surface: [new/modified endpoints]
- Service integrations: [how backend connects to systems]
- Dependencies: [what backend depends on]

### Frontend Plan (if Phase 2 said YES)
- Components/views: [new/modified units]
- Client-side state: [what state changes, and where it lives]
- API contracts consumed: [what backend endpoints are expected]
- Dependencies: [what frontend depends on]

### Infrastructure Plan (if Phase 2 said YES)
- Configuration/deployment changes: [what changes]
- Dependencies: [what must exist first]

### Integration Points
- [How the stacks interact]
- [Conflict resolution if any]

**Gate Question:**
Does this plan look correct? Reply 'approved' to proceed, or provide feedback.
```

**Gate**: Wait for user approval before proceeding to Phase 5.

---

## Phase 5: Documentation

**Prerequisite:** Phase 4 plan approved by user.

1. **Update domain AGENTS.md** (or create if missing):
   - Record the accepted Phase 4 plan in the section that already covers it — `Architecture Decisions` for a LADR, `Key Behaviors` for non-obvious behaviour, `Migration Plans` for follow-on work. `knowledge-conventional-contexts-quality.instructions.md` defines the permitted section list and is authoritative; **do not invent a `## Requirements` section**
   - Document architectural decisions (from Phase 3 LADRs)
   - Record tech stack requirements and integration points
   - Add test references (L0/L1 tier, test sub-folder paths)

2. **Update or create project ADRs** (`.docs/adr/` — singular; `slnx-docs-sync.py` and the `.slnx` solution folders recognise only that name):
   - For each Phase 3 LADR: create corresponding ADR file if it's a foundational architectural decision
   - Update existing ADRs if implementation changes behavior they document

3. **Update root AGENTS.md** if needed:
   - Cross-reference new feature/domain context document
   - Note significant architectural changes

4. **Update changelog** in context documents:
   - Record what was changed and why (for future reference)

**Output format:**

```markdown
## Phase 5: Documentation

### Context Documentation Updated
- [Domain]AGENTS.md: [sections updated]
- Root AGENTS.md: [sections updated]

### Architectural Decisions Recorded
- LADR-XXX: [ADR file created or updated]
- LADR-YYY: [ADR file created or updated]

### Changelog Entries
- [Date] | [Change] | [Reference to this task]

**Ready for Phase 6.**
```

---

## Phase 6: Implementation — 🔨 YOLO MODE

**Prerequisite:** Phase 5 documentation complete (Full path) or Phase 1 complete (Lightweight path).

Implement autonomously. No permission asks. Complete implementations (no TODOs). Fix forward.
**On failure:** attempt to fix forward. If blocked after 2 attempts, report status with suggested
options and wait for user guidance.

**Sequential by default.** One session implements the steps in the Phase 4 order.

**Parallelism = batched tool calls.** Issue independent reads, greps, and builds or tests of
mutually independent projects in a single message. Sequence only when one call's output feeds the
next. Never run two write operations against the same file, and never run concurrent
`dotnet build` / `dotnet test` over the same solution.

**Parallel writers are the exception and carry hard requirements.** Only when modules are genuinely
independent, and then:

- **Worktree isolation is mandatory** — each writer gets its own git worktree. Never two writers in
  one tree.
- **An explicit merge step is mandatory** — name it in the Phase 4 plan, with who merges and in
  what order.
- **Never two writes to the same file**, in any arrangement.
- **Never concurrent `dotnet build` / `dotnet test` over one solution.**

Costs to weigh before choosing this: a .NET worktree pays a **fresh package restore per agent**,
and **EF migration ordering still collides at merge** even when the code does not — two writers
each adding a migration produce a broken sequence that the merge must repair by hand. If either
cost outweighs the wall-clock saving, implement sequentially.

Model hint at a worktree-writer spawn point:

```yaml
models:
  claude: sonnet
  copilot: auto
  codex: gpt-5.4
```

**Output format:**

```markdown
## Phase 6: Implementation

### Implementation Complete
- [File 1]: [what was implemented]
- [File 2]: [what was implemented]
- Tests: [all passing, coverage X%]

**Ready for Phase 7 (Verification).**
```

---

## Phase 7: Verification

**Prerequisite:** Phase 6 complete.

**Checklist:** Code quality | Tests | Pattern consistency | Security | Spec/implementation sync | Changelog

1. **Spec/implementation sync check** (the mandatory one):
   - Does the implementation match the Phase 3 spec — or, if Phase 3 was skipped, the task Instructions?
   - Are the architectural decisions honored?
   - Any design issues introduced during implementation?

2. **Independent review** — invoke the existing **`/code-review`** skill rather than improvising
   reviewer roles. It carries the repo's review standards and reviews code the session just wrote.

3. **Test coverage check**:
   - Coverage adequate for the acceptance criteria?
   - Edge cases covered?
   - Acceptance criteria met?

**Consolidation & Decision:**

- If all pass: proceed to Phase 8
- If issues found: present to the user with options:
  - **Option A**: Fix issues in a follow-up execution (re-run Phase 6 with feedback)
  - **Option B**: Accept known issues and document (update AGENTS.md)
  - **Option C**: Block and ask for clarification/requirements change

**Output format:**

```markdown
## Phase 7: Verification

### Spec Sync
- ✅ Implementation matches the spec / task Instructions
- ✅ Architectural decisions honored
- Issues (if any): [list with remediation]

### Code Review (/code-review)
- Findings: [list with remediation, or none]

### Test Coverage
- Acceptance criteria: ✅ covered
- Edge cases: ✅ covered
- Coverage %: [X%]

### Decision
- ✅ Ready for Phase 8
- OR ⚠️ Issues found — [ask user for remediation approach]
```

**→ After review, proceed to Phase 8 (Handover). Do not stop here.**

---

## Phase 8: Handover — 🛑 MANDATORY

**Prerequisite:** Phase 7 passed (or user approved known issues).

Phase 8 is **mandatory to perform** — the documentation below is not optional. It **stops before
committing**: report the change set and wait, unless the Execution Profile's **Commits during
execution** field allows commits.

1. **Report the change set, then stop**:
   - List every file added, modified or deleted, with a one-line summary each
   - **Do not commit** unless the Execution Profile allows it. **Never push.**
   - If commits are allowed: invoke the **`git-commit` skill** so conventional-format validation
     runs. Do not compose commit messages inline. Commit in logical units of work

2. **Update AGENTS.md context document**:
   - Record actual implementation details (not just planned)
   - Update Tech Stack section with what was actually used
   - Update Key Behaviors if implementation differs from spec
   - Add Changelog entry: date, change, reference

3. **Update or create ADRs**:
   - If Phase 3 LADRs are still accurate: mark as finalized
   - If implementation changed decisions: update ADR with actual outcome
   - Add implementation status and rationale

4. **Finalize changelog**:
   - Comprehensive entry describing what was implemented
   - Breaking changes (if any)
   - Migration instructions (if needed)
   - Reference to this task

**Output format:**

```markdown
## Phase 8: Handover

### Change Set
- [file 1]: [what changed]
- [file 2]: [what changed]

### Commits
- Not created — Execution Profile sets Commits: not allowed. Awaiting instruction.
- OR (if allowed): [conventional message per commit, made via the git-commit skill]

### Documentation Updated
- [Domain]AGENTS.md: [sections changed]
- ADR files: [updated with actual implementation details]
- Root AGENTS.md: [if applicable]

### Changelog Entry
[Comprehensive entry for release notes]

### Task Complete ✅
Change set reported. Push is never performed by this workflow.
```

---

## Changelog

> AI loading note: Skip this section during routine task execution. Use it only when updating this template.

| Date | Task | Changes |
|------|------|---------|
| 2026-05-30 | | Initial version. |
| 2026-09-20 | | Rename workflow phase names to SE lifecycle stages (Discovery→Handover); see `ai-workflow-rules.instructions.md`. |
| 2026-09-20 | Worktask template remediation | Removed the `git-policy` override and autonomous-commit instructions (commits now governed by the per-task Execution Profile field, made via the `git-commit` skill; push never). Replaced the 17-row phase/model table and the fabricated cost metric with an `## Execution Profile` block; model hints now bind only at spawn points in per-runner `models:` form. Replaced `### Agent Fleet Autonomy` with `### Subagents` (default none; three justified shapes) and the Claude-only pseudo-code block with batched tool calls. Genericized stack-specific naming. Renamed self-check "Gate:" to "Exit check". Fixed `load-context`→`context-load-context` and test tiers to L0 unit / L1 component / L2 integration. Added the FALLBACK banner naming `ai-workflow-rules.instructions.md` authoritative, and this `## Changelog` heading. |
| 2026-09-21 | WT-001 (M0) | Synced with `ai-workflow-rules.instructions.md`: `.docs/adrs/`→`.docs/adr/` (singular, matching `slnx-docs-sync.py` and the `.slnx` folders); Phase 5 no longer instructs writing an invented `## Requirements` section into AGENTS.md — `knowledge-conventional-contexts-quality.instructions.md` owns the permitted section list. |
