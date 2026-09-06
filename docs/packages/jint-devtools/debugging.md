# Debugging

Create the engine with `UseDevTools()` before it runs any script:

```csharp
var engine = new Engine(options => options.UseDevTools());
```

This enables debugger support, retains source text, and treats a JavaScript `debugger` statement as a protocol
breakpoint. These are construction-time settings; attaching an `EngineTarget` later cannot add them to an
already-created engine.

## Name source files

Pass a stable source name when executing scripts:

```csharp
engine.Execute(
    """
    function calculate(value) {
        debugger;
        return value * 2;
    }

    calculate(21);
    """,
    "calculate.js");
```

Clients can then display the source and set breakpoints by URL. Absolute filesystem paths are published as
`file://` URLs.

## Supported debugger workflows

The `Debugger` domain supports:

- breakpoints by URL or script and possible breakpoint locations;
- pause, resume, step into, step over, and step out;
- continue to a location;
- call frames and getter-free scope snapshots;
- expression evaluation in any paused frame;
- changing a binding with `setVariableValue`;
- pausing on caught or uncaught exceptions.

The `Runtime` domain supports evaluation, object handles and properties, function calls, bindings, promises,
console events, and exception events.

## Pauses and the host thread

A debugger pause synchronously holds the engine thread. Jint.DevTools services pause-safe commands on that same
thread until the client resumes or `PauseTimeout` expires. A disconnected client also releases the pause.

For `HostOwned`, the thread being paused is your host thread. Choose a finite `PauseTimeout` appropriate for the
application, and keep the normal engine pump running whenever execution is not paused.

`Debugger.pause` takes effect at the next execution point; it cannot interrupt an indefinitely long operation
inside one execution point. Configure Jint execution constraints independently.

## Current limitations

Source maps, live source editing, frame restart, asynchronous call stacks, `HeapProfiler`, and per-session
breakpoints are not supported. Unsupported commands return CDP method-not-found (`-32601`) rather than silently
succeeding.
