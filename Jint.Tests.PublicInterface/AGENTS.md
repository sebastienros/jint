# Agent instructions: integrator-facing tests

> **Read this when:** You are writing a test that has to prove a third party can actually reach an API,
> **or a build here has failed on `PublicApiTest` or `PublicApiDocumentationTest`.**
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Where integrator-facing tests belong

`Jint.Tests.PublicInterface` is the only test project **without** `InternalsVisibleTo` (the grant list is `Jint.Tests`, `Jint.Tests.Test262`, `Jint.Benchmark`, `Jint.Repl`), so a test there actually proves the surface is reachable by a third party. Put new integrator-facing tests there, in **generically named files** describing the capability rather than any particular integrator — the `Host*Tests.cs` family (`HostObjectSemanticsTests`, `HostObjectProbeCountTests`, `HostObjectEnumerationTests`, `HostDelegateTests`, …) is the established precedent. Remember that a `protected internal` member is seen as `protected` from outside the assembly, so an override is spelled `protected override`.

### The public API baselines

`Verify/PublicApiTest_<tfm>.verified.txt` **is** Jint's public surface, written down — one file per target
framework it ships, produced by `PublicApiTest.cs`. It is the only guard this repository has against an
unintended public API change: there is no ApiCompat run and no shipped/unshipped API files.

**A failure is a diff to review, not a bug report to act on blindly.** Read it first.

- **Intended?** Accept the new baseline and carry the same diff into the v5 migration guide. Verify writes a
  `*.received.txt` beside the `*.verified.txt` on failure; accepting is replacing the one with the other (a
  diff tool, `DiffEngineTray`, or a plain copy — Verify's own docs cover the tooling).
- **Not intended?** The diff *is* the bug report. Nothing else in the repository would have caught it.
- **Never hand-edit a baseline.** A hand-written line is a claim about the assembly that nothing checked, and
  it survives forever because the next run compares against it rather than against the compiler.

These files are also what the migration guide gets written from: `git diff <rev>:<path> <rev>:<path>` over a
baseline is the exhaustive, always-current API delta between two revisions, which no hand-maintained table
stays.

Three things about how it is wired are worth knowing before touching it:

- **Five baselines, because the surface genuinely differs.** Everything under `Jint/WebApi/` is behind
  `#if NET8_0_OR_GREATER`, and `SUPPORTS_HALF` and its siblings add members downlevel targets lack, so the
  diff for a new member tells you *which consumers reach it*. As it stands the five collapse into two distinct
  surfaces — `net472` = `netstandard2.0` = `netstandard2.1`, and `net8.0` = `net10.0` — and keeping them as
  five separate files is what makes a future divergence inside either group show up as a diff instead of as
  nothing.
- **They are generated from `artifacts/bin/Jint/`, not from the loaded assembly.** A test project can only
  ever *load* two of the five, so `netstandard2.0`, `netstandard2.1` and `net8.0` are read out of the build
  output through a `MetadataLoadContext` — which composes with `PublicApiGenerator` because it reads the file
  with Mono.Cecil from `Assembly.Location` and never reflects over it. The `BuildJintForEveryShippedTargetFramework`
  target in the `.csproj` builds the outer, cross-targeting Jint project so all five are present and current
  whenever this suite builds; if one is missing or the tree is left over from a different build, the test says
  so and names `dotnet build -c Release Jint/Jint.csproj` rather than passing on stale output.
- **An attribute whose type is `internal` in that assembly is not rendered.** `PublicApiGenerator` skips it,
  so `[Experimental("JINT0001")]` — Jint's marker on its declared non-contracts — shows up only in the
  `net8.0` and `net10.0` baselines. Downlevel it is PolySharp's polyfill, which this repository deliberately
  generates `internal`. The diagnostic itself still fires for a downlevel consumer (the `net472` leg of this
  very project proves it, against Jint's `net472` asset); only the snapshot is silent. Do not "fix" that by
  making the polyfills public — that would put a type whose members vary by target framework into the surface
  these files exist to pin.
- **The rows come from `Jint.csproj`'s own `<TargetFrameworks>`.** Adding a target framework adds a failing row
  that wants a baseline, rather than silently shipping an unsnapshotted surface. The test itself runs on
  **exactly one** leg, selected by the `RunsPublicApiBaselines` property in the `.csproj` — which also
  defines the `PUBLIC_API_BASELINES` symbol the file is wrapped in and gates the build target, so the three
  cannot drift apart. Two reasons, and only the first is obvious. `net472` is excluded because
  `PublicApiGenerator` formats through CodeDom and nothing promises .NET Framework's lays the same metadata
  out identically, so that leg would be verifying the host rather than Jint. Every *other* leg is excluded
  because the snapshot is of five files on disk: which runtime reads them changes nothing, so a second leg
  is redundancy — but it is a second process reading `artifacts/bin/Jint/` while the other leg's build is
  still writing it, and a second `Jint.dll` beside a test binary that the outer Jint build never refreshes.
  A stale copy trips `TheSnapshottedAssembliesAreTheOnesThisTestRunWasBuiltFrom`; a torn read faults inside
  `MetadataLoadContext`, where xUnit cannot attribute it to a test, so the run dies part-way through with a
  `TestPipelineException` and still prints `Passed!` for what it managed. If a future change wants the
  baselines on a different runtime, move the property — do not add a second leg.

### The documentation gate

`PublicApiDocumentationTest.cs` holds every declaration of that same approved surface to carrying a
`<summary>`, and `UndocumentedPublicApi.txt` is the register of the ones that do not yet. **The house style a
new or rewritten doc comment is written to is [`docs/xml-doc-style.md`](../docs/xml-doc-style.md)** — read it
before writing one, not after a review says the summary is three sentences long.

Three things about it are worth knowing before a failure here sends you looking.

- **It does not parse the baseline.** The surface is enumerated out of the assembly's metadata as ISO
  documentation comment ids, because that is the key `Jint.xml` is already keyed by; deriving one from a
  C#-like baseline line would mean re-implementing overload and generic-arity rendering, which is the part
  that is hard to get right. The baseline is still load-bearing:
  `TheEnumeratedSurfaceIsTheApprovedBaseline` holds the two to the same declaration count, so an enumerator
  that quietly stopped seeing a whole category of member fails instead of reporting less debt.
- **The register may only shrink.** Documenting something that is in it fails the test until the file is
  regenerated, which is deliberate — an allowlist that silently absorbs its own progress stops being a count
  of what is left. `JINT_PUBLIC_API_DOCS=update` rewrites it; nothing else may edit it, and a line is never
  added by hand. A new public declaration ships documented.
- **It runs on one target framework and proves that is enough.** `NoTargetFrameworkIsDocumentedLessThanTheNewest`
  checks the other four are subsets *and* that nothing documented on the newest is undocumented on them, which
  is the failure a single-target gate would otherwise miss: a doc comment inside `#if NET8_0_OR_GREATER`, or on
  the only part of a `partial` type that is gated, is invisible to a `netstandard` consumer. It found two.

All four checks together cost about a second, so they are not gated on an environment variable the way
`JINT_WPT_CENSUS` is — every leg that runs this suite runs them.
