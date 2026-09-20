---
applyTo: '**'
description: >-
  Use code-review-graph MCP tools for token-efficient
  codebase exploration and code review.
---

<!-- code-review-graph MCP tools -->
## MCP Tools: code-review-graph

**IMPORTANT: This project has a knowledge graph. When the
code-review-graph MCP server is available, use its tools BEFORE
file/search tools to explore the codebase** — they are faster,
cheaper (fewer tokens), and give you structural context (callers,
dependents, test coverage) that file scanning cannot. If the MCP
tools are not configured in your environment, fall back to
file/search tools directly.

### When to use graph tools FIRST

- **Exploring code**: `semantic_search_nodes_tool` or `query_graph_tool`
- **Understanding impact**: `get_impact_radius_tool`
- **Code review**: `detect_changes_tool` + `get_review_context_tool`
- **Finding relationships**: `query_graph_tool` callers_of/callees_of
- **Architecture questions**: `get_architecture_overview_tool`

Fall back to file/search tools when the graph doesn't cover what
you need — or whenever the MCP tools aren't available.

### Key Tools

| Tool | Use when |
| ------ | ---------- |
| `detect_changes_tool` | Risk-scored change analysis |
| `get_review_context_tool` | Token-efficient source snippets |
| `get_impact_radius_tool` | Blast radius of a change |
| `get_affected_flows_tool` | Impacted execution paths |
| `query_graph_tool` | Trace callers, callees, imports, tests |
| `semantic_search_nodes_tool` | Find functions/classes by keyword |
| `get_architecture_overview_tool` | High-level structure |
| `refactor_tool` | Rename planning, dead code |

### Workflow

1. The graph auto-updates on file changes (via hooks).
2. Use `detect_changes_tool` for code review.
3. Use `get_affected_flows_tool` to understand impact.
4. Use `query_graph_tool` pattern="tests_for" to check coverage.
