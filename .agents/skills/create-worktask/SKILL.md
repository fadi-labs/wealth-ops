---
name: create-worktask
description: >
    Invoke to create a work task file under .context/work-tasks/ that a standalone
    AI coder can execute end to end. Trigger on imperative requests only: "create a
    worktask", "make a work task", "/create-worktask", "promote this to a worktask".
    Does NOT trigger on questions about how worktasks work, how the template is
    structured, or discussion of the worktask system itself.
allowed-tools: >
    Bash(.agents/skills/create-worktask/scripts/scaffold-worktask.sh:*),
    Read, Write, Edit, Grep, Glob
models:
  claude: opus        # high-complexity; the worktask is the input all nine phases run on
  copilot: auto
  codex: gpt-5.5
---

# Create Worktask — standalone work task authoring

## TL;DR

Produce a `.context/work-tasks/<slug>.md` file carrying **Title, Execution Profile, Contexts,
Instructions, Overrides**. It *links* `.agents/templates/AI_WORKTASK_PROMOTE_STANDALONE_TEMPLATE.md`
for the process; it does **not** copy the template's Constraints block. The work task is the task;
the template is the process manual.

## Non-Negotiables

- **Imperative triggering only.** This skill fires on a request to *create* a worktask. A question
  about how worktasks work, what the template contains, or how the workflow phases run is a
  question — answer it, do not scaffold a file. Do not loosen the `description` frontmatter to
  match discussion of the worktask system.
- **Every Contexts entry is a verified path.** List a document only after confirming it exists.
  A worktask that points a standalone coder at a file that is not there is worse than one that
  lists nothing.
- **Test tiers are L0 (unit) / L1 (component) / L2 (integration).** There is no E2E tier in this
  repo — see root `AGENTS.md`.
- **The output contract is the asset, not prose.** `assets/WORKTASK.template.md` defines the
  sections. Change the contract by changing the asset, so the two cannot drift.
- **Real content, not placeholders.** Populate every section from actual investigation. A
  scaffolded file left with its bracketed prompts intact is not a finished worktask.
- **Recommend the session model; never claim to set it.** Setting the model is a human action
  (`/model`), taken before execution starts.

## Invocation

`/create-worktask <kebab-slug>` — or describe the work in natural language and follow the workflow.

## Output structure (the contract)

```
.context/work-tasks/<kebab-slug>.md
├── # Task: <one-liner>
├── ## Execution Profile   # session model (human action), path, subagents, commits, push
├── ## Contexts            # verified paths only
├── ## Instructions        # the full requirement set (see step 6)
└── ## Overrides           # per-task workflow deviations; usually none
```

The file ends by **linking** the standalone template for the process. It carries no copy of the
template's Constraints, Phase Output Rules or Execution Phases — those live in one place.

`.context/work-tasks/` is gitignored (local only).

## Workflow

1. **Confirm it is a creation request.** If the user is asking *about* worktasks, answer the
   question and stop here.

2. **Understand the task** — parse the prompt and the conversation for what work is being
   requested. Ask if the intent is genuinely unclear; do not invent scope.

3. **Investigate before writing.** Read the relevant domain code and `*AGENTS.md` files, find
   similar existing implementations, and identify the integration points. The value of a worktask
   is the investigation baked into it.

4. **Scaffold** — run:
   ```bash
   .agents/skills/create-worktask/scripts/scaffold-worktask.sh <slug> [--title "One-liner"] [--force]
   ```
   Kebab-case slug matching the task summary, e.g. `add-vessel-eta-validation`,
   `fix-claims-filter-bug`. It prints JSON of what was created, and refuses to overwrite an
   existing file unless `--force` is passed.

5. **Populate Contexts** — verified paths only:
   - the domain/feature `*AGENTS.md` for the area being changed
   - root `AGENTS.md`
   - applicable rules under `.agents/rules/` (e.g. `code-review-standards.instructions.md`,
     `backend/backend-logging-conventions.instructions.md`, `git/git-policy.instructions.md`)
   - existing similar implementations worth following
   - schema or API documentation, if the change touches either

   Confirm each path exists before listing it. If no feature `*AGENTS.md` exists, say so and add
   "create the feature AGENTS.md" to Instructions.

6. **Populate Instructions** — the full requirement set:
   - **Task summary** — what is being built or fixed, and why
   - **Business intent** — problem statement, value, success metrics
   - **Acceptance criteria** — specific, measurable, testable
   - **Requirements and constraints** — functional requirements, edge cases, constraints
   - **Scope boundaries** — what is IN, what is OUT
   - **Integration points** — APIs, databases, services this connects to
   - **Non-functional requirements** — performance, security, compliance/audit
   - **Data model and contracts** — schema changes, API contracts, if any
   - **Testing expectations** — required tiers from **L0 (unit) / L1 (component) / L2
     (integration)**, coverage expectations, edge cases to cover
   - **Dependencies** — what must land first; related issues/PRs
   - **Gotchas** — pitfalls found during investigation

7. **Fill the Execution Profile** — and present the same recommendation in chat:
   - **Recommended session model** — a **human action**: the user sets it with `/model` before
     starting execution. Recommend one, with a one-line rationale. There is no orchestrator model
     and nothing in the file sets a model.
   - **Path** — Lightweight (0→1→6→7→8) for a single file, no architectural decisions, clear
     requirements; Full (0→1→2→3→4→5→6→7→8) for 3+ files, new patterns, cross-cutting concerns
     or ambiguous scope.
   - **Subagents** — default `none`.
   - **Commits during execution** — default `not allowed`. Only set it to allowed if the user
     says so; commits then go through the `git-commit` skill.
   - **Push** — never.

8. **Quality check** before confirming:
   - Would a coder with no prior conversation execute this without asking questions?
   - Does every Contexts path exist?
   - Is every acceptance criterion testable and measurable?
   - Are scope boundaries explicit — both IN and OUT?
   - Are the dependencies and integration points named?
   - Are all bracketed placeholders replaced with real content?

9. **Confirm to the user** — file path, key acceptance criteria, files/domains affected, the
   recommended session model and path, with a one-line rationale for each.

## Quality bar before marking ready

- [ ] Title is a one-liner that stands alone.
- [ ] Execution Profile is fully filled in — no leftover option lists.
- [ ] Every Contexts entry is a verified, existing path.
- [ ] Every acceptance criterion is measurable and testable.
- [ ] Scope boundaries state both IN and OUT.
- [ ] Test tiers use L0/L1/L2 vocabulary; no "E2E".
- [ ] No bracketed placeholders survive.
- [ ] The file links the standalone template rather than copying it.

## Agent-agnostic notes

- `scripts/scaffold-worktask.sh` is `bash` + coreutils only and finds the repo root via
  `git rev-parse` — Claude Code, Codex, Copilot and Cursor run it identically. It must stay
  executable (`chmod +x`).
- This skill replaced a Claude-only `UserPromptSubmit` hook (`worktask-create.sh`). A hook fires on
  a regex; a skill is selected by the model reading this `description`, so the "imperative only"
  trigger is a heuristic — which is why it is also the first Non-Negotiable above.

## Changelog

| Date | Change | Ref |
| :---- | :---- | :---- |
| 2026-09-20 | Created — replaces the Claude-only `worktask-create.sh` hook so worktask creation is reachable from all four runners. Carries the remediated contract: verified-paths-only Contexts, L0/L1/L2 tiers, imperative-only triggering, a Title/Execution Profile/Contexts/Instructions/Overrides output that links rather than copies the template, and session-model-as-human-action. | — |
