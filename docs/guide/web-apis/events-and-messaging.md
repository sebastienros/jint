# Events and messaging

## Events

`WebApiFeatures.Events` installs `Event`, `CustomEvent`, `EventTarget`, `AbortController`, and `AbortSignal`.
Jint has no DOM tree, so an `EventTarget` dispatches to itself without capture or bubble ancestors while retaining
listener ordering, `once`, removal, and propagation rules.

An event listener that throws erupts unless a `DiagnosticsSink` is configured; with a sink, Jint reports it and
continues to later listeners. `WebApiFeatures.GlobalEvents` adds global `error`, `unhandledrejection`, and
`rejectionhandled` handling. Script may observe those reports but cannot suppress the host diagnostics sink or
turn a constraint failure into an event.

Bridge a host token without running script on the cancelling thread:

```csharp
JsValue signal = engine.WebApi.CreateAbortSignal(cancellationToken);
engine.SetValue("signal", signal);
```

Cancellation enqueues the abort for the next pump. An already-cancelled token creates an already-aborted signal.

## Message channels

`MessageChannel`, `MessagePort`, and `BroadcastChannel` use structured clone. Messages are copied when posted;
`ArrayBuffer`, `MessagePort`, and streams can be transferred. Transferred values become unusable on the sender.

A port queue does not deliver until `start()` or an `onmessage` assignment; `addEventListener` alone does not
start it. Delivery is an event-loop task and requires the receiving engine to be pumped.

Connect two engines without sharing any `JsValue`:

```csharp
var pair = first.WebApi.CreateMessagePortPair(second);
first.SetValue("port", pair.Local);
second.SetValue("port", pair.Remote);

first.Execute("port.postMessage({ value: 21 })");
second.Tasks.ProcessTasks();
```

Each side serializes and deserializes on its own engine thread. A global snapshot restore ends ports created in
the previous cycle, so pooled engines need fresh channels.

`BroadcastChannel` is private to an engine by default. Assign the same thread-safe `BroadcastChannelBroker` to
`options.WebApi.Messaging.Broker` for explicitly shared channels. Close short-lived channels; a shared broker
retains its subscribers until close, engine restore, or disposal.

See [Workers](./workers.md) for worker messaging and
[Encoding, files, and streams](./encoding-files-and-streams.md) for transferable streams.
