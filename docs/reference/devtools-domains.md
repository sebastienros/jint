# DevTools Domains

`Jint.DevTools` provides engine-level Chrome DevTools Protocol domains:

| Domain | Purpose |
| --- | --- |
| `Runtime` | execution contexts, evaluation, and remote objects |
| `Debugger` | scripts, breakpoints, stepping, scopes, and exceptions |
| `Profiler` | sampling and precise coverage |
| `Console` and `Log` | script and host diagnostics |
| `Target` | target discovery and attachment |
| `Browser` and `Schema` | client discovery and capability reporting |

`Jint.Browser` adds page-level domains including `Page`, `DOM`, `Network`, `Fetch`, `Input`, `Emulation`,
`Storage`, and `Accessibility`.

Unsupported commands return the standard method-not-found response so clients can feature-detect them. See
[Supported Domains](../packages/jint-devtools/domains.md).
