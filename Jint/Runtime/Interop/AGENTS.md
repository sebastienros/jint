# Agent instructions: CLR interop

> **Read this when:** You are touching `ObjectWrapper`, `TypeReference`, a type or object converter, a reference resolver, or the compiled member-read and method-invoker lanes.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### What a host registration costs, and what buys it back

Every row is a one-line registration in the embedder's code that turns a compiled lane off engine-wide, and a
declaration that narrows it back to what the host actually touches. **A declaration is a promise the engine
takes at face value; nothing links it to the `switch` inside the converter**, so a case added to one and not
the other is skipped on exactly what the declaration excluded and honoured everywhere else, invisibly. Host-
contract verification is what reports that drift, and each check deliberately reuses the very filter the lane
consults so the promise and the check cannot diverge.

| Registration | What it disarms, undeclared | Declaring narrows it to | What still costs |
| --- | --- | --- | --- |
| `AddObjectConverter(c)` | compiled member-**read** lane and compiled method-**invoker** lane, every member and method | `AddObjectConverter(c, types)` or `ObjectConverter.HandledTypes` — members/return types no declared type can produce | a member or return type of `object`, which can hold anything; **one** undeclared converter unrestricts the whole set |
| `SetTypeConverter(f)` | compiled member-**write** lane, compiled method-**invoker** lane, and every non-`int`-keyed indexer accessor's place in the shared accessor cache | `SetTypeConverter(f, targetTypes)` or `ClrTypeConverter.HandledTargetTypes` — members/parameters/index types none of them is a supertype of | conversions to a declared type, and every target type when nothing was declared |
| a non-default `IReferenceResolver` | the non-computed member-read caches, the dense-array indexed-read lane, the member-call callee lane | `ReferenceResolverInterests`, at registration or on `Options` | identifier caches were never affected either way |
| `Options.Interop.TypeResolver` = a private resolver | nothing directly — but the accessor cache is per resolver, so a private one starts cold | — | see the `AddImmutableCrossing` row below for the read that stays uncached |

The two converter rows are asked **different questions**, and the difference is the signature. An
`ObjectConverter` is handed a *value*, so `ObjectConverterTypeFilter` has to guess which runtime types a
member's declared type could hold: it claims in both directions, treats `object` as claiming everything, and
needs sealed/interface reasoning to rule anything out. A `ClrTypeConverter` is handed the *target* `Type`, and
every call site passes a statically known one — the member type, the parameter type, the indexer's index type
— so `TypeConverterTargetFilter` is one `IsAssignableFrom` in one direction, and its verifier can be exact: it
runs the stock conversion beside the host's for every undeclared target and reports a disagreement.

Two further notes on the type-converter row. `_typeConverterTargetFilter` is `null` **only** for
`DefaultTypeConverter` itself, an exact type test that is now the only one left — a subclass of it exists to
change some conversion, and which ones is what the declaration is for. And the converter is deliberately not
part of `InteropResolutionProfile`: the only resolution artefact it steers is an `IndexerAccessor`'s baked-in
key, so `TypeResolver.IsConverterNeutral` excludes exactly those on the way in *and* on the way out, rather
than giving every engine with a converter a cache partition of its own.

### Gotchas

Each of these cost a real integrator or a real bug. These are the ones that bite in this area; the
rest of the list is split across the files indexed from the repository-root [`AGENTS.md`](../../../AGENTS.md).

