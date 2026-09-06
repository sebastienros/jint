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
