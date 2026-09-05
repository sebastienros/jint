# Encoding, files, and streams

## Encoding and file objects

`WebApiFeatures.Encoding` provides UTF-8 `TextEncoder` and the Encoding Standard's `TextDecoder` labels. Seven
legacy multibyte encodings—Big5, EUC-JP, EUC-KR, GBK, gb18030, ISO-2022-JP, and Shift_JIS—are recognized but
reported as unsupported. `WebApiFeatures.Base64` adds `atob` and `btoa`.

`WebApiFeatures.Files` adds `Blob`, `File`, `FormData`, `FileReader`, and progress events. `FileReader` events
are queued tasks even though blob bytes are already in memory, so the engine must be pumped before reading its
result.

Blob URLs are available when both files and URLs are enabled:

```javascript
const blob = new Blob(['hello'], { type: 'text/plain' });
const url = URL.createObjectURL(blob);
URL.revokeObjectURL(url);
```

When a network interface is also enabled, fetching a blob URL is answered from this store without using the
network. The store retains the blob until revocation or a global snapshot restore, so long-lived scripts should
always revoke URLs.

## Streams

`WebApiFeatures.Streams` provides readable, writable, transform, byte, BYOB, queuing, piping, teeing, and async
iteration APIs. Callbacks and promise reactions run on the engine thread; Jint starts no stream thread.
Readable, writable, and transform streams are transferable through structured clone rather than serializable.
Both engines must be pumped for a transferred stream to move data.

Text transform streams require both `Encoding` and `Streams`. Compression transform streams require both
`Compression` and `Streams`; naming only one half installs neither interface.

## Bridging `System.IO.Stream`

```csharp
var engine = new Engine(options =>
    options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Encoding));

using var source = File.OpenRead("input.txt");
engine.SetValue("input", engine.WebApi.CreateReadableStream(
    source,
    new HostReadableStreamOptions { LeaveOpen = true }));

var length = await engine.EvaluateAsync("""
    (async () => {
      const reader = input.getReader();
      let total = 0;
      for (;;) {
        const { value, done } = await reader.read();
        if (done) return total;
        total += value.byteLength;
      }
    })()
    """);
```

`CreateReadableStream` and `CreateWritableStream` bridge host streams with backpressure. By default the bridge
owns and closes the host stream; set `LeaveOpen` when the host retains ownership. Do not access a stream
concurrently after handing it to script.

For the opposite direction, use `StartReadableStreamCopy` when the host owns the pump loop, or
`CopyReadableStreamAsync` when an awaitable operation should drive turns. There is intentionally no adapter that
exposes a script `ReadableStream` as a synchronous `System.IO.Stream`, because arbitrary callers could then
enter the engine from the wrong thread.

See [Events and messaging](./events-and-messaging.md) for cross-engine transfers and
[Fetch and networking](./fetch-and-networking.md) for response body streams.
