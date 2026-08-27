# Agent instructions: Native AOT and trimming

> **Read this when:** You are adding a reflection lane anywhere under `Jint/`, writing or removing a
> trimming or AOT annotation, changing `Jint.csproj`'s IL suppressions, or editing `Jint.AotExample` —
> the example a CI leg publishes with `PublishAot=true` and *runs*, which is the only evidence any of
> it holds.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there. Every generic-instantiation site named below is in CLR interop, whose own rules are
> in [`Jint/Runtime/Interop/AGENTS.md`](../Jint/Runtime/Interop/AGENTS.md).

### Native AOT: what is measured, what is annotated, and what is still suppressed

`Jint.csproj` sets `<IsAotCompatible>` for net7.0+ and, in the same property group, `NoWarn`s nine IL
codes. **The property is not evidence, and the `NoWarn` is why**: it is a property of Jint's own
*compilation* and reaches nothing downstream, since ILC re-derives the whole set over the closed program
— so **an embedder publishing Native AOT sees every diagnostic it hides**, attributed to Jint's files.
What makes the claim checkable instead is the CI leg that publishes `Jint.AotExample` with
`PublishAot=true` and *runs the native binary* (`aot` in `.github/workflows/pr.yml` and `build.yml`).

**A source `#pragma warning disable IL....` is Roslyn-only for the same reason, and `Jint` now has
none** — the sixty-one that sat at the tops of the interop files bought Jint a green build and an
embedder nothing. `[UnconditionalSuppressMessage]` is what ILC honours; it asserts safety rather than
requesting silence, so every one in the assembly names the invariant it stands on — the degradation a
probe pins, the feature guard ILC folds, or the public entry point whose `[RequiresUnreferencedCode]`
already states the requirement. **Do not write one whose justification is not literally true, and do not
add a code back to `NoWarn` to quiet a new site.**

