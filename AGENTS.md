@~/.agents/AGENTS-STANDARD.md
<!--
  Canonical instruction file for every AI coding agent working in this
  repository (Claude Code, GitHub Copilot, Cursor, Codex, Gemini CLI,
  Windsurf, Aider, and others). Every agent reads this file before doing
  any work. Do not duplicate these instructions into a tool-specific file
  (CLAUDE.md, .github/copilot-instructions.md, ...) — those files only
  redirect here. See AGENTS-STANDARD.md in this kit for the full rationale.
-->


# AGENTS.md

## Folder structure convention (do not remove this section)

- Shared, reusable **skills** live in `.agents/skills/<skill-name>/SKILL.md`.
- Shared, reusable **project-level agent definitions** live in
  `.agents/agents/<agent-name>.md`.
- Create `.agents/skills/` or `.agents/agents/` the first time you need to
  add a skill or agent — they are not scaffolded ahead of time.
- Tool-specific files (`CLAUDE.md`, `.github/copilot-instructions.md`, ...)
  must only redirect to this file, never duplicate it.
- If a tool requires its own fixed skills/agents directory (e.g. Claude
  Code's `.claude/skills`, `.claude/agents`), that directory must be a
  symlink into `.agents/skills` / `.agents/agents`, not a separate copy.
  Run `scripts/bootstrap-agents-structure.sh` to set this up.

## Project overview

<!-- One or two paragraphs: what this project is, who it's for. -->

## Setup

<!-- Commands to install dependencies and get a working dev environment. -->

## Build, test, and lint commands

<!-- The exact commands an agent should run, e.g.:
- Install: `...`
- Build: `...`
- Test: `...`
- Lint / format: `...`
-->

## Code style & conventions

<!-- Language/framework conventions, naming, formatting rules, anything a
     linter doesn't already enforce. -->

## Directory map

<!-- Where key code lives, so an agent doesn't have to rediscover it. -->

## Things to avoid

<!-- Footguns, deprecated paths, files agents should never touch, etc. -->
