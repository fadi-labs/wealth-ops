# create-worktask — AGENTS.md

## TL;DR

Authoring skill for work task files under `.context/work-tasks/`. The skill carries the *judgment*
(what contexts, requirements and acceptance criteria to write); `scripts/scaffold-worktask.sh` only
lays down the deterministic skeleton from `assets/WORKTASK.template.md`.

## Non-Negotiables

- **Do not loosen the trigger.** The `description` frontmatter deliberately fires on imperative
  creation requests only and states that questions about worktasks must not trigger it. The
  predecessor hook matched any prompt containing "create … worktask" and fired while the user was
  merely *discussing* the system. A skill has no regex — this carve-out is a heuristic the model
  reads, so an edit that broadens the description silently restores the old failure.
- **The worktask links the template; it never copies it.** `.agents/templates/AI_WORKTASK_PROMOTE_STANDALONE_TEMPLATE.md`
  is the process manual and the single place the Constraints, Phase Output Rules and Execution
  Phases live. The predecessor hook contradicted itself — its step 6 listed three sections while
  its closing note called the output "a filled-in copy of the template". Keep one contract.
- **`assets/WORKTASK.template.md` carries no HTML comments.** Authoring guidance belongs in
  `SKILL.md`, which is read on every invocation anyway.
- **Test tiers are L0 unit / L1 component / L2 integration.** Root `AGENTS.md` defines them; there
  is no E2E tier. The predecessor hook shipped both `unit, integration, E2E` and a mislabelled
  `L1 (integration), L2 (E2E)`.

## Key Behaviors

- **The asset *is* the output contract.** The scaffolder renders it, so the shipped structure and
  the prose in `SKILL.md` describing it cannot drift. Change the contract by changing the asset.
- **`--force` exists here but not in `create-hld`.** `scaffold-hld.sh` hard-fails on an existing
  target because its zero-padded index makes collision nearly impossible. Worktask slugs are
  agent-chosen and collide easily, so re-scaffolding is a normal operation — but it still refuses
  by default, and reports `"overwrote": true` when it happens.
- **Only `{{SLUG}} {{TITLE}} {{DATE}}` are substituted**, by `sed`, not by an agent. Any other
  `{{...}}` in the asset survives verbatim into the scaffolded file.
- **`.context/work-tasks/` is gitignored** — worktasks are local only and never committed.
- Complexity tier is **high** (`claude: opus`): the worktask is the input all nine workflow phases
  run on, so a gap in it propagates through every one of them — the same reasoning that makes
  `create-hld` high.
- Scripts are `bash` + coreutils only and discover the repo root via `git rev-parse` — no
  Claude-specific behaviour, so Codex/Copilot/Cursor run them identically. Must stay executable
  (`chmod +x`); on a `core.fileMode=false` checkout, stage the mode with
  `git update-index --chmod=+x`.

## Changelog

| Date | Change | Ref |
|:-----|:-------|:----|
| 2026-09-20 | Initial version — replaces the `worktask-create.sh` `UserPromptSubmit` hook. Hooks are Claude-only (`.agents/config.toml` records that Codex has no hook system; Copilot and Cursor run none), so worktask creation was reachable from only one of the four runners the rsynced `.agents/` tree serves. Carries forward the hook's remediation: verified-paths-only Contexts, L0/L1/L2 tiers, imperative-only triggering, link-not-copy output contract, session-model-as-human-action. | — |
