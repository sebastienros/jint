---
paths:
  - "/Jint.SourceGenerators/**"
  - "/Jint.SourceGenerators.Interop/**"
  - "/Jint.Tests.SourceGenerators/**"
---

You are editing a Roslyn source generator. `Jint.SourceGenerators/AGENTS.md` carries why there are two analyzer assemblies and why only one may ever be packed, the consumer-facing Roslyn pin that a bump silently breaks, the value-equality every model type in the pipeline exists to preserve, and the snapshot discipline that is the only review the emitted C# gets.

**Read [`Jint.SourceGenerators/AGENTS.md`](../../Jint.SourceGenerators/AGENTS.md) before you edit, and [`Jint/Runtime/Interop/AGENTS.md`](../../Jint/Runtime/Interop/AGENTS.md) when what you are editing is what `[JsAccessible]` emits or declines.** Neither is repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.
