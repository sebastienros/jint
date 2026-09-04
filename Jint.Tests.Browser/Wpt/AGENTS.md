# Agent instructions: the web-platform-tests browser lane

> **Read this when:** You are touching `Jint.Tests.Browser/Wpt/` — the driver, the results overlay, the
> synthesized wrappers, the exclusion table or the census.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, and read
> [`Jint.Tests/Wpt/AGENTS.md`](../../Jint.Tests/Wpt/AGENTS.md) beside it — the corpus, the pin, the server and
> the exclusion vocabulary are that lane's and are shared, so every rule in it applies here and none of them
> is repeated below.

### What this lane is

The `.any.js` lane hands a file to an engine. **This lane loads a document.** A vendored `.html` is served by
`WptServer` at a real URL, a `Browser` navigates a fresh `Page` to it, and the document pulls in upstream's own
`resources/testharness.js` through a `<script src>` the way a browser does — the parser driver
([`Jint.Browser/Runtime/AGENTS.md`](../../Jint.Browser/Runtime/AGENTS.md)) is what makes that work. The realm is
a real `Window` with a `document`, a `<div id=log>` and a `load` event, so the harness is the real one and
there is nothing of Jint's between the corpus and the verdict. That is the whole point: `WptHarnessTests`
exists in the other lane because a shim that quietly passed everything would make five thousand cases mean
nothing, and here there is no shim to test.

**One corpus, one pin.** Everything this lane runs is under `Jint.Tests/Wpt/Vendor/`, at the commit
`Vendor/README.md` names, byte-verified the way that file describes. `Jint.Tests.Browser` holds no copy of a
vendored file and never will; it references `Jint.Tests` and reaches `WptCorpus`, `WptServer`,
`WptExclusion`/`WptDivergence` and the census machinery through `InternalsVisibleTo`. The dependency is one
way — **nothing in `Jint.Tests` may reference `Jint.Browser`** — which is what keeps the engine's own suite
free of a browser.

`Wpt/README.md` is this lane's `Vendor/README.md`: the suites, the deliberately-not-vendored table with a
reason per row, what the corpus says about this browser, and the census. Read it for *what* is here; this file
is *how* it works.

**There is a second wpt number and it is not this one.** `.github/workflows/wpt-scoreboard.yml` runs
*upstream's* `wpt run` nightly against `jint-browser serve` and publishes a page; the plugin that makes it
possible is [`tools/wpt-scoreboard/`](../../tools/wpt-scoreboard/README.md). It runs the same corpus at the
same pin — it reads the commit out of `Vendor/README.md` — but it is not a gate, it has no exclusion table,
and it runs whole suites rather than a vendored subset. So: **a failure there is never an entry here.** A
divergence the scoreboard finds in a suite this lane does not vendor is a candidate for vendoring, and one it
finds in a suite this lane *does* vendor is a defect this table should already name — if it does not, the
table is what is wrong. Nothing under `tools/wpt-scoreboard/` may become a second answer to "did this
subtest pass": every judgement there is upstream's `testharness_result_converter`, on purpose.

### A suite is a directory, and a case is a path on the server

`WptCorpus.BrowserSuites` names them and `WptCorpus.BrowserTestFiles` lists a directory's own documents without
descending, exactly as `TestFiles` does for `.any.js`. So a `resources/` or `support/` child holds the helpers
a case loads and never a case of its own, and a document belongs to exactly one suite.

A **case** is a path the server answers, which is not the same as a file on disk. Two kinds:

* a **vendored document** — `dom/events/Event-propagation.html`, bytes in this repository;
* a **synthesized wrapper** — `dom/events/Event-constructors.any.html`, which exists nowhere. Upstream's
  `AnyHtmlHandler` manufactures one per `.any.js` file, and `WptServerWrappers` is the port of it: the
  `self.GLOBAL` shim, the two harness scripts, one `<script src>` per `// META: script=`, the `<div id=log>`
  and then the file. So the `.any.js` corpus a suite already has runs **again** here, in a `Window` realm under
  the real harness, without being vendored twice — and the two lanes are allowed to disagree about a file,
  because a divergence only a document exposes is what this lane is for.

**The dedicated-worker wrapper is deliberately not generated.** `WorkersHandler`'s document creates a *classic*
worker whose generated body opens with `importScripts("/resources/testharness.js")`, and Jint runs module
workers only — so it would throw before registering a test, which is why `workers/*.worker.js` is a
not-vendored row in the other lane rather than an exclusion. Generating it would manufacture one red document
per file that says only that. The `.any.js` corpus *is* run in a real worker, by the engine lane's worker lane,
which builds a module worker instead.

