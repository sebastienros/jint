# The obstacle course

Offline fixtures that say whether real pages work. Each is a directory of files served over a real socket by
`Navigation/LoopbackServer` and driven through the public `Page` API — and three of them are driven again, over
the protocol, by PuppeteerSharp and by Playwright for .NET (`DevTools/PuppeteerSharpCourseTests`,
`DevTools/PlaywrightCourseTests`).

**Every byte is vendored and nothing reaches the network.** The page's `UrlFilter` is pinned to the loopback
server, so a fixture that reached for a CDN would fail rather than pass on whichever machine happened to have
one. The bundles are the libraries' own published files, byte for byte.

## What a fixture proves, and how one is added

A fixture is the smallest page that makes one thing observable end to end, and a case asserts two things: a
**DOM end state** and that **`Page.Errors` is empty**. The second is the one that matters — a framework that
threw half way through still renders something, so the error sink is what tells a half-working page apart from
a working one. A fixture documented to produce an error names exactly that error instead.

To add one:

1. Make `Fixtures/<name>/index.html` and whatever it loads. Reference a vendored bundle by its absolute path,
   `/vendor/<library>-<version>/<file>` — the server answers the corpus at the paths it is stored at.
2. Add a row to the inventory below. `FixtureInventoryTests` fails if a directory has no row or a row has no
   directory, so the table cannot drift from the corpus.
3. Add one NUnit case, through `FixtureCourse`. Drive it with the public `Page` input members — the same
   ones a protocol client's input goes through — rather than with raw script, and read the end state with
   `harness.js`'s. The fixture must work without any of it.
4. If it does not pass, **do not delete it and do not quietly ignore it.** Mark the case
   `[Explicit("<fixture>: <one-line diagnosis>")]` — the reason has to start with the fixture's directory name,
   which is what `FixtureInventoryTests` matches against the `needs triage` rows here — and set the row's
   status below. A `needs triage` row is somebody's debt, the way a `NeedsTriage` row in the web-platform-tests
   lane is.

`harness.js` is injected by the in-process suite and never loaded by a fixture, and it is now the readers
plus one click: `clickAt`, for the *n*th match of a repeated selector, which the public API has no member
for. Everything else a case does to a page — a click, a fill, a key, a wait — is `Page.ClickAsync`,
`FillAsync`, `PressAsync` and `WaitForAsync`, so a case here and the same case in the PuppeteerSharp or
Playwright suite exercise one input model rather than two. One thing about the driver is still real rather
than tidy: `FixtureCourse.EnterAsync` fills and presses Enter as **two** turns, because a browser runs a
microtask checkpoint between two user events and a library that re-renders on a microtask (Preact does,
React does not) would otherwise read a draft it had already cleared.

## The fixtures

| Fixture | What it exercises | Status |
| --- | --- | --- |
| `todomvc-react` | React 18 with a scheduler and synthetic events: controlled input, keyed list, hash-routed filters | passes |
| `todomvc-vue` | Vue 3 compiling the document's **own** markup: `v-model`, `v-for`, `:class`, `@keydown.enter`, mustaches | passes |
| `todomvc-preact` | Preact hooks writing to the DOM directly, with no scheduler between them | passes |
| `todomvc-svelte` | Svelte 5 compiled ahead of time: no framework runtime is loaded, only the component's own output | passes |
| `ssr-hydration` | React `hydrateRoot` over server-rendered markup — the nodes are adopted, not replaced, and `onRecoverableError` stays empty | passes |
| `jquery` | jQuery 3.7: `$.ajax({ async: false })` (a *synchronous* XMLHttpRequest), `$(fn)` readiness, delegated events on rows added later | passes |
| `htmx` | htmx 2: `hx-trigger="load"`, `hx-get` + `hx-swap`, `hx-boost` | passes |
| `alpine` | Alpine 3: `x-data`, `x-model`, `x-text`, `x-show` — attributes found by walking the document, kept live by a `MutationObserver` | passes |
| `spa-router` | An intercepted click, `history.pushState`, `popstate`, `back()`/`forward()`, and a link the router does not claim | passes |
| `custom-elements` | `customElements.define`, the upgrade of an element the parser already made, and all four reactions | passes |
| `modules-importmap` | `<script type="importmap">`, a bare specifier, a mapped directory prefix, and a dynamic `import()` | passes |
| `fetch-json` | `fetch` of JSON rendered into a list, a request header the script set, and the page's own request log | passes |
| `form-redirect` | A form's entry list, a urlencoded `POST`, a `303`, and the `GET` that shows what the server read | passes |
| `cookie-login` | `Set-Cookie` on a redirect, the jar carrying it to a protected page, and `HttpOnly` staying invisible to script | passes |
| `local-storage` | `localStorage` across two navigations and a second page of the same context; `sessionStorage` not leaving the page | passes |
| `intersection-observer` | A lazy list and a reveal-on-scroll panel — which load **everything at once**, see below | passes |
| `mutation-observer` | A widget kept in step with its subtree: `childList`, `attributeFilter` with old values, `characterData`, one batch per turn | passes |
| `dialogs` | `alert`/`confirm`/`prompt` answered through `Page.DialogOpened`, and the default dismiss with no handler | passes |

