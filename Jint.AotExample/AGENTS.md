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

`Jint.csproj` sets `<IsAotCompatible>` for net7.0+ and, in the same property group, suppresses eight IL
codes — `IL3050` (*requires dynamic code*) plus `IL2060`, `IL2067`, `IL2069`, `IL2070`, `IL2072`,
`IL2075`, `IL2080`. **The property is not evidence, and the suppressions are why.** What makes the
claim checkable is that a CI leg publishes `Jint.AotExample` with `PublishAot=true` and *runs the
native binary* (`aot` in `.github/workflows/pr.yml` and `build.yml`), because the run is the only thing
that distinguishes a warning that matters from one that does not.

Two facts about the suppressions are worth knowing before trusting either of them. Jint's `NoWarn` is a
property of Jint's own *compilation* and reaches nothing downstream: ILC re-derives the whole set when
it compiles the closed program, so **an embedder publishing Native AOT sees all of them**, attributed
to Jint's files, in their build. And a source `#pragma warning disable IL3050` — `ObjectWrapper` has
several — is Roslyn-only for the same reason; `[UnconditionalSuppressMessage]` is the one ILC honours,
and is what to reach for when a suppression is genuinely justified. Paying the remaining 115 down is
[#3305](https://github.com/sebastienros/jint/issues/3305), which carries the inventory by code and by
file, why it is not one change, and a suggested order; `Jint.csproj`'s own comment says the same.

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
   in both directions, so a gap that closes fails the leg.

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
a suppression per site. `Options.InteropOptions._defaultWrapObjectHandler` carries the one
`[UnconditionalSuppressMessage]`, because it has to stay assignable to the public `WrapObjectDelegate`.

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

#### Reading the example

`Jint.AotExample/Program.cs` pins all of this in both directions, and its csproj records why it
suppresses `IL2026` and `IL3000` rather than silencing them at a call site. Treat the run as an upper
bound rather than a lower one: the example roots the whole `Jint` assembly, which a real embedder would
not do. It roots its own assembly too, and that one is not cosmetic — without it the trimmer removes an
extension method nothing calls from C#, and `company.shout()` fails as *not a function* with no AOT
diagnostic anywhere. Reflection-reached host types need rooting; the failure mode is a wrong answer,
not an error. The embedder-facing form of all of it is section 6 of
[`docs/v5-migration.md`](../docs/v5-migration.md).
