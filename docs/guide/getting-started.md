# Getting Started

## Install

```bash
dotnet add package Jint
```

## Evaluate an expression

```csharp
using Jint;

var answer = new Engine()
    .Evaluate("6 * 7")
    .AsNumber();
```

`Evaluate` returns a `JsValue`. Use `AsString()`, `AsNumber()`, or `ToObject()` when the host needs a CLR value.

## Expose a host function

```csharp
var engine = new Engine()
    .SetValue("log", new Action<string>(Console.WriteLine));

engine.Execute("log('Hello from JavaScript')");
```

Only expose capabilities the script should have. CLR access is not enabled by default.

## Call JavaScript from .NET

```csharp
var result = new Engine()
    .Execute("function add(a, b) { return a + b; }")
    .Invoke("add", 2, 3)
    .AsNumber();
```

Continue with [Creating an Engine](../packages/jint/creating-an-engine.md) and
[Execute, Evaluate, and Invoke](../packages/jint/execution.md).
