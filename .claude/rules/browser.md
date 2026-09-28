---
paths:
  - "Jint.Browser/**"
  - "Jint.Tests.Browser/**"
  - "tools/dom-bindings/**"
---

Read [`Jint.Browser/AGENTS.md`](../../Jint.Browser/AGENTS.md) before editing: it defines native-parser versus
Browser ownership, parsing-performance hooks, the LightPanda scope, observer delivery and public seams.
AngleSharp is for comparison benchmarks only; do not add it to Browser, tests or binding generation.

Read the co-located file for the area being changed:

- [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md): explicit binding contract, conversions,
  native identity, lazy creation-realm capture and shared shapes.
- [`Jint.Browser/Events/AGENTS.md`](../../Jint.Browser/Events/AGENTS.md): dispatch, activation, focus and input.
- [`Jint.Browser/Runtime/AGENTS.md`](../../Jint.Browser/Runtime/AGENTS.md): page loop, budgets, navigation and
  the flat geometry documented in [`Layout/box-model.md`](../../Jint.Browser/Layout/box-model.md).
- [`Jint.Browser/Runtime/Parsing/AGENTS.md`](../../Jint.Browser/Runtime/Parsing/AGENTS.md): cooperative native
  parsing on the page loop, resource pumping and child-frame realms.
- [`Jint.Browser/DevTools/AGENTS.md`](../../Jint.Browser/DevTools/AGENTS.md): page targets and protocol domains.
- [`Jint.Browser/Accessibility/AGENTS.md`](../../Jint.Browser/Accessibility/AGENTS.md): accessibility and extraction.

Never hand-edit `Jint.Browser/Dom/Generated/*.g.cs`. Change `contract.json`, regenerate and review the diff;
[`tools/dom-bindings/README.md`](../../tools/dom-bindings/README.md) has the commands.
Historical `overrides.json` and `pin.json` do not drive the emitter.

One page-loop thread owns the engine and mutable DOM. Convert results inside mailbox requests; no
`JsValue` or native DOM node may escape through a returned task.
`Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the public API baseline.