### The triage table is empty, and what it took to empty it

No fixture is `needs triage` today, so `FixtureInventoryTests` holds the corpus to *no* `[Explicit]` case
at all. Both rows that stood were features rather than defects, and each was retired by the pull request
that added the feature — which is what a triage row is for:

| Fixture | What it was owed | Paid by |
| --- | --- | --- |
| `htmx` | htmx 2 builds an `XPathEvaluator` expression at the **top level** of its bundle, so the library was a `ReferenceError` before any `hx-` attribute was read | DOM §7's XPath over `AngleSharp.XPath` (`Jint.Browser/Dom/Views/JsXPath`), plus `CSS.escape` and `new DocumentFragment`, which are the next two things it reaches for |
| `custom-elements` | `customElements` did not exist | campaign item C6: the registry, the upgrade and the four reactions |

### The one whose *passing* behaviour is a divergence

`intersection-observer` asserts that **every** page of a lazy list loads at once. That is not what a browser
does and it is not a defect: with no layout there is nothing that can stop intersecting, so an observed target
is reported once, fully intersecting, and never again. The alternative — "never intersecting" — leaves every
lazy list and every reveal-on-scroll panel permanently empty, which is worse for the reader this browser is
for. `Jint.Browser/AGENTS.md` states the choice; this fixture is where it is asserted.

## The vendored libraries

Each directory under `vendor/` holds the library's published bundle and its own `LICENSE`, verbatim.

| Library | Version | Source | Licence | Used by |
| --- | --- | --- | --- | --- |
| React | 18.3.1 | `unpkg.com/react@18.3.1/umd/react.production.min.js`, `react-dom@18.3.1/umd/react-dom.production.min.js` | MIT | `todomvc-react`, `ssr-hydration` |
| Vue | 3.5.22 | `unpkg.com/vue@3.5.22/dist/vue.global.prod.js` | MIT | `todomvc-vue` |
| Preact | 10.29.8 | `unpkg.com/preact@10.29.8/dist/preact.umd.js`, `preact@10.29.8/hooks/dist/hooks.umd.js` | MIT | `todomvc-preact` |
| Svelte | 5.57.0 | compiled into `todomvc-svelte/todomvc.bundle.js`; only the licence is under `vendor/` | MIT | `todomvc-svelte` |
| jQuery | 3.7.1 | `unpkg.com/jquery@3.7.1/dist/jquery.min.js` | MIT | `jquery` |
| htmx | 2.0.10 | `unpkg.com/htmx.org@2.0.10/dist/htmx.min.js` | 0BSD | `htmx` |
| Alpine.js | 3.17.1 | `unpkg.com/alpinejs@3.17.1/dist/cdn.min.js` | MIT | `alpine` |

React 18 rather than 19, and Preact's UMD build, because React 19 publishes no UMD asset: a fixture must be a
`<script src>` a page loads, not a bundle this repository builds.

### The two files that were produced rather than downloaded

**`todomvc-svelte/todomvc.bundle.js`** is Svelte output. Its source is `todomvc-svelte/src/`, and it was built
with esbuild and `esbuild-svelte` against `svelte@5.57.0`:

```bash
npm install svelte@5.57.0 esbuild esbuild-svelte
npx esbuild src/main.js --bundle --format=iife --target=es2020 --outfile=todomvc.bundle.js
```

**`ssr-hydration/index.html`**'s server markup is what `ReactDOMServer.renderToString` emits for that fixture's
own `App` with `{ start: 2, notes: ['alpha', 'beta'] }`. It is checked in rather than computed, because a
fixture that rendered its own "server" markup would be testing nothing: hydration has to adopt markup that
arrived with the document. A mismatch is not silent — React reports it through `onRecoverableError`, and the
case asserts that list is empty.

## Where the files live

Everything here is an **embedded resource** of `Jint.Tests.Browser`, keyed by its path under `Fixtures/`
(`todomvc-react/index.html`, `vendor/react-18.3.1/react.production.min.js`, …) — the same arrangement the
web-platform-tests corpus uses, and for the same reason: a suite that read a working directory would behave
differently under `dotnet test`, under an IDE and under a single-file publish. `FixtureCorpus` is the index and
`FixtureOrigin` is what serves it.

`.gitattributes` marks `vendor/` (and the compiled Svelte bundle) `linguist-vendored`, so the minified files
do not count as this repository's own source, and pins `vendor/` to LF so that re-downloading a bundle at the
version above produces no diff on a Windows checkout. The fixture pages and the C# beside them are ours and
are deliberately not marked.
