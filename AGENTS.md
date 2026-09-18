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
- Before implementing new feature or bug fixingread read `.agents/code-analysis.md`.
<!-- after user submits implementation or bug fixing ask to update information in `.agents/code-analysis.md`. -->
- Check `.agents/todos.md` for deferred work items / future hooks relevant to the area you're touching.

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
- Maximize code reusability.
- Prefer to use functional paradigm when building piplenes, divide long methodes on chain of short methodes/functions with self-descriptive names.
- Prefer global wpf resources storing in separate file, referenced in App.xaml.
- Refactoring xaml files try to decompose them on separate components.
- In xaml code separate containers by empty rows; before container add comment to describe it purpose / content 
- Add x:Name attribute to xaml containers, tables, textboxes, inputs, etc.
- For complex ViewModels (WPF), keep member order: 
  1) private const fields,
  2) private readonly fields, 
  3) private fields, 
  4) constructor,
  5) `#region Properties` (public properties), 
  6) `#region ObservableProperties` (`[ObservableProperty]` fields, with their `OnChanging`/`OnChanged` partials kept right after each), 
  7) `#region Commands` (`[RelayCommand]` methods),
  8) `#region Methods` (private helper methods).

## Directory map

<!-- Where key code lives, so an agent doesn't have to rediscover it. -->

## Things to avoid

<!-- Footguns, deprecated paths, files agents should never touch, etc. -->
