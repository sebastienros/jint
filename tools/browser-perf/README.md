# Browser automation performance harness

One scenario, measured four ways, so a number for Jint.Browser is always next to the number Playwright users
already get from Chromium:

| lane | what drives what |
| --- | --- |
| `chromium` | Playwright for .NET launches Chromium (headless shell) — the baseline a Playwright user has today |
| `jint-serve` | Playwright for .NET over CDP to an out-of-process `jint-browser serve` (set `PERF_SERVE_EXE`; the shipped binary is NativeAOT) |
| `jint-cdp` | Playwright for .NET over CDP to a `Jint.Browser` + `DevToolsServer` hosted in the measuring process (JIT) |
| `jint-direct` | `Jint.Browser`'s own `Page` API in-process (JIT) — the "replace node + Chromium" lane |
| `chromium-cdp` | Playwright `connectOverCDP` to a Chromium started by hand, to separate launch from connect costs |

Every lane loads the same bytes from one loopback static server: `BrowserPerf/site/form.html` and the
`Jint.Tests.Browser/Fixtures` corpus (the React TodoMVC fixture).

## Scenarios

- `form` — new context and page, `goto`, `title`, `fill`, `check`, `selectOption`, a submit click, five clicks
  on a counter, three reads, close. Per-step rows plus `TOTAL form`.
- `todo` — React TodoMVC: `goto`, wait for `.new-todo`, three `fill` + `Enter`, count, close.
- `roundtrip` — `evaluate("1 + 1")` and a locator `textContent`, many times on one page.
- `clicks` — `--roundtrips` clicks, then as many fills, on one page: the action path in isolation.
- `goto` — open, navigate, close.

## Running it

```bash
cd tools/browser-perf/BrowserPerf
dotnet build -c Release
dotnet bin/Release/net10.0/BrowserPerf.dll --lanes chromium,jint-cdp,jint-direct --iterations 30 --warmup 30 --scenarios form,todo
```

`dotnet run --no-build` works too. Options: `--lanes`, `--scenarios`, `--iterations`, `--warmup`,
`--roundtrips`. Environment:

- `PERF_CHROMIUM` — the Chromium binary (defaults to the pre-installed headless shell under `/opt/pw-browsers`).
- `PERF_SERVE_EXE` — the `jint-browser` executable for `jint-serve`. NativeAOT:
  `dotnet publish Jint.Browser.Tool/Jint.Browser.Tool.csproj -c Release -f net10.0 -r linux-x64 -p:BrowserToolNative=true -o <dir>`.
- `PERF_RAF_MS` — `BrowserOptions.AnimationFrameInterval` for the in-process Jint lanes.
- `PERF_TREND=1` — print every iteration's total with the managed heap size, for cold-start and drift checks.
- `DEBUG=pw:protocol` — Playwright's own protocol log, with timestamps, on standard error.

## Reading the numbers

- **Warm up for real.** The in-process Jint lanes are JIT-compiled; with five warmups a run still reflects
  tier-0 code and a later scenario looks faster than an earlier one. Thirty is the floor for a steady-state
  figure, and a cold figure is the first iteration of a fresh process (`--warmup 0` with `PERF_TREND=1`).
- **Use one lane per process** for anything you quote, and keep the machine idle: an MSBuild or
  `VBCSCompiler` node left over from a build takes a core (`dotnet build-server shutdown`).
- These are wall-clock medians on whatever machine ran them, not `Jint.Benchmark` figures; the benchmark
  rules in `Jint.Benchmark/AGENTS.md` still decide what may be quoted in a pull request.
