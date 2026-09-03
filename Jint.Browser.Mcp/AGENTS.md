# Agent instructions: the Model Context Protocol server

> **Read this when:** You are touching anything under `Jint.Browser.Mcp/`, the `mcp` command in
> `Jint.Browser.Tool/`, or its tests in `Jint.Tests.Browser/Mcp/`.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first, then
> [`Jint.Browser.Tool/AGENTS.md`](../Jint.Browser.Tool/AGENTS.md) — the rule that both packages exist to
> keep, and the table of seams that pressure has promoted, are there and are not repeated here. Nothing
> below is in either.

### It is a consumer of the public surface, exactly as the tool is

**No `InternalsVisibleTo` grant from `Jint.Browser`, ever.** Every tool here is a `Page` member, and a tool
that needs something `Page` does not publish is a seam to promote — with XML docs and a baseline diff — not
a grant to widen. The consequence worth stating: `BrowserAgent` is a **thin** layer. It resolves nothing,
walks no DOM and dispatches no event; it calls one `Page` member per tool and shapes the answer. A method
here that is doing real work is a method in the wrong assembly.

### The descriptions are the product

**A tool description is read by a model, and it is the only documentation that model will ever have.** So
each says what the tool does, what it takes, and what to call before or after it — `snapshot` says to call
it after every action, `fill` says when to use `type` instead, `evaluate` says to prefer the others. They
are `[Description]` attributes rather than XML doc comments, deliberately: the SDK turns XML docs into
descriptions only through its source generator and only on `partial` methods, and a non-partial method with
`///` docs ships **an empty description with no warning**. If you write a tool, write a `[Description]`.

The one thing every description must keep saying is that there are no screenshots. A model that believes it
can see the page will keep asking for a picture instead of a snapshot.

### Errors are answers, not exceptions

**Every tool returns a `CallToolResult` and none of them throws.** `ToolJson.Ok` fills both `content` (the
JSON as text, which is what a model reads) and `structuredContent` (the same bytes, for a client that binds
to it); `ToolJson.Failed` sets `isError` with one sentence.

That is not belt and braces over the SDK's own handling — it is a correction to it. **The SDK turns an
unhandled exception into `isError` with the message *redacted*** ("An error occurred invoking 'click'."),
which tells an agent nothing it can act on. Only `McpException` survives, and shaping every failure as one
would mean throwing to communicate. So `BrowserAgent` throws `BrowserToolException` with a sentence written
for a model, and `BrowserTools` catches it at the boundary. A new tool that forgets the `AnswerAsync`
wrapper compiles and silently loses every message it would have given.

**A tool that found nothing is not an error.** `click` on a target that matches nothing answers
`done: false` with a note saying to take an `ax` snapshot and use a `ref=`; only something the agent cannot
recover from — a URL that is not one, a mode that names no representation, an expression that threw — is
`isError`.

### One session, and where that stops being true

`AddJintBrowser` registers one `Browser` and one `BrowserAgent` **as singletons**. For stdio that is exactly
a session per client, because the client starts the process; the gate in `BrowserAgent` then serializes tool
calls, not for thread safety — every `Page` member is already safe from any thread — but so that a snapshot
cannot answer about a document a click is in the middle of replacing.

**Over HTTP it is wrong, and the package says so rather than pretending.** The protocol's 2026-07-28
revision removed the session header from streamable HTTP (SEP-2567), so the SDK's transport is stateless by
default and one server serves every caller; `AddScoped` is per *request*, not per session, and
`McpServer.Services` is the root provider. The only session-ended hook in the SDK is
`HttpServerTransportOptions.RunSessionHandler`, which is `[Experimental]` (`MCPEXP002`) and whose `Stateful`
mode forces a client to negotiate down to `2025-11-25`. So: **`ModelContextProtocol.AspNetCore` is not
referenced anywhere in this repository**, `jint-browser mcp` serves stdio and refuses `--http` as an unknown
option, and a host that wants HTTP writes the six lines of `ConfigureSessionOptions` + `RunSessionHandler`
itself. Reversing that decision means measuring the downgrade again, not just adding a package.

### The tests drive a client, not a method

`Jint.Tests.Browser/Mcp/` joins a real server and the SDK's real client with a pair of `System.IO.Pipelines`
pipes, so every message is serialized, framed, parsed and dispatched exactly as it would be over stdio. A
suite that called `BrowserAgent` directly would pass with the schema broken, the serializer context missing
a type, or `isError` never set. `McpFixture` pins the context's `UrlFilter` to its own loopback server, which
is what gives the refusal path something real to refuse and what stops a test reaching the network.

The command line's own half — the options `mcp` accepts and refuses — is `Jint.Tests.Browser/Tool/`, because
that is about a command line rather than about a protocol.

### Serialization

**Every result goes through `ToolJsonContext`**, the source-generated context, for the discipline
`Jint.DevTools` keeps: the first reflective serialization is the one that fails in a published binary rather
than in a test. A new result record is a new `[JsonSerializable]` entry and a new `JsonTypeInfo` at the call
site; there is no reflective fallback to catch the omission, and there must not be one.
