# Agent instructions: the source generators

> **Read this when:** You are changing either Roslyn generator, adding or changing an attribute one of them
> reads, or accepting a generator snapshot. Declaring a `[JsObject]` host and its members is an ordinary
> day's work under `Jint/Native/` and `Jint/WebApi/` and needs none of this — the attribute XML docs in
> `Attributes.cs` are the reference for that.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there. The `[JsAccessible]` generator's *behaviour* — which member shapes it declines, what the
> emitted accessor promises a host, the diagnostics it reports — belongs to CLR interop and is in
> [`Jint/Runtime/Interop/AGENTS.md`](../Jint/Runtime/Interop/AGENTS.md); what is here is what the two
> projects are and what breaks when they are treated as one.

## Two analyzer assemblies, and only one of them ships

- **`Jint.SourceGenerators`** holds `ObjectGenerator`, which turns a `[JsObject]` class into its
  `CreateProperties_Generated()` body. It is how the built-ins under `Jint/Native/` and the web APIs under
  `Jint/WebApi/` are declared — two hundred-odd types. `Jint.csproj` is the *only* project that references
  it as an analyzer, and it never leaves this repository.
- **`Jint.SourceGenerators.Interop`** holds `JsAccessibleGenerator`, and it is the one `Jint.csproj` packs
  into `analyzers/dotnet/cs` for consumers.

**The split is not organisational, and reversing it does not fail here.** `ObjectGenerator` calls
`RegisterPostInitializationOutput` unconditionally, and the source it emits declares members typed
`Jint.Native.Function.FastCallGuard`, which is `internal` to Jint. Any assembly outside Jint's
`InternalsVisibleTo` list that referenced that analyzer would therefore fail to compile *before reaching a
line of its own code*. So: never pack `Jint.SourceGenerators`, never give `JsAccessibleGenerator`
post-initialization output, and do not merge the two projects. `tools/package-consumer` is what actually
checks it — it consumes the produced `.nupkg` through a `PackageReference` and **runs** the result, because
a package carrying no generator still compiles every line of Jint's public API.

## One Roslyn version is a consumer-facing number and the other is not

`Jint.SourceGenerators.Interop` pins `Microsoft.CodeAnalysis.CSharp` with `VersionOverride="4.8.0"` —
*below* the repository-wide version, and deliberately. An analyzer is loaded by whatever compiler the
consumer's SDK carries, and a compiler refuses to load one built against a newer `Microsoft.CodeAnalysis`
than its own: it reports `CS9057`, skips the generator, and the host's call to the generated `RegisterAll()`
then fails to compile. 4.8.0 is the .NET 8 SDK's own Roslyn, which is the floor that makes the package
usable on every in-support SDK — and `net472`, where the run-time compiled interop lanes decline outright,
is both the target with the most to gain and the one most likely to be on that SDK. **Raising it is a
breaking change for consumers with no signature to show for it.** `Jint.SourceGenerators` is deliberately
not pinned: it never leaves this repository, so this repository's SDK floor is the only one it has to meet.

## The pipeline model must be value-equatable, and every odd type here is why

`ObjectDefinition` is a `record class` whose collection members are all `EquatableArray<T>` and whose
diagnostics are `DiagnosticInfo`, never `Diagnostic`. That is the whole design: an incremental generator
caches on the equality of its model, so **nothing that is not a value may enter it** — no `ISymbol`, no
`Compilation`, no `Location`, no `ImmutableArray<T>` left bare (its equality is reference equality, so an
identically-shaped model would compare unequal and re-emit). Get this wrong and nothing fails; every
`[JsObject]` host is simply re-emitted on every keystroke in an editor, and the only symptom is that
somebody's IDE became slow.

`EquatableArray.cs` is `<Compile Include>`-shared into `Jint.SourceGenerators.Interop` rather than copied,
because the incremental pipelines of both generators depend on it behaving identically. **`DiagnosticInfo`
is deliberately *not* shared**, and that asymmetry is load bearing rather than an oversight: an
`.editorconfig` severity is resolved per syntax tree, and `DiagnosticInfo` rebuilds its location from a file
path, so a tree-less diagnostic silently keeps its default severity. The whole argument is in
[`[JsAccessible]`: the generated
lane](../Jint/Runtime/Interop/AGENTS.md#jsaccessible-the-generated-lane-and-why-it-is-equivalent-rather-than-merely-fast).

## The attributes are compile-time only, and their docs are the specification

`Attributes.cs` is one `const string` of C# that `ObjectGenerator` emits through
`RegisterPostInitializationOutput`. Every attribute in it carries
`[Conditional("JINT_SOURCE_GENERATORS")]`, and that symbol is defined nowhere in the repository: the
attributes are erased at the call site, never reach metadata, and cost a built engine nothing at run time.
The corollary is that **nothing can reflect over them** — a decision that has to be visible at run time
needs a real member, not a new attribute property.

Their XML docs are where an author of a host looks, so an argument about what a property means belongs
beside the property in `Attributes.cs`, not in this file. Two of them already carry a normative citation
that the rest of the repository links back to: `JsFunctionAttribute.Flags` (ECMA-262's attributes for a
built-in function property, which WebIDL inverts) and `JsObjectAttribute.PreserveDeclarationOrder`. The
emission order those interact with is argued beside the sort in `Models.cs`, and
[`Jint/WebApi/AGENTS.md`](../Jint/WebApi/AGENTS.md#web-apis) is what cites it.

## Diagnostics are `JINT0nn`, and a retired id stays retired

`Diagnostics.cs` numbers them and keeps the gaps: `JINT022` is a retired id with a comment saying what it
used to mean, and reusing a number would silently change what somebody's `.editorconfig` severity applies
to. Whether a newly declined member shape earns a diagnostic at all is decided by one question — *would
reflection have served this member?* — and that rule, with its worked examples, is in
[`Jint/Runtime/Interop/AGENTS.md`](../Jint/Runtime/Interop/AGENTS.md).

## Snapshots are the only review the emitted code gets

`Jint.Tests.SourceGenerators` runs each generator over a small source through `CSharpGeneratorDriver` in
memory and snapshots the driver's whole result with Verify, under `Snapshots/`. Nothing else in this
repository reads the emitted C#, so **accepting a snapshot is the review**: read the diff as code, not as a
checksum.

`.gitattributes` pins `*.verified.cs` and `*.verified.txt` to `eol=lf` on every platform. Verify 31.21 and
newer reject carriage returns in a verified file, so a Windows checkout that let `autocrlf` rewrite them
would fail every snapshot in the project for a reason that has nothing to do with the generator. Do not
"fix" that by re-saving a snapshot with CRLF.

## The project shape, briefly

Both generators target `netstandard2.0` with `IsRoslynComponent` and `EnforceExtendedAnalyzerRules`,
because an analyzer is loaded into the compiler's own context and gets none of the repository's normal
package set. Both also `<Using Remove="Acornima" />` and its siblings: the global usings in
`Directory.Build.props` name runtime types a generator has no reference to.
