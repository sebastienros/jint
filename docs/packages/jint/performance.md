# Performance

Measure with your scripts and host objects; embedding shape often matters more than script size.

For measured results across Jint, other managed engines, and V8, see the
[JavaScript engine comparison](./engine-comparison.md).

## Prepare repeated programs

Parse and analyze repeated source once:

```csharp
var prepared = Engine.PrepareScript(
    "items.reduce((sum, x) => sum + x, 0)",
    source: "sum.js",
    strict: true);

foreach (var items in batches)
{
    engine.SetValue("items", items);
    var total = engine.Evaluate(in prepared).AsNumber();
}
```

`Prepared<Script>` and `Prepared<Module>` are reusable, thread-safe, and shareable across engines. Engine-owned
call-site caches warm on repeated execution in the same engine. Prefer strict mode where compatible.

See [Advanced hosting](./advanced-hosting.md) for shared layouts, specialized host projections, lazy globals,
request state, and the boundary between shareable prepared code and engine-affine values.

## Choose the right lifetime

A fresh engine provides the clearest isolation but does not reuse warmed engine caches. Pooling can improve
reuse, but a warmed call site may retain its last receiver and pooled state includes globals, modules, intrinsic
mutations, and host references.

Global snapshots can cheaply reset global bindings between trusted evaluations:

```csharp
var snapshot = engine.Advanced.CaptureGlobalSnapshot();

engine.Advanced.WithRestoredGlobals(snapshot, () =>
{
    engine.Evaluate(in prepared);
});
```

Always restore on failure as well as success. A snapshot is a configuration-reuse primitive, not isolation:
prototype mutations, reachable object graphs, CLR state, symbols, and modules survive. It also keeps the engine
and captured values alive.

Restoring ends the previous event-loop cycle: queued work is discarded, and a promise registered before the
restore cannot later resume against the restored globals. Restore only after every owned async operation has
completed.

## Project data efficiently

`SetValue` wraps CLR objects and uses cached member resolution. CLR arrays default to
`ArrayConversionMode.Copy`, producing native JavaScript arrays; `LiveView` avoids the initial copy but exposes a
fixed-size wrapper and requires a separate `AllowWrite` grant for mutation.

For high-volume host-defined data, prefer built-in projection types over general `ObjectInstance` subclasses:
`JsObject.Create` with a shared `JsObjectLayout` for fixed records, `ArrayLikeObject` for live indexed
collections, and `NamedPropertyObject` for named records.

## Instrumentation changes execution cost

Statement coverage and exact custom constraints disable tight-loop optimizations. The evented profiler records
every call and adds per-call cost. Do not compare an instrumented run directly with an ordinary one; use
[Profiling](./profiling.md) to diagnose script time and a CLR profiler for time inside host callbacks.
