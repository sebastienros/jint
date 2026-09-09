---
paths:
  - "Jint/WebApi/**"
  - "Jint/Options.WebApi.cs"
  - "Jint/Engine.WebApi.cs"
---

You are editing the opt-in WHATWG surface. `Jint/WebApi/AGENTS.md` carries the four conventions that hold the subtree together, the whole-file `#if NET8_0_OR_GREATER` gate, and WebIDL property attributes — enumerable, which is the opposite of ECMAScript's rule and of the generator default.

**Read [`Jint/WebApi/AGENTS.md`](../../Jint/WebApi/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.

When what you are editing is `fetch`, a cookie jar, a redirect hop or `FetchObserver`, read [`Jint/WebApi/Fetch/AGENTS.md`](../../Jint/WebApi/Fetch/AGENTS.md) too.

When it is `EventTarget`, a listener or the dispatch itself, the algorithm — both lanes, the microtask checkpoint a listener returns to, the tree seams and the bound on every walk — is beside the classes in [`Jint/WebApi/Events/event-dispatch.md`](../../Jint/WebApi/Events/event-dispatch.md), which that file points at.
