# Getting Started

## Install

```bash
dotnet add package Jint
```

The examples below assume `using Jint;`.

## Evaluate an expression

<!-- snippet: guide-evaluate -->
```csharp
var answer = new Engine()
    .Evaluate("6 * 7")
    .AsNumber();
```
<!-- endSnippet -->

`Evaluate` returns a `JsValue`. Use `AsString()`, `AsNumber()`, or `ToObject()` when the host needs a CLR value.

## Expose a host function

<!-- snippet: guide-expose-host-function -->
```csharp
var engine = new Engine()
    .SetValue("log", new Action<string>(Console.WriteLine));

engine.Execute("log('Hello from JavaScript')");
```
<!-- endSnippet -->

Only expose capabilities the script should have. CLR access is not enabled by default.

## Call JavaScript from .NET

<!-- snippet: guide-invoke -->
```csharp
var result = new Engine()
    .Execute("function add(a, b) { return a + b; }")
    .Invoke("add", 2, 3)
    .AsNumber();
```
<!-- endSnippet -->

Continue with [Creating an Engine](./creating-an-engine.md) and
[Execute, Evaluate, and Invoke](./execution.md).
