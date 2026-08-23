---
paths:
  - "Jint/**/*.cs"
---

This rule exists because the file it points at would otherwise never fire. `Jint` multi-targets down to `net462`, so write the call site the way you would on .NET 10 and add the missing API to `Jint/Extensions/Polyfills.cs` rather than open-coding an old-framework equivalent or scattering `#if` through a spec algorithm. `Jint/Extensions/AGENTS.md` has the container rules and the ways a polyfill silently stops being one.

**Read [`Jint/Extensions/AGENTS.md`](../../Jint/Extensions/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.
