# Workers

Workers are not enabled by `UseWebApis()`. They require both the feature and a host-supplied `WorkerProvider`:

```csharp
var engine = new Engine(options => options
    .UseWebApis()
    .UseWorkers(workerProvider));

engine.Execute("""
    const worker = new Worker('./worker.js', { type: 'module' });
    worker.postMessage({ value: 42 });
    worker.onmessage = event => console.log(event.data);
    """);
```

Only module workers are supported. With no provider, the `Worker` global is not installed. The provider sees
every request and may return `null` to refuse it. It creates a quiescent worker `Engine`, configures its module
loader and restrictions, then starts and owns the thread or loop that pumps it. Jint itself never starts a
worker thread.

Use `WorkerRequest.CreateDefaultOptions()` as a starting point: it carries the parent's restrictive posture and
cancellation wiring, while withholding capability grants. The provider must explicitly grant each worker's web,
CLR, network, storage, and nested-worker capabilities. Reapply the host's hardening policy rather than treating
the copied options as a security boundary.

`Options.WebApi.Workers.MaxWorkers` defaults to 16 per parent engine, and `MaxQueuedMessages` defaults to 16,384
per direction. Set lower application-specific limits where appropriate. Messages use structured clone and may
transfer buffers, ports, and streams; engines never share `JsValue` instances.

`worker.terminate()`, worker-side `close()`, failure, parent snapshot restore, and disposal end the connection.
The provider must stop its pump and dispose the worker from the thread that owns it. Provider callbacks can come
from different threads and must be thread-safe.

A worker engine is still in-process. Threads isolate engine state but do not protect the process from unsafe CLR
access, native calls, or unbounded resources. Combine workers with [constraints](../constraints.md),
[untrusted-code hardening](../untrusted-code.md), and the general [thread-safety rules](../thread-safety.md).

## Shared workers in Jint.Browser

`Jint.Browser` installs `SharedWorker` alongside its default dedicated-worker provider when
`WebApiFeatures.Workers` is enabled. This is a Browser feature: standalone `UseWorkers` does not expose it,
and replacing Browser's worker provider does not install it. It is currently exposed in top-level page
realms only.

```javascript
const worker = new SharedWorker('./shared.js', { name: 'counter', type: 'classic' });
worker.onerror = () => console.log('Shared worker could not start');
worker.port.onmessage = event => console.log(event.data);
worker.port.postMessage('increment');
```

The classic `shared.js` can retain state across connections:

```javascript
let count = 0;
onconnect = event => {
    const port = event.ports[0];
    port.onmessage = () => port.postMessage(++count);
};
```

Both classic (the default) and module scripts are supported; the second argument can also be a string name.
Classic workers have `importScripts`; modules use imports and reject `importScripts`. The shared global
has `name`, `onconnect`, `close()`, timers, fetch and the same provider-granted capabilities as a dedicated
worker, but no global `postMessage`. Each trusted `connect` event has empty-string data and the new port
as both `source` and `ports[0]`. Assigning `port.onmessage` starts the port; listeners registered with
`addEventListener` require `port.start()`.

Following the [HTML SharedWorker algorithms](https://html.spec.whatwg.org/multipage/workers.html#shared-workers-and-the-sharedworker-interface),
the context's locked registry matches constructor origin, resolved script URL and name. Two pages in one
`BrowserContext` can share a worker; different contexts cannot. Creator-inherited `about:blank` popups
retain their document origin and base URL; host fetch-origin overrides do not change worker identity.
Opaque document origins are isolated.
URLs resolve against the document base and cross-origin URLs throw `SecurityError`. The HTTP(S) loader
does not support `data:` or `blob:` scripts; failed loads raise an asynchronous worker-object `error`.
A matching worker requested with a different type, credentials or `extendedLifetime` setting also raises
`error` on the new object without connecting it. Uncaught runtime exceptions go to the shared global's
`onerror`, not the owners' worker objects.

Ownership belongs to documents, not port reachability. A document retains its worker until disposal,
snapshot restore or navigation replaces its engine. The last owner leaving terminates the worker
immediately; there is no grace period, including for `extendedLifetime: true`. Worker-side `close()`
also ends it. Shutdown cancels pending loads and execution through the same connection and constraint
plumbing as dedicated workers. A surviving owner's worker keeps running when its first creator leaves;
other inherited host cancellation and per-task/microtask budgets still apply.

`Options.WebApi.Workers.MaxWorkers` separately bounds live shared instances per BrowserContext; joining
an existing instance consumes no slot. `MaxQueuedMessages` applies per port direction, including transferred
ports. Configuration and network/diagnostic hooks come from the first creator. `Page.Workers` remains the
dedicated-worker count. Neither kind currently appears as a CDP worker target. Service workers are not
implemented.
