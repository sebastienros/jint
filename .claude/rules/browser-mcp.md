---
paths:
  - "Jint.Browser.Mcp/**"
  - "Jint.Tests.Browser/Mcp/**"
---

You are editing the Model Context Protocol server over the headless browser. `Jint.Browser.Mcp/AGENTS.md` carries why `BrowserAgent` must stay a thin layer over public `Page` members, why a tool's `[Description]` is the product rather than documentation (and why an XML doc comment silently ships an empty one), why every tool returns a `CallToolResult` and none of them throws — the SDK redacts an ordinary exception's message — and why stdio is the only transport the tool serves.

**Read [`Jint.Browser.Mcp/AGENTS.md`](../../Jint.Browser.Mcp/AGENTS.md) before you edit**, and [`Jint.Browser.Tool/AGENTS.md`](../../Jint.Browser.Tool/AGENTS.md) beside it for the rule both packages exist to keep and the table of seams that pressure has promoted. Neither is repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.

No `InternalsVisibleTo` grant from `Jint.Browser`, ever: a tool that needs something `Page` does not publish is a seam to promote, not a grant to widen.

A tool that found nothing answers `done: false` with what to try instead; only something the agent cannot recover from is `isError`. Every result goes through the source-generated `ToolJsonContext`, and there is no reflective fallback to catch a missing entry.
