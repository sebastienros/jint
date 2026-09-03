---
paths:
  - "/Jint/**"
---

You are editing the engine assembly. `Jint/AGENTS.md` carries the key runtime types, the namespace map, the engine-source conventions (code patterns, data structures, visibility), and a pointer to the table of what counts as a frozen public contract, which lives in `Jint.Tests.PublicInterface/AGENTS.md` beside the baselines that pin it. Most changes here are visible to embedders even when the signature does not change.

**Read [`Jint/AGENTS.md`](../../Jint/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.
