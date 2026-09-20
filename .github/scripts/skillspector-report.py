#!/usr/bin/env python3
"""Render a SkillSpector JSON report into a GitHub job summary (stdout) and SARIF,
applying an accepted-findings baseline to decide the gate.

SkillSpector emits one --format per run, and its native SARIF is minimal (no
risk score, category, or confidence). The JSON report carries everything, so we
scan once with --format json and derive everything here:

  - Markdown summary -> stdout (redirect into $GITHUB_STEP_SUMMARY)
  - SARIF 2.1.0      -> <sarif_out> (for GitHub code scanning, when enabled)
  - Gate decision    -> --decision-file (pass|fail), read by the workflow

Why a baseline instead of SkillSpector's own exit code: these are FIRST-PARTY agent
skills that legitimately run shell, gh/git, and template file operations. SkillSpector
(correctly, for an untrusted third-party skill) rates those HIGH, so its risk score is
pinned at 100 and a raw `risk > 50` gate would block every PR forever. The baseline
(.github/skillspector-baseline.yml) lists each such finding by (id, file) WITH a written
justification. The gate fails only on ACTIVE (non-baselined) findings, so a genuinely new
dangerous pattern — real exfiltration, hidden instructions, supply-chain — still blocks.

Scan-mode policy (A): the GATE is computed from the deterministic STATIC report passed
as <report.json> (the workflow runs the gating scan with --no-llm). The LLM semantic
stage is nondeterministic across model/prompt drift, so it runs as a separate ADVISORY
scan whose report is passed via --advisory and rendered as an informational, NON-GATING
section. The advisory report never changes the decision file. Static analyzers still
catch real dangerous patterns (curl|bash, eval, rm -rf, secret exfiltration), so gating
on static keeps the gate meaningful while staying stable.

Usage:
  skillspector-report.py <report.json> <out.sarif> [path_prefix]
                         [--baseline FILE] [--decision-file FILE] [--advisory FILE]

`path_prefix` (default ".agents/skills") is prepended to each finding's file so SARIF
locations resolve from the repo root. Missing/empty JSON (e.g. SkillSpector errored
before writing) is handled gracefully: a note is emitted, an empty SARIF is written,
and the gate decision is `fail` (a scan that did not complete must not pass).
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

# SkillSpector severity -> (SARIF level, GitHub security-severity numeric)
LEVEL = {"CRITICAL": "error", "HIGH": "error", "MEDIUM": "warning", "LOW": "note"}
SECSEV = {"CRITICAL": "9.5", "HIGH": "8.0", "MEDIUM": "5.0", "LOW": "2.0"}
SEV_ORDER = {"CRITICAL": 0, "HIGH": 1, "MEDIUM": 2, "LOW": 3}
EMOJI = {"CRITICAL": "🟥", "HIGH": "🟧", "MEDIUM": "🟨", "LOW": "🟦"}


def empty_sarif() -> dict:
    return {
        "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
        "version": "2.1.0",
        "runs": [
            {
                "tool": {
                    "driver": {
                        "name": "SkillSpector",
                        "informationUri": "https://github.com/NVIDIA/SkillSpector",
                        "rules": [],
                    }
                },
                "results": [],
            }
        ],
    }


def load_baseline(path: str | None) -> tuple[dict[tuple[str, str], str], str | None]:
    """Return ({(id, file): reason}, error). Empty dict if no baseline configured.

    Parses YAML (PyYAML is a hard SkillSpector dependency, so it is present in the
    scan environment). Falls back to JSON if YAML is unavailable, so the script never
    hard-crashes on an environment quirk — it just reports the problem and treats the
    baseline as empty, which fails the gate loudly rather than silently muting it.
    """
    if not path:
        return {}, None
    p = Path(path)
    if not p.exists():
        return {}, f"baseline file not found: {path}"
    text = p.read_text()
    data = None
    try:
        import yaml  # type: ignore

        data = yaml.safe_load(text)
    except ModuleNotFoundError:
        try:
            data = json.loads(text)
        except json.JSONDecodeError as exc:
            return {}, f"PyYAML unavailable and baseline is not JSON: {exc}"
    except Exception as exc:  # malformed YAML
        return {}, f"could not parse baseline {path}: {exc}"

    accepted: dict[tuple[str, str], str] = {}
    for entry in (data or {}).get("accepted", []) or []:
        rid = (entry.get("id") or "").strip()
        f = (entry.get("file") or "").strip()
        reason = (entry.get("reason") or "").strip()
        if rid and f:
            accepted[(rid, f)] = reason
    return accepted, None


def is_baselined(issue: dict, accepted: dict[tuple[str, str], str]) -> str | None:
    """Return the baseline reason if this issue is accepted, else None. Match on
    (id, file) — line-agnostic, so it survives the line drift the upstream pin warns of."""
    rid = issue.get("id") or ""
    f = (issue.get("location") or {}).get("file") or ""
    return accepted.get((rid, f))


def build_sarif(report: dict, prefix: str, accepted: dict[tuple[str, str], str]) -> dict:
    issues = report.get("issues") or []
    version = (report.get("metadata") or {}).get("skillspector_version")
    rules: dict[str, dict] = {}
    results = []
    for it in issues:
        # Baselined findings are excluded from the SARIF entirely so the
        # code-scanning check stays green; they remain visible (and auditable)
        # in the job summary's "Accepted (baselined)" section, which reads the
        # scan output directly. SARIF `suppressions` are not reliably honored by
        # every code-scanning backend, so omitting is the robust choice.
        if is_baselined(it, accepted) is not None:
            continue
        rid = it.get("id") or "UNKNOWN"
        sev = (it.get("severity") or "MEDIUM").upper()
        if rid not in rules:
            rules[rid] = {
                "id": rid,
                "name": (it.get("category") or rid).replace(" ", ""),
                "shortDescription": {"text": it.get("category") or rid},
                "fullDescription": {"text": it.get("explanation") or it.get("pattern") or rid},
                "helpUri": "https://github.com/NVIDIA/SkillSpector",
                "properties": {
                    "security-severity": SECSEV.get(sev, "5.0"),
                    "tags": [t for t in [it.get("category")] if t],
                },
            }
        loc = it.get("location") or {}
        f = loc.get("file")
        uri = f"{prefix.rstrip('/')}/{f}" if f else (f or "")
        region = {}
        if loc.get("start_line"):
            region["startLine"] = loc["start_line"]
        if loc.get("end_line"):
            region["endLine"] = loc["end_line"]
        physical = {"artifactLocation": {"uri": uri}}
        if region:
            physical["region"] = region
        result = {
            "ruleId": rid,
            "level": LEVEL.get(sev, "warning"),
            "message": {"text": f"[{sev}] {it.get('pattern') or it.get('category') or rid}: {it.get('explanation') or ''}".strip()},
            "locations": [{"physicalLocation": physical}],
        }
        results.append(result)
    driver = {
        "name": "SkillSpector",
        "informationUri": "https://github.com/NVIDIA/SkillSpector",
        "rules": list(rules.values()),
    }
    if version:
        driver["version"] = version
    return {
        "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
        "version": "2.1.0",
        "runs": [{"tool": {"driver": driver}, "results": results}],
    }


def _skill(it: dict) -> str:
    """The owning skill = first path segment of the finding's file, relative to the
    scanned skills dir (e.g. 'ai-template-sync/scripts/sync.sh' -> 'ai-template-sync').
    Files directly under the scan root (e.g. 'README.md') have no owning skill."""
    f = (it.get("location") or {}).get("file") or ""
    head = f.split("/", 1)[0]
    return head if head and "/" in f else "(root)"


def _issue_row(it: dict) -> str:
    loc = it.get("location") or {}
    where = loc.get("file", "?")
    if loc.get("start_line"):
        where += f":{loc['start_line']}"
    conf = it.get("confidence")
    conf_s = f"{conf:.0%}" if isinstance(conf, (int, float)) else ""
    return (
        f"| {(it.get('severity') or '?').upper()} | {it.get('id','')} | "
        f"`{_skill(it)}` | {it.get('category','')} | {it.get('pattern','')} | `{where}` | {conf_s} |"
    )


def build_summary(
    report: dict,
    active: list[dict],
    accepted_issues: list[dict],
    accepted: dict[tuple[str, str], str],
    baseline_err: str | None,
) -> str:
    ra = report.get("risk_assessment") or {}
    score = ra.get("score")
    sev = (ra.get("severity") or "?").upper()
    issues = report.get("issues") or []
    meta = report.get("metadata") or {}
    llm = "LLM semantic + static" if meta.get("llm_available") else "static-only"

    counts: dict[str, int] = {}
    for it in issues:
        s = (it.get("severity") or "?").upper()
        counts[s] = counts.get(s, 0) + 1
    breakdown = ", ".join(
        f"{counts[s]} {s}" for s in sorted(counts, key=lambda x: SEV_ORDER.get(x, 9))
    ) or "none"

    out = ["## 🛡️ SkillSpector scan", ""]
    badge = EMOJI.get(sev, "⬜")
    out.append(f"{badge} **SkillSpector raw score: {score}/100 — {sev}** (first-party skills; gate uses the baseline below, not this raw score)")
    out.append("")
    out.append(f"**{len(issues)} findings** ({breakdown}) · analysis: {llm}")
    out.append("")

    # Gate verdict
    if active:
        out.append(f"### ❌ Gate: FAIL — {len(active)} active (non-baselined) finding(s)")
        out.append("")
        out.append("These are NOT in `.github/skillspector-baseline.yml`. Fix them, or add a justified")
        out.append("baseline entry if the behavior is inherent and safe for first-party tooling.")
        out.append("")
        out.append("| Sev | ID | Skill | Category | Pattern | Location | Conf |")
        out.append("|-----|----|-------|----------|---------|----------|------|")
        for it in sorted(active, key=lambda i: SEV_ORDER.get((i.get("severity") or "").upper(), 9)):
            out.append(_issue_row(it))
        out.append("")
    else:
        out.append("### ✅ Gate: PASS — no active (non-baselined) findings")
        out.append("")

    if baseline_err:
        out.append(f"> ⚠️ Baseline problem: {baseline_err} — treated as empty (all findings active).")
        out.append("")

    # Accepted / baselined section
    if accepted_issues:
        out.append(f"### Accepted (baselined) — {len(accepted_issues)} finding(s), report-only")
        out.append("")
        out.append("Suppressed from the gate per `.github/skillspector-baseline.yml`. Each is inherent")
        out.append("first-party capability or benign documentation; see the file for per-entry justification.")
        out.append("")
        out.append("| Sev | ID | Skill | Category | Pattern | Location | Conf |")
        out.append("|-----|----|-------|----------|---------|----------|------|")
        for it in sorted(accepted_issues, key=lambda i: SEV_ORDER.get((i.get("severity") or "").upper(), 9)):
            out.append(_issue_row(it))
        out.append("")

    # Stale baseline entries (matched nothing) — surfaces drift after a fix removes a finding
    present = {(it.get("id") or "", (it.get("location") or {}).get("file") or "") for it in issues}
    stale = sorted(k for k in accepted if k not in present)
    if stale:
        out.append("### ℹ️ Stale baseline entries (matched no current finding)")
        out.append("")
        out.append("A fix or pin bump removed these findings — prune them from the baseline.")
        out.append("")
        for rid, f in stale:
            out.append(f"- `{rid}` in `{f}`")
        out.append("")

    return "\n".join(out)


def build_advisory(report: dict, accepted: dict[tuple[str, str], str]) -> str:
    """Render the LLM semantic report as a NON-GATING advisory section. These findings
    are nondeterministic across model/prompt drift (policy A), so they are surfaced for
    human review only and never feed the gate decision."""
    issues = report.get("issues") or []
    meta = report.get("metadata") or {}
    model = meta.get("model") or meta.get("llm_model")
    model_s = f" (model: `{model}`)" if model else ""

    out = [f"### 🔬 LLM semantic analysis — advisory, NON-GATING{model_s}", ""]
    out.append(
        "Informational only. The gate decision comes from the deterministic static scan "
        "above; these semantic findings are nondeterministic across model/prompt changes, "
        "so they are reported for human review and never block the PR."
    )
    out.append("")
    if not issues:
        out.append("No semantic findings.")
        out.append("")
        return "\n".join(out)

    known = [it for it in issues if is_baselined(it, accepted) is not None]
    novel = [it for it in issues if is_baselined(it, accepted) is None]
    out.append(
        f"**{len(issues)} semantic finding(s)** — {len(novel)} not matched by the static "
        f"baseline, {len(known)} also covered by it."
    )
    out.append("")

    def _table(title: str, items: list[dict]) -> None:
        out.append(f"#### {title}")
        out.append("")
        out.append("| Sev | ID | Skill | Category | Pattern | Location | Conf |")
        out.append("|-----|----|-------|----------|---------|----------|------|")
        for it in sorted(items, key=lambda i: SEV_ORDER.get((i.get("severity") or "").upper(), 9)):
            out.append(_issue_row(it))
        out.append("")

    if novel:
        _table("Semantic-only — not matched by the static baseline (review, not gating)", novel)
    if known:
        _table("Also covered by the static baseline", known)
    return "\n".join(out)


def main() -> int:
    ap = argparse.ArgumentParser(description="Render SkillSpector JSON -> job summary + SARIF, apply baseline gate.")
    ap.add_argument("report_json")
    ap.add_argument("sarif_out")
    ap.add_argument("path_prefix", nargs="?", default=".agents/skills")
    ap.add_argument("--baseline", help="Path to skillspector-baseline.yml (accepted findings).")
    ap.add_argument("--decision-file", help="Write the gate decision (pass|fail) here for the workflow to read.")
    ap.add_argument("--advisory", help="Path to a secondary (LLM) report rendered as a NON-GATING advisory section. Never affects the decision.")
    args = ap.parse_args()

    def write_decision(value: str) -> None:
        if args.decision_file:
            Path(args.decision_file).write_text(value + "\n")

    p = Path(args.report_json)
    if not p.exists() or p.stat().st_size == 0:
        Path(args.sarif_out).write_text(json.dumps(empty_sarif()))
        write_decision("fail")  # an incomplete scan must not pass the gate
        print("## 🛡️ SkillSpector scan\n\n❌ **Gate: FAIL** — no JSON report produced; the scan likely errored before completing. See the scan step log.\n")
        return 0

    report = json.loads(p.read_text())
    accepted, baseline_err = load_baseline(args.baseline)

    issues = report.get("issues") or []
    active = [it for it in issues if is_baselined(it, accepted) is None]
    accepted_issues = [it for it in issues if is_baselined(it, accepted) is not None]

    Path(args.sarif_out).write_text(json.dumps(build_sarif(report, args.path_prefix, accepted)))
    print(build_summary(report, active, accepted_issues, accepted, baseline_err))

    # Advisory LLM section (non-gating). Rendered only when a non-empty report is given;
    # a parse error is noted but never changes the gate decision.
    if args.advisory:
        ap_path = Path(args.advisory)
        if ap_path.exists() and ap_path.stat().st_size > 0:
            try:
                print(build_advisory(json.loads(ap_path.read_text()), accepted))
            except json.JSONDecodeError as exc:
                print(f"\n### 🔬 LLM semantic analysis — advisory, NON-GATING\n\n> ⚠️ Advisory report could not be parsed ({exc}); skipped. Gate unaffected.\n")

    # Gate: fail on ANY active (non-baselined) finding, or on a baseline parse error.
    # The advisory (LLM) report above NEVER contributes to this decision (policy A).
    write_decision("fail" if (active or baseline_err) else "pass")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
