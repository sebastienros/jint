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
   - **stage 2.7/3 proposals not yet in ECMA-262** — that proposal's own spec, `https://tc39.es/proposal-<name>/`. Jint ships a lot of these (the "ECMAScript proposals" list in `README.md` is the current set), and the `ecma262` URL form is simply wrong for them.
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
