# Supported API

The table summarizes the current direct adapter. Members not listed should be treated as unsupported.

| Interface | Supported surface |
| --- | --- |
| `IBrowserType` | name, executable path, `LaunchAsync` with headless |
| `IBrowser` | type, contexts, connection state, version, context/page creation, close, events |
| `IBrowserContext` | browser, pages, closed state, new page, close, default timeouts, events |
| `IPage` | context/frame metadata, URL, closed state, CSS/role locators, goto/reload/back/forward, set/get content, title, evaluate, wait for function, close, defaults |
| `IFrame` | page/URL/name/frame metadata, CSS locator, content, title |
| `ILocator` | first/last/nth, count, text contents, text content, input value, visibility, wait, click, fill, press |
| `IResponse` | frame, service-worker flag, URL/status/status text/OK, headers, finished |
| `IJSHandle` | `AsElement`, JSON value, disposal |

The adapter is built against Microsoft.Playwright 1.62. Because it uses runtime dispatch rather than implementing interfaces directly, a newer Playwright package may expose new members without a compile-time error. Pin and test the version your application uses.

Options are validated per operation. A non-default option is accepted only when the adapter implements its behavior; otherwise the call fails.
