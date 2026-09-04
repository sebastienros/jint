# Choosing a Package

Start with `Jint`. The other packages add optional hosting surfaces.

| Package | Use it when you need |
| --- | --- |
| [`Jint`](../packages/jint/) | JavaScript execution, .NET interop, modules, constraints, promises, or opt-in web APIs |
| [`Jint.DevTools`](../packages/jint-devtools/) | Chrome DevTools Protocol for an engine hosted by your application |
| [`Jint.Browser`](../packages/jint-browser/) | HTML, DOM, navigation, extraction, or browser-style automation without rendering |
| [`Jint.Browser.Playwright`](../packages/jint-browser-playwright/) | Public Playwright interfaces backed directly by `Jint.Browser` |
| [`Jint.Browser.Tool`](../packages/jint-browser-tool/) | A command-line browser, CDP endpoint, or ready-made MCP process |
| [`Jint.Browser.Mcp`](../packages/jint-browser-mcp/) | Browser tools embedded in your own Model Context Protocol server |

The browser packages do not render pixels. Use a native browser when you need layout, screenshots, PDF,
media, WebGL, or extensions.
