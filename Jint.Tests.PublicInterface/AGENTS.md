# Agent instructions: integrator-facing tests

> **Read this when:** You are writing a test that has to prove a third party can actually reach an API.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Where integrator-facing tests belong

`Jint.Tests.PublicInterface` is the only test project **without** `InternalsVisibleTo` (the grant list is `Jint.Tests`, `Jint.Tests.Test262`, `Jint.Benchmark`, `Jint.Repl`), so a test there actually proves the surface is reachable by a third party. Put new integrator-facing tests there, in **generically named files** describing the capability rather than any particular integrator — the `Host*Tests.cs` family (`HostObjectSemanticsTests`, `HostObjectProbeCountTests`, `HostObjectEnumerationTests`, `HostDelegateTests`, …) is the established precedent. Remember that a `protected internal` member is seen as `protected` from outside the assembly, so an override is spelled `protected override`.
