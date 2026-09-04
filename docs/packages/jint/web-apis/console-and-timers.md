# Console, diagnostics, timers, and scheduling

## Console

Enabling `console` alone does not write to standard output. Its default `ConsoleSink.Null` discards records.
Provide a text writer for the common case:

```csharp
var engine = new Engine(options => options.UseConsole(Console.Out));
engine.Execute("console.log('hello %s', 'world')");
```

Derive from `ConsoleSink` to route `ConsoleRecord` values to structured logging. A record identifies the
`ConsoleMethod`, raw arguments, log level, group depth, and trace frames. Read engine-owned `JsValue` arguments
during the callback rather than retaining them. Console formatting does not invoke script-visible getters.

Uncaught promise rejections and errors escaping deferred callbacks use a separate `DiagnosticsSink`:

```csharp
var engine = new Engine(options =>
    options.UseWebApis().UseDiagnostics(diagnosticsSink));
```

Installing a sink changes callback failure handling: without one, the exception erupts from the pump; with one,
it is reported and pumping continues. Execution-limit exceptions always erupt. `DiagnosticsSink.Null` means
continue without host output, not the same behavior as no sink.

## Timers and the pump

Jint starts no thread or `System.Threading.Timer`. A timer fires on the first pump at or after its due time:

```csharp
var engine = new Engine(options => options.UseWebApis());

var value = await engine.EvaluateAsync("""
    new Promise(resolve => setTimeout(() => resolve(42), 20))
    """);
```

Promise reactions run before due timers, and each timer gets its own microtask checkpoint. Timers, delayed
scheduler tasks, idle-callback timeouts, and `AbortSignal.timeout` share the timer capacity;
`Options.WebApi.Timers.MaxActiveTimers` defaults to 1,000.

Hosts that own a loop call `engine.Tasks.ProcessTasks()`. `TimeUntilNextScheduledWork` reports when to pump;
`WaitForScheduledWork` and `WaitForScheduledWorkAsync` wait but never process work.

`scheduler.postTask` adds priority and cancellation, while `scheduler.yield()` resumes in a fresh task.
`requestIdleCallback` runs only after higher-priority work; its default per-pump budget is controlled by
`Options.WebApi.Timers.IdleBudget`.

See [Thread safety](../thread-safety.md) before pumping from a dedicated thread.
