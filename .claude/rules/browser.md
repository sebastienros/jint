---
paths:
  - "Jint.Browser/**"
  - "tools/dom-bindings/**"
---

You are editing the browser package — the DOM bindings, or the page runtime built on them. `Jint.Browser/AGENTS.md` carries the founder's AngleSharp principle (an AngleSharp defect is reported upstream, never worked around silently), what is generated versus hand-written, how AngleSharp's `[DomName]` attributes are read as WebIDL, the override table, the conversion table's divergences in both directions, wrapper identity, the shape discipline every prototype is held to, the page loop's thread rule, the `Window` installer's unqualified-call trap, and why the parse must stay synchronous.

**Read [`Jint.Browser/AGENTS.md`](../../Jint.Browser/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.

Never hand-edit a file under `Jint.Browser/Dom/Generated/`: `DomBindingsStalenessTests` runs the same emitter in memory and fails on any difference. Regenerate with `JINT_DOM_BINDINGS=update`.

One thread owns a page's engine and its DOM. Every public `Page` member is a mailbox request, and nothing that belongs to an engine — a `JsValue`, an AngleSharp node — may be in the task it returns; convert inside the request. `Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the baseline for everything the package publishes.
