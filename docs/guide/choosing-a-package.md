# Additional Packages

The `Jint` package is the core runtime used throughout this documentation. Add
one of these packages when an application needs an optional debugging, browser,
automation, command-line, or agent surface.

| Package | Use it when you need |
| --- | --- |
| [`Jint.DevTools`](../packages/jint-devtools/) | Chrome DevTools Protocol for an engine hosted by your application |
| [`Jint.Browser`](../packages/jint-browser/) | HTML, DOM, navigation, extraction, or browser-style automation without rendering |
| [`Jint.Browser.Playwright`](../packages/jint-browser-playwright/) | Public Playwright interfaces backed directly by `Jint.Browser` |
| [`Jint.Browser.Tool`](../packages/jint-browser-tool/) | A command-line browser, CDP endpoint, or ready-made MCP process |
| [`Jint.Browser.Mcp`](../packages/jint-browser-mcp/) | Browser tools embedded in your own Model Context Protocol server |

The browser packages do not render pixels. Use a native browser when you need layout, screenshots, PDF,
media, WebGL, or extensions.