`EverySynthesizedCaseIsAWrapperTheServerGenerates` holds both halves: a synthesized case must be a wrapper the
server really answers (the underlying file vendored, its `// META: global=` naming the window), and a vendored
document must not look like one.

### The overlay, and the three decisions in it

`Wpt/Prelude/testharnessreport.js` is what `WptServer` answers `/resources/testharnessreport.js` with, through
the per-instance overlay that constructor takes. Upstream ships a stub whose whole purpose is to be replaced;
`Jint.Tests/Wpt/Prelude/testharnessreport.js` reproduces that stub for every other caller, and this file is the
browser lane's replacement. Nothing under `Vendor/` changes for it, which is the point of the slot.

* **It posts strings, never values.** `__jintWptReport` is a delegate the driver installs on *every* page
  engine — every one, because a navigation builds a new engine and this lane reaches its documents by
  navigating — and everything handed to it is one JSON string. A page's engine and its DOM belong to that
  page's own thread, so a `JsValue` crossing to the driver would be a value belonging to a thread the driver is
  not on. One `JSON.stringify` makes that boundary visible instead of a rule somebody has to remember. The
  overlay then **deletes its own global**, before the test file is even fetched, so a document that enumerates
  `window` does not find this lane's plumbing among the results.
* **Which page a report belongs to is answered by the engine it came from**, `PageRuntime.Find(engine).Page`,
  and never by a slot the driver sets before each case. Fixtures run in parallel, so several pages are open at
  once; a slot would be a race whose symptom is one document's results appearing in another's report.
* **It turns the harness's own output off**, with `setup({output: false})`. That is the sanctioned vendor
  call — `Output.prototype.setup` reads the property with `this.enabled = this.enabled && …` and says in a
  comment that a test may not override a report file's decision — and it is what wpt's own runner does, since
  a driver taking results programmatically has no use for a rendering of them. It also stepped around a real
  gap: the renderer calls `Element.insertAdjacentText`, which this package's bindings did not then have, and with the
  output on that threw out of the harness's *own* completion callback, which is registered before this file's
  — so a throw there took every result with it and every document in the lane timed out with no report at all.
  The missing member is recorded in `README.md` rather than left as something this line quietly hides.

### The second overlay: `testdriver-vendor.js`

`Wpt/Prelude/testdriver-vendor.js` is the other slot upstream ships for a vendor to fill, and it is what turns
a document that drives input through `test_driver` into one that reports. Upstream's file at that path is
**empty** and *is* vendored — unlike `testharnessreport.js`, whose whole content is a placeholder — so
`WptServer`'s second overlay parameter defaults to serving the corpus copy, and a caller that passes none gets
`testdriver.js`'s own "not implemented by testdriver-vendor.js" rejections.

* **It dispatches nothing itself.** `__jintWptInput` is a delegate installed on every page engine beside
  `__jintWptReport` — `testharnessreport.js` makes it non-enumerable on the way past, because a document that
  never loads this file would otherwise leave it among the names an enumeration of `window` finds, and this
  file deletes it outright when it runs. Every call hands it one JSON string describing one input event, and
  `WptBrowserInput` decodes it into `Jint.Browser`'s `InputDispatcher` — the same flat hit test `Input.dispatchMouseEvent`
  reaches and the same key dispatch `Input.dispatchKeyEvent` reaches. **A second implementation here would be
  a second answer to "what does a click do",** and the whole value of running these documents is that a wpt
  case and a Puppeteer client cannot disagree.
* **What is here is coordinate resolution and unpacking, because that is all that can be.** A WebDriver
  `origin` is an element, and turning one into a point needs `getClientRects` — the page's DOM, on the page's
  thread. So the resolution happens in the page and only numbers cross, which is the results overlay's
  boundary in the other direction. `duration` is ignored: there is no rendering to animate a move across.
* **Three calls are implemented and the rest stay upstream's rejections.** `click`, `send_keys` and
  `action_sequence` are what the vendored documents use. Cookies, permissions, window rects and the BiDi
  surface are *not* accepted-and-ignored: a call that silently succeeds and changes nothing turns a missing
  environment into an assertion failure three lines later, which is the harder failure to read. Adding one
  means a vendored document needs it.

