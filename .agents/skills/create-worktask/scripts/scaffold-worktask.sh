#!/usr/bin/env bash
# Skill: create-worktask
# Deterministic scaffolder for a work task file under .context/work-tasks/.
#
# Copies the skill's asset template with placeholder substitution and prints a
# JSON object describing what was created. The skill carries the *judgment*
# (what contexts, requirements and acceptance criteria to write); this script
# only lays down the deterministic skeleton, so the output contract cannot
# drift from the prose describing it.
#
# Tool agnostic: bash + coreutils only. Repo root discovered via git.

set -euo pipefail

SCRIPT_DIR=$(cd -P "$(dirname "$0")" && pwd -P)
SKILL_DIR=$(cd -P "$SCRIPT_DIR/.." && pwd -P)
ASSETS_DIR="$SKILL_DIR/assets"

SLUG=""
TITLE=""
FORCE=0

usage() {
    cat <<'USAGE'
Usage:
  scaffold-worktask.sh <kebab-case-slug> [--title "Human Title"] [--force]

Arguments:
  <kebab-case-slug>   Lowercase, hyphen-separated, e.g. add-vessel-eta-validation
                      Produces .context/work-tasks/<slug>.md

Options:
  --title "..."       One-liner task description for the H1.
                      Defaults to the slug, title-cased.
  --force             Overwrite an existing work task file.
                      Without it, an existing file is refused (exit 1).

Output:
  JSON object on stdout with the slug, title, file path and overwrite flag.

Exit codes:
  0  created
  1  target exists and --force was not passed
  2  usage error (missing or non-kebab-case slug, unknown option)
USAGE
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        -h|--help) usage; exit 0 ;;
        --title) TITLE="${2:-}"; shift 2 ;;
        --force) FORCE=1; shift ;;
        --) shift; break ;;
        -*) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
        *)
            if [ -z "$SLUG" ]; then SLUG="$1"; shift
            else echo "Unexpected argument: $1" >&2; usage >&2; exit 2; fi
            ;;
    esac
done

[ -n "$SLUG" ] || { echo "Error: slug is required." >&2; usage >&2; exit 2; }

# Validate kebab-case: lowercase letters, digits, single hyphens; no leading/trailing/double hyphen.
if ! printf '%s' "$SLUG" | grep -Eq '^[a-z0-9]+(-[a-z0-9]+)*$'; then
    echo "Error: slug must be kebab-case (lowercase letters, digits, single hyphens): '$SLUG'" >&2
    exit 2
fi

REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
TASK_ROOT="$REPO_ROOT/.context/work-tasks"
mkdir -p "$TASK_ROOT"

TARGET="$TASK_ROOT/${SLUG}.md"
OVERWROTE=false
if [ -e "$TARGET" ]; then
    if [ "$FORCE" -eq 1 ]; then
        OVERWROTE=true
    else
        echo "Error: work task already exists: $TARGET (pass --force to overwrite)" >&2
        exit 1
    fi
fi

# Derive title from slug if not supplied: hyphens -> spaces, title-case each word.
if [ -z "$TITLE" ]; then
    TITLE=$(printf '%s' "$SLUG" | tr '-' ' ' | awk '{ for (i=1;i<=NF;i++) $i=toupper(substr($i,1,1)) substr($i,2) } 1')
fi
DATE=$(date +%F)

# Render the template with placeholder substitution.
# Placeholders: {{SLUG}} {{TITLE}} {{DATE}}
SRC="$ASSETS_DIR/WORKTASK.template.md"
[ -f "$SRC" ] || { echo "Error: missing template: $SRC" >&2; exit 1; }
sed -e "s|{{SLUG}}|${SLUG}|g" \
    -e "s|{{TITLE}}|${TITLE}|g" \
    -e "s|{{DATE}}|${DATE}|g" \
    "$SRC" > "$TARGET"

# Emit JSON (path relative to repo root for portability).
rel() { printf '%s' "${1#"$REPO_ROOT"/}"; }
printf '{\n'
printf '  "slug": "%s",\n' "$SLUG"
printf '  "title": "%s",\n' "$TITLE"
printf '  "file": "%s",\n' "$(rel "$TARGET")"
printf '  "overwrote": %s\n' "$OVERWROTE"
printf '}\n'
