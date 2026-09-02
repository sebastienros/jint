---
paths:
  - "Jint.Browser/**"
  - "tools/dom-bindings/**"
---

You are editing the DOM bindings. `Jint.Browser/AGENTS.md` carries the founder's AngleSharp principle (an AngleSharp defect is reported upstream, never worked around silently), what is generated versus hand-written, how AngleSharp's `[DomName]` attributes are read as WebIDL, the override table, the conversion table's divergences in both directions, wrapper identity, and the shape discipline every prototype is held to.

**Read [`Jint.Browser/AGENTS.md`](../../Jint.Browser/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.

Never hand-edit a file under `Jint.Browser/Dom/Generated/`: `DomBindingsStalenessTests` runs the same emitter in memory and fails on any difference. Regenerate with `JINT_DOM_BINDINGS=update`.