**Uncaught exceptions are upstream's business here, not the driver's, and that is the one place this lane
deliberately does not mirror the other one.** `testharness.js` registers `error` and `unhandledrejection`
listeners at the global scope, and Jint fires both (`Jint/WebApi/GlobalEvents/GlobalEventTarget.cs`), so an
exception escaping a listener, a timer callback or a microtask becomes a harness `ERROR` — or the file's one
test failing, or nothing at all under `setup({allow_uncaught_exception: true})` — by upstream's own code and at
upstream's own rules. The `.any.js` driver synthesizes that rule because its shim has no global event target to
listen at; doing it again here would both double-count the failure and override the one property that
`setup` call exists to declare. The **single** exception is `PageErrorKind.BudgetExceeded`, which is the pump
abandoning a turn: no exception reaches script and nothing fires, so `WptBrowserHarness.BudgetFailure` reads it
out of `Page.Errors` and makes it a harness error. It should never happen — see the next section — and seeing
one means a constraint this lane did not arm is bounding a page and the file's results are not trustworthy.

### The environment a document runs in

One `WptServer` and one `Browser` for the whole lane, and a fresh `BrowserContext` and `Page` per case. The
server is process-wide for the reason the engine lane's is; the browser holds no thread of its own, so neither
is worth building per fixture. What *is* per case is the isolation that matters: its own cookie jar, its own
storage partition, its own thread, its own engine, its own realm.

