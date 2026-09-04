---
layout: home

hero:
  name: Jint
  text: JavaScript for .NET
  tagline: Embed a modern ECMAScript interpreter in any .NET application, without a native runtime.
  image:
    src: /favicon.svg
    alt: Jint
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: Explore Packages
      link: /guide/choosing-a-package

features:
  - icon: ⚡
    title: Embed JavaScript
    details: Execute scripts, evaluate expressions, call functions, and exchange values with .NET.
    link: /packages/jint/execution
  - icon: 🔒
    title: Bound Execution
    details: Apply statement, time, memory, recursion, parsing, and cancellation limits.
    link: /packages/jint/untrusted-code
  - icon: 🌐
    title: Opt-in Web APIs
    details: Add console, timers, fetch, streams, crypto, storage, workers, and more when your host needs them.
    link: /packages/jint/web-apis
  - icon: 🧭
    title: Headless Browser
    details: Parse HTML, run page scripts, automate a DOM, and inspect the result without Chromium.
    link: /packages/jint-browser/
  - icon: 🐛
    title: DevTools Protocol
    details: Connect Chrome DevTools, Puppeteer, or Playwright to an engine your application hosts.
    link: /packages/jint-devtools/
  - icon: 📦
    title: Modern .NET
    details: Use the core engine from .NET Framework 4.7.2 through current .NET, with Native AOT support on modern targets.
    link: /guide/supported-platforms
---

## Start with Jint

```bash
dotnet add package Jint
```

[View Jint on NuGet.org](https://www.nuget.org/packages/Jint)

```csharp
using Jint;

var result = new Engine()
    .SetValue("name", "World")
    .Evaluate("`Hello, ${name}!`")
    .AsString();
```

The `Jint` package is the main entry point. Add another package only when you need debugging or browser
functionality. Jint is open source under the
[BSD 2-Clause License](https://github.com/sebastienros/jint/blob/main/LICENSE.txt).

<div class="package-grid">
  <a class="package-card" href="./packages/jint/">
    <strong>Jint</strong>
    <span>The JavaScript interpreter, .NET interop, constraints, modules, promises, and opt-in runtime APIs.</span>
  </a>
  <a class="package-card" href="./packages/jint-devtools/">
    <strong>Jint.DevTools</strong>
    <span>A Chrome DevTools Protocol server for an engine hosted by your application.</span>
  </a>
  <a class="package-card" href="./packages/jint-browser/">
    <strong>Jint.Browser</strong>
    <span>An in-process, non-rendering browser built with Jint and AngleSharp.</span>
  </a>
  <a class="package-card" href="./packages/jint-browser-playwright/">
    <strong>Jint.Browser.Playwright</strong>
    <span>A direct implementation of public Microsoft.Playwright interfaces over Jint.Browser.</span>
  </a>
  <a class="package-card" href="./packages/jint-browser-tool/">
    <strong>Jint.Browser.Tool</strong>
    <span>The <code>jint-browser</code> command for extraction, evaluation, CDP, and MCP.</span>
  </a>
  <a class="package-card" href="./packages/jint-browser-mcp/">
    <strong>Jint.Browser.Mcp</strong>
    <span>An MCP server that lets agents navigate and interact with non-rendered pages.</span>
  </a>
</div>

## Used by

Projects using Jint include
[RavenDB](https://github.com/ravendb/ravendb),
[EventStoreDB](https://github.com/EventStore/EventStore),
[Orchard Core](https://github.com/OrchardCMS/OrchardCore),
[Elsa Workflows](https://github.com/elsa-workflows/elsa-core),
[Docfx](https://github.com/dotnet/docfx), and
[JavaScript Engine Switcher](https://github.com/Taritsyn/JavaScriptEngineSwitcher).
