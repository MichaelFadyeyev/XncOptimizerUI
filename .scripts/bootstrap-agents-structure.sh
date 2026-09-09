#!/usr/bin/env bash
# Idempotent setup for the shared AGENTS.md / .agents/ folder structure
# standard (see AGENTS-STANDARD.md). Safe to re-run at any time: it only
# creates what's missing and never overwrites existing content.
#
# This script is meant to live at <project-root>/scripts/bootstrap-agents-structure.sh.
# The project root is derived from the script's own location (the parent
# of the "scripts" folder it's in) — NOT from your current working
# directory — so it's safe to run regardless of where you `cd`'d from:
#   ./scripts/bootstrap-agents-structure.sh          (from the project root)
#   ./bootstrap-agents-structure.sh                  (from inside scripts/)
#   /path/to/project/scripts/bootstrap-agents-structure.sh   (from anywhere)
# Pass an explicit root as $1 to override this (e.g. to target a different
# project than the one the script physically lives in).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ROOT="${1:-$DEFAULT_ROOT}"
cd "$ROOT"

# Where to look for template/AGENTS.md, if present (only exists when running
# straight out of the unzipped kit, before its template/ contents have been
# copied into a project — see the kit's top-level README.md).
KIT_DIR="$DEFAULT_ROOT"

echo "Bootstrapping shared agent structure in: $ROOT"

# 1. Shared skills / agents folders
mkdir -p .agents/skills .agents/agents

# 2. AGENTS.md (canonical instructions)
if [ ! -f AGENTS.md ]; then
  if [ -f "$KIT_DIR/template/AGENTS.md" ]; then
    cp "$KIT_DIR/template/AGENTS.md" AGENTS.md
  else
    cat > AGENTS.md <<'EOF'
# AGENTS.md

## Folder structure convention (do not remove this section)

- Shared, reusable skills live in `.agents/skills/<skill-name>/SKILL.md`.
- Shared, reusable project-level agent definitions live in
  `.agents/agents/<agent-name>.md`.
- Tool-specific files (CLAUDE.md, .github/copilot-instructions.md, ...)
  must only redirect to this file, never duplicate it.
EOF
  fi
  echo "  created AGENTS.md"
else
  echo "  AGENTS.md already exists, left untouched"
fi

# 3. CLAUDE.md bridge (Claude Code doesn't read AGENTS.md natively)
if [ ! -e CLAUDE.md ]; then
  cat > CLAUDE.md <<'EOF'
@AGENTS.md

## Claude Code notes

- Shared skills live in `.agents/skills/` (bridged to `.claude/skills/`).
- Shared project-level subagents live in `.agents/agents/` (bridged to
  `.claude/agents/`).
- Author new skills/agents under `.agents/`, never directly under
  `.claude/`.
EOF
  echo "  created CLAUDE.md"
else
  echo "  CLAUDE.md already exists, left untouched"
fi

# 4. Copilot bridge (only needed for older Copilot clients; modern Copilot
#    reads AGENTS.md natively)
mkdir -p .github
if [ ! -f .github/copilot-instructions.md ]; then
  cat > .github/copilot-instructions.md <<'EOF'
See [AGENTS.md](../AGENTS.md) for full project instructions, including
where shared skills (`.agents/skills/`) and shared project-level agents
(`.agents/agents/`) live.
EOF
  echo "  created .github/copilot-instructions.md"
else
  echo "  .github/copilot-instructions.md already exists, left untouched"
fi

# 5. Bridge Claude Code's fixed skill/agent directories to the shared ones
link_dir() {
  local target="$1" link="$2"
  if [ -L "$link" ]; then
    echo "  $link already a symlink, left untouched"
    return 0
  fi
  if [ -e "$link" ]; then
    echo "  WARNING: $link exists and is not a symlink — leaving it alone." >&2
    echo "           Move its contents into ${target#../} and re-run to link it." >&2
    return 0
  fi
  mkdir -p "$(dirname "$link")"
  if ln -s "$target" "$link" 2>/dev/null; then
    echo "  linked $link -> $target"
  else
    echo "  WARNING: could not create symlink $link -> $target" >&2
    echo "           On Windows: enable Developer Mode, or run:" >&2
    echo "           mklink /D \"$link\" \"$target\"" >&2
  fi
}

link_dir "../.agents/skills" ".claude/skills"
link_dir "../.agents/agents" ".claude/agents"

echo "Done."