- **Dictionary-valued reads re-wrap on every access, and only a host promise can stop them.** `record.value.customer.country` over a wrapped `JsonObject`/`IDictionary` resolves each level through the dictionary lane, which probes the target and converts the value afresh every time — the read itself is compiled, but its *result* was never cached, and the bounded recent-wrapper ring (8 slots) cannot hold a nested walk's nodes. The engine cannot fix this alone: a value-identity change inside the host's dictionary is invisible to it. `Options.AddImmutableCrossing(params Type[])` supplies the missing fact — instances of the declared types, while exposed to this engine, are immutable — and in exchange an `ObjectWrapper` over such a target memoizes each key's resolved descriptor, so a repeated read touches neither reflection nor the indexer and each child node is wrapped once per wrapper lifetime. Assignability is the rule (an interface covers its implementations), and unlike `ObjectConverterTypeFilter` this one never guesses in the permissive direction — a wrong claim here would serve stale reads rather than merely cost a fast lane, so an open generic declaration claims nothing. Three things to know before relying on it. The memo lives in a **store of its own**, not in `_properties`: enumeration stays live from the target (a key added CLR-side afterwards still enumerates), no `_propertiesVersion` is bumped, and anything a script actually defines — `Object.freeze`, `Object.defineProperty`, a host `SetOwnProperty` — outranks it and drops it. A **write** through the wrapper evicts that key's memo, which is insurance for the host that was wrong and not a licence to be; write behaviour itself is unchanged. And the memo is **per wrapper**, so it only pays while the wrapper is alive — a stable root (anything reached through `Engine.SetValue`) memoizes its whole subtree, but a node reached through a transient wrapper, such as an element of a wrapped list, still re-resolves on each crossing; that lane is the ring's job, or `TrackObjectWrapperIdentity`'s. The same promise also reaches reflected members, where `ReflectionDescriptor` stops invoking the CLR getter entirely after the first read, superseding both `CacheRecentObjectWrappers` and `TrackObjectWrapperIdentity` for a declared type. No promise is needed for questions *about* the keys rather than their values: `ObjectWrapper.ProbeOwnProperty` answers a string-keyed generic dictionary from the target's own `ContainsKey`, so `in`, `hasOwnProperty`, `propertyIsEnumerable`, `Object.keys`/`values`/`entries`, `for..in`, `Object.assign`, spread and `JSON.stringify` stop converting a value and building a descriptor per key only to discard both. That lane never decides a key is *missing* — a `false` falls through to the descriptor path, because a dictionary wrapper still resolves CLR members for names the dictionary does not carry — so the only way it can disagree with `GetOwnProperty` is a target whose own `ContainsKey` and `TryGetValue` disagree with each other.
- **Deriving from `DefaultTypeConverter` used to disarm three lanes, and nothing said so.** The gate was `converter.GetType() == typeof(DefaultTypeConverter)` — an exact type test — so the elegant and obvious thing to write, a subclass that adjusts one conversion and inherits the rest, was indistinguishable from a converter that replaces everything. It is now the declaration that decides, so a subclass declaring what it changes keeps every lane for everything else. Two things follow. The **compiled method-invoker lane** is gated on both converter kinds at once: the object-converter filter answers for the return value and the type-converter filter for the parameter types, and either can decline it. And a `ClrTypeConverter` installed *after* construction (through `Engine.TypeConverter`) is honoured, which a value captured once into `InteropResolutionProfile` was not.

### `[JsAccessible]`: the generated lane, and why it is equivalent rather than merely fast

A `[JsAccessible]`-annotated type registers typed member lanes with `JsAccessibleRegistry`, and
`TypeResolver.ResolvePropertyDescriptorFactory` consults it in place of — and only in place of — the
declared property/field/method lookup. **The design rule is that the generated lanes are the four
delegates `CompiledMemberAccessor` builds at run time, emitted at compile time instead**: same shapes,
same decline rules, same bounds, and the surrounding `ReflectionAccessor` / `ReflectionDescriptor`
machinery untouched. That is what makes a generated member behave identically to the reflected one
rather than approximately like it, and it is why `GeneratedMemberAccessor` reads as a copy of
`CompilableMemberAccessor`. **Change one and change the other**; `Jint.Tests.PublicInterface/HostGeneratedInteropTests.cs`
runs every case against both an annotated type and an un-annotated twin and compares the results, so a
drift shows up as a failure naming the script that diverged.

Four consequences worth knowing before touching any of it.

