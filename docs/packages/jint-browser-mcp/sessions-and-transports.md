# Sessions and transports

Browser state includes the current page, history, cookies, and storage. The packaged command uses stdio because its lifecycle has a clear owner:

1. A client starts `jint-browser mcp`.
2. That process owns one browser agent and browsing context.
3. The client closes the pipe or process.
4. The browser, page, and context are disposed.

Tool calls are serialized within the session so a read cannot race a navigation or action.

`close` clears the current page and context. A later `navigate` lazily opens a fresh context with no previous cookies, storage, or history.

## HTTP hosts

`AddJintBrowser` registers one browser and one browser agent as singletons. This is correct for one stdio client per process, but it would share state among callers of a normal multi-client HTTP server.

The current MCP streamable-HTTP model is stateless by default and does not provide the older session header as a general scoping key. Dependency-injection request scope is therefore not browsing-session scope.

An HTTP application that needs stateful independent sessions must use the MCP SDK's explicit session lifecycle (`ConfigureSessionOptions` and `RunSessionHandler`), create and bind a `BrowserAgent` for each session, and dispose it when that session ends. This path uses experimental SDK APIs and may require negotiating an older protocol revision. The package deliberately does not choose that trade-off for hosts.

The `jint-browser mcp` command does not reference or expose an HTTP transport.
