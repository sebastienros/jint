---
paths:
  - "Jint/Runtime/Interpreter/**"
  - "Jint/Runtime/Coverage/**"
---

You are editing the interpreter. `Jint/Runtime/Interpreter/AGENTS.md` carries the engine-affine vs shareable-state contract and the AST `UserData` invariant that makes a shared `Prepared<Script>` safe, why coverage counters cannot live on a handler node, and what a warmed call site retains.

**Read [`Jint/Runtime/Interpreter/AGENTS.md`](../../Jint/Runtime/Interpreter/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.
