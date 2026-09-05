# Modules

Enable file modules with a base path, then import an ES module:

```csharp
var engine = new Engine(options => options.UseModules("/app/scripts"));
var module = engine.Modules.Import("./main.js");
var value = module.Get("value").AsString();
```

The default loader restricts resolution to the configured base path by default. It does not implement npm
package resolution; use `NodeStyleModuleLoader` when that behavior is required.

## Programmatic modules

Modules registered in memory do not require `UseModules`:

```csharp
var engine = new Engine();

engine.Modules.Add("settings", builder => builder
    .ExportValue("mode", "production")
    .ExportType<MyPublicApi>());

var module = engine.Modules.Import("settings");
```

You can also add JavaScript source:

```csharp
engine.Modules.Add("math", "export const twice = x => x * 2;");
```

## Asynchronous loaders

Use an `IAsyncModuleLoader` or derive from `AsyncModuleLoader` when loading source requires I/O:

```csharp
var engine = new Engine(options => options.UseModules(myAsyncLoader));
var module = await engine.Modules.ImportAsync("./main.js");
```

Static dependencies are loaded transitively before linking. `ImportAsync` does not hold the calling thread.
For a UI or game loop that must control exactly where engine turns run, start the operation and pump it:

```csharp
var import = engine.Modules.StartImport("./main.js");

engine.Tasks.ProcessTasks();
if (import.IsCompleted)
{
    var module = import.GetResult();
}
```

Do not use synchronous `Import` when loader completions need the blocked thread; it can deadlock. See
[Asynchronous execution](./async.md) for engine ownership and failure behavior.

## Locations and policy

Every loaded module has a non-null `ModuleRecord.Location`. Relative imports, stack traces, debugger locations,
and a host-provided `import.meta.url` may expose it. Keep credentials and other secrets out of module keys and
URLs.

For untrusted graphs, configure finite module count, source-byte, depth, and resolution-hop limits plus a
`ModuleAllowlistPolicy`. The built-in file-root policy performs lexical containment; it does not resolve symbolic
links or reparse points. Custom network loaders must enforce redirect and destination policy on every hop.

Prepared module ASTs can be shared across engines, but each engine still owns its module registry, linking, and
evaluation. See [Performance](./performance.md).
