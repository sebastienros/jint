# Agent instructions: CLR interop

> **Read this when:** You are touching `ObjectWrapper`, `TypeReference`, a type or object converter, a reference resolver, or the compiled member-read and method-invoker lanes.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Gotchas

Each of these cost a real integrator or a real bug. These are the ones that bite in this area; the
rest of the list is split across the files indexed from the repository-root [`AGENTS.md`](../../../AGENTS.md).

- **A registered `IObjectConverter` used to disable the compiled interop member-read lane engine-wide.** It can now declare the CLR types it handles — through the registration overload `AddObjectConverter(converter, params Type[] handledTypes)`, not an interface member — so unrelated *members* keep the read lane. A converter registered untyped still degrades every wrapped member. Note the two lanes are gated differently: the type filter covers the compiled member-read lane, while the compiled method-invoker lane keys on the method's return type being unobservable to a converter (`ReturnValueIsInvisibleToObjectConverters`), so even a typed converter still costs the invoker lane on any method with an observable return type. Nothing links the declaration to the `switch` inside `TryConvert`, so the two drift: a case added to the converter and not to the registration is skipped on exactly the members the declaration excluded and honoured everywhere else, invisibly. Host-contract verification checks a `true` answer against the registration's own `ObjectConverterTypeFilter` — the same predicate the read lane consults, deliberately reused so the promise and the check cannot diverge.
- **Dictionary-valued reads re-wrap on every access, and only a host promise can stop them.** `record.value.customer.country` over a wrapped `JsonObject`/`IDictionary` resolves each level through the dictionary lane, which probes the target and converts the value afresh every time — the read itself is compiled, but its *result* was never cached, and the bounded recent-wrapper ring (8 slots) cannot hold a nested walk's nodes. The engine cannot fix this alone: a value-identity change inside the host's dictionary is invisible to it. `Options.AddImmutableCrossing(params Type[])` supplies the missing fact — instances of the declared types, while exposed to this engine, are immutable — and in exchange an `ObjectWrapper` over such a target memoizes each key's resolved descriptor, so a repeated read touches neither reflection nor the indexer and each child node is wrapped once per wrapper lifetime. Assignability is the rule (an interface covers its implementations), and unlike `ObjectConverterTypeFilter` this one never guesses in the permissive direction — a wrong claim here would serve stale reads rather than merely cost a fast lane, so an open generic declaration claims nothing. Three things to know before relying on it. The memo lives in a **store of its own**, not in `_properties`: enumeration stays live from the target (a key added CLR-side afterwards still enumerates), no `_propertiesVersion` is bumped, and anything a script actually defines — `Object.freeze`, `Object.defineProperty`, a host `SetOwnProperty` — outranks it and drops it. A **write** through the wrapper evicts that key's memo, which is insurance for the host that was wrong and not a licence to be; write behaviour itself is unchanged. And the memo is **per wrapper**, so it only pays while the wrapper is alive — a stable root (anything reached through `Engine.SetValue`) memoizes its whole subtree, but a node reached through a transient wrapper, such as an element of a wrapped list, still re-resolves on each crossing; that lane is the ring's job, or `TrackObjectWrapperIdentity`'s. The same promise also reaches reflected members, where `ReflectionDescriptor` stops invoking the CLR getter entirely after the first read, superseding both `CacheRecentObjectWrappers` and `TrackObjectWrapperIdentity` for a declared type. No promise is needed for questions *about* the keys rather than their values: `ObjectWrapper.ProbeOwnProperty` answers a string-keyed generic dictionary from the target's own `ContainsKey`, so `in`, `hasOwnProperty`, `propertyIsEnumerable`, `Object.keys`/`values`/`entries`, `for..in`, `Object.assign`, spread and `JSON.stringify` stop converting a value and building a descriptor per key only to discard both. That lane never decides a key is *missing* — a `false` falls through to the descriptor path, because a dictionary wrapper still resolves CLR members for names the dictionary does not carry — so the only way it can disagree with `GetOwnProperty` is a target whose own `ContainsKey` and `TryGetValue` disagree with each other.
- **A non-default `IReferenceResolver` used to disable the member and call inline caches engine-wide.** It can now declare `ReferenceResolverInterests` at registration or via `Options.ReferenceResolverInterests`. The scope was always the non-computed member-read caches, the dense-array indexed-read lane and the member-call callee lane; identifier caches were never affected.

### Native AOT: what is measured, and what is still only asserted

