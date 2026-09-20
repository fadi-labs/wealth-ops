# Task: {{TITLE}}

## Execution Profile
- **Recommended session model:** [model] — human action: set with `/model` BEFORE starting
- **Path:** Lightweight (0→1→6→7→8) | Full (0→1→2→3→4→5→6→7→8)
- **Subagents:** none | Explore (read-only fan-out) | worktree-isolated writers
- **Commits during execution:** not allowed | allowed via the git-commit skill
- **Push:** never

## Contexts

Every entry must be a path that exists. Do not list a document that has not been verified.

- [ ] domain/feature `*AGENTS.md` for the area being changed
- [ ] root `AGENTS.md`
- [ ] applicable rules under `.agents/rules/`
- [ ] existing similar implementations to follow
- [ ] if no feature `*AGENTS.md` exists, say so here and add "create the feature AGENTS.md" to Instructions

## Instructions

### Task summary
[what is being built or fixed, and why]

### Business intent
- Problem statement:
- Value / outcome:
- Success metrics:

### Acceptance criteria
- [ ] [specific, measurable, testable]

### Requirements and constraints
- Functional requirements:
- Edge cases:
- Constraints:

### Scope boundaries
- IN:
- OUT:

### Integration points
[where this connects to existing systems — APIs, databases, services]

### Non-functional requirements
- Performance:
- Security:
- Compliance / audit:

### Data model and contracts
- Schema changes (if any):
- API contracts (if any):

### Testing expectations
- Test tiers required: L0 (unit) / L1 (component) / L2 (integration)
- Coverage expectations:
- Edge cases to cover:

### Dependencies
- Blocked by:
- Related issues / PRs:

### Gotchas
[pitfalls found during investigation that the implementing agent should avoid]

## Overrides

Deviations from the standard workflow for this task only. Leave empty if none.

- [ ] none

---

**Process:** follow `.agents/templates/AI_WORKTASK_PROMOTE_STANDALONE_TEMPLATE.md`.
That template is the process manual — this file is the task. Do not copy the template's
Constraints block here; link it, as this line does.
