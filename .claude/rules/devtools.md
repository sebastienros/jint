---
paths:
  - "Jint.DevTools/**"
  - "tools/devtools-protocol/**"
---

You are editing the Chrome DevTools Protocol server. `Jint.DevTools/AGENTS.md` carries the thread rule (a `JsValue` never leaves the engine thread; a transport thread only moves strings), the protocol pin and how to regenerate, and the manifest rule that an unimplemented method is `-32601` before its parameters are read. `Jint.DevTools/Session/AGENTS.md` carries the mailbox a command crosses to reach the engine, targets and sessions, why a target outlives its engine, and the pause-time message loop. `Jint.DevTools/Domains/AGENTS.md` carries what a domain promises about a value a client cannot see.

**Read [`Jint.DevTools/AGENTS.md`](../../Jint.DevTools/AGENTS.md) before you edit, and [`Jint.DevTools/Session/AGENTS.md`](../../Jint.DevTools/Session/AGENTS.md) when what you are editing is a session, a target or the pause loop.** It is not repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.
