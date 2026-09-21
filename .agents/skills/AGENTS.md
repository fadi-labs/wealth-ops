# .agents/skills — AGENTS.md

## TL;DR

First-party AI agent skills. They legitimately run shell, `gh`/`git`, and template file operations.

## Non-Negotiables

- **Secrets go through the environment, never into text.** Any skill needing a secret MUST follow `.github/instructions/skill-secret-handling.instructions.md`: a script reads the value from a runtime environment variable; the value never appears in `SKILL.md`, prompts, agent YAML, README, or any committed file. No skill handles a real secret today.

## Changelog

> AI loading note: Skip this section during routine task execution. Use it only when updating this rule file.

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-06-21 | Initial version — documents the secret-handling guardrail for skills. | #52 |
| 2026-09-20 | Added the `create-worktask` skill (replaces the Claude-only `worktask-create.sh` hook). | |
| 2026-09-21 | Removed the NVIDIA SkillSpector security-scan gate (workflow, baseline, report script) and all related documentation; no skill-scanning gate runs in CI today. | |
