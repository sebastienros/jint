[![Build](https://github.com/sebastienros/jint/actions/workflows/build.yml/badge.svg)](https://github.com/sebastienros/jint/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Jint.svg)](https://www.nuget.org/packages/Jint)
[![Feedz](https://img.shields.io/feedz/vpre/sebastienros/jint/Jint.svg?label=Feedz)](https://feedz.io/org/sebastienros/repository/jint/packages/Jint)

# Jint

Jint is a modern ECMAScript interpreter for .NET. It runs JavaScript directly in
your process without native dependencies, bytecode generation, or a separate
runtime.

Jint targets .NET Framework 4.7.2, .NET Standard 2.0/2.1, .NET 8, and .NET 10.
Start with the [documentation](https://sebastienros.github.io/jint/) or install
the core package:

```bash
dotnet add package Jint
```

## Quick start

The examples below assume `using Jint;`.

### Evaluate JavaScript

<!-- snippet: readme-evaluate -->
```csharp
var engine = new Engine();
var result = engine.Evaluate("40 + 2").AsNumber();
```
<!-- endSnippet -->

### Expose .NET and call JavaScript

<!-- snippet: readme-expose-and-invoke -->
```csharp
var engine = new Engine()
    .SetValue("log", new Action<string>(Console.WriteLine))
    .Execute("""
        function greet(name) {
            const message = `Hello, ${name}!`;
            log(message);
            return message;
        }
        """);

var greeting = engine.Invoke("greet", "Ada").AsString();
```
<!-- endSnippet -->

### Prepare code that runs repeatedly

<!-- snippet: readme-prepare -->
```csharp
var script = Engine.PrepareScript(
    "items.reduce((sum, value) => sum + value, 0)",
    source: "sum.js",
    strict: true);

var engine = new Engine();
engine.SetValue("items", new[] { 1, 2, 3 });

var total = engine.Evaluate(in script).AsNumber();
```
<!-- endSnippet -->

`Prepared<Script>` and `Prepared<Module>` are thread-safe and may be shared
across engines. JavaScript objects are not: a `JsValue` that contains an object
belongs to the engine and realm that created it.

## Features

- Modern ECMAScript, modules, promises, async functions, generators, `Intl`,
  and `Temporal`
- Direct projection of .NET values, delegates, objects, collections, and types
- Synchronous and asynchronous module loaders
- Execution constraints, hardened untrusted-code defaults, and security
  diagnostics
- Profiling, statement coverage, debugging, and Chrome DevTools Protocol
  integration
- Opt-in Web APIs and Node-compatible modules
- A headless browser, Playwright adapter, command-line tool, and MCP server

Browser and Node APIs are not installed by default. Hosts explicitly grant the
capabilities each script needs.

## Used by

Projects using Jint include
[RavenDB](https://github.com/ravendb/ravendb),
[EventStoreDB](https://github.com/EventStore/EventStore),
[Orchard Core](https://github.com/OrchardCMS/OrchardCore),
[Elsa Workflows](https://github.com/elsa-workflows/elsa-core),
[Docfx](https://github.com/dotnet/docfx), and
[JavaScript Engine Switcher](https://github.com/Taritsyn/JavaScriptEngineSwitcher).

## Packages

| Package | Purpose |
| --- | --- |
| [`Jint`](https://www.nuget.org/packages/Jint) | ECMAScript engine, CLR interop, modules, constraints, and opt-in runtime Web APIs |
| [`Jint.DevTools`](https://www.nuget.org/packages/Jint.DevTools) | Chrome DevTools Protocol server for debugging and profiling an engine |
| [`Jint.Browser`](https://www.nuget.org/packages/Jint.Browser) | Headless HTML, DOM, navigation, networking, storage, and content extraction |
| [`Jint.Browser.Playwright`](https://www.nuget.org/packages/Jint.Browser.Playwright) | Direct Playwright-compatible adapter without the bundled Node driver |
| [`Jint.Browser.Tool`](https://www.nuget.org/packages/Jint.Browser.Tool) | `jint-browser` command-line tool |
| [`Jint.Browser.Mcp`](https://www.nuget.org/packages/Jint.Browser.Mcp) | Model Context Protocol server for browser automation |

See [Choosing a package](https://sebastienros.github.io/jint/guide/choosing-a-package)
for dependencies, target frameworks, and common combinations.

## Standards conformance

These percentages describe the pinned test corpora in this repository, not the
entire web platform.

| Surface | Result | Measured scope |
| --- | ---: | --- |
| ECMAScript and ECMA-402 | **99.9%** (102,585 / 102,692) | Generated test262 cases from `annexB`, `built-ins`, `intl402`, `language`, and `staging`; 107 cases are skipped |
| WinterTC Minimum Common API | **79.5%** (62 / 78) | 59 members present and 3 correctly absent for Jint's global shape; the remaining 16 are WebAssembly, which Jint declines by design |
| Jint runtime Web APIs | **92.9%** (38,649 / 41,581) | Assertions in the vendored `.any.js` WPT corpus across 44 suite directories |
| Jint.Browser | **84.0%** (9,691 / 11,541) | Tests in the gated in-process browser WPT corpus |

The test262 result uses commit
[`419d3e0`](https://github.com/tc39/test262/commit/419d3e0a2273ba01a3bfcbec423f2801425b8e93).
Read the detailed scope and exclusions for
[ECMAScript](https://sebastienros.github.io/jint/reference/ecmascript),
[Web APIs](https://sebastienros.github.io/jint/reference/web-api-features), and
[Jint.Browser](https://sebastienros.github.io/jint/reference/browser-compatibility).

## Recommended usage

- Enable strict mode when compatible with the scripts you run.
- Prepare and cache scripts or modules that execute repeatedly.
- Give each engine to only one operation at a time; await async operations
  before reusing or disposing it.
- Expose narrow, purpose-built host capabilities instead of broad CLR access.
- Use a fresh engine across trust domains. Global snapshots help trusted reuse,
  but they are not an isolation boundary.
- Measure with your own scripts and host objects. Interpreter performance
  depends heavily on call frequency and script-to-host traffic.

See [performance](https://sebastienros.github.io/jint/packages/jint/performance)
and [advanced hosting](https://sebastienros.github.io/jint/packages/jint/advanced-hosting)
for engine lifetime, pooling, projection, and caching guidance.

## Benchmarks

The repository's current default-job BenchmarkDotNet comparison uses each
engine's recommended cached execution path:

- Jint is the fastest managed engine on 10 of 12 scripts and the fastest
  interpreter on all 12.
- Jint has the lowest managed allocation on 10 of 12 scripts.
- Script-to-host interop is 3.4x to 11.2x faster than the measured ClearScript
  lanes.
- The startup-shaped minimal script runs in about 1.1 us with a prepared Jint
  script, compared with about 380 us for the compiled V8 lane.

Native V8 remains faster on long, compute-heavy loops. Managed allocation
figures also cannot include V8's native heap. Read the
[engine comparison and methodology](https://sebastienros.github.io/jint/packages/jint/engine-comparison)
before comparing engines; absolute numbers from different benchmark sessions
are not comparable.

## Running untrusted code

Jint is an in-process interpreter, not an operating-system security boundary.
Start with the hardened profile, keep the host surface small, and add a
process-level boundary for hostile input.

<!-- snippet: readme-untrusted-code -->
```csharp
var limits = UntrustedCodeLimits.Default with
{
    TimeoutInterval = TimeSpan.FromSeconds(1),
    MaxStatements = 50_000,
    MemoryLimit = 16_000_000
};

var options = new Options().ForUntrustedCode(limits);
using var engine = new Engine(options);

using (limits.BeginOperation(engine, cancellationToken))
{
    var value = engine.Evaluate(source);
    return engine.ConvertResult(value, limits.ResultLimits);
}
```
<!-- endSnippet -->

The profile disables broad CLR and reflection access, module loading, string
compilation, projected CLR writes, live CLR-array views, registered extension
methods, and blocking `Atomics.wait`. It also applies finite parsing, execution,
memory, recursion, regular-expression, promise, and result limits.

Constraints are cooperative and cannot preempt arbitrary host callbacks. Use
an external deadline, restrict module and network loaders on every hop, and run
hostile code in a least-privileged disposable process or container.

Read [Running untrusted code](https://sebastienros.github.io/jint/packages/jint/untrusted-code)
and the [threat model](https://github.com/sebastienros/jint/blob/main/.github/THREAT_MODEL.md)
before deploying a script service.

## Documentation and support

- [Documentation](https://sebastienros.github.io/jint/)
- [Preview packages](https://feedz.io/org/sebastienros/repository/jint/packages)
- [Migration to Jint 5](https://sebastienros.github.io/jint/guide/migrating-to-v5)
- [Issues](https://github.com/sebastienros/jint/issues)
- [Sponsors](https://sebastienros.github.io/jint/sponsors)

Jint is licensed under the [BSD 2-Clause License](LICENSE.txt).
