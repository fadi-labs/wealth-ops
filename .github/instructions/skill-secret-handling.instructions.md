---
description: 'How AI agent skills must handle secrets — read from the runtime environment via a script, never embed secret values in model-visible or committed text.'
globs: ".agents/skills/**"
paths:
  - ".agents/skills/**"
applyTo: '.agents/skills/**'
alwaysApply: false
---

# Skill Secret Handling

How any skill under `.agents/skills/` must handle a secret (API key, token, password, connection string). Updated: 2026-06-21

## The Rule

A skill that needs a secret **MUST delegate to a script that reads the secret from the runtime environment** (an environment variable injected at execution time) and uses it there. The secret **value** must never appear in any model-visible or committed text.

| Allowed | Forbidden |
|---------|-----------|
| `SKILL.md` instructs the agent to run a script that reads `$MY_API_KEY` from the env | A real key, token, or password written literally in `SKILL.md`, a prompt, agent YAML, README, reference doc, or any committed file |
| A bash/python script reads the secret via `os.environ` / `"$VAR"` and passes it to the tool | Echoing/printing the secret, putting it in a URL query string, or passing it as a logged CLI argument |
| Documenting the env var **name** the script expects (e.g. `MY_API_KEY`) | Documenting the env var **value** |

The secret value flows: **runtime environment → script → tool**. It is never typed into a file an agent reads, generates, or commits.

## Reference Pattern

A CI workflow step that needs a secret should inject it as an environment variable scoped to that step only (e.g. from `secrets.<NAME>` in GitHub Actions), read it exclusively inside the invoked script or process, and never echo, log, or persist it. No file in the repo should contain the literal value. Mirror this shape for any skill that needs a secret: declare the env var name, read it in a script, never persist it.

## Current Status

**No skill handles a real secret today.** This rule is a **standing guardrail** so that if a future skill needs a secret, it is added the safe way.

## Changelog

> AI loading note: Skip this section during routine task execution. Use it only when updating this rule file.

| Date | Change |
|:-----|:-------|
| 2026-06-21 | Initial version — env-via-script secret handling for skills. |
| 2026-09-21 | Removed the NVIDIA SkillSpector-specific reference example and status note; the rule and guardrail are unchanged. |
