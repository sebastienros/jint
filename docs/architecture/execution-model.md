# Execution Model

Jint is a tree-walking interpreter. It does not emit bytecode or use the Dynamic Language Runtime.

## From source to execution

1. Acornima parses JavaScript into an ECMAScript abstract syntax tree.
2. Jint prepares reusable, engine-neutral analysis for that tree.
3. Expression and statement handlers interpret the nodes.
4. Runtime objects implement ECMAScript realms, environments, values, intrinsics, and jobs.
5. Host APIs convert values and expose explicitly configured .NET capabilities.

`Execute` runs a script and returns the engine for chaining. `Evaluate` runs an expression or script and returns
its resulting `JsValue`. `Invoke` converts CLR arguments and calls a JavaScript function.

## Prepared code

```csharp
var script = Engine.PrepareScript(
    "function square(x) { return x * x; }",
    source: "math.js",
    strict: true);

var engine = new Engine();
engine.Execute(in script);
```

`Prepared<Script>` and `Prepared<Module>` can be cached and shared across engines. They contain no engine-owned
objects. Runtime caches and values remain attached to the engine executing them.

## One engine, one active operation

An engine is not thread-safe and admits one operation at a time. Host callbacks can re-enter synchronously on the
owning thread. Work arriving from another thread must be queued through the engine task API and executed by the
thread pumping that engine.

Promises, timers, module continuations, and browser work all use the same queued-work model. Jint schedules jobs
but never starts a thread merely to run script.
