# Automation Performance

Jint.Browser runs a page in the calling process, so there is no browser to launch and no process boundary on
every call. Three settings decide how much of that an automation run keeps.

## Animation frames

Nothing is painted, so an animation frame is only a scheduling point, but automation waits for frames.
Playwright's actionability check waits for two `requestAnimationFrame` callbacks before every `click`,
`check`, `hover`, `dblclick` and `tap`, to confirm the element has stopped moving. A frame runs
`BrowserOptions.AnimationFrameInterval` after it is requested, 16 ms by default, so each of those actions
costs two intervals over CDP.

`jint-browser serve` uses 1 ms. A host that publishes its own browser on a `DevToolsServer` for Playwright or
Puppeteer can do the same:

```csharp
await using var browser = new Browser(new BrowserOptions
{
    AnimationFrameInterval = TimeSpan.FromMilliseconds(1),
});
```

A page that animates continuously runs its frames that much more often. The in-process `Page` API does not
wait for frames: `Page.ClickAsync` checks the element's box once and dispatches.

## Shared script parses

`BrowserOptions.ShareParsedScripts`, on by default, parses a classic script of 1 KiB or more once per process
and reuses the parse in every page of every browser. A test suite loads the same framework bundle into every
page it opens. The same cache serves `Jint.DevTools`: an automation client's injected script (Playwright sends
329 KB to every new page through `Runtime.evaluate`) and the helper declarations it calls through
`Runtime.callFunctionOn`.

The cache holds about 30 MB of parsed scripts and evicts the least recently used. A page engine that sets
parsing limits or a regular-expression timeout parses for itself. Turning the option off parses every script
in every page.

## Test projects

A test process that drives pages in-process spends most of its first tests compiling Jint, the parser and the
bindings. The interpreter reaches its steady speed only after hundreds of pages, so a short run never gets
there. Two runtime settings shorten that.

- `<TieredPGO>false</TieredPGO>` in the test project skips the instrumented tier, so methods reach optimized
  code sooner.
- ReadyToRun precompiles every assembly the tests load. A Microsoft Testing Platform test project is an
  executable, so it can be published and run directly:

```bash
dotnet publish -c Release -r linux-x64 -p:PublishReadyToRun=true -o out
```

Measured on one four-core Linux machine, for a form test of ten actions run in a fresh process:

| Configuration | First test | 10 tests | 50 tests |
| --- | --- | --- | --- |
| JIT, defaults | ~700 ms | ~1.2 s | ~2.5 s |
| JIT, `TieredPGO` off | ~800 ms | ~1.1 s | ~2.0 s |
| ReadyToRun | ~300 ms | ~0.6 s | ~2.0 s |
| ReadyToRun, `TieredPGO` off | ~280 ms | ~0.5 s | ~1.4–1.7 s |

The same 50 tests through Playwright and Chromium took about 0.5 s to start and 0.33 s per test, around 17 s.
`tools/browser-perf` in the repository is the harness these figures came from.
