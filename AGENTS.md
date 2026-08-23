# Agent Instructions for Jint

Jint is a JavaScript interpreter for .NET. It parses JavaScript using the Acornima library (AST), then interprets it directly — no bytecode generation or DLR usage.

This is the canonical instruction file for every agent working on this repository; `CLAUDE.md` imports it. Target and compare work against the **`main`** branch, which is also the PR target.

## How these instructions are laid out

This file is the entry point and is kept small: it holds what every agent needs before its first edit,
whatever that edit turns out to be. Everything else lives in an `AGENTS.md` beside the code it governs.
**Read the file on the row matching what you are about to do, before you touch that code.**

| If you are about to… | Read first | What is in it, and what a violation costs |
| --- | --- | --- |
| change a signature or observable behaviour under `Jint/`, or anything outliving one evaluation — `Prepared<T>`, `Options`, a snapshot, the event loop, an `*Async` entry | [`Jint/AGENTS.md`](Jint/AGENTS.md) | The key runtime types, the namespace map, the engine-source conventions (type co-location, the unsigned-cast bounds check), what counts as a frozen public contract, the snapshot / event-loop / async rules. Cost: a silent breaking change for embedders, or one engine's state leaking into another through a shared `Prepared<Script>`. |
| implement or change an ECMAScript built-in, intrinsic, coercion rule or new syntax | [`Jint/Native/AGENTS.md`](Jint/Native/AGENTS.md) | Which spec document is authoritative, and that test262 beats prose. Cost: implementing a dated snapshot or a compatibility table instead of the living spec — or un-gating a web API that must stay opt-in. |
| touch `ObjectInstance`, a property descriptor, a property-access lane, or anything a host subclasses | [`Jint/Native/Object/AGENTS.md`](Jint/Native/Object/AGENTS.md) | The subclassing cliff, `PropertyAccessSemantics`, host-contract verification, `ArrayLikeObject`, who may reach a new fast lane. Cost: sorting every embedder into a slow path they cannot see or escape. |
| touch CLR interop — wrappers, converters, reference resolvers, dictionary-backed reads | [`Jint/Runtime/Interop/AGENTS.md`](Jint/Runtime/Interop/AGENTS.md) | Which host registrations silently disable a compiled lane engine-wide, and the immutable-crossing promise. Cost: an engine-wide deoptimisation nobody can see, or stale reads. |
| add or change a statement/expression handler, a fast path in one, coverage, or anything published onto the AST or a `Prepared<T>` | [`Jint/Runtime/Interpreter/AGENTS.md`](Jint/Runtime/Interpreter/AGENTS.md) | Engine-affine vs shareable state and the AST `UserData` invariant, why coverage counters cannot live on a handler node, and what a warmed call site retains. Cost: a fast path that silently stops being counted, or an engine pinning a host object for its lifetime. |
| touch module loading, linking, evaluation, or a module's location | [`Jint/Runtime/Modules/AGENTS.md`](Jint/Runtime/Modules/AGENTS.md) | The load phase, which failures become rejections and which must stay fatal, the three host entry points and which one deadlocks, `ModuleFactory.LocationOf`. Cost: a widened `catch` turns a constraint into a rejection that bounds nothing. |
| touch anything bounding execution — statements, time, memory, recursion, cancellation | [`Jint/Constraints/AGENTS.md`](Jint/Constraints/AGENTS.md) | How limits and CLR access are configured, what disarms the tight-loop lane, what a fan-out brackets, why the cancellation cadence is engine state, how `MaxRecursionDepth` counts, saturated sentinels. Cost: a limit that no longer limits. |
| write a call site needing an API `net472` / `netstandard2.0` / `netstandard2.1` lacks | [`Jint/Extensions/AGENTS.md`](Jint/Extensions/AGENTS.md) | The polyfill-downwards discipline and the ways a polyfill stops being one. Cost: `#if` scattered through spec algorithms, or a downlevel `OrderBy` that spins forever on a comparer JavaScript may legally supply. |
| touch anything under `Jint/WebApi/` or `Options.WebApi` | [`Jint/WebApi/AGENTS.md`](Jint/WebApi/AGENTS.md) | The four subtree conventions, the whole-file `net8.0` gate, WebIDL's property attributes (enumerable — the opposite of ECMAScript's rule), timer ordering, the diagnostics sink. Cost: a member shipped with the wrong attributes, or a build that breaks only on `net472`. |
| touch the vendored web-platform-tests corpus, its shim or its driver | [`Jint.Tests/Wpt/AGENTS.md`](Jint.Tests/Wpt/AGENTS.md) | The exclusion table is the artefact — an entry must match a failing test and no passing one — and a non-zero `NeedsTriage` count means the corpus found a defect somebody still owes the engine a fix for. Cost: five thousand green cases that mean nothing. |
| bump the pinned test262 SHA or triage a conformance failure | [`Jint.Tests.Test262/AGENTS.md`](Jint.Tests.Test262/AGENTS.md) | A bump is a code change, not a pin change; and the three exclusion banners, one of which is deliberately not debt. Cost: an upstream normative change landing unread. |
| write a test proving a third party can reach an API | [`Jint.Tests.PublicInterface/AGENTS.md`](Jint.Tests.PublicInterface/AGENTS.md) | It is the only test project without `InternalsVisibleTo`, which is the whole reason a test there means anything. |
| write, run, or quote a benchmark number | [`Jint.Benchmark/AGENTS.md`](Jint.Benchmark/AGENTS.md) | The measurement environment and its three modes, the paired comparison, one engine per row. Cost: a `--job short` number in a PR, or a row whose result depends on which sibling rows exist. |

