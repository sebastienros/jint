# Web Locks

`WebApiFeatures.WebLocks` installs `navigator.locks` with the `LockManager` and `Lock` interfaces. Script asks
for a named resource, holds it while a callback runs, and the promise `request()` returned settles when the lock
is released:

```javascript
const result = await navigator.locks.request('inventory', async lock => {
  // Nothing else in this lock space holds 'inventory' while this runs.
  return await rebuildIndex();
});
```

The feature is part of `UseWebApis()`. It grants no network access and no persistence: a lock exists only while
something holds it, and by default the lock space is private to one engine, so a script serializes only against
itself.

## Sharing a lock space between engines

`LockManager` is the host seam. One manager is one agent cluster and one origin — engines given the same
instance queue behind each other's locks:

```csharp
var locks = new LockManager();

var window = new Engine(options => options.UseWebApis().UseWebLocks(locks));
var worker = new Engine(options => options.UseWebApis().UseWebLocks(locks));
```

`LockManager` is thread-safe, so the engines may run on different threads. Nothing that crosses between them is
a `JsValue`: a request carries a resource name, a mode and a client id, and a grant is queued onto the receiving
engine's own event loop. The callback, the `Lock` object and every promise settle on whichever thread pumps that
engine.

Say nothing and each engine gets a private manager. A worker built from `WorkerRequest.CreateDefaultOptions()`
gets one too — the manager is never inherited, exactly as `Options.WebApi.Messaging.Broker` is not — so a
`WorkerProvider` that wants a browser's arrangement assigns the parent's manager to the worker's options.

## Modes, options and query

`mode` is `"exclusive"` (the default) or `"shared"`. Shared holders coexist; an exclusive holder excludes
everything else with that name.

- `ifAvailable: true` hands the callback `null` instead of waiting when the lock cannot be granted at once.
- `steal: true` releases every held lock for the name, rejecting their `request()` promises with an
  `AbortError`, and preempts the queue. Exclusive mode only.
- `signal` takes an `AbortSignal`. Aborting before the grant rejects `request()` with the signal's reason and
  removes the request from the queue; once the lock is granted the signal is ignored.

`steal` and `ifAvailable` are mutually exclusive, and `signal` may be combined with neither; each combination is
a `NotSupportedError`. A name starting with `-` is reserved and is also a `NotSupportedError`. Neither method
throws — both return promises, so every failure arrives as a rejection.

`navigator.locks.query()` resolves with `{ held, pending }`, each an array of `{ name, mode, clientId }`. The
client id identifies the engine, so two engines sharing a manager report two different ids for the same name.

## Pumping, and what a stalled engine costs

A lock is granted as an event-loop task, so it is never granted inside the call that asked for it and it only
happens while the owning engine is pumped. Jint starts no thread to pump one:

```csharp
holder.Execute("navigator.locks.request('r', () => neverSettles);");
waiter.Execute("navigator.locks.request('r', () => { /* queued */ });");

holder.Execute("release();");
waiter.Tasks.ProcessTasks(); // the grant runs here, on the waiting engine's own pump
```

An engine that stops being pumped therefore holds its locks indefinitely, and everything sharing its manager
waits. Two things give an engine's locks and requests back: `Engine.Advanced.RestoreGlobalSnapshot`, which ends
the evaluation cycle, and `Engine.Dispose`. Script's own recovery from a stalled peer is the `steal` option.

Continue with [Events and messaging](./events-and-messaging.md) for the `AbortSignal` the `signal` option takes,
or [Workers](./workers.md) for what a worker does and does not inherit.
