@AGENTS.md

## Claude Code notes

- Shared skills live in `.agents/skills/` (bridged to `.claude/skills/` via
  symlink — run `.scripts/bootstrap-agents-structure.sh` if that symlink is
  missing).
- Shared project-level subagents live in `.agents/agents/` (bridged to
  `.claude/agents/` the same way).
- Author new skills/agents under `.agents/`, never directly under
  `.claude/` — see `AGENTS-STANDARD.md`.