Before adding to any of them, read [the size budget](#the-size-budget-and-which-agents-load-what) at the end of this file: keep this one under 24 KiB and each of those under 32 KiB.

## Build & Test

```bash
# Build (solution, or a single project)
dotnet build -c Release
dotnet build -c Release Jint/Jint.csproj

# Run all tests
dotnet test -c Release

# A specific project, class, or single test
dotnet test Jint.Tests\Jint.Tests.csproj -c Release
dotnet test -c Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests"
dotnet test -c Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests.CanAccessCLR"

# Test262 conformance suite
dotnet test -c Release Jint.Tests.Test262/Jint.Tests.Test262.csproj
```

Always build and test in **Release** — it is the faster feedback loop and the configuration performance claims are about. Never pass `--no-build`; always work against freshly compiled code. `TreatWarningsAsErrors` is on, so every warning must be fixed. Packages are managed centrally through `Directory.Packages.props`.

Setting `JINT_HOST_CONTRACT_VERIFICATION=1` runs `Jint.Tests` and `Jint.Tests.PublicInterface` with the host-contract verifiers on in Release, which is the configuration an embedder is told to use; see [Host-contract verification](Jint/Native/Object/AGENTS.md#host-contract-verification). It is a separate leg, not the default.

### Quick manual testing with Jint.Repl

```bash
# -f <path> executes a file, -t <secs> sets a timeout, stdin works too
dotnet run --project Jint.Repl -c Release -- -f script.js -t 10
echo "Math.sqrt(16)" | dotnet run --project Jint.Repl -c Release -- -t 10
```

**Always pass `-t`** so a runaway script cannot hang the session. Use the REPL for quick one-off checks; use a test in `Jint.Tests` for anything worth keeping.

## Architecture

```
Acornima Parser (external) → AST → Interpreter → Runtime → Interop
```

### Execution pipeline

1. **Parsing** — Acornima parses JavaScript source into an AST (`Acornima.Ast` nodes).
2. **Jint wrapping** — AST nodes are wrapped in `Jint*` interpreter classes: `JintExpression` subclasses in `Runtime/Interpreter/Expressions/` (`JintCallExpression`, `JintBinaryExpression`, …) and `JintStatement` subclasses in `Runtime/Interpreter/Statements/` (`JintIfStatement`, `JintForStatement`, …).
3. **Execution** — `Engine` drives execution. Statements return `Completion` (a value plus a completion type: Normal/Break/Continue/Return/Throw); expressions return `JsValue` or `Reference`.

### Test projects

- **`Jint.Tests`** — Main unit tests (xUnit v3, AwesomeAssertions), organized by topic (`Runtime/`, `Parser/`, `Debugger/`, …). Test classes mirror runtime types; JS scripts are embedded resources in `Runtime/Scripts/` and `Parser/Scripts/`. `Wpt/` is the web-platform-tests area — see [Web platform tests](Jint.Tests/Wpt/AGENTS.md#web-platform-tests). Use a 30-second timeout when invoking the runner.
- **`Jint.Tests.Test262`** — Official TC39 conformance suite (NUnit). `Test262Harness.settings.json` holds exclusions/inclusions and, in `SubDirectories`, which of test262's `test/` sub-directories are generated at all — `annexB`, `built-ins`, `intl402`, `language` and `staging` (see [Updating the test262 suite](Jint.Tests.Test262/AGENTS.md#updating-the-test262-suite); `staging/` is an explicit opt-in, not the tool's default). Test sources live in `..\test262\test`, which you may always read, and the harness scripts a test `includes` in `..\test262\harness` — including `harness/sm/` for the staged SpiderMonkey ports. Failure output contains the failing script — strip the line numbers to reproduce. **Never "fix" these tests.** No runner timeout needed; the engine defaults to 30 seconds.
- **`Jint.Tests.CommonScripts`** — Real-world scripts (crypto, 3D rendering, …) run as correctness and performance validation (NUnit).
- **`Jint.Tests.PublicInterface`** — API contract tests (xUnit v3). See the integration-surface section below.
- **`Jint.Tests.SourceGenerators`** — Tests for the source generators.

## Third-party integration surface

Jint is not only an engine to work on, it is an engine that gets **embedded**. Integrators host it in-process, project host-supplied objects into script, and bound execution. A significant part of the public API exists only to serve them, and for everything in this section **changing observable behaviour is breaking even when the signature is untouched**. Assume an embedder depends on it before simplifying or "optimising" it.

Its rules are split across the files in the index above, and every one of them is in one of those. Four stay here, because they are the ones an agent breaks *before* it knows which file to open — each looks like an ordinary internal refactor right up to the moment an embedder's bounded execution stops being bounded.

### Gotchas

Each of these cost a real integrator or a real bug.

- **Constraints bound one entry into the engine, never a host-driven sequence of them.** Every public entry that runs script — `Execute`, `Evaluate`, `Invoke`, `Engine.Call`, the `JsValue.Call` extension helpers — funnels through `Engine.ExecuteWithConstraints` (`Jint/Engine.cs`), which calls `ResetConstraints()` before the callback and again in its `finally` for any entry that is not nested (nesting is `_hostEntryDepth > 0 || _executionContexts.Count > 1`). So `foreach (var row in rows) predicate.Call(row);` — the single most common embedding shape — hands every element a fresh statement budget, a fresh allocation budget and, worst, a **freshly armed timeout deadline**. Measured: `LimitStatements(100)` does not stop 1000 host `Call`s, `LimitExecutionTime(200ms)` does not fire across 3 s of continuous host-driven execution, and `LimitMemory` never sees more than one call's allocations — while the identical work inside one `Execute` throws in every case. The reset itself is not the mistake: per-run reset is exactly what `Constraint.Reset`'s doc promises and what makes a reused engine usable, and the nested case is handled deliberately (a host callback re-entering the engine from inside a running script does *not* re-arm, or `while (true) hostCallback()` would run forever). What no embedder expects is that a single function call is a **run**. `Engine.Constraints.Check()` from the host loop does not close the gap — `TimeConstraint` re-arms its deadline on the way *out* of every run, so a host-side check measures the time since the last call returned. What an embedder must do instead: bound the loop host-side (its own `Stopwatch`, checked between iterations), or move the loop into the script (`rows.forEach(predicate)`) so the whole traversal is one run and one budget. The in-engine option for a *budget* is a user-derived `Constraint` whose `Reset()` is a no-op and which stays *exact* (the default `IsAmortizable`, so it is checked on every statement) — which costs the tight-loop lane above. All of it is pinned from the embedder's side in `Jint.Tests.PublicInterface/HostCallLoopConstraintTests.cs` and `HostMemoryLimitTests.cs`; those tests assert the behaviour as it is, so changing it is a deliberate act that updates them. For the wall-clock half of that budget case there is now an in-box class — `Jint.Constraints.OperationDeadlineConstraint`, which the host brackets with `Begin(budget, token)` / `End()` around the whole operation and whose no-op `Reset()` therefore survives every per-entry reset in between; it observes only a clock and a token, so unlike a hand-written exact budget it declares `IsAmortizable => true` and keeps the tight-loop lane armed, and it throws a real `OperationCanceledException` for the token (Jint's own `ExecutionCanceledException` is a `JintException` and is not one) and the usual `TimeoutException` for the budget. The allocation half has the same shape: `MemoryLimitConstraint.Begin` / `End` brackets one managed-allocation budget across every entry the operation makes, and unlike the deadline it stays *exact*, so it disarms the tight-loop lane for as long as a memory limit is configured at all.
- **`FastSetProperty` / `FastSetDataProperty` always create an *own* property.** They shadow anything of that name on the prototype chain, invoke no inherited setter, and run no `[[DefineOwnProperty]]` validation (so they can never raise `TypeError`). Storing a raw descriptor under a string key is a dictionary-mode operation, so a shape-mode receiver is permanently deoptimized and forfeits the shape inline cache. Two refinements: symbol keys do not deopt, and a `BuiltinShapeMode` receiver can survive an in-place slot replacement. Use them for setup-time writes only — `Set` for steady-state mutation, `JsObject.Create` to build objects.
- **`GetOwnProperties` is not the enumeration hook.** `Object.keys`/`values`/`entries`, `for..in`, object spread and rest, `Object.assign`, `JSON.stringify` and `JsonSerializer` all list keys through `GetOwnPropertyKeys` and filter them with `ProbeOwnProperty`; none of them calls `GetOwnProperties`. Its real callers are the CLR conversion path (`ToObject` under `Options.Interop.CreateClrObject`), `GetSmallestIndex`, the debugger's `GetAllBindingNames`, and the debug view. A host that overrides only `GetOwnProperties` to expose projected properties therefore ships an object whose keys are invisible to every script-visible enumeration — a real integrator did exactly that. Overriding `GetOwnPropertyKeys` (plus `ProbeOwnProperty`, so existence and enumerability are answered without materializing a descriptor) is the pair that works.
- **Sharing a `JsValue` across engines is unsupported**, and nothing validates or guards it. An `ObjectInstance` holds a hard reference to its creating engine and realm. README.md's "Embedding performance" section states this for embedders; there is still no XML doc on `JsValue` / `Engine.SetValue` saying it, and no test pins it.

The other seventeen are in the files indexed above. **Do not add a new gotcha here.** Add it to the file for the area it governs; if none fits, say so in the pull request rather than growing this one.

## Conventions

Global usings for Acornima and `Acornima.Ast` are defined in `Directory.Build.props`. Nullable reference types are enabled across the codebase, unsafe code is allowed for performance-critical paths, and the latest analyzers run with `EnforceCodeStyleInBuild`.

### Performance is critical

Performance is a first-class concern; every change must consider its impact.

- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot paths.
- Prefer `readonly struct` and `readonly record struct` with primary constructors for small data types.
- Use `Span<T>`, `ReadOnlySpan<T>` and stack allocation wherever possible.
- Leverage the pools in `Jint.Pooling` instead of allocating fresh instances.
- Mark types `sealed` whenever possible — it enables devirtualization and inlining.
- Prefer `internal` visibility — it avoids virtual dispatch and enables inlining.
- Cache `Prepared<Script>` / `Prepared<Module>` when executing the same source repeatedly, and prefer strict mode, which executes faster.

### Code patterns

- **Lazy initialization** — Built-in objects and their properties initialize on first access to keep startup cheap. `JintStatement` subclasses set `_initialized = false` and override `Initialize()` for deferred setup.
- **Error throwing** — Use the static `Throw.*` helpers (`Jint/Runtime/Throw.cs`) rather than `throw new`; they are `[DoesNotReturn]` and keep non-error paths allocation-free.
- **Built-in JS types** follow the Constructor/Prototype/Instance split matching the spec structure.
- **Engine carries all state** — `ObjectInstance` and most runtime types take `Engine` in their constructors; interpreter classes receive state via `EvaluationContext`.
- **Partial classes** — Large types are split (`Engine.*.cs`, `Intrinsics.*.cs`, `ObjectInstance.*.cs`). Keep related functionality together when editing.
- **Type flags** — The `InternalTypes` enum enables fast type checks without casting; many hot paths depend on it.
- **Property keys** — `KnownKeys` holds pre-computed common property names.
- **Spec references** — Code cites the section it implements, in a `<summary>` or a comment, and the URL says which document is authoritative: `https://tc39.es/ecma262/#sec-...` for merged language features, `https://tc39.es/ecma402/#sec-...` for i18n, and `https://tc39.es/proposal-<name>/#sec-...` for a feature that is still a proposal. Maintain them when editing, and re-point a proposal's citations when it merges into ECMA-262 — the anchors get renamed on the way in (`sec-iteratorprototype.take` became `sec-iterator.prototype.take`). The web APIs under `Jint/WebApi/` are owned by WHATWG living standards instead, each with its own document and anchor vocabulary: `https://console.spec.whatwg.org/#...`, `https://webidl.spec.whatwg.org/#...` (`#idl-*`, `#es-*`, `#dfn-*`), `https://fetch.spec.whatwg.org/#...` (`#dom-*`, `#concept-*`), `https://dom.spec.whatwg.org/#...`, `https://html.spec.whatwg.org/multipage/...`, `https://encoding.spec.whatwg.org/#...` and `https://url.spec.whatwg.org/#...`. Cite the one that actually defines the thing — `fetch` is WHATWG Fetch, `setTimeout` is HTML, `DOMException` is WebIDL — never MDN.

### Data structures

Prefer a **`readonly record struct` over a tuple** for returning multiple values — named properties beat `Item1`/`Item2` at the call site. Mark it `[StructLayout(LayoutKind.Auto)]` and pass it into methods with `in`. Use a class or plain struct instead once the type carries behavior, validation, or many fields.

### Visibility: internal-first

Default every new type, member, field and parameter to the **narrowest visibility that compiles**, and climb only when a real consumer requires it:

1. **`private`** — single-class implementation detail.
2. **`internal`** — shared within the Jint assembly; the default for most new runtime types.
3. **`protected internal`** — extension points on public abstract classes that user-derived classes legitimately need.
4. **`public`** — only when the type appears in an already-public signature, or end users must construct it directly.

If a type is only referenced by `internal` members, it must be `internal`. When a public surface seems to force your hand, first check whether that surface can be split so the implementation detail stays internal — `ModuleImportPhase` stayed internal by splitting `public GetModuleNamespace(Module)` from `internal GetModuleNamespace(Module, ModuleImportPhase)`. Public API is a durable commitment; `internal` costs nothing to widen later.

## The size budget, and which agents load what

Keep this file under **24 KiB** and every co-located file under **32 KiB**. Both numbers come from what the
tools actually do, verified 2026-08-23:

| Agent | Root file it loads | Loads a nested `AGENTS.md`? |
| --- | --- | --- |
| OpenAI Codex | `AGENTS.override.md`, then `AGENTS.md` | No — never below the working directory |
| Claude Code | `CLAUDE.md`, which imports this file; it does **not** read `AGENTS.md` | No — `.claude/rules/*.md` with `paths:` does it instead |
| Copilot cloud agent | `AGENTS.md` (since 2025-08-28) *and* `.github/copilot-instructions.md`; both are supplied | Yes, `**/AGENTS.md`, nearest in the tree wins |
| Copilot code review | `AGENTS.md` (since 2026-06-18) | No — root only |
| Copilot CLI | `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` | Upward only, cwd → git root |
| Copilot in VS Code | `AGENTS.md` (on by default since v1.105) | Off by default; even when on it injects the *path*, not the content |
| Copilot in Visual Studio / JetBrains | `.github/copilot-instructions.md` only — no `AGENTS.md` support | No |
| Cursor | `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/*.mdc` | Yes, combined with parents, more specific wins |
| Amp | `AGENTS.md`, falling back per directory to `AGENT.md` / `CLAUDE.md` | Yes, lazily, when it reads a file in that subtree |
| Devin Desktop (ex-Windsurf) / Devin CLI | `AGENTS.md`, `.devin/rules/*.md`, `.windsurf/rules/*.md` | Yes — a subdirectory file becomes a glob rule for `<dir>/**` |
| Gemini CLI | `GEMINI.md`; `AGENTS.md` only if `context.fileName` names it | Upward from a touched path, just in time |
| Jules | `AGENTS.md` at the repository root | Not documented |
| Aider | nothing automatically; needs `read: [AGENTS.md]` in `.aider.conf.yml` | No |

**32 KiB is Codex's `project_doc_max_bytes` default**, and it is a running budget across the whole
root-to-cwd chain rather than a per-file allowance, so a fat root file starves a nested one; overflow is a
silent mid-file byte truncation whose only signal is a log line below the level `codex exec` prints at. The
Devin CLI caps an always-on rule file at the same 32 KiB, truncating with a pointer to the source. Devin
Desktop documents 12,000 characters per workspace rule file; whether that applies to an `AGENTS.md` its rules
engine processes is undocumented, so treat it as the tightest plausible budget.

Only four of those ecosystems auto-load the co-located files. The rest reach them because the index names
them — which is why the index has a trigger column rather than being a list of links, and why anything an
agent must obey *before* it knows which area it is in has to stay in this file.
