# Security

MCP pages are hardened by default:

```csharp
.AddJintBrowser(options =>
{
    options.Trusted = false;
    options.UrlFilter = uri => uri.Host == "docs.example.org";
});
```

With `Trusted = false`, each page and worker uses Jint's untrusted-content profile. It disables dynamic code evaluation, CLR interop, module loading, debugger and experimental features, and applies bounds to statements, time, allocation, recursion, arrays, regular expressions, parsing, promise waits, and conversion.

Private, loopback, link-local, and cloud-metadata addresses are blocked by default. Set `BlockPrivateNetwork = false` only when the agent must reach a local service and the surrounding environment permits it.

Prefer a narrow `UrlFilter`. It runs on the first request and every redirect, so a permitted public URL cannot redirect directly to a forbidden origin. The filter must be thread-safe and quick.

Private-address checks cannot by themselves prevent DNS rebinding or protect services reachable through unusual routing. Use operating-system, container, or network isolation for strong boundaries.

`Trusted = true` removes the hardened profile and should be reserved for content controlled by the deployment. It does not add rendering or unsupported browser features.

Snapshot length, navigation timeout, task duration, and memory limit should be set to values appropriate for the model and workload.
