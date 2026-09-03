---
paths:
  - "Jint.Tests.Browser/Wpt/**"
---

You are editing the web-platform-tests **browser** lane: the driver that loads a vendored `.html` document into a real `Page` and reads its results back through upstream's own `testharness.js`. `Jint.Tests.Browser/Wpt/AGENTS.md` explains what the lane runs, why the results overlay posts strings and never values, how a `.any.js` file becomes a synthesized `.any.html` wrapper, the five categories that exist for this lane alone, and why the census's "Not passing" column only ever goes down.

**Read [`Jint.Tests.Browser/Wpt/AGENTS.md`](../../Jint.Tests.Browser/Wpt/AGENTS.md) before you edit**, and [`Jint.Tests/Wpt/AGENTS.md`](../../Jint.Tests/Wpt/AGENTS.md) beside it — the corpus, the pin, the server and the exclusion vocabulary are that lane's and are shared, so a change to either reaches both. Neither is repeated here or in the repository-root `AGENTS.md`; that file's index says what each co-located instruction file covers.

The corpus is vendored once. A document goes under `Jint.Tests/Wpt/Vendor/` at the pin that file names, byte-verified the same way, and `Jint.Tests.Browser` never holds a copy of one.