`Jint.csproj` sets `<IsAotCompatible>` for net7.0+ and, in the same property group, suppresses eight IL
codes — `IL3050` (*requires dynamic code*, the one that says an API cannot work under Native AOT) plus
`IL2060`, `IL2067`, `IL2069`, `IL2070`, `IL2072`, `IL2075`, `IL2080`. **The property is not evidence, and
the suppressions are why.** Nothing about them has changed; what has changed is that a CI leg now
publishes `Jint.AotExample` with `PublishAot=true` and *runs the native binary* (`aot` in
`.github/workflows/pr.yml` and `build.yml`), because the run is the only thing that distinguishes a
warning that matters from one that does not.

Two facts about the suppressions are worth knowing before trusting either of them. Jint's `NoWarn` is a
property of Jint's own *compilation* and reaches nothing downstream: ILC re-derives the whole set when it
compiles the closed program, so **an embedder publishing Native AOT sees all 122 of them**, attributed to
Jint's files, in their build. And a source `#pragma warning disable IL3050` — `ObjectWrapper` has
several — is Roslyn-only for the same reason; `[UnconditionalSuppressMessage]` is the one ILC honours.

What the leg proves today: an engine with `AllowClr()` reading properties, fields, indexers and methods
off a host object works natively, as do arrays, `Dictionary<,>`, delegates in both directions, extension
methods, `TypeReference` construction, JSON, promises, and an `Interop.Enabled = false` engine. What it
proves does *not* work is one shape, in five places — **a generic instantiation over a value type built at
run time**. Reference-type arguments share canonical code and are fine; `int`, `double` and friends need a
specific instantiation ILC never saw, and raise `NotSupportedException` at the crossing:

| site | shape | fails as |
| --- | --- | --- |
| `ObjectWrapper.TryBuildArrayLikeWrapper` | `typeof(GenericListWrapperFactory<>).MakeGenericType(int)` | `List<int>` reaching script |
| `ObjectWrapper.TryBuildArrayLikeWrapper` | `typeof(ReadOnlyListWrapperFactory<>).MakeGenericType(double)` | `IReadOnlyList<double>` reaching script |
| `ObjectWrapper.ResolveEnumerableSnapshotFactory` | `typeof(EnumerableSnapshotFactory<>).MakeGenericType(int)` | `IEnumerable<int>` under `EnumerableConversionMode.Snapshot` |
| `DefaultTypeConverter.GetFromResultMethod` | `Task.FromResult<double>` through `MakeGenericMethod` | a JS function converted to `Func<double, Task<double>>` |
| `MethodInfoFunction.ResolveMethod` | `methodInfo.MakeGenericMethod(double)` | any generic host method called with a number |

The first three already carry a `catch (MissingMethodException)` fallback written for exactly this
scenario, and **it never engages**: Native AOT raises `NotSupportedException` from
`Activator.CreateInstance`, not `MissingMethodException`, so the non-generic `ListWrapper` /
`ObjectEnumerableSnapshotFactory` fallback beside it is dead code under AOT. Do not add another such
fallback without checking which exception the runtime actually throws. All five rows, and what a fix
would have to decide, are [issue #3299](https://github.com/sebastienros/jint/issues/3299); none is fixed
yet, and **not one member is annotated `[RequiresDynamicCode]`**, so an embedder's first warning is the
run-time throw.

Two costs land in the *embedder's* project rather than in Jint's, both from
`Engine.SetValue<T>`'s `[DynamicallyAccessedMembers]`, and both from ordinary one-line registrations. An
uncast `SetValue("f", new Func<int, int>(…))` binds to `SetValue<T>` rather than to
`SetValue(string, Delegate)`, and the annotation then demands every public method of `Func<int, int>` —
including the inherited, `[RequiresUnreferencedCode]` `Delegate.CreateDelegate` overloads: six IL2026 and
IL2111 **errors** in the host's own build, cleared by casting to `Delegate`. `SetValue("a", new[] { 1, 2, 3 })`
costs four IL3050 the same way, through `Array.CreateInstance`. Widening either annotation is not the fix;
knowing that the annotation is what an embedder pays is the point.

`Jint.AotExample/Program.cs` pins all of this in both directions. A `Probe` must pass on every runtime; a
`KnownAotGap` must pass under a JIT **and** throw under Native AOT, so a gap that closes fails the leg and
forces the file to be updated — the same discipline as the web-platform-tests exclusion table. Treat the
run as an upper bound rather than a lower one: the example roots the whole `Jint` assembly, which a real
embedder would not do. It roots its own assembly too, and that one is not cosmetic — without it the
trimmer removes an extension method nothing calls from C#, and `company.shout()` fails as *not a function*
with no AOT diagnostic anywhere. Reflection-reached host types need rooting; the failure mode is a wrong
answer, not an error.
