# ai-terse — AGENTS.md

## TL;DR

Single-turn output reformatter (pure prompt, no tools); applies to the current reply only and must never truncate code/command payloads while compressing prose.

## Key Behaviors

- **Unrelated to the `--autonomous` git flag:** `--autonomous` on `git-commit`/`git-commit-push`/`git-commit-push-pr` means "suppress interactive questions, decide autonomously" — it does NOT invoke this skill or its output format. Don't merge or cross-reference the two when editing either side.
- The TL;DR block (Value/Holes/Ignored) is the skill's contract with downstream readers — edits that drop it break consumers who scan only that block.

## Changelog

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-06-12 | Initial version. | |
| 2026-07-31 | Renamed from `ai-mansplain` → `ai-terse`; updated collision note to reference the `--autonomous` git flag. | |