[#3305](https://github.com/sebastienros/jint/issues/3305) took the published-and-run inventory from 113
to 67 and carries what is left, grouped by what would close it. Five moves did most of it, and each is
worth trying before a suppression is:

* **Spell a constant type as `typeof(X)` at the reflection call.** That token is folded, so the lookup
  *preserves* the member; the same lookup through a `static readonly Type` field loses the type's
  identity on the way and merely reports. Six `IL2080` in `DefaultTypeConverter`'s static constructor
  were exactly this.
* **Do not reflect inside a cache lambda.** A lambda parameter cannot carry
  `[DynamicallyAccessedMembers]`, so `GetOrAdd(type, static t => …reflect over t…)` drops at the closure
  boundary what the caller had already promised. Hoist the resolution into the annotated method and
  store through `GetOrAdd`'s *value* overload, which keeps the one-instance-per-key guarantee the
  factory overload gave.
* **Restate a feature guard in the method that makes the call.** Both analyzers reason one method at a
  time, so a lane gated on `RuntimeFeature.IsDynamicCodeSupported` two frames up still reports
  `IL3050`; an early return on the same property beside the `Expression.TryGetFuncType` call is a branch
  they fold instead.
* **Declare what the method reads, and no more.** `InteropHelper.IsAssignableToGenericType` demanded
  every public member of *both* parameters while reading the interfaces of one and merely comparing the
  other; no caller could satisfy the excess, so it propagated outwards as a diagnostic at every one of
  them.
* **Spell a `BindingFlags` argument as a constant when only constants reach it.** A `Type.Get*` call
  whose flags the analyzer cannot fold is assumed to ask for non-public members, so it demands
  `NonPublic*` of every annotated caller — a requirement that then propagates to the public entry
  points for a lookup nothing can reach. `TryFindMemberAccessor`'s nested-type lookup wrote
  `bindingFlags ?? …` where the one caller that passes flags asks `GetNestedType` exactly what the
  default asks. Its three siblings above it genuinely take host-configured flags
  (`Options.Interop.ObjectWrapperReported*BindingFlags`), so they cannot be spelled this way and must
  not be closed by widening the annotation instead: that would push every host type's *non-public*
  members into what every AOT consumer preserves, for a lookup only a host that asked for
  `BindingFlags.NonPublic` can reach.

**One diagnostic group is deliberately left standing, and it is the one that looks most closable.** The
two `IL2072` on `TypeReference.ReferenceType` ask for `PublicNestedTypes`, which
`TypeResolver.TryFindMemberAccessor` genuinely reads and which
`InteropHelper.DefaultDynamicallyAccessedMemberTypes` genuinely could carry. Measured, twice, on a
published native binary: carrying it preserves **nothing** — ILC already keeps the nested types of a
type whose metadata it emits, checked for six candidates with the annotation present and absent — while
it marks each nested type `All`, so every nested **enum** of every exposed type raises an `IL3050` in
the *embedder's own file* through the inherited `[RequiresDynamicCode] Enum.GetValues(Type)`. One
`engine.SetValue("Env", typeof(Environment))` cost two. That is the `Delegate` trap of
`docs/v5-migration.md` §6.4 a second time, and trading two diagnostics in Jint's files for an unbounded
number in every host's was the wrong way round. **Do not close them by widening the constant.**

**What is left is close to a floor, and the shape of it is one sentence:** 58 of the 67 are dataflow
whose value is a `Type` produced at run time by something that cannot be annotated — `object.GetType()`
(14, plus 8 more through `ObjectWrapper.ClrType` and `.Target`), an element of `Type.GetInterfaces()`
(6, and `[DynamicallyAccessedMembers]` is not honoured on a `Type[]`), `ParameterInfo.ParameterType`
(3, plus 3 through Jint's own `ParameterMetadata`), `Type.GetElementType()` (2). The remaining nine are
the §6.2 gaps. **Annotating a Jint-owned property or field that merely carries one of those values is
not a fix**: it collapses N reports into one and then has the analyzer *trust* a promise nothing keeps,
downstream and silently. That is why `ObjectWrapper.ClrType` has no annotation and must not get one.

#### What the leg proves

An engine with `AllowClr()` reading properties, fields, indexers and methods off a host object works
natively, as do `T[]`, `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, `Dictionary<,>`, a CLR array as a
live view under `ArrayConversionMode.LiveView` (its element writes and its resize refusals included),
delegates in both directions, extension methods, `TypeReference` construction, JSON, promises, and an
`Interop.Enabled = false` engine. What it proves does *not* work is one shape — **a generic
instantiation over a value type built at run time**. Reference-type arguments share canonical code and
are fine; `int`, `double` and friends need a specific instantiation ILC never saw.

**Eight sites have that shape, not the five
[#3299](https://github.com/sebastienros/jint/issues/3299) was filed with** — `MakeGenericType` and
`MakeGenericMethod` are the whole inventory, and `grep` for those two names is how to re-derive it after
this area moves again. Four degrade, four throw, and the split is the same question every time: *is
there a non-generic value that satisfies what was asked for?*

| site | shape | today |
| --- | --- | --- |
| `ObjectWrapper.ResolveArrayLikeWrapperFactoryType`, `T[]` branch | `ArrayWrapperFactory<int>` | degrades to `ListWrapper` |
| same site, `IList<>` branch | `GenericListWrapperFactory<int>` | degrades to `ListWrapper` |
| same site, `IReadOnlyList<>` branch | `ReadOnlyListWrapperFactory<double>` | degrades to a plain `ObjectWrapper` (loses array-likeness, keeps the indexer) |
| `ObjectWrapper.ResolveEnumerableSnapshotFactory` | `EnumerableSnapshotFactory<int>` | degrades to `ObjectEnumerableSnapshotFactory` |
| `DefaultTypeConverter.GetFromResultMethod` | `Task.FromResult<double>` | throws `NotSupportedException` |
| `MethodInfoFunction.ResolveMethod` | `methodInfo.MakeGenericMethod(double)` | throws `NotSupportedException` |
| `DefaultTypeConverter.TryConvertInternal`, ×2 | `List<long>` for an `IEnumerable<long>` parameter, and the inner list a `Collection<short>` takes | throws `NotSupportedException` |
| `NamespaceReference.MakeGenericType` → `TypeReference` | a closed generic type the *script* named | throws `NotSupportedException`, on construction |

The four that degrade do so through `ObjectWrapper.IsMissingGenericInstantiation`, which catches
`NotSupportedException` as well as the `MissingMethodException` the original `catch` named. **The
original never fired**: Native AOT raises `NotSupportedException` from `Activator.CreateInstance`, so
the fallback beside it was dead code exactly where it was needed. Do not add another such fallback
without checking which exception the runtime actually throws.

**A fallback only counts if it answers the way the wrapper it replaced does, and the `T[]` row is what
proves that is a separate question from whether it throws.** `ArrayWrapper<T>` carries two facts in its
type argument that `ListWrapper` used to take from nobody — the element type, and that the length is
fixed — so the degradation read correctly while writing a boxed `double` into an `int[]`
(`InvalidCastException`) and serving `push` through `IList.Add`, which raises the CLR's own *Collection
was of a fixed size* past script where the typed wrapper raises a `TypeError`. `ListWrapper` now takes
both from the target (`Array.GetType().GetElementType()`, `IList.IsFixedSize`), which also closes the
same hole for a genuinely fixed-size `IList` under a JIT. **Probe the degraded path's writes and
resizes, not only its reads.**

**The four that throw are not oversights, and must not be "fixed" by widening a `catch`.** None has a
non-generic answer to degrade *to*: nothing produces a `Task<double>` without `Task.FromResult<double>`;
`ResolveMethod` returning `null` means *this candidate does not apply*, so swallowing the failure would
report "no matching overload" for a method that plainly exists (which is why its `catch (ArgumentException)`
carries a comment saying it is deliberately not `NotSupportedException`); and nothing but a `T`-typed
value satisfies an `IEnumerable<T>` parameter or a `Box<short>` the script asked for. Nor may Jint root
a guessed set of value types: every closed generic rooted costs binary size for every AOT consumer,
including the ones that never touch that path, and no signature in Jint predicts which value types a
host's members and a script's arguments will produce. **The embedder can close all four and Jint cannot**,
which is what `docs/v5-migration.md` §6.2 now tells them — verified on a published binary, including the
asymmetry that makes the recipe worth writing down: a compiled reference in the host's own
`TrimmerRootAssembly` closes three of them, and does *not* close `Task.FromResult<double>`, because that
one lives in an assembly the host has not rooted and Jint reaches it reflectively. Rooting it takes the
same expression Jint evaluates —
`typeof(Task).GetMethod(nameof(Task.FromResult)).MakeGenericMethod(typeof(double))` — which ILC folds
because both tokens are constants.

#### The rule for new work here

**A new reflection lane needs an annotation and a probe.** Concretely, when you add one:

1. Decide what it *requires*, and say so on the public entry point that reaches it.
   `[RequiresUnreferencedCode]` when behaviour depends on metadata a trimmer may remove;
   `[RequiresDynamicCode]` only when the thing genuinely cannot work natively — the measurement above
   is the standard of proof, not the ILC warning count. Over-annotating is worse than not annotating:
   it teaches an embedder to suppress the diagnostic on APIs that are fine.
2. Prefer `[DynamicallyAccessedMembers]` wherever a `Type` or a type parameter is in the signature. It
   *preserves* what you reflect over instead of merely warning, and it is why `SetValue<T>`,
   `SetValue(string, Type)`, `TypeReference.CreateTypeReference` and `ModuleBuilder.ExportType` carry
   no `[RequiresUnreferencedCode]` and never should. Note that the attribute is honoured on a `Type` or
   a `string` only — never on a `Type[]`, which is why `Options.AddExtensionMethods` had to take the
   `[RequiresUnreferencedCode]` instead.
3. Annotate the *element*, not the container. `SetValue<T>(string, T[])` exists because inferring
   `T = Company[]` preserved `System.Array`'s members and left `companies[0].name` — the one thing
   script reaches — to be trimmed away.
4. Add a `Probe` to `Jint.AotExample/Program.cs`, with a sibling using the other kind of type argument
   where the lane is generic. A `KnownAotGap` is for a shape that must *keep* throwing; it is checked
   in both directions, so a gap that closes fails the leg. A `KnownTrimmedAway` is for a shape that must
   keep answering **wrongly** — see the next section — and is checked in both directions for the same
   reason.
5. If what you added is an *annotation* rather than a lane, its subject goes in
   `Jint.AotExample.UnrootedHost`, not beside `Program.cs`. See below.

The eight annotated members today are `OptionsExtensions.AllowClr` (two overloads),
`OptionsExtensions.AddExtensionMethods`, `Engine.SetValue(string, object?)`,
`ShadowRealm.SetValue(string, object?)`, `ObjectWrapper.Create`, and `ClrHelper.Unwrap` / `Wrap`. The two
`SetValue` families are one API pointed at two global objects and share one implementation
(`Jint/GlobalValueRegistration.cs`) and one message constant, precisely so a fifth rule is not needed:
they had drifted, and the drift put the annotation on `ShadowRealm`'s harmless `Delegate` overload while
leaving its reflective one silent. **Annotate the pair, not the member** — and if you are about to write
the same annotation twice, share the implementation instead. `JsValue.FromObject` is deliberately not among them: it is the engine's own
conversion funnel with ~60 call sites inside Jint, the interpreter's operator-overloading path among
them, so annotating it would either cascade `[RequiresUnreferencedCode]` across the interpreter or need
a suppression per site. `Options.InteropOptions._defaultWrapObjectHandler` carries one of the
`[UnconditionalSuppressMessage]`s, because it has to stay assignable to the public `WrapObjectDelegate`.

#### Two costs that land in the embedder's project

Both come from `Engine.SetValue<T>`'s `[DynamicallyAccessedMembers]`, and from a one-line registration
in the *host's* code. The array half is fixed; the delegate half is not, and the asymmetry is the point.

* `SetValue("a", new[] { 1, 2, 3 })` cost four `IL3050` through `Array.CreateInstance`, because
  preserving all public methods of an array type reaches it. `SetValue<T>(string, T[])` now wins
  overload resolution for any array (`T[]` is more specific than `T`) and infers `T = int`. Gone.
* `SetValue("f", new Func<int, int>(…))` binds to `SetValue<T>` rather than to
  `SetValue(string, Delegate)`, and the annotation then demands every public method of
  `Func<int, int>` — including the inherited, `[RequiresUnreferencedCode]` `Delegate.CreateDelegate`
  overloads: three `IL2026` and three `IL2111` **as errors** in the host's build, cleared by casting to
  `Delegate`. **The same trick does not work here**: an overload constrained `where T : Delegate` has
  the same signature as `SetValue<T>` after substitution and would make every delegate call site
  ambiguous. Widening the annotation is not the fix either; knowing that the annotation is what an
  embedder pays is the point.

#### The unrooted half, and what an annotation is worth

**A rooted subject cannot measure an annotation.** `Jint.AotExample` roots `Jint` and itself, so every host
type declared beside `Program.cs` — `Company`, `ReadOnlyStrings`, `GenericHost`, `Box<T>` — survives
whatever `SetValue<T>`'s `[DynamicallyAccessedMembers]` says, and a probe over one cannot tell the
annotation from the root. `Jint.AotExample.UnrootedHost` is the second project that closes that
([#3479](https://github.com/sebastienros/jint/issues/3479)): referenced, deliberately **not** in
`TrimmerRootAssembly`, and the home of every host type whose only protection is the attribute on the entry
point it is registered through. **Nothing in C# may read a member of a type in there** — one call from
`Program.cs`, one `ToString` override that touches a property, and the probe silently stops measuring.

**The standard of proof is that removing the annotation makes a probe fail.** Deleting
`[DynamicallyAccessedMembers]` from `Engine.SetValue<T>(string, T?)` and
`GlobalValueRegistration.RegisterTyped<T>` — nothing else — turns four probes red on a published native
binary: the two unrooted ones, *and* `Dictionary<string, object>` / `Dictionary<string, int>`, which were
already annotation-sensitive because their subject is a framework type this project does not root either.
Those two discriminated by accident; nothing said they were the annotation's evidence, and rooting one
more assembly would have retired them without a word. **A probe that passes either way must be deleted
rather than kept — it reads as evidence and is not.**

**`DynamicallyAccessedMemberTypes.Interfaces` is not what any probe here pins, and it is still worth
carrying.** Two measurements, and neither is the one the entry looks like it should have:

* Removing it from `InteropHelper.DefaultDynamicallyAccessedMemberTypes` leaves the unrooted
  `IReadOnlyList<string>` probe **entirely green** — ILC keeps the interface implementations of a type whose
  MethodTable it emits whenever the interface is used anywhere in the closed program, and Jint itself uses
  that one. What that probe actually pins is `Count`, riding on `PublicProperties`. So no probe can be
  written that discriminates the entry at run time, which is why none claims to.
* What removing it *does* change is the **analysis**: the published inventory goes 67 → **75** (`IL2067`
  8 → 11, `IL2070` 4 → 9), every one of them in Jint's files and in the embedder's build. That is the
  opposite of `PublicNestedTypes`, which bought nothing and cost the embedder diagnostics; this one buys
  nothing at run time and *saves* eight. **Do not delete it** on the strength of the first bullet.

**And it does not reach a member behind an *explicit* implementation at all.** `Interfaces` asks for the
implemented interfaces, not for their members, so `TypeResolver`'s `iface.GetProperties()` walk finds
nothing and the member reads `undefined` with the annotation present and correct — pinned as a
`KnownTrimmedAway`. Adding `typeof(TheInterface).GetProperties()` anywhere in the program makes it
resolve, which is what identifies the missing thing as member metadata. **Do not close it by widening the
constant**; the embedder closes it by rooting their own assembly, which is what `docs/v5-migration.md`
§6.4 already tells them.

#### Reading the example

`Jint.AotExample/Program.cs` pins all of this in both directions, and its csproj records why it
suppresses `IL2026` and `IL3000` rather than silencing them at a call site. Treat the run as an upper
bound rather than a lower one: the example roots the whole `Jint` assembly, which a real embedder would
not do. It roots its own assembly too, and that one is not cosmetic — without it the trimmer removes an
extension method nothing calls from C#, and `company.shout()` fails as *not a function* with no AOT
diagnostic anywhere. Reflection-reached host types need rooting; the failure mode is a wrong answer,
not an error. The embedder-facing form of all of it is section 6 of
[`docs/v5-migration.md`](../docs/v5-migration.md).
