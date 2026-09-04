# Package Matrix

| Package | Frameworks | Depends on |
| --- | --- | --- |
| [`Jint`](https://www.nuget.org/packages/Jint) | net472, netstandard2.0, netstandard2.1, net8.0, net10.0 | Acornima |
| [`Jint.DevTools`](https://www.nuget.org/packages/Jint.DevTools) | net8.0, net10.0 | Jint |
| [`Jint.Browser`](https://www.nuget.org/packages/Jint.Browser) | net8.0, net10.0 | Jint, Jint.DevTools, AngleSharp |
| [`Jint.Browser.Playwright`](https://www.nuget.org/packages/Jint.Browser.Playwright) | net8.0, net10.0 | Jint.Browser, Microsoft.Playwright API contracts |
| [`Jint.Browser.Tool`](https://www.nuget.org/packages/Jint.Browser.Tool) | net8.0, net10.0 | Jint.Browser, Jint.DevTools, Jint.Browser.Mcp |
| [`Jint.Browser.Mcp`](https://www.nuget.org/packages/Jint.Browser.Mcp) | net8.0, net10.0 | Jint.Browser, Model Context Protocol SDK |

`Jint.Browser.Tool` is installed as a .NET tool. The others are libraries consumed by an application.

`Jint` is available on NuGet.org today. The optional package pages will become available there with the Jint 5
release; until then, install their development builds from the
[Feedz preview feed](https://feedz.io/org/sebastienros/repository/jint/packages).
