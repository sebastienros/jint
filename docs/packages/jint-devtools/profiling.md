# Profiling and coverage

`UseDevTools()` enables profiling when the engine is constructed:

```csharp
var engine = new Engine(options => options.UseDevTools());
```

A CDP client can start and stop a sampled CPU profile through the `Profiler` domain. Chrome DevTools displays
the result in its Performance tooling. Sampling is used by default; a short function that completes between
sampling points might not appear.

## Enable coverage

Coverage has a running cost and is disabled by default. Enable it explicitly at engine construction:

```csharp
var engine = new Engine(options =>
    options.UseDevTools(devTools => devTools.Coverage = true));
```

The protocol supports precise and best-effort coverage. Declared functions that never run are reported with a
zero count, so clients can show unused functions instead of omitting them.

Coverage is function-granular for unexecuted code: a statement skipped inside a function that ran may still be
represented by that function's count.

## Operational effects

- Profiling and coverage options cannot be enabled after engine construction.
- Precise coverage collection uses the engine's coverage counters.
- Starting and taking precise coverage resets those counters, so an attached client can affect coverage data
  the host also reads.
- Coverage enables per-statement instrumentation for all scripts, even when no client is attached.
- Debugger support enabled by `UseDevTools()` also disables Jint's tight-loop optimization.

If an engine was not configured for the requested feature, the domain returns an explicit server error rather
than an empty result that could be mistaken for a valid profile or uncovered script.
