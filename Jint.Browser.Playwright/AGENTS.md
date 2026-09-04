# Agent instructions: the Playwright adapter

> **Read this when:** You are adding or changing a member of the Microsoft.Playwright surface this package
> implements, deciding what an unsupported call should do, touching its options handling, or bumping the
> Microsoft.Playwright version.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there. Everything the adapter drives belongs to the browser package, and its rules — the one
> thread that owns a page's engine and its DOM, the flat box model a coordinate resolves against — are in
> [`Jint.Browser/Runtime/AGENTS.md`](../Jint.Browser/Runtime/AGENTS.md).

`Jint.Browser.Playwright` implements Playwright's *public* interfaces directly over `Jint.Browser`. It
starts no browser and no Node process, and opens no Chrome DevTools Protocol connection. The CDP lane that
drives a page with the real Playwright driver is a different thing entirely and lives in
[`Jint.Browser/DevTools/AGENTS.md`](../Jint.Browser/DevTools/AGENTS.md).

## The compiler cannot tell you a member is missing

The public surface of this package is two members — `JintPlaywright.BrowserType` and
`JintPlaywright.CreateBrowserType`. Everything a caller then touches is a `DispatchProxy` built by
`ProxyFactory.Create<T>` over a `ProxyTarget`, and each target dispatches on **`method.Name`** in a
`switch`, with property getters spelled as raw strings (`"get_Name"`, `"get_Contexts"`).

Nothing here declares `: IPage`. So no interface member is ever *missing* as far as the build is concerned:
a member this package has never heard of compiles, links and ships, and is discovered when a caller invokes
it. **A Microsoft.Playwright version bump is therefore a behavioural change with an empty diff** — new
members appear on the interfaces, renamed ones stop matching their `case`, and neither shows up as a
warning. Read the release notes for interface changes, and add a test in
`Jint.Tests.Browser/Playwright/DirectPlaywrightTests.cs` for anything you claim to support; that suite is
the only thing that proves a member is reachable at all. The package is built and tested against
Microsoft.Playwright 1.62.

## An unsupported call fails loudly, and the *shape* of the failure is the contract

`UnsupportedOperation.For` is the only correct answer for a member this adapter does not implement, and what
it does is deliberate:

- a member returning `Task`, `Task<T>` or `ValueTask` gets a **faulted task**, not a synchronous throw;
- everything else throws `NotSupportedException` outright.

The asymmetry is the point. A `DispatchProxy` that threw synchronously out of a `Task`-returning member
would surface the failure at the call rather than at the `await`, so a client that hands the task around
before awaiting it — which is ordinary Playwright code — would see it escape somewhere its `try`/`catch`
is not. **Never return `null`, an empty result, or a completed task for something the adapter cannot do.**
A silent no-op turns a missing feature into a wrong answer, and an automation script cannot tell the
difference between "the button was not there" and "clicking is not implemented".

## An option that is not honoured throws; it is never ignored

Every entry point taking an options object calls `OptionSupport.EnsureOnly(options, operation, …supported)`,
naming the options it actually honours. It reflects over the rest and throws for any set to a non-default
value. Adding a member without that call accepts every option and quietly drops it — a `Timeout` or a
`WaitUntil` that does nothing is worse than a `NotSupportedException`, because the test that depended on it
goes green. Widening support is adding a name to the list beside the code that now reads it, in the same
change.

## Two things in the project file that look like tidiness and are not

- **`ExcludeAssets="build;buildTransitive;contentFiles"` on the Microsoft.Playwright reference.** Only the
  public API contracts are used. A plain `PackageReference` also brings Playwright's build targets, which
  copy its bundled Node runtime and browser driver into every application that consumes this package —
  precisely the payload the adapter exists so that nobody needs.
- **`IsAotCompatible` is `false`, on purpose.** `DispatchProxy` generates its types at run time. Do not flip
  the property to make a warning go away; Jint's own AOT claim and what backs it are
  [`Jint.AotExample/AGENTS.md`](../Jint.AotExample/AGENTS.md), and this package is deliberately not part of
  it.

## What must fail rather than be approximated

Anything needing pixels, a native browser or an operating-system window — screenshots, PDF generation,
video, browser extensions, CDP sessions. `Jint.Browser` has a flat box model and no renderer, so there is
nothing behind these to be almost right; a plausible-looking approximation is the failure mode to avoid.

The same applies to clients that reach past the interfaces. Playwright's own assertions cast its public
interfaces to its internal implementation types, so they cannot run against a third-party implementation at
all — that is Playwright's constraint, not something a shim here can or should work around. Applications
use the public browser, context, page, frame and locator interfaces; the `README.md` beside this file is
what says so to a consumer, and it is the file to keep current when support widens.

Locator support is CSS selectors and a first set of role locators, with strict matching, waiting and trusted
input dispatch — deliberately short of Playwright's full actionability, accessibility-name and atomic
resolution semantics. Say what is supported rather than implying the rest.

`Jint.Tests.Browser/Verify/PlaywrightPublicApiTest.verified.txt` is the baseline for the two public members,
and it only runs when `RunsPublicApiBaselines` is set; a change to it is a change somebody reads.
