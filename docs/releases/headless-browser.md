# Release notes draft: the headless browser and the DevTools protocol

**This is a draft for the maintainer to lift into the v5 release announcement**, not a published changelog —
the repository keeps no changelog, and `.github/workflows/release.yml` produces packages from a `vX.Y.Z` tag
and nothing else. It records what the campaign tracked by
[#3575](https://github.com/sebastienros/jint/issues/3575) shipped: four new packages, the engine seams they
were built on, the upstream contributions the work produced, and what is knowingly left.

Reader-facing documentation is [`README.md`](../../README.md#chrome-devtools-protocol-opt-in-package) and
[the browser section beside it](../../README.md#headless-browser-opt-in-package). The designs and the index of
what was built against them are [`docs/design/devtools-protocol.md`](../design/devtools-protocol.md) §9 and
[`docs/design/headless-browser.md`](../design/headless-browser.md) §12.

## The new packages

| Package | What it is | Targets |
| --- | --- | --- |
| `Jint.DevTools` | A Chrome DevTools Protocol server over a WebSocket, so a debugging client attaches to an engine the host is already running. `Runtime`, `Debugger`, `Profiler`, `Console`, `Log`, `Target`, `Browser`, `Schema`. Native AOT compatible, measured by a published binary a CI leg drives over a real socket. | `net8.0`, `net10.0` |
| `Jint.Browser` | A headless browser: AngleSharp's parser, DOM and CSSOM under Jint, with a page runtime, HTML's script scheduling, custom elements, navigation, forms, cookies, storage, workers, a deterministic box model in place of a layout, and the page-level protocol domains that make a page drivable by Puppeteer, PuppeteerSharp, Playwright and Playwright for .NET. Not trim- or AOT-compatible in this version, because AngleSharp is not trim-annotated. | `net8.0`, `net10.0` |
| `Jint.Browser.Tool` | The `jint-browser` dotnet tool: `serve` publishes the protocol, `fetch` dumps a page as HTML, text, CommonMark or its accessibility tree, `eval` answers an expression evaluated in the page, and `mcp` serves the MCP server on stdio. It consumes only the package's published surface. | `net8.0`, `net10.0` |
| `Jint.Browser.Mcp` | A Model Context Protocol server over the same `Page` API: eighteen tools, an accessibility snapshot carrying a `ref=` on every element that the input tools take in place of a CSS selector, and the page as a resource. A host running a server of its own composes the same tools with `AddJintBrowser()`. | `net8.0`, `net10.0` |

All five are packed by `release.yml` alongside `Jint` and carry a package README of their own.

## The engine seams the campaign added

Every one is additive, public, and usable without any of those packages — which is what lets a third party
build the same thing. The migration guide's own rows are the reference; this is the index.

| Seam | Migration row | PR |
| --- | --- | --- |
| `engine.Tasks.Post(Action)` — the one thread-safe door into a running engine — and `Engine.Advanced.TryGetSourceText(Program, out string)` | [5.8](../v5-migration.md#58-a-host-thread-can-hand-the-engine-work-and-read-back-the-source-a-program-was-parsed-from-3587) | [#3587](https://github.com/sebastienros/jint/pull/3587) |
| `DebugHandler.GetStepLocations` / `FindStepLocation` — where the engine will stop, and snapping a breakpoint onto one | [5.9](../v5-migration.md#59-a-debugger-can-ask-where-the-engine-will-stop-3614) | [#3614](https://github.com/sebastienros/jint/pull/3614) |
| `fetch` as a document's fetch: `BaseUrl`, `Referrer`, `ReferrerPolicy`, `Origin`, `CookieJar`, and the `FetchObserver` seam with interception | [5.10](../v5-migration.md#510-fetch-can-behave-as-a-documents-fetch-3617) | [#3617](https://github.com/sebastienros/jint/pull/3617) |
| Evaluation in any call frame, not only the innermost | [5.11](../v5-migration.md#511-a-debugger-can-evaluate-in-any-call-frame-3622) | [#3622](https://github.com/sebastienros/jint/pull/3622) |
| `PauseOnExceptions`, `ExceptionPauseMode`, `PauseType.Exception` — stopping at the throw, before the unwind | [5.12](../v5-migration.md#512-a-debugger-can-stop-where-an-exception-is-thrown-3623) | [#3623](https://github.com/sebastienros/jint/pull/3623) |
| `XMLHttpRequest`, opt-in, synchronous requests included, and no network grant of its own | [5.13](../v5-migration.md#513-xmlhttprequest-opt-in-and-without-a-network-grant-of-its-own-3626) | [#3626](https://github.com/sebastienros/jint/pull/3626) |
| `ConsoleSink.WantsStackTrace`, and a described function that carries its source | [5.14](../v5-migration.md#514-a-console-sink-can-ask-where-each-message-was-logged-from-3635), [5.15](../v5-migration.md#515-a-described-function-can-carry-its-source-instead-of-a-label-3635) | [#3635](https://github.com/sebastienros/jint/pull/3635) |
| `CallFrame.Program`, `ScriptProfileFrame.Program`, `CoverageSource.Program` — a position matched to a script by identity rather than by name | [5.17](../v5-migration.md#517-a-frame-a-profile-frame-and-a-coverage-source-name-the-program-they-belong-to-3632) | [#3651](https://github.com/sebastienros/jint/pull/3651) |
| `SampledProfile`'s sample, stack, frame and function tables, readable rather than only writable | [5.18](../v5-migration.md#518-a-sampled-profile-is-readable-not-only-writable-3630) | [#3654](https://github.com/sebastienros/jint/pull/3654) |
| `ExceptionPauseMode.Caught` and `StepMode.Unchanged`, so declining a pause cannot cancel a step | [5.19](../v5-migration.md#519-a-debugger-can-stop-on-caught-exceptions-and-decline-a-pause-without-cancelling-a-step-3631) | [#3662](https://github.com/sebastienros/jint/pull/3662) |
| `PerformanceObserver`, `FileReader` and blob URLs | [5.20](../v5-migration.md#520-a-script-can-watch-the-performance-timeline-as-it-fills-3660), [5.21](../v5-migration.md#521-a-blob-can-be-read-through-the-file-apis-reader-3660), [5.22](../v5-migration.md#522-a-blob-has-a-url-and-fetching-one-reaches-no-network-3660) | [#3660](https://github.com/sebastienros/jint/pull/3660) |
| DOM's default passive value, the current event, and a real initialized flag — the four event seams a page needs and an engine has to own | [5.23](../v5-migration.md#523-addeventlistener-implements-doms-default-passive-value-3692), [5.24](../v5-migration.md#524-a-dispatch-maintains-doms-current-event-for-a-window-global-3687), [5.25](../v5-migration.md#525-an-events-initialized-flag-is-real-3686), [4.118](../v5-migration.md#4118-initevent-and-initcustomevent-require-a-type-3686) | [#3696](https://github.com/sebastienros/jint/pull/3696) |
| A structured `ConsoleRecord` sink overload, and `ValueInspector.Describe` — a bounded, getter-free description of any value that runs no script | chapter 5's table | [#3597](https://github.com/sebastienros/jint/pull/3597) |
| The sampling profiler: where script time goes, by sampling the engine's own call stack | chapter 5's table | [#3608](https://github.com/sebastienros/jint/pull/3608) |
| Tree-aware event dispatch, so a host that supplies a node tree gets DOM's event path, retargeting and activation behaviour | none owed — the seam (`TreeParent`) is `internal`, and a target with no tree is untouched | [#3592](https://github.com/sebastienros/jint/pull/3592) |

Eleven behaviour changes came out of the same work and are in chapter 4 rather than chapter 5:
[4.108](../v5-migration.md#4108-a-frame-the-engine-was-entered-at-is-named-and-a-timer-callback-has-one-3635),
[4.109](../v5-migration.md#4109-a-request-built-from-another-request-consumes-it-3618),
[4.110](../v5-migration.md#4110-a-module-a-host-loader-supplied-honours-retainfunctionsourcetext-3588),
[4.111](../v5-migration.md#4111-a-coverage-source-is-one-parse-not-one-name-3632),
[4.112](../v5-migration.md#4112-exceptionthrown-fires-once-per-throw-not-once-per-frame-it-unwinds-through-3624) and
[4.113](../v5-migration.md#4113-an-event-listener-has-a-call-stack-frame-of-its-own-3644),
[4.114](../v5-migration.md#4114-bindfunction-is-a-function-3645),
[4.115](../v5-migration.md#4115-performance-is-an-eventtarget-and-asking-for-it-brings-the-events-3660),
[4.116](../v5-migration.md#4116-the-file-api-brings-the-event-interfaces-with-it-3660),
[4.117](../v5-migration.md#4117-foruntrustedcode-keeps-the-cancellation-token-the-host-registered-3575) and
[4.119](../v5-migration.md#4119-a-fetchobserver-is-told-which-requests-are-xmlhttprequests-and-sees-their-bodies-3575).

## Upstream contributions

Building the binding layer against AngleSharp found defects in AngleSharp itself, and the campaign's rule is
that they are fixed there rather than worked around here. Eight pull requests and seven issues came out of it;
six of the eight are merged, and the issues are the divergences whose fix has to be designed upstream rather
than written from here.

**AngleSharp** — merged:

| | |
| --- | --- |
| [#1310](https://github.com/AngleSharp/AngleSharp/pull/1310) | `IStringMap.Remove` left the `data-*` attribute in place |
| [#1311](https://github.com/AngleSharp/AngleSharp/pull/1311) | `AdjacentPosition` carries the `DomLiterals` annotation, so the enumeration is projectable |
| [#1312](https://github.com/AngleSharp/AngleSharp/pull/1312) | `Document.CurrentScript` answered the deferred-script queue |
| [#1313](https://github.com/AngleSharp/AngleSharp/pull/1313) | A reflected `DOMString` attribute returns the empty string when it is absent |

**AngleSharp** — open:

| | |
| --- | --- |
| [#1314](https://github.com/AngleSharp/AngleSharp/pull/1314) | A `DomSameObject` annotation for the `[SameObject]` IDL members |
| [#1315](https://github.com/AngleSharp/AngleSharp/pull/1315) | The HTML parser tracks an exception escaping host code instead of throwing it or dropping it |
| [#1309](https://github.com/AngleSharp/AngleSharp/issues/1309) | Host-owned navigation and parsing: a `location` setter navigates on the caller's thread, a sync-parse script exception is dropped, and `ReadyState` cannot be set by a host — the issue behind the parser driver's own `readyState` shadow |

**AngleSharp.Css** — merged:

| | |
| --- | --- |
| [#228](https://github.com/AngleSharp/AngleSharp.Css/pull/228) | `matchMedia`'s media query list is evaluated against the render device instead of answering `false` for every query |
| [#229](https://github.com/AngleSharp/AngleSharp.Css/pull/229) | An opt-in switch for CSSOM-compliant colour serialization, for [#227](https://github.com/AngleSharp/AngleSharp.Css/issues/227) — an opaque colour serializes as `rgba(r, g, b, 1)` where CSSOM says `rgb(r, g, b)` |

**AngleSharp.Css** — issues, each one a divergence this work measured; all five are fixed on `devel` (#230–#233 by the maintainer, #234 by our [#235](https://github.com/AngleSharp/AngleSharp.Css/pull/235), which adds `IRenderDevicePreferences`) and wait for a release, which is #3726's job to consume:

| | |
| --- | --- |
| [#230](https://github.com/AngleSharp/AngleSharp.Css/issues/230) | A comma-separated media query list is evaluated as a conjunction |
| [#231](https://github.com/AngleSharp/AngleSharp.Css/issues/231) | `not <known media type>` is always false |
| [#232](https://github.com/AngleSharp/AngleSharp.Css/issues/232) | `Is()` never matches a `CssConstantValue`, so `orientation` and `scan` evaluate wrongly |
| [#233](https://github.com/AngleSharp/AngleSharp.Css/issues/233) | The `scripting` media feature is registered with the `scan` validator |
| [#234](https://github.com/AngleSharp/AngleSharp.Css/issues/234) | `IRenderDevice` has no Media Queries Level 5 user-preference features, which is why the browser answers them itself |

The divergences that are *not* upstream contributions — because they are decisions AngleSharp is entitled to
and this package works within — are the two tables at the end of
[`Jint.Browser/AGENTS.md`](../../Jint.Browser/AGENTS.md) and
[`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md).

## What is measured

- **The obstacle course**: eighteen offline pages built from the libraries' own published bundles — React 18,
  Vue 3, Preact, Svelte 5, jQuery 3.7, Alpine 3, htmx 2, an import map, a `pushState` router, forms, cookies,
  storage, both observers, dialogs — each asserting a DOM end state *and* an empty error sink. Sixteen pass;
  three of them are driven again over the protocol by PuppeteerSharp and by Playwright for .NET.
  [`Jint.Tests.Browser/Fixtures/README.md`](../../Jint.Tests.Browser/Fixtures/README.md).
- **The web-platform-tests browser lane**: 133 vendored documents and 1,417 of upstream's own tests under
  upstream's own `testharness.js`, of which 866 pass. The figure is a ceiling — a rise fails as a regression,
  a fall fails as staleness. [`Jint.Tests.Browser/Wpt/README.md`](../../Jint.Tests.Browser/Wpt/README.md).
- **Native AOT for `Jint.DevTools`**, published and run rather than claimed, with a CI step that fails if any
  trim or AOT diagnostic is attributed to a file in the package.

## What is knowingly left

| | |
| --- | --- |
| [#3701](https://github.com/sebastienros/jint/issues/3701) | `Fetch`'s response stage and the `IO` domain, WebSocket and EventSource observation, and request timing |
| [#3712](https://github.com/sebastienros/jint/issues/3712) | A nullable `DOMString` parameter in the binding generator, which is the single biggest cause in the custom-element corpus |
| [#3720](https://github.com/sebastienros/jint/issues/3720) | `BrowserOptions.UserAgent` reaches script but not the wire |
| [#3721](https://github.com/sebastienros/jint/issues/3721) | A page's browsing context has no `IRenderDevice`, so `@media` and `matchMedia` disagree |
| [#3711](https://github.com/sebastienros/jint/issues/3711) | An already-rejected promise fires `unhandledrejection` before a handler can attach |
| [#3670](https://github.com/sebastienros/jint/issues/3670) | An AngleSharp `DomException` crosses into script as a raw CLR exception |
| [#3698](https://github.com/sebastienros/jint/issues/3698), [#3706](https://github.com/sebastienros/jint/issues/3706), [#3707](https://github.com/sebastienros/jint/issues/3707), [#3684](https://github.com/sebastienros/jint/issues/3684), [#3683](https://github.com/sebastienros/jint/issues/3683) | The seams each area of the work wished it had, filed where the work stopped |

Iframe scripting, `ElementInternals`, scoped custom element registries, real isolated worlds, screenshots and
anything else that needs a rendering are v1 non-goals rather than debt; the design docs say which is which.
