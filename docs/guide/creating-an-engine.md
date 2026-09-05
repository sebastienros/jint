# Creating an engine

`Engine` owns a JavaScript realm, globals, modules, constraints, and queued work.

```csharp
using Jint;

var engine = new Engine(options =>
{
    options.Strict = true;
    options.Culture = CultureInfo.GetCultureInfo("en-US");
    options.TimeZone = TimeZoneInfo.Utc;
});
```

Strict mode is usually preferable and can execute faster. CLR namespace access, module loading, web APIs,
and Node compatibility are disabled until explicitly installed.

## Reusable options

Configure an `Options` instance completely before constructing engines:

```csharp
var options = new Options
{
    Strict = true
};

var first = new Engine(options);
var second = new Engine(options);
```

The configured instance may be shared by engines being constructed concurrently. Construction freezes the
options; later setters and registrations throw. Each engine still receives its own constraint state and
JavaScript realm.

## Expose host values

```csharp
var engine = new Engine()
    .SetValue("log", new Action<string>(Console.WriteLine))
    .SetValue("answer", 42);

engine.Execute("log(String(answer))");
```

`SetValue` adds a global. Exposing a CLR object grants scripts access to its public members under the
[interop policy](./clr-interop.md).

## Engine lifetime

An engine serves one operation at a time and is not thread-safe. Keep it assigned to one request or operation,
and await any async API before reuse or disposal. A pooled engine retains globals, modules, intrinsic mutations,
and caches unless the host explicitly manages them.

`CaptureGlobalSnapshot` and `RestoreGlobalSnapshot` can reset the global binding table for trusted reuse, but
they are not an isolation boundary: prototype changes, reachable object graphs, CLR state, symbols, and modules
survive. Use separate engines for separate trust domains. See [Performance](./performance.md) and
[Thread safety](./thread-safety.md).
