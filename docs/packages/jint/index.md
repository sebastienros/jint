# Jint

Jint is a JavaScript interpreter that embeds in a .NET process. The `Jint` NuGet package targets .NET
Framework 4.7.2, .NET Standard 2.0/2.1, and modern .NET.

## Install

```bash
dotnet add package Jint
```

[View Jint on NuGet.org](https://www.nuget.org/packages/Jint)

## First script

With `using Jint;` in scope:

<!-- snippet: package-jint-first-script -->
```csharp
var engine = new Engine()
    .SetValue("name", "Ada");

var greeting = engine.Evaluate("`Hello, ${name}!`").AsString();
Console.WriteLine(greeting);
```
<!-- endSnippet -->

Jint implements modern ECMAScript, including modules, promises, async functions, `Intl`, and `Temporal`.
Browser APIs and Node compatibility are separate, explicit opt-ins.

## Guide

- [Creating an engine](./creating-an-engine.md)
- [Executing scripts](./execution.md)
- [Working with values](./values.md)
- [Exposing .NET values and functions](./host-values.md)
- [Advanced hosting](./advanced-hosting.md)
- [CLR interop](./clr-interop.md)
- [Modules](./modules.md)
- [Asynchronous execution](./async.md)
- [Web APIs](./web-apis.md)
- [Node compatibility](./node-compatibility.md)
- [Internationalization](./internationalization.md)
- [Execution constraints](./constraints.md)
- [Running untrusted code](./untrusted-code.md)
- [Error handling](./errors.md)
- [Performance](./performance.md)
- [JavaScript engine comparison](./engine-comparison.md)
- [Profiling](./profiling.md)
- [Code coverage](./code-coverage.md)
- [Thread safety](./thread-safety.md)

Jint runs in-process. For hostile code, begin with [Running untrusted code](./untrusted-code.md), not
with the default engine.
