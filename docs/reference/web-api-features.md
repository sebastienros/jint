# Web API Features

Web APIs are part of the core `Jint` package on .NET 8 and later. They are opt-in and install no globals unless
requested.

```csharp
var engine = new Engine(options => options.UseWebApis());
```

`UseWebApis()` enables the non-network default set. Network access and persistent state require separate,
explicit grants.

## Measured conformance

Jint conforms to **62 of the 78 WinterTC Minimum Common API member outcomes (79.5%)** after the explicit fetch
grant is enabled. That total consists of 59 present members and three global event-handler properties that
WinterTC requires this non-`EventTarget` global shape not to expose. The remaining 16 members are WebAssembly,
which Jint declines by design because it would require a separate bytecode virtual machine.

The vendored `.any.js` web-platform-tests corpus passes **38,649 of 41,581 assertions (92.9%)** across 44 selected
suite directories representing 14 standards. It is a gated, Windows-measured subset rather than a claim about
the complete web platform.

| Area | Examples | Default |
| --- | --- | --- |
| Console and scheduling | `console`, timers, microtasks, scheduler, idle callbacks | Yes |
| Data | encoding, base64, blobs, files, streams, compression | Yes |
| Platform | events, URL, crypto, performance, navigator | Yes |
| Messaging | structured clone, message channels, broadcast channels | Yes |
| Storage | local and session storage | No |
| Networking | fetch, XHR, WebSocket, EventSource | No |
| Cache | Cache API with host-provided storage | No |
| Workers | `Worker` with a host-provided worker provider | No |
| Request handling | fetch events | No |

Dependencies between flags are expanded automatically. For example, fetch also installs the URL, files, streams,
and events it needs.

See [Web APIs](../packages/jint/web-apis.md) for configuration and security guidance.
