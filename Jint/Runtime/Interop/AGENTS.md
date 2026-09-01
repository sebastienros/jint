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
| `SetTypeConverter(f)` | compiled member-**write** lane, compiled method-**invoker** lane, and the shared-accessor-cache place of every member of a type carrying a non-`int`-keyed indexer | `SetTypeConverter(f, targetTypes)` or `ClrTypeConverter.HandledTargetTypes` — members/parameters/index types none of them is a supertype of | conversions to a declared type, and every target type when nothing was declared |
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
part of `InteropResolutionProfile`: resolution asks it exactly one question — `IndexerAccessor.TryFindIndexer`
asking whether the member *name* converts to an indexer's index type — so `TypeResolver.IsConverterNeutral`
excludes the members of the types that question is ever asked about, on the way in *and* on the way out,
rather than giving every engine with a converter a cache partition of its own. **That one answer decides four
things, and only the first is visible in the resolved artefact**: whether an `IndexerAccessor` exists and what
key it bakes in; whether a `PropertyAccessor`/`FieldAccessor` is handed an `indexerToTry`, which
`ReflectionAccessor.GetValue` probes *before* the declared member; whether a `[JsAccessible]` type may take
its generated lane, gated on there being no such indexer; and whether resolution ends at `NullAccessor`
instead of at the indexer. So the exclusion is asked of the **type** — does it carry a non-`int` indexer whose
index type this engine's converter claims — and not of the accessor. Asking the accessor was
[#3436](https://github.com/sebastienros/jint/issues/3436): a declining converter's engine warmed the cache
with a `PropertyAccessor` carrying no indexer probe, which is not an `IndexerAccessor`, so a stock engine
asking next read the declared member where alone it reads the indexer — and reversing the order flipped it.

### What counts as a public contract

These are this area's rows of Jint's public surface. The rule they all obey — **a change to any of it
is a row in [`docs/v5-migration.md`](../../../docs/v5-migration.md), written in the same pull request**, even
when it breaks nothing at compile time — and the engine-wide rows are in
[`Jint/AGENTS.md`](../../AGENTS.md#what-counts-as-a-public-contract).

| Surface | Location |
| --- | --- |
| `IReferenceResolver` + `ReferenceResolverInterests` | `Jint/Runtime/Interop/IReferenceResolver.cs` |
| `NullPropagatingReferenceResolver` — sealed, singleton `Instance`, the shipped null-propagation resolver the engine recognizes by identity and serves inline | `Jint/Runtime/Interop/NullPropagatingReferenceResolver.cs` |
| `ObjectConverter`, `ClrTypeConverter` — public abstract classes | `Jint/Runtime/Interop/` |
| `JintException.TryGetClrException` + `Options.Interop.ChainClrExceptionAsInnerException` + `JavaScriptException(ErrorConstructor, string?, Exception)` — the CLR exception behind an interop error the script was allowed to catch, held on the error **value** and readable by the host alone | `Jint/JintException.cs`, `Jint/Runtime/JavaScriptException.cs`, `Jint/Options.cs` |
| `ProxyHandler` — public abstract class, 13 virtual traps (the ECMAScript trap set), `null` meaning "forward to target" | `Jint/Runtime/Interop/ProxyHandler.cs` |
| `ObjectWrapper.GetPropertyDescriptor(Engine, object, MemberInfo)` — public **static** | `Jint/Runtime/Interop/ObjectWrapper.cs` |
| `Options.Interop.ImmutableCrossingTypes` + `Options.AddImmutableCrossing` — extension method on `OptionsExtensions` | `Jint/Options.cs`, `Jint/Options.Extensions.cs` |

### Gotchas

Each of these cost a real integrator or a real bug. These are the ones that bite in this area; the
rest of the list is split across the files indexed from the repository-root [`AGENTS.md`](../../../AGENTS.md).

- **Dictionary-valued reads re-wrap on every access, and only a host promise can stop them.** `record.value.customer.country` over a wrapped `JsonObject`/`IDictionary` resolves each level through the dictionary lane, which probes the target and converts the value afresh every time — the read itself is compiled, but its *result* was never cached, and the bounded recent-wrapper ring (8 slots) cannot hold a nested walk's nodes. The engine cannot fix this alone: a value-identity change inside the host's dictionary is invisible to it. `Options.AddImmutableCrossing(params Type[])` supplies the missing fact — instances of the declared types, while exposed to this engine, are immutable — and in exchange an `ObjectWrapper` over such a target memoizes each key's resolved descriptor, so a repeated read touches neither reflection nor the indexer and each child node is wrapped once per wrapper lifetime. Assignability is the rule (an interface covers its implementations), and unlike `ObjectConverterTypeFilter` this one never guesses in the permissive direction — a wrong claim here would serve stale reads rather than merely cost a fast lane, so an open generic declaration claims nothing. Three things to know before relying on it. The memo lives in a **store of its own**, not in `_properties`: enumeration stays live from the target (a key added CLR-side afterwards still enumerates), no `_propertiesVersion` is bumped, and anything a script actually defines — `Object.freeze`, `Object.defineProperty`, a host `SetOwnProperty` — outranks it and drops it. A **write** through the wrapper evicts that key's memo, which is insurance for the host that was wrong and not a licence to be; write behaviour itself is unchanged. And the memo is **per wrapper**, so it only pays while the wrapper is alive — a stable root (anything reached through `Engine.SetValue`) memoizes its whole subtree, but a node reached through a transient wrapper, such as an element of a wrapped list, still re-resolves on each crossing; that lane is the ring's job, or `TrackObjectWrapperIdentity`'s. The same promise also reaches reflected members, where `ReflectionDescriptor` stops invoking the CLR getter entirely after the first read, superseding both `CacheRecentObjectWrappers` and `TrackObjectWrapperIdentity` for a declared type. No promise is needed for questions *about* the keys rather than their values: `ObjectWrapper.ProbeOwnProperty` answers a string-keyed generic dictionary from the target's own `ContainsKey`, so `in`, `hasOwnProperty`, `propertyIsEnumerable`, `Object.keys`/`values`/`entries`, `for..in`, `Object.assign`, spread and `JSON.stringify` stop converting a value and building a descriptor per key only to discard both. That lane never decides a key is *missing* — a `false` falls through to the descriptor path, because a dictionary wrapper still resolves CLR members for names the dictionary does not carry — so the only way it can disagree with `GetOwnProperty` is a target whose own `ContainsKey` and `TryGetValue` disagree with each other.
- **Deriving from `DefaultTypeConverter` used to disarm three lanes, and nothing said so.** The gate was `converter.GetType() == typeof(DefaultTypeConverter)` — an exact type test — so the elegant and obvious thing to write, a subclass that adjusts one conversion and inherits the rest, was indistinguishable from a converter that replaces everything. It is now the declaration that decides, so a subclass declaring what it changes keeps every lane for everything else. Two things follow. The **compiled method-invoker lane** is gated on both converter kinds at once: the object-converter filter answers for the return value and the type-converter filter for the parameter types, and either can decline it. And a `ClrTypeConverter` installed *after* construction (through `Engine.TypeConverter`) is honoured, which a value captured once into `InteropResolutionProfile` was not.
- **A wrapper's prototype comes from the engine's *running* realm, so building it is realm-scoped work.** `JsValue.FromObject` reaches `ObjectInstance`'s base constructor, `TypeReference.CreateTypeReference` reaches `TypeReferencePrototype`, `DelegateWrapper` reads it outright — all three take `engine.Realm.Intrinsics`, which is `ExecutionContext.Realm` and therefore whichever realm happens to be current at the call. That is right for `Engine.SetValue`, where the realm being written *is* the current one, and wrong for anything projecting into a second realm: `ShadowRealm.SetValue` converted against the principal realm and installed on the shadow realm's global, so a host object handed to a realm was not `instanceof Object` inside it ([#3325](https://github.com/sebastienros/jint/issues/3325)). Jint has exactly one realm-scoped construction path — push that realm's `ExecutionContext` for the duration (`ShadowRealm.EnterRealm`, and `ShadowRealmImportValue` before it) — and no lighter one; `Engine._realmInConstruction` is not it, being neither nestable nor exception-safe. Three host-facing base constructors deliberately do **not** follow the running realm: `ClrFunction(Engine, …)`, `HostFunction` and `Constructor(Engine, string)` pin `engine._originalIntrinsics`, because a function a host builds against an `Engine` belongs to the realm the surrounding script can reach whatever was running when it was built ([#2893](https://github.com/sebastienros/jint/pull/2893)). The two rules do not conflict — one is about a realm named by the caller, the other about a realm that merely happened to be current — but a member built through those constructors *inside* a wrapper (its `toJSON`, its `Symbol.dispose`) still lands in the principal realm, which realm-scoping the wrapper does not change.
- **Array-like is not indexable, and `TypeDescriptor.IsArrayLike` is the weaker of the two.** It means the target has a `Count`, nothing more: `ICollection`, `ICollection<T>` and `IReadOnlyCollection<T>` are count-and-copy contracts with no index in them, so `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `SortedSet<T>` and `HashSet<T>` are all array-like with no element at index 0. Any lane that *reads by index* must gate on `ObjectWrapper.HasIndexedElements` instead — `ArrayOperations.For` gated on array-likeness and a bare `ICollection` and handed `IndexWrappedOperations` a target it then hard-cast to `IList`, which is [#3302](https://github.com/sebastienros/jint/issues/3302): a raw `InvalidCastException` out of `Evaluate` for every `Array.prototype` generic over a `Queue<T>`. Falling through to `ObjectOperations` is not a consolation prize — it asks the *object*, so a host collection that really does have an integer indexer still gets its elements through the reflected accessor, and only a genuinely index-less one reads `undefined` per index, which is what an array-like with no index properties means. The same rule governs `ObjectWrapper.HasOriginalIterator`: a wrapper's `Symbol.iterator` is never the array iterator (it enumerates the CLR target), so the index-reading fast path it enables for array destructuring may only stand in for `GetIterator` where index reads reproduce what enumeration yields — which is exactly `HasIndexedElements`.
- **Overload scoring's last rule is the converter's own answer, and it has to be.** `InteropHelper.CalculateMethodParameterScore` rates an argument against a parameter with a dozen structural rules, and everything they did not recognize scored a blanket 100 — "will rarely succeed". `FindBestMatch` discards only a *negative* score, so 100 is a match, and a candidate the argument can never bind to is the *best* one whenever it is the only one. That is survivable where the caller retries — `MethodInfoFunction.TryCall` asks `converter.TryConvert` per candidate and moves on when it declines — and not where the caller takes the first match and stops, which `JintBinaryExpression.TryOperatorOverloading` and `TypeReference`'s constructor selection both do: `'s' + v` selected `op_Addition(T, T)` and died converting the string instead of concatenating, for any host type whose only `+` is `(T, T)` ([#3407](https://github.com/sebastienros/jint/issues/3407)). The last rule now asks the installed `ClrTypeConverter` — the very one `MethodDescriptor.Call` will use — so the score cannot claim a conversion the call then fails to perform, and only what it *confirms* keeps the 100. Three things follow. **What the 100 was protecting is three shapes no structural rule can see**: a JS function to a delegate parameter, an enum parameter given a number outside its defined members, and a conversion operator declared on the **target** type — the operator scan above it reads the *argument's* type only, while `DefaultTypeConverter.TryCastWithOperators` reads both. Each is pinned in `Jint.Tests.PublicInterface/HostOverloadScoringTests.cs`, and deleting the probe fails exactly those three and nothing else in the suite. A parameter type still carrying **open** type parameters (`T`, `Func<T, bool>`) is the one thing the converter cannot be asked — the closed type does not exist until `MethodInfoFunction.ResolveMethod` builds it, and handing an open one over is an `ArgumentException` rather than an answer — so those keep the undecided 100. And the probe *performs* the conversion rather than predicting it, so a user-defined `op_Implicit` on a candidate that is then selected runs twice; the `CanChangeType` rule above it already converts speculatively, so that is this function's established cost rather than a new one, but it is why nothing here may assume a conversion happens once.
- **A numeric score states its bound in what the conversion accepts, not in what the type is called.** The same rule one level up, and it cost the same two lanes. `CalculateMethodParameterScore` rated an integral number a **perfect** 0 against an `int` parameter and a 2 against a `long` one at *any* magnitude, while `float`, `short`, `ushort`, `byte` and `sbyte` all checked the range - so `3000000000` was a perfect match for `int`, and a perfect score also ends `FindBestMatch` with a one-element candidate set. On the two lanes that take the first match and stop the `OverflowException` left through the embedder's `Evaluate`; on the one that retries it became `No public methods with the specified arguments were found.`, the `object` overload beside it never scored ([#3577](https://github.com/sebastienros/jint/issues/3577)). `int` and `long` gate through `FitsInt32`/`FitsInt64`, the very predicates `TryConvertNumberFast` converts by, and the narrow checks read the double rather than a `(int)` cast of it that truncated before they read it - and differently on .NET and .NET Framework. Pinned in `Jint.Tests.PublicInterface/HostNumericRangeOverloadTests.cs`.
- **An index-shaped key on a wrapped bounded collection is the wrapper's to answer, in every lane.** The reflected indexer parses an index out of whatever key it is handed and takes it to the collection, so an out-of-range `x[3] = 9` was the CLR's own `ArgumentOutOfRangeException` out of `Evaluate` — invisible to a script `try`/`catch` and to a host `catch (JavaScriptException)` alike. `ArrayLikeWrapper` owns those keys now ([#3384](https://github.com/sebastienros/jint/issues/3384)), *including* the descriptor lane ([#3423](https://github.com/sebastienros/jint/issues/3423)): `Get`, `Set`, `HasProperty`, `Delete`, `GetOwnProperty`, `ProbeOwnProperty`, `DefineOwnProperty` and both key enumerations all answer from `Length`, and a lane added later that does not is a lane where `in` and `hasOwnProperty` contradict each other — which they may not, `OrdinaryHasProperty` being defined in terms of `[[GetOwnProperty]]`. A **plain** `ObjectWrapper` over a bounded target has no view and reads the target's own count instead ([#3422](https://github.com/sebastienros/jint/issues/3422)); that check is deliberately conditioned on two facts at once, because the same lane serves `Dictionary<int, string>`, where `d[99] = "x"` is a legitimate add, and a string-keyed indexer on a collection, where `x["3"]` names a key rather than a position. `ObjectWrapper.ClassifyElementKey` is the one definition of "index-shaped key" and lives on the base class for that reason; a second copy is the thing that would drift. Owning the key also means owning the **containment** decision the reflected lane used to make for free: a view resolves no member per access, so `TypeResolver.MemberFilter` has to be asked about the indexer the lanes stand for, once per (resolver, type), and `ArrayLikeWrapper._elementsExposed` is that answer ([#3558](https://github.com/sebastienros/jint/issues/3558)). It is asked **before** `CanWrite`/`IsFixedSize`, because containment decides whether there is a property at all and writability only what may be done to one — a fixed-size array whose indexer is hidden must report "no such property", not the `TypeError` naming its bounds. `HasIndexedElements` carries it, so a hidden element lane routes every `Array.prototype` generic to `ObjectOperations` exactly as a `Queue<T>` is routed. What the filter does *not* speak for, and must not be extended to without a decision: `length` (produced from `Count`, filtered separately), iteration (`GetEnumerator`), and `ArrayConversionMode.Copy`, which converts a `T[]` before any member is touched.

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

Eight consequences worth knowing before touching any of it.

- **Anything the two paths cannot agree on is not generated at all.** A method parameter not typed
  `JsValue` goes through a conversion chain steered by `Options.Interop.ValueCoercion` and the installed
  `ClrTypeConverter`; a property with a non-public or `init` accessor is writable by reflection and not by
  emitted C#. Both stay reflected. The MVP this replaced generated them, and acquired two behavioural
  divergences doing it — `p.Name = null` storing the string `"null"`, and a hard cast to a `JsValue`
  subtype throwing `InvalidCastException` out of `Evaluate` where the reflected path raises a catchable
  `TypeError`. **Widening the supported set means proving the new shape's reflected binding first.**
- **A generated member is filtered and named the way a reflected one is, and there are two lanes that
  reach it — only one of which is free.** While the resolver's `MemberFilter`, `MemberNameCreator` and
  `MemberNameComparer` are all still the defaults *and* the engine's reported binding flags still include
  public instance members, `ResolvePropertyDescriptorFactory` answers straight out of the registry's
  name-keyed table (`TypeResolver.GeneratedNameLookupIsEquivalent`), reflecting over the type not at all,
  which is what the feature is for. Change any of them and that shortcut stops being equivalent — the
  table is keyed by CLR name under the default comparer and holds public instance members only — so
  resolution falls through to the ordinary reflected selection in `TryFindMemberAccessor` and the
  generated accessor is **substituted for the member that selection landed on**
  (`TrySubstituteGeneratedAccessor`). Containment is then honoured by construction, because reflection
  has already applied the filter, the name policy and the flags; the host keeps the generated read,
  write and invoke lanes and pays one reflected selection per `(type, member, requirement, profile)`,
  cached like every other. **Do not simplify the substitution into a name lookup.**
  `JsAccessibleRegistry.TryGetAccessorForDeclaredMember` matches on *identity* — `DeclaringType == type`,
  an ordinal name match, a non-static member of the matching kind, and for a method the registered
  parameter count — precisely because a host name policy can route a *different* member to the name the
  registry holds. The `Readable`/`Writable` parity check beside it is load-bearing in the other
  direction: resolution is filtered by `MemberResolutionRequirement` further up, so a generated accessor
  disagreeing with the reflected one there would turn a resolved member into an unresolved one.
- **What neither lane covers is an annotated type carrying an indexer.** An indexer is probed before the
  member itself (`ReflectionAccessor.GetValue`) and a generated accessor has no such probe, so such a
  type keeps the reflection path for its properties and fields. That is also why the registry consult
  sits *after* `IndexerAccessor.TryFindIndexer` rather than before it, and must stay there: an annotated
  type must resolve names the way an un-annotated one does.
- **The registry is process-wide and bumps a generation counter, which every `TypeResolver` compares
  against before answering from its cache.** Without that, a `RegisterAll()` landing after an engine had
  already resolved a member of the same type would go on being answered with the reflected accessor and
  nothing would say so.
- **Every decline is reported, and a decline that costs a host nothing is deliberately not.**
  `JsAccessibleModels.cs` reports `JINT030`–`JINT036` at **info**, one id per remedy: the type is out of
  scope (030), the annotation registers nothing at all (031), a method name is not unique (032), a method
  signature is outside the lane (033), a member's declaration is (034), a member's type cannot be named in
  emitted C# (035), and an indexer, which is probed ahead of every declared member and therefore takes the
  generated lane out of play for every name it answers for (036). **The rule for a new decline is whether
  reflection would have served the member**, not whether the generator took it: a non-public member, a
  static property or field, a `const` (which is static), a backing field and a property accessor are all
  declined *silently*, because the default `ObjectWrapperReported*BindingFlags` never report them and
  nothing was lost. A public **static method** is reported, because the default method flags include
  `BindingFlags.Static` and reflection does serve it. Getting that wrong in the permissive direction is how
  a rule earns a blanket suppression and stops being read at all. There is deliberately **no attribute
  property** promoting these: `dotnet_diagnostic.JINT033.severity = error` in an `.editorconfig` is what
  promotes one, it is per id rather than all-or-nothing, and it is a property of the build rather than of
  one annotated type. **That works only because each diagnostic carries a real `Location` with a
  `SourceTree`**, which is why these do not use `Jint.SourceGenerators`' `DiagnosticInfo`: that type
  rebuilds its location from a file path through `Location.Create(string, TextSpan, LinePositionSpan)`,
  and the compiler resolves an `.editorconfig` severity *per syntax tree* — a tree-less diagnostic skips
  that lookup and keeps its default severity whatever the host writes, with no error and nothing in a
  snapshot to show it, since Verify records the line and column either way. It costs the other generator
  nothing, every descriptor there being an error already. A `Location` is not a value, so
  `JsAccessibleGenerator` keeps reporting and emitting as two outputs: the emitting one selects
  `AccessibleTypeDefinition`, which carries no location, and stays on the cached path. A branch that
  cannot be reached is declined silently and says so in a comment (an abstract method, a `ref` field)
  rather than carrying a message no snapshot can pin.
- **The generator lives in its own analyzer assembly, `Jint.SourceGenerators.Interop`, and that is not
  organisational.** `Jint.SourceGenerators` emits its attribute set through
  `RegisterPostInitializationOutput` unconditionally, and that source declares members typed
  `Jint.Native.Function.FastCallGuard`, which is `internal`. Any assembly outside Jint's
  `InternalsVisibleTo` list that referenced *that* analyzer would fail to compile before reaching a line
  of its own code — which is exactly why nobody had noticed that the MVP's emitted code could not compile
  in a consumer assembly either. Do not move the `[JsAccessible]` generator into that project, and do not
  give it post-initialization output. It is that assembly, and only that one, which `Jint.csproj` packs
  into `analyzers/dotnet/cs`.
- **The emitted code is compiled by the consumer's compiler, so it is bound by the consumer's language
  version and by their SDK's Roslyn — two floors nothing inside this repository can observe.** The
  language floor is **C# 7.3**, which is what the .NET SDK gives a plain `net472` or `netstandard2.0`
  project, and which is the target that has the most to gain here since the run-time compiled lanes
  decline there outright: a `#nullable enable`, a `?` annotation or a `!` in the emitted text is a build
  error in somebody's project, not a style question. It shipped that way, and every snapshot passed.
  `EmittedCodeCompilesAsCSharp7_3` is what notices now. The Roslyn floor is **4.8**, the .NET 8 SDK's own,
  which is why `Jint.SourceGenerators.Interop` pins `Microsoft.CodeAnalysis.CSharp` *below* the
  repository-wide version: a compiler refuses to load an analyzer built against a newer
  `Microsoft.CodeAnalysis` than its own, reports `CS9057`, skips the generator, and the host's call to the
  generated `RegisterAll()` then fails to compile. Raising either floor is a breaking change for
  consumers with no signature to show for it.
- **A project reference cannot verify any of that, which is what `tools/package-consumer` is for.** It
  consumes the produced `.nupkg` through a `PackageReference`, compiles an annotated type on both shipped
  extremes, and **runs** both — a package carrying no generator still compiles every line of Jint's own
  API, so only the run tells them apart. Its empty `Directory.Build.props` is what keeps it
  consumer-shaped, and its own `RestorePackagesPath` is not optional: a re-pack does not change the
  version, so the machine's shared cache would go on serving whichever build of `5.0.0-beta-0` it
  extracted first.

### Native AOT

**Every one of the eight generic-instantiation sites is in this directory, and the rules for them are
not.** Which four degrade, which four must keep throwing, what the published-and-run leg proves, and
the annotation rule a new reflection lane owes, are in
[`Jint.AotExample/AGENTS.md`](../../../Jint.AotExample/AGENTS.md) — beside the example that is the only
evidence for any of it. They bind a change made here on exactly the same terms as everything above.
