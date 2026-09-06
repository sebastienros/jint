# Supported domains

Jint.DevTools implements the engine-level part of CDP.

| Domain | Available features |
| --- | --- |
| `Runtime` | Evaluation, object handles, properties, function calls, bindings, promises, execution contexts, console and exception events |
| `Debugger` | Sources, breakpoints, stepping, call frames, scopes, frame evaluation, variable updates, exception pauses |
| `Profiler` | Sampled CPU profiles, precise coverage, best-effort coverage |
| `Console` | Console message events and clearing the console journal |
| `Log` | Runtime and debugger failures reported to the client |
| `Target` | Listing and attaching to targets with flattened sessions |
| `Browser` | Server version and connection-level behavior |
| `Schema` | The domains reported by this server |

`Runtime`, `Debugger`, `Profiler`, `Console`, and `Log` are target-session domains. `Target`, `Browser`, and
`Schema` are browser-session domains.

Console events require the engine to enable Jint's optional `console` Web API. `UseDevTools()` preserves and
wraps the host's configured console sink; it does not install the `console` object.

## Unsupported commands

Commands not implemented by the target return:

```text
-32601 "'Domain.method' wasn't found"
```

This is intentional CDP feature detection, not a successful no-op. A few connection-negotiation options that
clients routinely send are accepted without changing engine behavior.

Notable unsupported features include source maps, asynchronous call stacks, blackboxing, live source editing,
`HeapProfiler`, `Runtime.terminateExecution`, and per-session breakpoints. Host-configured Jint constraints,
not a protocol client, decide how execution is bounded.

## Browser domains

An `EngineTarget` has no document. Consequently, page-level domains such as `Page`, `DOM`, `Network`, `Fetch`,
`Input`, `Emulation`, `Storage`, and `Accessibility` are absent.

Use the separate `Jint.Browser` package when a CDP client needs page targets and browser domains.
