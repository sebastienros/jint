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

The timeline retains at most 10,000 entries; clear marks and measures when they are no longer needed.
`PerformanceObserver` callbacks are queued microtasks and run only while the engine is pumped.

Timers and performance share `Options.WebApi.Timers.TimeProvider`, allowing deterministic tests without a
background timer. Jint does not reduce timer precision: if untrusted code should not receive a high-resolution
clock, do not enable this feature or provide an appropriately coarse `TimeProvider`.
