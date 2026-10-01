# Crypto and performance

## Web Crypto

`WebApiFeatures.Crypto` provides `crypto.getRandomValues`, `crypto.randomUUID`, and `crypto.subtle`. The subtle
API supports its twelve standard operations over SHA digests, HMAC, AES-CTR/CBC/GCM/KW, RSA signatures and
OAEP, ECDSA/ECDH on the NIST curves, HKDF, and PBKDF2.

```csharp
var engine = new Engine(options =>
    options.UseWebApis(WebApiFeatures.Crypto | WebApiFeatures.Encoding));

var digest = await engine.EvaluateAsync("""
    crypto.subtle.digest('SHA-256', new TextEncoder().encode('hello'))
    """);
```

Cryptographic work is synchronous and the returned promises are already settled. Script failures are promise
rejections.

The implementation uses .NET cryptography and exposes a few platform limits: AES-GCM requires a 96-bit IV and
supported .NET tag sizes; RSA-PSS accepts the hash-sized salt; RSA-OAEP labels must be empty; generated RSA keys
use exponent 65537 and are capped at 8192 bits; PBKDF2 is capped at 4,194,304 iterations. These caps bound BCL
operations that execution constraints cannot interrupt. Treat key extraction and persistence as host security
decisions.

## Performance timeline

`WebApiFeatures.Performance` provides `performance.now()`, `timeOrigin`, marks, measures, entry queries, and
`PerformanceObserver`.

```javascript
performance.mark('start');
for (let i = 0; i < 1000; i++) Math.sqrt(i);
const measure = performance.measure('work', 'start');
console.log(measure.duration);
```

The user-timing buffer retains at most 10,000 marks and measures; clear them when no longer needed.
`PerformanceObserver` callbacks are tasks, delivered after pending microtasks, and run only while the engine is pumped.

With Fetch or XMLHttpRequest also enabled, completed responses create `PerformanceResourceTiming` entries:

```javascript
new PerformanceObserver(list => {
    for (const entry of list.getEntries()) console.log(entry.name, entry.duration);
}).observe({ type: 'resource', buffered: true });
await (await fetch('https://example.org/data')).text();
```

Resource entries have their own 250-entry buffer. `performance.clearResourceTimings()` clears it without
removing marks or measures; `setResourceTimingBufferSize(size)` changes its capacity. A
`resourcetimingbufferfull` listener can clear or enlarge it to recover pending entries. Observers receive
entries even when the primary buffer is full. Timings use the same clock as `performance.now()`.
Transport threads collect only CLR timing facts; generation-stamped jobs create entries on the engine thread,
so restoring a snapshot discards old completions.

Configure `Options.WebApi.Fetch.Origin` (or `BaseUrl`) for same-origin timing visibility. Cross-origin
responses require `Timing-Allow-Origin` to reveal detailed timing, protocol, size, content type and status.
Without it only the overall start/end/duration remain visible. DNS/connect/request phases collapse to
`fetchStart`; TLS and redirect phases are zero when unavailable. `serverTiming` is an empty frozen array and
`deliveryType` is empty. Transfer size estimates header overhead as 300 bytes; automatic decompression can
make encoded size unavailable, in which case the bytes actually read are used. Since response bodies are
read on demand, an unconsumed fetch body has no completed entry until consumed or cancelled.

`Jint.Browser` additionally records document subresources and exposes `PerformanceNavigationTiming` for its
top-level document. An ordinary engine has no document or navigation entry.

Timers and performance share `Options.WebApi.Timers.TimeProvider`, allowing deterministic tests without a
background timer. Jint does not reduce timer precision: if untrusted code should not receive a high-resolution
clock, do not enable this feature or provide an appropriately coarse `TimeProvider`.