**A page that never yields hangs the lane, and nothing here can stop it.** The driver's deadline is a wait on
another thread, so it ends a page that is *idle and not done* and not one whose engine is in a loop; with
`MaxTaskDuration` infinite (below) there is no constraint armed either — and a walk that never re-enters the
engine would not meet one even if it were armed. Four `dom/traversal/` documents found this: AngleSharp's
`TreeWalker.ToPrevious` did not terminate, which is why DOM §6.1's seven traversals are `Jint.Browser`'s own
([`Jint.Browser/Dom/Views/DomTreeWalker.cs`](../../Jint.Browser/Dom/Views/DomTreeWalker.cs), and its file
argues each loop's termination) and why all four are cases now. **A new suite that wedges the run rather than
failing it is that shape**, and while it is unfixed the document is a `NotVendored` row naming the defect —
never a document deleted, and never one left in to hang the lane.

* **`BrowserOptions.MaxTaskDuration` is `Timeout.InfiniteTimeSpan`.** `PageBudget` brackets every page turn with it and
  reports a `PageErrorKind.BudgetExceeded`; a legitimately slow wpt file would be cut mid-script and the
  failure would read as an engine defect three layers from its cause. The bound here is the **driver's own**
  per-file deadline (`WptBrowserHarness.Deadline`, 30 s, and no deadline under a debugger because a breakpoint
  is not a hang), and before that upstream's harness timeout, which is the one that usually fires first and is
  left exactly as upstream sets it.
* **The context's `UrlFilter` is the server's own `Owns`**, so the oldest promise this corpus makes is kept
  here too: no document can open a socket to anything but the loopback port, on the first hop and on every
  redirect.
* **The driver waits in slices.** A page is not idle while the harness's timeout timer is scheduled, and it
  can be idle and *not* done, because a request in flight is a thread-pool completion nothing on the engine's
  queue reports — the same reason the engine lane's server-lane drive loop polls. So the wait is
  `WaitForIdleAsync` in short slices until the completion callback has fired, and a slice that comes back idle
  waits off the page's own thread rather than spinning on it.

### The exclusion table, the five categories, and where a divergence goes

The rule is the engine lane's, unchanged: **an entry must match at least one failing test and no passing one**,
so a fix, a rename or a corpus bump makes the run fail until the table is brought back in line, and a `*` glob
can never widen into a blanket. `WptBrowserExclusions` holds all three tables — what is not vendored, the
minimum-test counts, and the exclusions — because the runner is a driver and those are an inventory.

The vocabulary is `WptDivergence`, shared. Six of its members exist for this lane, and the first four say so on
themselves: `NeedsLayout`, `NeedsIframeScripting`, `NeedsIndexedDb`, `NeedsTestDriver`, `NeedsXmlDocuments` and
`NeedsMoreEventInterfaces` — the last two being a shape rather than `NeedsTriage`'s: a page here parses HTML, AngleSharp builds
no XML document, and the rows name what would move them rather than a fix somebody owes. **`NeedsTestDriver` has no
entries any more**: campaign item C4 mapped `testdriver.js` onto the same `InputDispatcher` the protocol's
`Input` domain reaches, and the seven documents that were waiting for it were re-examined one at a time —
five are cases now, and the two that still cannot report are `NotVendored` rows naming what each really needs
(a rendering, and a pseudo-element model) rather than the driver. The member stays, because the rest of
`test_driver` is deliberately still upstream's rejections and the next suite this lane vendors may need it. `NeedsTriage` means what it means
everywhere: **a genuine defect the corpus found, recorded rather than fixed so that the change which
first runs a suite is not also the change that moves the engine.** A non-zero count there is a list somebody
owes a fix for, and `README.md` names each one.

**Where a divergence goes is decided by whether the document can produce a report at all.** The driver's unit
is a test, so a document that produces none — a harness `ERROR` or `TIMEOUT`, a page the driver's own deadline
had to end — is a harness error covering the whole file and belongs in `NotVendored` with its reason, never in
the exclusion table. That is the ninth rule of the other lane's file, and it decides more here than it does
there: a document is a whole environment, and the ways one can fail to report — a frame that had to run script,
a navigation the page really performed, a `javascript:` URL, a document that replaced itself — have no analogue
in a file handed to an engine.

### The other generated table: what each failure is

`README.md`'s **cause table** — "What the DOM corpus says about this browser" — counts, per cause, how many
tests fail and over how many documents. Both columns are generated, and the machinery is deliberately unlike
the census's in one way and identical in another.

**A cause is a value, not a comment.** `WptBrowserExclusions.Causes` is the exclusion table split into named
groups and `All` is that flattened, so what used to be a `// ----` header is now the name of the array under
it. That name is what a README row keys on, in an `<!-- cause: … -->` comment the rendered page does not show
— **the prose stays hand-written and only the two numbers and the order are rendered.** A group renamed in
one place and not the other is the mistake this exists to catch, and it fails for free.

**Two properties make the arithmetic mean anything**, and `WptBrowserCauseTests` holds both: every failing
test in the lane is claimed by **exactly one** cause — the runner already refuses a failing test no exclusion
names, and this adds that no two name the same one — and a cause that accounts for anything in the six suites
the section is about has a row while a row accounts for something in them.

**Neither column is a ceiling**, and that is the difference from the census. `Not passing` bounds *how much*
fails, so it may only go down; these two say what each failure *is*, so a cause that grew and a cause that
shrank are equally a table that has stopped describing this browser. Both are equalities in both directions.

The reason it is built this way is on the class: the columns were typed, nothing counted them, and nine of
seventeen rows had gone stale in both directions before anybody measured — one cause growing 165 → 478 as a
fix uncovered a table a missing member had been hiding, another shrinking 65 → 24 as its own fix landed.

### The census is a ceiling

`Wpt/README.md`'s table is generated by `WptBrowserCensusTests`. `Documents` and `Synthesized` are read off the
corpus and `Tests` counts registrations, so those three are equalities in both directions. **`Not passing` is
the only column that counts outcomes, and it only ever goes down**: a rise fails as a regression naming the
suite and the size of it, a fall fails as staleness, and `JINT_WPT_BROWSER_CENSUS=update` refuses to write a
larger figure. A check satisfiable by re-baselining a bad run is not a ceiling, it is a suggestion — which is
what [#3339](https://github.com/sebastienros/jint/issues/3339) records three unrelated pull requests being
invited to do with the engine lane's table. The one deliberate spelling that may raise it is
`JINT_WPT_BROWSER_CENSUS=update-raising-the-ceiling`, for a corpus bump that genuinely arrives with new
failures, and it leaves the raised numbers in the diff.

Like the engine lane's, the measured half is opt-in — totalling the table means running every document — and
Windows-only, because a `TIMEOUT` is an outcome a loaded machine can produce on its own.

### Adding a suite

1. Vendor its documents and their helpers into `Jint.Tests/Wpt/Vendor/` at the pin, LF, byte-verified the way
   `Vendor/README.md` describes, and add the directory to `WptCorpus.BrowserSuites`.
2. Give the suite a `[TestCaseSource]` in `WptBrowserTestRunner`. `EveryCaseIsReachedByExactlyOneSource` fails
   until it has one, and `EveryVendoredDocumentIsAccountedFor` fails until every document is a case, a
   `NotVendored` row or a helper under `resources/`/`support/`.
3. Run it, then write the three tables from what it reported — never the other way round.
4. Re-census (`JINT_WPT_BROWSER_CENSUS=update`) and add the suite's row to `README.md`'s prose.

The engine lane's `_notVendored` covers `.any.js` and this lane's covers documents, so a directory both lanes
touch has a row in each; neither table may name a file the other vendors.
