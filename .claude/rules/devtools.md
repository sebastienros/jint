---
paths:
  - "Jint.DevTools/**"
  - "tools/devtools-protocol/**"
---

You are editing the Chrome DevTools Protocol server. `Jint.DevTools/AGENTS.md` carries the thread rule (a `JsValue` never leaves the engine thread; a transport thread only moves strings), the pause-time message loop, the protocol pin and how to regenerate, and the manifest rule that an unimplemented method is `-32601` before its parameters are read.

**Read [`Jint.DevTools/AGENTS.md`](../../Jint.DevTools/AGENTS.md) before you edit.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.