- **Anything the two paths cannot agree on is not generated at all.** A method parameter not typed
  `JsValue` goes through a conversion chain steered by `Options.Interop.ValueCoercion` and the installed
  `ClrTypeConverter`; a property with a non-public or `init` accessor is writable by reflection and not by
  emitted C#. Both stay reflected. The MVP this replaced generated them, and acquired two behavioural
  divergences doing it — `p.Name = null` storing the string `"null"`, and a hard cast to a `JsValue`
  subtype throwing `InvalidCastException` out of `Evaluate` where the reflected path raises a catchable
  `TypeError`. **Widening the supported set means proving the new shape's reflected binding first.**
- **The generated lane is skipped for every annotated type when the host has installed a `MemberFilter`,
  a `MemberNameCreator`, a `MemberNameComparer`, or binding flags that no longer report public instance
  members** (`TypeResolver.GeneratedMembersAreConsultable`). Those four decide which members exist and
  under what names, and the generated lanes do not run through them. Not being able to honour a filter
  and honouring it anyway are the same bug; declining is the difference.
- **The registry is process-wide and bumps a generation counter, which every `TypeResolver` compares
  against before answering from its cache.** Without that, a `RegisterAll()` landing after an engine had
  already resolved a member of the same type would go on being answered with the reflected accessor and
  nothing would say so.
- **The generator lives in its own analyzer assembly, `Jint.SourceGenerators.Interop`, and that is not
  organisational.** `Jint.SourceGenerators` emits its attribute set through
  `RegisterPostInitializationOutput` unconditionally, and that source declares members typed
  `Jint.Native.Function.FastCallGuard`, which is `internal`. Any assembly outside Jint's
  `InternalsVisibleTo` list that referenced *that* analyzer would fail to compile before reaching a line
  of its own code — which is exactly why nobody had noticed that the MVP's emitted code could not compile
  in a consumer assembly either. Do not move the `[JsAccessible]` generator into that project, and do not
  give it post-initialization output.

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
natively, as do `T[]`, `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, `Dictionary<,>`, delegates in
both directions, extension methods, `TypeReference` construction, JSON, promises, and an
`Interop.Enabled = false` engine. What it proves does *not* work is one shape — **a generic
instantiation over a value type built at run time**. Reference-type arguments share canonical code and
are fine; `int`, `double` and friends need a specific instantiation ILC never saw.

Five sites had that shape ([#3299](https://github.com/sebastienros/jint/issues/3299)). Three now
degrade; two still throw, and that is the interesting half:

| site | shape | today |
| --- | --- | --- |
| `ObjectWrapper.TryBuildArrayLikeWrapper` | `GenericListWrapperFactory<int>` | degrades to `ListWrapper` |
| same site, `IReadOnlyList<>` branch | `ReadOnlyListWrapperFactory<double>` | degrades to a plain `ObjectWrapper` (loses array-likeness, keeps the indexer) |
| `ObjectWrapper.ResolveEnumerableSnapshotFactory` | `EnumerableSnapshotFactory<int>` | degrades to `ObjectEnumerableSnapshotFactory` |
| `DefaultTypeConverter.GetFromResultMethod` | `Task.FromResult<double>` | throws `NotSupportedException` |
| `MethodInfoFunction.ResolveMethod` | `methodInfo.MakeGenericMethod(double)` | throws `NotSupportedException` |

The three that degrade do so through `ObjectWrapper.IsMissingGenericInstantiation`, which catches
`NotSupportedException` as well as the `MissingMethodException` the original `catch` named. **The
original never fired**: Native AOT raises `NotSupportedException` from `Activator.CreateInstance`, so
the fallback beside it was dead code exactly where it was needed. Do not add another such fallback
without checking which exception the runtime actually throws.

**The last two are not oversights, and must not be "fixed" by widening a `catch`.** Neither has a
non-generic answer to degrade *to*: nothing produces a `Task<double>` without `Task.FromResult<double>`,
and `ResolveMethod` returning `null` means *this candidate does not apply*, so swallowing the failure
would report "no matching overload" for a method that plainly exists. Both would trade a diagnosable
throw for a wrong answer, which is why `ResolveMethod`'s `catch (ArgumentException)` carries a comment
saying it is deliberately not `NotSupportedException`.

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
[`docs/v5-migration.md`](../../../docs/v5-migration.md).
