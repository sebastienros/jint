# Agent instructions: ECMAScript built-ins

> **Read this when:** You are implementing or changing something the ECMAScript specification defines — a built-in, an intrinsic, a coercion rule, or new syntax.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

## ECMAScript compliance

Follow the specification as closely as practical, support both strict and sloppy mode with their spec-defined differences, and do not introduce non-standard language extensions. When implementing a feature:

1. Read the normative text before writing code, from whichever document currently owns the feature:
   - **merged language features** — the *living* spec, <https://tc39.es/ecma262/>. Not a dated snapshot, not MDN, not a compatibility table.
   - **stage 2.7/3 proposals not yet in ECMA-262** — that proposal's own spec, `https://tc39.es/proposal-<name>/`. Jint ships a lot of these ([`docs/reference/ecmascript.md`](../../docs/reference/ecmascript.md) summarizes the current set), and the `ecma262` URL form is simply wrong for them.
   - **internationalization** — ECMA-402, <https://tc39.es/ecma402/>.

   Cite the URL you actually read, per the **Spec references** convention above.
2. Where the prose and test262 disagree, **test262 at the pinned SHA wins** — see the `Jint.Tests.Test262` note about never "fixing" those tests. A test's `info:` block usually quotes the numbered algorithm verbatim, which is the fastest way to read a normative change and the most reliable way to get argument-validation order right.
3. Put the built-in where its peers live under `Jint/Native/` (e.g. `Array/` for Array methods).
4. Register new globals and well-known symbols in `Intrinsics`.
5. Update `TypeConverter` if new coercion rules apply.
6. Add a statement/expression handler under `Runtime/Interpreter/` if it is new syntax.

Proposal-stage **TC39** built-ins are registered unconditionally — there is no per-feature option or ES-version gate for anything ECMAScript defines, however early its stage. `Options.ExperimentalFeatures` is about CLR interop and has nothing to do with which built-ins exist.

## Which `Intrinsics` members are public

A property on `Jint.Runtime.Intrinsics` is `public` when a host has a reason to **call** the object it
returns from C#, not because the intrinsic exists. Today that is the seven `ErrorConstructor` properties —
`new JavaScriptException(intrinsic, message)` takes one, so picking the error the specification would pick is
a host operation — plus the handful of constructors a host builds instances with (`Array`, `Map`, `Set`, the
typed arrays, `ArrayBuffer`, `RegExp`, `ShadowRealm`, `Object`, `Function`, `Eval`). Everything else stays
`internal`, and the *type* is what enforces it: every remaining intrinsic's type is `internal` too, so its
property could not be promoted without first widening a class. Widen the class only when a host must invoke
it — an intrinsic a host would merely hand back to script is already reachable as `engine.GetValue("Name")`,
which is not a reason to freeze a second spelling into the public surface. A new error constructor
(`AggregateError`, `SuppressedError`) is the case that qualifies on the first test but fails on the second:
both are `internal sealed` classes that do not derive from `ErrorConstructor`, so exposing them is a type
promotion to argue on its own, not a modifier to flip.

**That rule stops at the language.** The WHATWG web APIs under `Jint/WebApi/` are host APIs, not language features, and are deliberately **opt-in**: nothing is installed unless `Options.WebApi.Features` names it, and a default engine is byte-for-byte the engine it was before they existed. Do not "fix" that gating by registering them unconditionally — see [Web APIs](../WebApi/AGENTS.md#web-apis) for how it works and why.

## Handing an array to script: `MaxArraySize` is checked by the way you build it

An array a built-in returns must respect `Options.Constraints.MaxArraySize`, and the check lives in the constructor
path, not in the caller. `ArrayCreate`, `CreateArrayFromList`, `ConstructFromBuilder` and both `ConstructFast`
overloads all make it (`ArrayConstructor.ValidateElementCount` or the `ArrayInstance` constructor), and throw
`MemoryLimitExceededException`. `new JsArray(engine, items)` and the `ArrayInstance(engine, items)` constructor under
it take ownership of a finished array and do **not**, so a built-in that reaches for them with a list whose length the
script chose hands out an array the host forbade. `Promise.all` did exactly that through `ConstructFast` until it
checked ([#4493](https://github.com/sebastienros/jint/issues/4493)). Prefer `ConstructFast` or `ConstructFromBuilder`
for a finished list: one compare, then a single copy, where `CreateArrayFromList` writes element by element.

**An argument list is bounded where it is built, never where it lands.** The rest parameter
(`FunctionEnvironment.HandleRestElementArray`) and the `arguments` object take the list unchecked, so a call without a
spread pays nothing. What makes a list longer than its source is checked instead
([#4505](https://github.com/sebastienros/jint/issues/4505)):
- the spread builder, `ExpressionCache.BuildArgumentsWithSpreads`, which array literals share;
- the array-like list behind `apply`, `Reflect.apply`, `Reflect.construct` and a script `ownKeys` trap;
- a bound function's `CreateArguments`.

Each tests `Engine._hasMaxArraySize`, so a default engine pays one byte test, and only an engine with a limit reaches
the out-of-line `ArrayInstance.ValidateListLength` or `FunctionPrototype.CreateListFromArrayLikeWithinLimit`, which
checks the length after its one read and before allocating. A new path that builds a list from a run-time count owes
the same check, and a list a web API fills from what script stored takes `ValidateListLength` before allocating. A list
the host already built is exempt, because the check would bound no memory: a host's own argument array, and a CLR
`ProxyHandler`'s `OwnKeys`, which never reaches the bound.

**Keep the bound off the inlined lane.** `CreateListFromArrayLike`, both `ArrayOperations.GetAll`s and
`JsProxy.ValidateOwnKeysTrapResult` compile as they did before the bound, and the bound sits beside them. That shape
was measured into place:
- A first version snapshotted the limit as an 8-byte `Engine` field. That moved every primitive field after it by
  8 bytes, the hot flags and counters among them; a `bool` declared after the other flags sits in padding.
- It also passed the limit through `CreateListFromArrayLike` and `GetAll`, which `ValidateOwnKeysTrapResult` inlines.
  Any change there, an unused parameter included, flipped the inliner's other decisions in that method:
  `ArrayOperations.For` stopped being inlined, and a copy of `ObjectInstance.GetOwnPropertyKeys` was inlined instead.
  `ProxyBenchmark.OwnKeysTrap` measured 6% slower.

Check a change near there with `DOTNET_JitDisasm` against the method's inlinee counts before and after.

## Closing an iterator on the way out of a throw: never wrap the close in a `catch`

IteratorClose with a throw completion lets the throw win over what `return` raises
(https://tc39.es/ecma262/#sec-iteratorclose step 5), and `IteratorInstance.Close(CompletionType.Throw)` does that
discarding itself. What it still raises is an exception that ends the run — one `Throw.MustPropagateHostException`
names, every execution constraint among them. So a built-in leaving by a throw calls `Close(CompletionType.Throw)` and
lets whatever comes out propagate. A `catch { }` around it, or `catch when (completion == CompletionType.Throw)` in a
close written by hand, is how a time limit spent inside `return` stopped bounding anything
([#PRNUMBER](https://github.com/sebastienros/jint/pull/PRNUMBER)). A hand-written close — `return` called directly, as
the helpers' argument validation and `Array.fromAsync` do — adds `&& !Throw.MustPropagateHostException(e)` to its
filter. Pinned in `Jint.Tests.PublicInterface/HostIteratorCloseConstraintTests.cs`.
