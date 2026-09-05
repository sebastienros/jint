# Web APIs

Jint provides an opt-in, WHATWG-oriented web platform surface for scripts that need APIs such as `console`,
timers, events, encoding, streams, `fetch`, storage, or workers. Web APIs require .NET 8 or later.

```csharp
var engine = new Engine(options => options
    .UseWebApis(WebApiFeatures.Console | WebApiFeatures.Timers)
    .UseConsole(Console.Out));
```

Nothing is installed by default. `UseWebApis()` enables the default non-network, non-persistent set; outbound
networking, storage, caches, script-registered inbound handlers, and workers require separate grants.
Installation is lazy, non-clobbering, and limited to the principal realm.

Jint never starts a thread to run script. Timers, messages, network completions, and worker communication move
only while the owning engine is pumped. Enabling `fetch`, sockets, or event streams gives script the process's
network position: restrict destinations and sizes, partition state per tenant, and apply
[execution constraints](./constraints.md). Jint remains an in-process library, not a security boundary.

## Topics

- [Enabling features](./web-apis/enabling.md)
- [Console, diagnostics, timers, and scheduling](./web-apis/console-and-timers.md)
- [Events and messaging](./web-apis/events-and-messaging.md)
- [Encoding, files, and streams](./web-apis/encoding-files-and-streams.md)
- [Fetch and networking](./web-apis/fetch-and-networking.md)
- [Storage and Cache API](./web-apis/storage-and-cache.md)
- [Crypto and performance](./web-apis/crypto-and-performance.md)
- [Workers](./web-apis/workers.md)
