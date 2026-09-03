# The DOM binding generator

`Jint.Browser.BindingGenerator` reads the pinned **AngleSharp** and **AngleSharp.Css** assemblies through
`System.Reflection.MetadataLoadContext`, treats their `AngleSharp.Attributes` as the WebIDL they effectively
are, and emits the checked-in `Jint.Browser/Dom/Generated/*.g.cs`.

`pin.json` records the two versions the checked-in output was produced from. It and the `AngleSharp` /
`AngleSharp.Css` entries in `Directory.Packages.props` have to say the same thing, which
`Jint.Tests.Browser/DomBindingsPinTests.cs` checks by reading both — plus the versions the test process
actually loaded, so a pin cannot drift from the reference.

Nothing about AngleSharp is vendored here: the assemblies come from the package reference, and the generator
is pointed at whatever the build resolved.

## A bump is a code change

Re-pointing the pin is not a configuration edit that lands on its own. AngleSharp adds interfaces, renames
`[DomName]`s, changes a signature from `string` to `string?` and moves a member between interfaces, and every
one of those changes the generated surface. So a bump is:

1. Update both versions in `Directory.Packages.props` and in `pin.json`.
2. Regenerate (below) and **read the diff of `Jint.Browser/Dom/Generated/`**. That diff is the upstream
   change, stated in the vocabulary this repository compiles.
3. Read the report's *diagnostics* and *skipped members* sections. A new diagnostic is something nobody has
   looked at; a new skip is a member that stopped crossing the boundary.
4. Fix whatever the diff broke, in the same pull request.

The same discipline `tools/devtools-protocol` and `Jint.Tests.Test262/Test262Harness.settings.json`'s
`SuiteGitSha` carry, and for the same reason: a pin that moves without anybody reading what moved is an
upstream change landing unread.

## Regenerating

The short way, which is also what fails when the checked-in code is stale:

```bash
JINT_DOM_BINDINGS=update dotnet test -c Release Jint.Tests.Browser/Jint.Tests.Browser.csproj \
    --filter FullyQualifiedName~DomBindingsStalenessTests
```

The long way, which additionally prints the inventory and the skip report:

```bash
dotnet run --project tools/dom-bindings/Jint.Browser.BindingGenerator -c Release -- \
    --core  <path>/AngleSharp.dll \
    --css   <path>/AngleSharp.Css.dll \
    --overrides tools/dom-bindings/overrides.json \
    --output Jint.Browser/Dom/Generated \
    --report /tmp/dom-bindings-report.txt
```

`--core` and `--css` are the two assemblies the build resolved; the easiest way to find them is
`artifacts/bin/Jint.Browser/release_net8.0/AngleSharp.dll` after a build, or the NuGet cache at the pinned
version. The staleness test does not need them named at all: it asks the loaded `IElement` and
`ICssStyleDeclaration` types where they came from.

A Roslyn source generator was considered and rejected, for the reasons `tools/devtools-protocol/README.md`
gives: a binding surface is exactly the kind of thing whose diff a reviewer wants to read, and the build stays
analyzer-free.

## The two files beside the generator

- **`pin.json`** — the versions above.
- **`overrides.json`** — the curated half of the binding: the interfaces the runtime owns instead, the shapes
  that are hand-written, the members a later campaign item owns, the members routed through a host hook, the
  reflected content attributes, the `DOMString?` list, and the two enum decisions the heuristics cannot make.
  Every entry carries a reason, and an entry naming a member the pinned assemblies no longer have becomes a
  diagnostic. One list is the exception to that shape and says so in the file: `reflected` states what **HTML**
  says about an attribute rather than correcting what AngleSharp says, so its entries replace a projection
  rather than colliding with one, and the report names every one of them.
  [`Jint.Browser/AGENTS.md`](../../Jint.Browser/AGENTS.md) explains each list and why it exists.

A member the generator simply could not convert is **not** in `overrides.json`. It is skipped with the reason
the generator worked out, and that reason is in the report. The split is deliberate: the table is for
decisions, the report is for consequences.
