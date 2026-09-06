# Agent instructions: the `jint-browser` command line

> **Read this when:** You are touching anything under `Jint.Browser.Tool/`, or the tests of it in
> `Jint.Tests.Browser/Tool/`.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Read
> [`Jint.Browser/AGENTS.md`](../Jint.Browser/AGENTS.md) beside it for the package this drives, and its
> [`Runtime/AGENTS.md`](../Jint.Browser/Runtime/AGENTS.md) before assuming anything about which thread a
> `Page` member runs on. Nothing below is repeated in any of them.

### It is a consumer of the public surface, and that is the whole point

**This project has no `InternalsVisibleTo` grant from `Jint.Browser`, and must never take one.** It is the
first thing outside the repository's own test projects to drive that package the way an embedder does, which
is what makes it worth having twice over: it ships a useful tool, and it is the standing proof that the
published surface is enough to build one. Every time it turns out not to be, the answer is a **seam promoted
on `Jint.Browser`** — public, XML-documented, with a row in the baseline diff — and never a widened
`InternalsVisibleTo` or a copy of the package's code here.

**The same is true of `Jint.Browser.Mcp`**, which drives a page for an agent over the same published members
this does. Between them they are where the pressure to promote a seam comes from, and
[`Jint.Browser/AGENTS.md`](../Jint.Browser/AGENTS.md#the-seams-promoted-later) points here for the table of
what it has promoted so far. Every row is a `Page` member over the internals the protocol layer already
used — **one implementation**, so a caller in this process and a client on the socket cannot make a page do
two different things, or be told different things about one document:

| What was needed | What was published | Over | What was *not* done |
| --- | --- | --- | --- |
| `fetch --dump markdown\|text\|ax`, `snapshot` | `Page.MarkdownAsync`, `TextAsync`, `AccessibilitySnapshotAsync` | `Extraction/PageContent`, which the `Jint` protocol domain also answers from | reaching `Extraction/` and `Accessibility/`, which are internal |
| `--wait-until networkidle`, `wait_for` | `Page.WaitForNetworkIdleAsync`, `WaitForSelectorAsync`, `WaitForTextAsync` | the request log's own quiet period, and the document | a second quiet-period timer beside the page's |
| `serve --block-private-network` | `BrowserOptions.BlockPrivateNetwork` | the browser-wide default a context inherits | a context option, which cannot reach a context a protocol client mints |
| `click`, `fill`, `type`, `press`, `select`, `hover`, `scroll` | `Page.ClickAsync` and its siblings | `Events/InputDispatcher`, `Layout/`, `ActivationBehaviors.SelectOption` — the `Input` domain's own paths | a third input implementation, or `element.click()`, which is untrusted |
| `back`, `forward`, `reload` | `Page.GoBackAsync`, `GoForwardAsync`, `ReloadAsync` | `Runtime/SessionHistory` and the navigation gate | `evaluate("history.back()")`, which answers before the traversal commits |

**A target is a selector or a `ref=`**, and `Runtime/ElementLocator` is the one place that decides: an agent
reading `- button "Save" [ref=42]` out of a snapshot has no selector to write. The number is the
accessibility tree's own identifier and **deliberately not the protocol's `backendNodeId`** — that one is
`DevTools/DomNodeTracker`'s, which belongs to a page *target*, and a caller with no client attached has none.
A reference dies with its document, because the table is keyed on the document.

### The `mcp` command belongs to another file

`McpCommand` is a shell over `Jint.Browser.Mcp`: it reads a command line, builds a generic host, and serves
the protocol on standard input and output. Everything about *what* it serves —
[`Jint.Browser.Mcp/AGENTS.md`](../Jint.Browser.Mcp/AGENTS.md) — is there, including why stdio is the only
transport and why `--http` is refused as an unknown option rather than accepted. Two rules to carry across
without opening it: **standard output is the protocol and nothing else** while `mcp` runs, so that command
prints no banner and every diagnostic goes to standard error; and `mcp` **hardens the pages by default**,
which is the opposite of every other command here and is why it takes `--trusted` rather than `--untrusted`.

**`serve` has a second consumer now, and it is the one that would find a missing switch.**
`.github/workflows/wpt-scoreboard.yml` runs upstream's own `wpt run` against `jint-browser serve` nightly
(the plugin is [`tools/wpt-scoreboard/`](../tools/wpt-scoreboard/README.md)). It needed no new seam — but it
did need two options to *not* be defaults, and the reasons are worth knowing before either is changed.
`--max-task-duration 0` is passed because a five-second turn cuts a legitimately slow conformance file
mid-script, and `--untrusted` is deliberately **not**, because `ForUntrustedContent` applies
`UntrustedCodeLimits.Default` — one second per engine entry, 50 000 statements, 10 000-element arrays,
recursion depth 64, and four more — and no option here can move any of the eight it sets. A measurement taken under that profile
would be a report on the profile.

### What is deliberately absent

- **No command-line library.** A `dotnet tool` is restored by everyone who runs it, so every dependency is
  one more thing between a user and a page, and the whole grammar is four commands and about twenty options.
  `CommandLine.cs` is the parser and its own doc comment says when to stop defending that: a third level of
  subcommand is the moment to take `System.CommandLine` rather than to grow it. `Spectre.Console.Cli` being
  pinned in `Directory.Packages.props` is not a reason — nothing references it.
- **No output format of its own.** Everything written to standard output is a string the package computed.
  A JSON envelope around a page dump, a table, a progress bar — each would be a second thing to keep true.
- **Nothing that runs script to read the document.** `--dump` is `Extraction/` and `Accessibility/` through
  the three page members, which touch no engine; `eval` is the one command that runs anything, and it says so
  in its name.

### The rules a change here has to keep

- **An unknown option is a usage error, never a positional argument.** A typo in `--main-content` that
  silently became the URL to fetch is the worst failure this program has. `--` ends the options, which is how
  an expression starting with a dash reaches `eval`.
- **An option a command cannot act on is refused, not ignored.** `--dump html --main-content` is the worked
  example: a narrowed document is not a document, so it stops rather than quietly answering the whole one.
- **The exit codes are a contract.** `0` ok, `1` usage, `2` no document, `3` a budget, `4` the expression
  threw — separated so a script can tell its own mistake from the site's from the page's, and so that
  retrying a `2` is right and retrying a `1` would loop for ever. They are in `ExitCode`, in the README's
  table and in `--help`; a new one changes all three.
- **Standard output is the answer and standard error is everything else.** The banner `serve` prints is the
  answer; a page's own errors never are. Output is UTF-8 with no byte order mark, which is why `Program.cs`
  opens its own writers instead of using `Console.Out`.
- **`Program.cs` stays a shell.** Everything is `ToolProgram.RunAsync(args, output, error, token)` so the
  suite runs the real entry point in process — an exit code, standard output and standard error as one
  assertion, and a `serve` that ends because a token was cancelled rather than because somebody killed a
  process. Logic that moves into `Program.cs` is logic nothing tests.

### Packaging

`PackAsTool` with `ToolCommandName` `jint-browser`. Ordinary builds keep `net8.0;net10.0` for the
in-process tests; **distribution sets `BrowserToolNative=true`**, selects `net10.0` and uses .NET 10's
platform-specific Native AOT tool format. Installation requires the .NET 10 SDK, execution no runtime.

- **Publish AOT on this executable, not as a global MSBuild property.** A global `PublishAot=true`
  reaches Jint's downlevel targets and its source generators and fails before compiling the tool.
  No assembly is rooted wholesale and no new IL diagnostic is suppressed. `IlcTreatWarningsAsErrors=false`
  keeps the core engine's standing inventory visible, as in `Jint.AotExample`; the distribution workflow
  rejects diagnostics outside that core-engine inventory. This closed program's native run is not a
  general `IsAotCompatible` claim for the browser libraries.
- **`browser-tool.yml` is reused by PR, build and release workflows.** Each of the six OS/architecture
  legs packs a RID implementation and the selection manifest, installs from a source-mapped local feed,
  and runs `Tool/PublishedToolTests` against the installed executable. The tests cover extraction, scripts,
  CSS, XML, XPath, globalization, CDP and MCP. Set `JINT_BROWSER_TOOL` and `JINT_BROWSER_TOOL_VERSION` to
  reproduce; without them those process tests are skipped rather than passed.
- **`build.yml` and `release.yml` publish the same seven tool packages**, at the libraries' version:
  six RID packages first, then `Jint.Browser.Tool`'s manifest. The manifest alone is not an installable
  distribution. Publishing a GitHub release attaches the six single executables and `SHA256SUMS`, without
  publishing NuGet again. The nuget.org trusted-publishing policy for `release.yml` / environment `nuget`
  must permit creating the new `Jint.*` IDs, not only updating `Jint`. Symbols and reference documentation
  are not runtime files.
- **`README.md` here is the package README NuGet shows.** It is written for somebody who found the tool
  rather than the engine: it must keep saying that this renders nothing, and it must keep the exit-code
  table in step with `ExitCode`. `Jint.Browser.Mcp/README.md` is the other one, for the other audience.
