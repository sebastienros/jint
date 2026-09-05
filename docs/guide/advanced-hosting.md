# Advanced hosting

Use these APIs when a host creates many engines, projects high-volume data, or
needs request-specific state without rebuilding its configuration.

## Keep engine-affine values inside their engine

`Prepared<Script>`, `Prepared<Module>`, and a configured `Options` may be shared
across engines. Object-valued `JsValue` instances may not: each JavaScript
object belongs to the engine and realm that created it.

Convert results to CLR values before crossing an engine boundary. Build
`JsObject`, `JsArray`, and other engine-owned values while their engine is idle
or owned by the current operation.

## Supply request state without capturing it

`Engine.HostDefined` is an opaque slot for host state. Jint does not inspect or
interpret it, so a shared options factory can remain static:

```csharp
var options = new Options()
    .AddLazyGlobal("user", static engine =>
        JsValue.FromObject(
            engine,
            ((RequestContext) engine.HostDefined!).User));

using var engine = new Engine(options)
{
    HostDefined = requestContext
};
```

`AddLazyGlobal` installs the property immediately but creates its value only on
the first read. The result is memoized for that engine. Use the engine overload
when adding a lazy global after construction.

## Choose the narrowest projection

General `ObjectInstance` subclasses are flexible, but specialized projections
avoid descriptor allocation and keep property-access caches effective:

| Data shape | Preferred API |
| --- | --- |
| Fixed records with a shared shape | `JsObject.Create` with a shared `JsObjectLayout` |
| Live indexed collection | Derive from `ArrayLikeObject` |
| Live named record | Derive from `NamedPropertyObject` |
| Stateful host callable | Derive from `HostFunction` |
| Ordinary CLR object | Pass it through `SetValue` |

Copy a snapshot into `JsArray` when the collection does not need to stay
connected to its CLR source. CLR arrays use `ArrayConversionMode.Copy` by
default; opt into `LiveView` only when shared mutation is required.

## Reuse trusted engine configuration

A global snapshot can restore the global binding table around a trusted
evaluation:

```csharp
var snapshot = engine.Advanced.CaptureGlobalSnapshot();

engine.Advanced.WithRestoredGlobals(snapshot, () =>
{
    engine.Evaluate(in prepared);
});
```

Restoration is guaranteed on both success and failure. It does not reset
intrinsic or prototype mutations, reachable object graphs, modules, symbols,
or CLR state. `HostDefined` also survives and must be replaced by the host.

Restoring ends the previous event-loop cycle. Work registered before the
restore cannot later resume against the restored globals. Wait for every owned
async operation before restoring or pooling the engine.

For mutually distrusting scripts, use separate engines rather than snapshots.
For hostile code, use a separate process or container as described in
[Running untrusted code](./untrusted-code.md).
