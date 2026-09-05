# Node compatibility

Jint implements ECMAScript, not the complete Node.js runtime. Its Node compatibility features are narrow,
explicit opt-ins.

## `process`

```csharp
var engine = new Engine(options => options.UseNodeProcess(process =>
{
    process.EnvironmentVariableAllowlist = ["NODE_ENV"];
    process.EnvironmentOverrides = new Dictionary<string, string>
    {
        ["NODE_ENV"] = "production"
    };
}));

var mode = engine.Evaluate("process.env.NODE_ENV").AsString();
```

No environment variable is visible until allowlisted. `process.env` is materialized once and script writes
remain local. `cwd()` returns the configured `WorkingDirectory` (`"/"` by default), never the host process's
real directory. `argv` is empty, while `exit`, `abort`, `kill`, and `chdir` are absent.

`process.nextTick` uses Jint's single job queue, so it and promise reactions run in registration order rather
than Node's separate next-tick ordering.

## Package resolution and built-ins

Use `NodeStyleModuleLoader` for an on-disk `node_modules` tree:

```csharp
var engine = new Engine(options => options
    .UseModules(new NodeStyleModuleLoader("/app"))
    .UseNodeBuiltinModules());
```

The base path bounds both reachable files and the upward package search. Built-ins are limited to pure utility
modules:

- `node:path`, `node:path/posix`, and `node:path/win32`
- `node:querystring`
- `node:url`

Unprefixed `path`, `querystring`, and `url` work by default. A host-registered module with the same name wins.
Resource-bearing modules such as `node:fs`, `node:child_process`, `node:http`, `node:crypto`, `node:os`, and
`node:buffer` are deliberately absent.

`node:path` is available on all Jint target frameworks. `node:url` and `node:querystring` require .NET 8 because
they use Jint's WHATWG URL implementation.

See [Modules](./modules.md) for general module loading and graph policy.
