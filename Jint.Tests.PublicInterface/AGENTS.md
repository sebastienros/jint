# Agent instructions: integrator-facing tests

> **Read this when:** You are writing a test that has to prove a third party can actually reach an API,
> **or a build here has failed on `PublicApiTest`.**
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
  surfaces — `net462` = `netstandard2.0` = `netstandard2.1`, and `net8.0` = `net10.0` — and keeping them as
  five separate files is what makes a future divergence inside either group show up as a diff instead of as
  nothing.
- **They are generated from `artifacts/bin/Jint/`, not from the loaded assembly.** A test project can only
  ever *load* two of the five, so `netstandard2.0`, `netstandard2.1` and `net8.0` are read out of the build
  output through a `MetadataLoadContext` — which composes with `PublicApiGenerator` because it reads the file
  with Mono.Cecil from `Assembly.Location` and never reflects over it. The `BuildJintForEveryShippedTargetFramework`
  target in the `.csproj` builds the outer, cross-targeting Jint project so all five are present and current
  whenever this suite builds; if one is missing or the tree is left over from a different build, the test says
  so and names `dotnet build -c Release Jint/Jint.csproj` rather than passing on stale output.
- **The rows come from `Jint.csproj`'s own `<TargetFrameworks>`.** Adding a target framework adds a failing row
  that wants a baseline, rather than silently shipping an unsnapshotted surface. The test itself runs on the
  `net10.0` leg only — `PublicApiGenerator` formats through CodeDom and nothing promises .NET Framework's lays
  the same metadata out identically, so the `net472` leg would be verifying the host rather than Jint.
