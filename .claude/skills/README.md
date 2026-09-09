# `.agents/skills/`

Shared, tool-agnostic skills for this project. One subfolder per skill:

```
.agents/skills/<skill-name>/SKILL.md
```

`SKILL.md` format:

```markdown
---
name: skill-name
description: One line — what this skill does and when an agent should use it.
---

# Skill body

Step-by-step instructions, conventions, or a checklist the agent should
follow whenever this skill applies.
```

Any agent tool wired per `AGENTS-STANDARD.md` reads skills from here,
either directly (most tools) or through a symlink at the tool's own fixed
skills directory (Claude Code: `.claude/skills`). Never author a skill a
second time inside a tool-specific folder — add it here once.

Delete `example-skill/` once you've added your project's real skills; it
exists only to show the expected shape.
