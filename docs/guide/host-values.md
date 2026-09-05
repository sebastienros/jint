# Exposing .NET Values and Functions

`SetValue` adds a value to the JavaScript global object.

```csharp
var engine = new Engine()
    .SetValue("taxRate", 0.2)
    .SetValue("write", new Action<string>(Console.WriteLine));

engine.Execute("write(`Tax: ${100 * taxRate}`)");
```

Delegates become JavaScript functions. Their arguments and return values are converted between JavaScript and
.NET automatically.

## Expose an object

```csharp
var order = new Order { Total = 42 };

var result = new Engine()
    .SetValue("order", order)
    .Evaluate("order.Total")
    .AsNumber();
```

Projected CLR objects expose members allowed by the engine's interop policy. Direct writes to fields, properties,
indexers, dictionaries, lists, and arrays are disabled by default.

```csharp
var engine = new Engine(options => options.Interop.AllowWrite = true)
    .SetValue("order", order);

engine.Execute("order.Total = 50");
```

`AllowWrite` does not make an object safe or unsafe by itself. Public methods remain callable and can mutate host
state. Do not expose side-effecting methods to scripts that should not have that capability.

## Read and call values

```csharp
engine.Execute("globalThis.api = { double: x => x * 2 }");

var api = engine.GetValue("api");
var twice = engine.GetValue(api, "double");
var result = engine.Invoke(twice, 21).AsNumber();
```

A string passed to `Invoke` names one global property; it is not parsed as an expression or dotted path. Evaluate
an expression or read nested properties explicitly when the function is not global.

For namespace access, overload selection, custom converters, and extension methods, continue with
[CLR interop](./clr-interop.md).
