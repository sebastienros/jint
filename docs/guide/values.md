# Working with values

Jint represents JavaScript values with `JsValue`. Inspect the type before using a typed accessor:

```csharp
JsValue value = new Engine().Evaluate("({ name: 'Ada', score: 9.5 })");

if (value.IsObject())
{
    var obj = value.AsObject();
    Console.WriteLine(obj.Get("name").AsString());
    Console.WriteLine(obj.Get("score").AsNumber());
}
```

Common predicates include `IsUndefined`, `IsNull`, `IsString`, `IsNumber`, `IsBoolean`, `IsBigInt`,
`IsSymbol`, `IsObject`, `IsArray`, `IsDate`, `IsPromise`, and `IsCallable`. Typed accessors such as
`AsString`, `AsNumber`, `AsBoolean`, `AsObject`, and `AsArray` throw when the value has the wrong type.

## CLR conversion

`JsValue.FromObject` projects a CLR value into an engine. `ToObject` converts ordinary JavaScript values to
CLR primitives, arrays, and dictionaries:

```csharp
var engine = new Engine();
var jsValue = JsValue.FromObject(engine, new[] { 1, 2, 3 });
var clrValue = engine.Evaluate("({ ok: true, items: [1, 2] })").ToObject();
```

For untrusted or externally returned results, prefer `Engine.ConvertResult`. It creates a detached CLR graph
and can enforce `ResultLimits` while traversing:

```csharp
var engine = new Engine(options =>
    options.ResultLimits = ResultLimits.Conservative);

var detached = engine.ConvertResult(engine.Evaluate("({ value: 'ok' })"));
```

Cycles, functions, and symbols cannot be detached this way. A wrapped CLR object is already host-owned, so
conversion returns its target rather than walking its graph.

## Engine affinity

Do not pass a `JsValue` object from one engine to another. A JavaScript object retains its creating engine and
realm, and cross-engine use is unsupported and not validated. Share `Prepared<Script>`,
`Prepared<Module>`, or plain CLR data instead.

CLR arrays are copied to independent native JavaScript arrays by default. See [CLR interop](./clr-interop.md)
for live views and write permissions.
