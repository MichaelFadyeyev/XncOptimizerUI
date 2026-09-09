# `.agents/agents/`

Shared, tool-agnostic project-level agent (subagent) definitions. One file
per agent:

```
.agents/agents/<agent-name>.md
```

Format:

```markdown
---
name: agent-name
description: One line — what this agent is for and when it should be invoked.
tools: []        # optional — restrict which tools this agent may use, if your tool supports it
model: inherit    # optional — pin a model, if your tool supports it
---

You are <role>. <System prompt / instructions for this agent.>
```

Any agent tool wired per `AGENTS-STANDARD.md` reads project-level agents
from here, either directly or through a symlink at the tool's own fixed
directory (Claude Code: `.claude/agents`). Never author an agent a second
time inside a tool-specific folder — add it here once.

See `example-agent.md` for the expected shape; delete it once you've added
your project's real agents.
