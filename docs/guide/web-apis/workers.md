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

Standalone providers support module workers; Browser's default provider also supports classic workers.
With no provider, the `Worker` global is not installed. The provider sees
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

## Dedicated workers in Jint.Browser

Browser's default provider accepts `new Worker(url)` and `{ type: 'classic' }` as classic global scripts.
Top-level `var` and function declarations are globals, top-level `this` is `self`, and classic code is not
implicitly strict. Static module declarations are syntax errors. Messages posted during startup wait until
the initial script has run. Runtime exceptions use the worker error channel without disabling message delivery;
fetch and parse failures fire a worker-object `error` and release the worker.

Classic script URLs resolve against the document base and must be same-origin HTTP(S), including redirects.
The existing network filter, response-size limit, timeout and worker execution budgets remain in force.
The response must have a JavaScript MIME type. Worker-relative `fetch` and synchronous `importScripts` use
the final script response URL as their base; imports execute on the worker thread, never the page thread.
`data:` and `blob:` worker entry scripts are not supported. Module workers remain explicit
`{ type: 'module' }` and reject `importScripts`.

Browser workers expose `navigator.language` and a frozen `navigator.languages` list. They snapshot the
constructor's browser locale and `Accept-Language` override using the same rules as the parent navigator;
they do not retain the page to answer those getters. Standalone providers keep the engine's minimal
navigator unless they explicitly extend it.

Browser's classic and module workers expose a readonly `WorkerLocation` through `self.location`.
Its URL components and stringifier use the final entry-script response URL, including redirects.
Termination and owning-document navigation cancellation do not report script failures; unrelated
cancellation and genuine startup/runtime errors retain their diagnostic channels.

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
