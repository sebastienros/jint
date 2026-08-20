# Serializable `Prepared<Script>` — feasibility study

Status: **spike, recommendation is _do not build it as specified_.**
Scope: the last checkbox of [#3089](https://github.com/sebastienros/jint/issues/3089) — *"persist the parsed/analyzed AST to disk and rehydrate — cold-start wins for serverless-style hosts, complementing `RestoreGlobalSnapshot` engine reuse. Needs an Acornima serialization story, so possibly an upstream-adjacent effort."*

This document is a design spike only. No engine code is proposed here and none was written; the probe used to
produce the numbers below is throwaway and is not part of this branch.

---

## 1. Summary

A serializable `Prepared<Script>` is **mechanically buildable** — Acornima's AST is publicly constructible, and
none of the blockers that were suspected up front (internal constructors, unsettable node positions, label sets
lost on reconstruction) survived contact with the assembly. What kills it is the arithmetic, not the API surface.

Three measured facts decide it:

1. **Materializing the object graph is already 20–35 % of what `PrepareScript` costs**, measured with the strings
   already interned, nothing decoded and no analyzer output built at all. That is the floor any deserializer pays
   before it has read a single byte.
2. **Over half of `PrepareScript` is the static-analysis pass, not the parse** (54–67 % across the three scripts
   measured). Serializing only the AST — the part Acornima could plausibly help with — therefore addresses the
   *smaller* half, and the larger half is Jint-internal state with AST back-references that would have to be
   serialized by hand and re-versioned on every release.
3. **A knob that captures much of the same win already ships.** `ScriptPreparationOptions.StaticAnalysis = false`
   removes 54–67 % of preparation time for one line of host code, no new format, no versioning hazard, and it is
   already documented and pinned. Its cold-start benefit is proportional to how much of the script never runs
   (§4.2): −49 % on the whole cold path for a library that defines much and executes little, −4 % for a script
   that executes most of its top level.

Best case for a hand-written binary format is on the order of **4–7 ms saved on a 184 KB script**, once, per
process — against a permanent, hand-maintained serializer over 118 public Acornima node types plus about ten
`internal` Jint types that carry AST back-references and change shape release to release, where a single wrong
byte is a silently miscompiled program rather than a load failure.

**Recommendation: wontfix as specified.** Ship the two cheap things in [§8](#8-what-to-do-instead) instead — a
cold-start playbook in the README and (optionally) an upstream ask to Acornima that costs Jint nothing now and
keeps the door open. Revisit only if Jint ever grows a bytecode/flat-IR representation, at which point *that* is
the artifact worth persisting, and an AST serializer built today would be a format we would have to keep alive
for a representation we replaced.

---

## 2. What `Prepared<Script>` actually holds

`Jint/Prepared.cs` is three fields:

```csharp
public readonly struct Prepared<TProgram> where TProgram : Program
{
    public TProgram? Program { get; }                  // the Acornima AST, with UserData attached
    public ParserOptions? ParserOptions { get; }       // delegate-bearing; rebuilt, never serialized
    public ReferencedGlobals? ReferencedGlobals { get; } // sorted string[] + lookup
}
```

`ParserOptions` and `ReferencedGlobals` are not the problem. `ParserOptions` carries `OnRegExp` and `OnNode`
delegates and is re-derived from the host's `ScriptParsingOptions` on load — the serialized artifact only has to
record *which* options it was prepared under so a mismatch can be rejected. `ReferencedGlobals` is an immutable
sorted `string[]` with an eagerly built lookup (`Jint/ReferencedGlobals.cs`) and serializes trivially.

Everything hard is in `Program` — specifically in `Node.UserData`, where `Engine.PrepareScript`'s `AstAnalyzer`
(`Jint/Engine.Ast.cs:132`) stashes its output.

### 2.1 Inventory, classified

Classification per the task: **(a)** pure AST, **(b)** engine-neutral analyzer product, **(c)** rebuilt per engine
anyway. The counts come from the probe ([§9](#9-probe-method-and-raw-numbers)); the "per engine anyway" column is
the existing `StaticAnalysis = false` contract, documented on `ScriptPreparationOptions.StaticAnalysis`.

| Carrier | What it is | Class | Serializable? |
| --- | --- | --- | --- |
| The Acornima node objects themselves | node type, children, `Range`, `Location`, literal values, operators, flags | (a) | Yes — every concrete `Acornima.Ast` node has a public constructor; `Range`/`Location` are init-only and settable in an object initializer |
| `Statement.LabelSet` | which label a loop/switch answers to | (a) | Yes, **derived for free** — `LabeledStatement`'s constructor writes it into its body (probe §9.4). The field is `internal`, so this was the one hard blocker until it was disproved |
| `RegExpLiteral.ParseResult` | an adapted, `RegexOptions.Compiled` .NET `Regex` | (a)-ish | **No** — must be recompiled on load; `RegexCompilation.Compiled` is the prepared-script default (`Jint/Native/RegExp/RegExpParseCache.cs:23`), so this cost is unavoidable and unamortizable across processes |
| `Identifier.UserData` → `Environment.BindingName` | `Key` + `JsString` + a `CalculatedValue` for `undefined` | (b) | Yes, but see the string-identity note in §2.2. **7,855 of 27,496 nodes on handlebars.js** |
| `Literal`/`UnaryExpression`/`BinaryExpression`.`UserData` → `JintConstantExpression` | a folded `JsValue` (`JsString`/`JsNumber`/`JsBoolean`/`JsBigInt`/`JsValue.Null`) plus the node | (b) | Yes — the values are engine-independent singletons/immutables |
| `MemberExpression.UserData` → `JsValue` | the determined non-computed property name, a `JsString` | (b) | Yes |
| `ReturnStatement.UserData` → `ConstantStatement` | constant-return shortcut | (b) | Yes |
| `NestedBlockStatement.UserData` → `JintBlockStatement.BlockState` | `DeclarationCache`, `List<ScopedDeclaration>`, slot names, `Binding[]` slot templates | (b) | Yes, **with node back-references** — `ScopedDeclaration` holds `Node Declaration` (`Jint/Runtime/Interpreter/DeclarationCache.cs:8`) |
| function nodes' `UserData` → `JintFunctionDefinition.State` | ~30 fields: parameter names, var/lexical names, `FunctionsToInitialize`, `AnnexBFunctionDeclarations`, fixed-slot layout, six precomputed dispatch-eligibility flags, `SourceText` | (b) | Yes in principle, **worst case in practice** — see §2.3 |
| `Script` root `UserData` → `CachedHoistingScope` | `HoistingScope` (six lists of declaration *nodes*), `List<Key> VarNames`, `DeclarationCache LexNames` | (b) | Yes, with node back-references throughout |
| class nodes' `UserData` → `string` | retained source text, only when `RetainFunctionSourceText` | (a) | Yes — but then the whole source travels with the artifact. Default is `false` |
| `State._cachedSlots`, `State._dynamicCachedEnv`, `State.CtorBodyShapeEligibility`, `State.TailCallMarkersInitialized` | run-time caches mutated after preparation | (c) | **Must not be serialized** — `_dynamicCachedEnv` holds a `FunctionEnvironment`, which roots an engine (issue #2560) |
| `RegExpLiteral.UserData` → `RegExpParseResult` | the per-node compiled-regex memo written on *first evaluation* | (c) | No; recomputed, and backed by the process-wide `RegExpParseCache` |
| identifier binding names, determined member value, script hoisting scope, unary/binary folding under `StaticAnalysis = false` | | (c) | Already rebuilt per engine today; the option's whole point |

**Census on the three probe scripts** — nodes carrying `UserData` after a default `PrepareScript`:

| script | AST nodes | with `UserData` | `BindingName` | `JintConstantExpression` | `JsString` (member) | `BlockState` | function `State` |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| handlebars.js | 27,496 | 17,132 | 7,855 | 5,527 | 2,789 | 451 | 379 |
| linq-js.js | 11,155 | 6,585 | 4,474 | 178 | 1,321 | 133 | 461 |
| dromaeo-3d-cube.js | 2,361 | 1,607 | 763 | 438 | 163 | 43 | 16 |

So a full-fidelity artifact is not "the AST plus a bit of metadata": it is roughly **1.6 objects per AST node**,
the extra ones being Jint-internal types with lists, hash sets, arrays and back-references into the node graph.

### 2.2 Three properties of the current representation that a serializer has to reproduce

- **String identity is load-bearing.** The analyzer builds `BindingName`s from *the parser's own deduplicated
  string instances*, with an explicit comment saying why (`Jint/Engine.Ast.cs:168-174`): environment storage is
  keyed by `Key`s made from the same parse, so `Key` comparison stays on `string.Equals`'s reference-equality fast
  path. A deserializer must therefore emit a string table and hand out the *same* instance everywhere. This is not
  a blocker — a string table does this better than the parser does — but a naive per-node `ReadString()` would
  silently move every property lookup onto the `memcmp` path.
- **Node identity is load-bearing.** `ScopedDeclaration.Declaration`, `HoistingScope._functionDeclarations`,
  `State.FunctionsToInitialize`, `State.AnnexBFunctionDeclarations` all reference nodes by identity, and
  `AnnexBFunctionDeclarations` is a `HashSet<FunctionDeclaration>` used precisely to *distinguish same-named
  declarations at different block levels*. The format needs node ids and a two-pass reader (materialize, then
  wire). Standard, but it is exactly the part that makes hand-written serializers rot.
- **`UserData` mutation after load is expected.** `JintFunctionDefinition.Initialize` publishes `State` under a
  `lock (node)`; `JintLiteralExpression` writes the regex memo; `JintBlockStatement` does `UserData ??=`. A
  deserialized tree must land in a state where those lazy publications still work, i.e. the shared-AST invariant
  in AGENTS.md ("nothing engine-affine in `UserData`") has to hold for the *deserialized* graph too. The
  `StaticAnalysis = false` path already proves this is reachable — it is the same contract.

### 2.3 `JintFunctionDefinition.State` is the real cost centre

`Jint/Runtime/Interpreter/JintFunctionDefinition.cs:427-583` is a ~30-field class, and about a third of those
fields are *derived dispatch decisions* — `CanUseFastFDI`, `CanUseEmptyFDI`, `CanSkipThisBinding`,
`SupportsLeafCall`, `SupportsRegisterCall`, `EnvironmentMayEscape`, `IsDirectRecursive`, `UseFixedSlots` — each
computed by its own AST scan (`EnvironmentEscapeAstVisitor`, `SelfCallAstVisitor`,
`ComputeCtorBodyShapeEligibility`). Every one of them is a *performance* decision that the interpreter team
changes routinely; several were added in the last few release cycles.

That has a consequence the checkbox does not anticipate. Serializing `State` **freezes Jint's internal
optimization decisions into an on-disk format**. Every change to the eligibility rules — every new fast lane, of
which the register-argument and leaf-call lanes are recent examples — is either a format-version bump that
invalidates every cached artifact, or a correctness bug where a stale artifact claims a lane the current engine's
rules would refuse. The safe design is the version bump, which means **the cache is invalidated on every Jint
upgrade** anyway.

---

## 3. Acornima's serialization story

Checked against `Acornima` / `Acornima.Extras` **1.7.0** (the pinned version in `Directory.Packages.props`), by
reflecting over the shipped `net8.0` assemblies.

### 3.1 What exists

| API | Direction | Verdict |
| --- | --- | --- |
| `Acornima.AstToJson.ToJson/WriteJson` | AST → JSON text | Exists, one-way |
| `Acornima.AstToJavaScript.ToJavaScript/WriteJavaScript` | AST → JS source | Exists, one-way |
| `Acornima.AstVisitor` / `Acornima.AstRewriter` | tree walk / structural rewrite | Exists |
| **JSON → AST**, or any reader | — | **Does not exist**, in either assembly, public or not |

So Acornima's "serialization story" today is *write-only*. There is no `JsonToAst`, no `AstReader`, no binary
format, and no round-trip test corpus to inherit.

### 3.2 What is nevertheless publicly reconstructible

This is the good news, and it contradicts the pessimistic framing in the issue:

- **Every concrete `Node` subtype has a public constructor** — 118 public types in `Acornima.Ast`, 84 with public
  constructors (the rest are abstract bases, interfaces and enums), and *zero* concrete node types without one.
  The only two concrete types with no public constructor are `ChildNodes` and `NodeList<T>`, neither of which is
  a node, and both of which are built through the public `NodeList.From`/`Create`/`Empty` factories.
- **`Node.Range` and `Node.Location` are `init`-only with public setters**, so a reader sets them in the object
  initializer at construction: `new Identifier(name) { Range = …, Location = … }` compiles. (`Range`, `Position`
  and `SourceLocation` are built through static `From` factories, not constructors.)
- **`Node.UserData` has a plain public setter**, so Jint's analyzer output can be re-attached.
- **`Statement.LabelSet` has no public setter and its backing field `_labelSet` is `internal`** — and Jint is not
  in Acornima's `InternalsVisibleTo` list (only `Acornima.Benchmarks`, `Acornima.Extras`, `Acornima.Tests`). This
  looked like a hard blocker, because `continue label` correctness depends on it: `JintForStatement.cs:722` and
  `JintForInForOfStatement.cs:935` both decide whether to keep looping by comparing `result.Target` against
  `_statement?.LabelSet?.Name`. **It is not a blocker.** `LabeledStatement`'s constructor writes the label into
  its body, so a reader that builds `new LabeledStatement(label, body)` gets it for free — proved directly in
  §9.4, where a from-scratch `ForStatement` reports `LabelSet == null` before being wrapped and `outer` after.

The remaining "properties without a matching constructor parameter" found by the probe are all false positives —
derived properties (`Operator` on the operator-typed subclasses), by-ref `NodeList` parameters under a different
name, and `LabelSet` itself.

**Conclusion for option (A): the API surface is not the obstacle.** A reader would be a large hand-written switch
over `NodeType` plus per-type discriminators, entirely reflection-free (so AOT-clean), with no internal member
required.

### 3.3 The version-coupling problem is real, though

The format encodes Acornima's `NodeType` enum *and* the per-type field layout. Acornima adds node types as
TC39 features land, and Jint bumps Acornima regularly (1.7.0 today; thirteen 1.x releases have shipped).
Every bump is a potential format break. Combined with §2.3's Jint-side churn, the artifact
must be stamped with `(Jint version, Acornima version, parsing-options hash, source hash)` and **rejected, not
migrated**, on any mismatch — falling back to `PrepareScript`. That is the right design, and it means the cache is
cold after every dependency update.

---

## 4. The cost actually being avoided

**Measurement caveat, stated plainly.** These are single-process `Stopwatch` probes, minimum of 7–15 timed
repetitions after warm-up, on a machine running many concurrent agent workloads. They are **not** benchmark
claims and no BenchmarkDotNet job was run — AGENTS.md reserves quotable figures for a gated (`JINT_BENCH_MODE=gate`)
run on a verified-idle machine, and this was neither.
Medians drifted by 2–3× between runs under that load; minima were stable to roughly ±20 %. Treat every figure as
order-of-magnitude, and treat the *ratios within one run* as far more trustworthy than the absolute times.

Scripts: `Jint.Benchmark/Scripts/`. `.NET 10.0.11`, workstation GC, Release.

### 4.1 Where preparation time goes

| script | source | AST nodes | `PrepareScript` | `StaticAnalysis=false` | bare `ParseScript` | tokenize only | analysis share |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| handlebars.js | 184,515 ch | 27,496 | **12.3 ms** | 5.6 ms | 4.8 ms | 3.3 ms | 54–55 % |
| linq-js.js | 34,289 ch | 11,155 | **2.8 ms** | 1.0 ms | 1.0 ms | 1.0 ms | 64–67 % |
| dromaeo-3d-cube.js | 9,008 ch | 2,361 | **0.48 ms** | 0.21 ms | 0.22 ms | 0.21 ms | 54–56 % |

(One of the four runs — the most contaminated one — reported 39 % for linq-js, against 64 / 64 / 67 % in the
other three. It is quoted here only so the outlier is on the record, not averaged in.)

Two things fall out immediately.

- **The static-analysis pass is about half of preparation, sometimes two thirds.** This matches what
  `ScriptPreparationOptions.StaticAnalysis`'s own doc comment already claims from the module-graph benchmark
  (−47.6 % preparation time). An AST-only serializer attacks the *other* half.
- **Tokenization is 66–100 % of a bare parse.** On the two smaller scripts the standalone tokenizer loop costs
  essentially as much as the full parse. Whatever else that says about the tokenizer's standalone path, it
  forecloses option (B) — see §5.2.

### 4.2 The cold-start context

| script | `new Engine()` | `new Engine()` + `Execute(prepared)` | prepare, as a multiple of the run |
| --- | ---: | ---: | ---: |
| handlebars.js | 0.003 ms | 3.6 ms | ≈ 3.4× |
| linq-js.js | 0.003 ms | 0.38 ms | ≈ 7.4× |

Engine construction is free. For a large library script, **preparing it costs several times more than running
it once** — which is the honest case *for* the feature, and the reason the checkbox exists.

Measuring the whole cold path end to end — prepare a *fresh* tree, build a fresh engine, run the script once —
qualifies summary claim 3 in a way worth being explicit about:

| script | prepare(full) + Engine + Execute | prepare(`StaticAnalysis=false`) + Engine + Execute | delta |
| --- | ---: | ---: | ---: |
| handlebars.js | 14.9 ms | 14.2 ms | **−4 %** |
| linq-js.js | 3.3 ms | 1.7 ms | **−49 %** |

**`StaticAnalysis = false` is a cold-start win only in proportion to how much of the script never runs.**
linq-js defines a large library and executes almost none of it, so the analysis the option skipped is never
paid at all. handlebars executes most of its top level, so the option merely *defers* the work into the run and
the cold path barely moves. Any host reaching for the option on cold-start grounds should measure its own
script's shape rather than assume the documented module-graph figure. This is also the one shape where a
serializer would beat the existing knob — and §4.4 is what it would beat it by.

### 4.3 What a deserializer would cost — measured floors, not guesses

Three floors, each strictly below what a real reader pays:

**(i) Bare allocation.** Allocating one `Identifier` per AST node, with `Range` and `Location` set, from a
pre-built string array: **0.42 ms / 27,496 nodes** on handlebars (3 % of preparation). This is the absolute
minimum for "make N small objects" and is unrealistically cheap — real nodes are larger and carry `NodeList`
children.

**(ii) Real object-graph materialization.** Rebuilding the *actual* tree from its *live* children through the
public constructors, using `AstRewriter` forced to replace every `Identifier` so `UpdateWith` propagates upward.
No decoding, no string work, no `UserData`, no `NodeList` construction from scratch:

| script | rebuild | fraction of tree actually rebuilt | extrapolated to 100 % | as % of `PrepareScript` |
| --- | ---: | ---: | ---: | ---: |
| handlebars.js | 1.6–1.9 ms | 65.4 % | ≈ 2.5 ms | **≈ 20 %** |
| linq-js.js | 0.78–0.87 ms | 95.5 % | ≈ 0.85 ms | **≈ 29 %** |
| dromaeo-3d-cube.js | 0.13–0.22 ms | 78.6 % | ≈ 0.19 ms | **≈ 35 %** |

**Materializing the AST object graph alone already costs a fifth to a third of the whole preparation**, before
reading any bytes, before interning any string, and before building a single one of the 17,132 analyzer objects
handlebars needs.

**(iii) Decoding, for a JSON encoding specifically.** `AstToJson` on handlebars produces **1,447,245 bytes — 7.8×
the source** (and 12.9×/16.7× on the two denser scripts, which have fewer source characters per node). Merely
*scanning* that with `JsonDocument.Parse` — which builds no nodes at all — costs **7.7–13.0 ms**, i.e. roughly
what `PrepareScript` costs outright. **A JSON-based format is a guaranteed net loss** and needs no further
analysis.

### 4.4 Adding it up

For a compact binary format with a string table, on handlebars.js (12.3 ms to prepare):

| phase | estimate | basis |
| --- | ---: | --- |
| read ~27.5 k node records + string table | 0.5–1.5 ms | span-based reader over ~400–600 kB, estimate |
| materialize AST object graph | ≈ 2.5 ms | **measured** (§4.3 ii) |
| materialize ~17 k analyzer objects (lists, sets, arrays, back-reference wiring) | 2–5 ms | estimate, scaled from (ii) by object count |
| recompile regex literals | unavoidable | `RegexOptions.Compiled` IL emission, not cacheable across processes |
| **total** | **≈ 5–9 ms** | vs **12.3 ms** to prepare from source |

So the optimistic saving is **≈ 3.5–7 ms per process, on a 184 KB script** — call it 30–55 % of preparation, and
about 25–45 % of the whole cold path once the single run is included. On the 34 KB script the saving is under
2 ms. These are the *upper* bounds; the estimates in the table are the ones that could only move down.

---

## 5. Options compared

### 5.1 (A) Full AST + analyzer-output binary serialization

**Feasible. Not blocked by any API.** §3.2 clears every suspected blocker: public constructors everywhere,
`Range`/`Location` settable in an initializer, `UserData` settable, `LabelSet` derived by the `LabeledStatement`
constructor, no reflection needed (AOT-clean).

Blockers that *do* exist are none of the ones expected:

- **`Prepared<Script>`'s constructor is `internal`**, and no public API turns a `Script` into a `Prepared<Script>`
  (`Engine.Execute`/`Evaluate` take `in Prepared<Script>` only). So the serializer cannot live outside Jint even
  in principle — this is a Jint feature or nothing.
- **There is no standalone analyzer.** `AstAnalyzer` is installed as `parserOptions.OnNode`
  (`Engine.Ast.cs:100`) and only ever runs *during* a parse. Any variant that re-analyzes on load needs a
  tree-walking analyzer to be written first. That is genuinely easy — each arm of the `switch` is self-contained
  and needs only the node and the source text — and it is separately useful ([§8](#8-what-to-do-instead)), but it
  does not exist today.
- **The regex literals must be recompiled** on every load, in every process, because prepared scripts default to
  `RegexCompilation.Compiled` and a compiled `Regex` is emitted IL.
- **`State`'s ~30 fields encode Jint's current dispatch-eligibility rules** (§2.3), so the format is coupled to
  interpreter internals that change every release.

Cost: a two-pass reader/writer over 118 node types plus ~10 internal Jint types, node-id and string tables,
a version stamp with hard rejection, a round-trip fuzz corpus (structural equality of the rebuilt tree *and*
behavioural equality of what the engine does with it), and a permanent maintenance obligation on a file that must
be updated in lockstep with both Acornima bumps and interpreter changes. Realistically 2,000–4,000 lines plus
tests, forever.

Payoff: §4.4 — a few milliseconds, once per process.

### 5.2 (B) Serialize source, skip the re-parse via a cached tokenization

**Rejected on the numbers, as the task suspected.** §4.1: tokenization is **66 % of a bare parse on handlebars and
97–101 % on the two smaller scripts**. There is no meaningful "parse minus lex" residue to buy back. Even taking
the most favourable measurement, the ceiling is a third of a bare parse — which is itself only 39 % of
`PrepareScript` on handlebars — so the ceiling on the whole idea is ≈ 13 % of preparation, *before* paying to
decode a token stream that is necessarily larger than the source it came from. Every plausible decoder eats more
than the ceiling. Dead.

### 5.3 (C) Get it from Acornima upstream

Acornima has the write half and not the read half (§3.1). The ask is small and well-shaped, because Acornima
already generates `AstVisitor`, `AstRewriter` and `AstToJson` from an internal node model — a reader is the same
model traversed the other way.

**Draft ask (one issue, `adams85/acornima`):**

> **Add an AST reader to match `AstToJson`.** Acornima can write an AST (`AstToJson`, `AstToJavaScript`) but
> cannot read one back, so a consumer that wants to persist a parsed tree has to hand-write a switch over all
> 118 `Acornima.Ast` types and keep it in lockstep with every release.
>
> Requested, in preference order:
> 1. `Acornima.AstReader` / `AstWriter` — a compact, versioned binary round-trip generated from the same node
>    model that generates `AstVisitor`/`AstRewriter`, with a format-version constant a consumer can pin.
> 2. Failing that, `JsonToAst` as the exact inverse of `AstToJson`.
> 3. Failing both, just a **public factory** `Node Create(NodeType type, ReadOnlySpan<…> children, …)` — or the
>    generator's node-model metadata exposed — so a consumer's reader is generated rather than hand-maintained.
>
> Notes from a consumer's audit of 1.7.0, which the above should preserve: every concrete node type already has
> a public constructor; `Range`/`Location` are `init`-only and reachable from an object initializer;
> `LabeledStatement`'s constructor already propagates `_labelSet` into its body, which is what makes faithful
> reconstruction possible from public API at all — please treat that as contract, not incidental.
> `RegExpLiteral` needs its `RegExpParseResult` supplied by the caller, which is correct — the consumer
> recompiles.

**But even a perfect upstream reader does not make (A) worth doing**, because it addresses only the parse half.
Jint would still hand-serialize `JintFunctionDefinition.State`, `BlockState`, `CachedHoistingScope` and the
node back-references between them — which is where both the bulk (§2.1: 1.6 objects per node) and all of the
version fragility (§2.3) live. Worth filing as a cheap, non-blocking upstream nicety; not worth waiting for.

### 5.4 (D) Do nothing — what already amortizes cold start

The existing mechanisms, in the order a host should reach for them:

1. **`Prepared<Script>` is already shareable and thread-safe across engines** — documented on
   `Engine.PrepareScript`, pinned by `Jint.Tests.CommonScripts/ConcurrencyTest.cs` and by
   `GarbageCollectionTests.SharedPreparedScriptDoesNotRetainEngines`. A host that prepares once per *process* and
   pools engines pays preparation once, no matter how many requests that process serves. This is the whole ball
   game for serverless: a warm Lambda/Functions instance serves thousands of invocations, so 12 ms amortizes to
   microseconds.
2. **`Engine.Advanced.RestoreGlobalSnapshot`** deliberately preserves `_scriptStatementLists`,
   `_functionDefinitions` and `_evaluatedScripts` while resetting globals (`GlobalSnapshotInternalsTests`), so a
   pooled engine keeps its *warm handler trees* across requests, not just the prepared AST. This is a strictly
   bigger win than anything an AST serializer could offer, and it already ships.
3. **`ScriptPreparationOptions.StaticAnalysis = false`** removes 54–67 % of preparation time (§4.1) at the price
   of ~5 % per engine materializing the tree. One line of host code — but read §4.2 first: on the cold path it
   only wins in proportion to how much of the script never runs.
4. **Lazy preparation.** A host with hundreds of scripts (the OrchardCore shape) should prepare on first use, not
   at startup. Costs nothing to implement host-side, and no engine feature can beat "don't do the work".

**When cold start actually bites.** The genuine residual cases, ranked:

| case | is it real? |
| --- | --- |
| Serverless instance recycle, warm instance serves many requests | **No.** Preparation amortizes to nothing; and .NET runtime + assembly load (hundreds of ms) dwarfs a 12 ms prepare in the cold path that remains |
| Serverless with a *very* large bundle (≥ 1 MB of script) | **Marginal.** Extrapolating §4.1 linearly, ~1 MB ≈ 70 ms to prepare; a serializer might save ~25–35 ms of it, once per instance |
| Host that must prepare hundreds of scripts at startup | **No** — fixed by lazy preparation, item 4 above |
| Host that genuinely cannot keep a warm process (CLI tool, per-invocation container) | **Yes, but small.** This host also pays full .NET startup every time, and preparation is the smaller term |
| Constrained device where a prepared artifact is built on a *different, faster* machine | **Yes** — and this is the only case where the feature is qualitatively rather than quantitatively better. Not a case anyone has asked for |

Break-even, put bluntly. The saving is **per process start**, not per request, so it scales with how often the
host starts a process — and the two ends of that range are both uninteresting. A host that starts a process per
request saves ~4 ms on a path already carrying hundreds of milliseconds of .NET runtime and assembly-load cost,
so under 2 %. A host that starts one process per thousands of requests saves ~4 ms, total, ever. There is no
regime in between where the number becomes interesting, because the term the serializer removes does not grow
with traffic.

### 5.5 The strategic argument, which outweighs all of the above

The obvious precedent is V8's code cache, and it is instructive in the wrong direction for this proposal. **V8
caches bytecode — a flat, position-independent byte array — not an AST.** Decoding it is a `memcpy` plus fix-ups,
which is why the trade works there. Jint has no such artifact: its "compiled" form is the per-engine handler tree
(`JintStatement`/`JintExpression` graphs in `Engine._scriptStatementLists` / `_functionDefinitions`), which is
explicitly engine-affine, carries per-node inline caches, and can never be shared or persisted.

So the artifact Jint *would* persist is the intermediate one — an object graph whose materialization cost is
already a fifth to a third of building it from scratch (§4.3). That is precisely the shape where caching does not
pay, and it is the same reason `StaticAnalysis = false` exists: for tree-shaped intermediate state, redoing the
work is competitive with moving it.

If Jint ever gains a bytecode or flat-IR representation — the known next lever for the interpreter — *that* is the
artifact worth persisting, and the format is then trivially serializable by construction. Building an AST
serializer today would commit the project to maintaining, versioning and testing a persistence format for a
representation we would want to replace, and would make the bytecode transition harder rather than easier.

---

## 6. Recommendation

**Wontfix as specified.** Close the checkbox with a link to this document.

The reasoning, in one line each:

- It is not blocked — it is simply not worth the milliseconds it buys (§4.4).
- The half it can most easily attack is the smaller half (§4.1), and the bigger half is Jint-internal state that
  freezes interpreter optimization decisions into a file format (§2.3).
- A JSON encoding is a measured net loss (§4.3 iii); a binary one saves single-digit milliseconds once per process.
- Three shipping mechanisms already amortize the cost to zero for every host that keeps a process warm (§5.4).
- The artifact worth persisting is bytecode, which Jint does not have yet (§5.5).

---

## 7. If it is pursued anyway — a phased plan

Included so the decision is reversible on evidence rather than on re-litigation. Each phase is independently
useful and independently abandonable, and **each has an explicit kill criterion**.

**Phase 0 — standalone analyzer (useful regardless).** Extract `AstAnalyzer`'s per-node `switch` into an internal
tree-walking pass that visits a parsed `Program` in post-order (the order `OnNode` fires) and publishes the same
`UserData`. `Engine.PrepareScript` then becomes "parse, then analyze" instead of "parse *with* a visitor". Value
on its own: it makes `StaticAnalysis` a post-hoc choice, it lets a host analyze a tree it transformed itself, and
it is the prerequisite for every later phase. *Kill criterion:* if separating the pass costs more than ~5 % of
preparation time versus the fused visitor, stop here and keep the fused form.

**Phase 1 — round-trip harness, no format.** A test-only structural comparer that proves an AST rebuilt through
public constructors is indistinguishable from the parsed one, run over the whole `Jint.Tests.Test262` corpus.
This is where the real risks surface (node types with hidden derived state, `NodeList` edge cases, tolerant-mode
trees). *Kill criterion:* any node type that cannot be reconstructed faithfully from public API and cannot be
fixed upstream.

**Phase 2 — AST-only binary format, behind an explicitly experimental API.** `Engine.SerializeAst` /
`Engine.PrepareScriptFromAst`, with a `(Jint version, Acornima version, options hash, source hash)` stamp and
hard rejection on mismatch. Load path = decode + Phase 0's analyzer. *Kill criterion — the important one:*
measure the real load path against `PrepareScript` with `measure-paired.ps1` on at least handlebars-scale input.
**Per §4.4 this phase is predicted to be a wash or a small loss; if it is, stop.**

**Phase 3 — analyzer-output serialization.** Only if Phase 2's measurement beat the prediction. Node-id table,
two-pass reader, and the full `State`/`BlockState`/`CachedHoistingScope` graph. Requires a policy for §2.3's
version coupling *before* a line is written. *Kill criterion:* anything less than a 2× improvement on the whole
cold path is not worth the permanent maintenance.

**Non-negotiables for any phase.** No reflection (AOT). No `InternalsVisibleTo` dependency on Acornima. Nothing
engine-affine ever reaches `UserData` — a deserialized tree is a *shared* tree and inherits the AGENTS.md
invariant verbatim, including `ParseOnlyPreparationRaceTests`-style concurrent-publication safety. Version
mismatch rejects and falls back; it never migrates.

---

## 8. What to do instead

Two cheap, concrete deliverables that address the actual embedder pain, which is not "preparation is slow" but
"nobody told me preparation is a per-process cost".

1. **A cold-start section in `README.md`** (or in the existing embedding guidance): prepare once per process and
   share the `Prepared<T>` across engines (with the thread-safety guarantee stated); pool engines and use
   `RestoreGlobalSnapshot` to keep warm handler trees; reach for `StaticAnalysis = false` when engines outnumber
   preparations; prepare lazily when there are many scripts. Every one of these ships today; the measurements in
   §4 are exactly the evidence to quote. **This is where the wins are, and it costs a documentation PR.**
2. **File the Acornima ask from §5.3** as a non-blocking upstream issue. It costs nothing, it is genuinely useful
   to other Acornima consumers, and it removes the largest single chunk of work from any future revisit.

Optionally, **Phase 0 alone** (§7) is worth doing on its own merits — it decouples analysis from parsing and makes
`StaticAnalysis` a post-hoc decision — but it should be justified by that, not by this feature.

---

## 9. Probe method and raw numbers

### 9.1 Method

A throwaway `net10.0` console project referencing `Jint/Jint.csproj` and `Acornima.Extras` 1.7.0, Release,
workstation GC. **Not committed.** For each script: three warm-up preparations, then `Stopwatch` timings, N = 15
(N = 7 for the execute/cold-path rows), reporting the **minimum** — under concurrent load on this machine the
median is contaminated (handlebars' `PrepareScript` median ranged 25–43 ms across runs while its minimum stayed
12–17 ms), and the minimum is the least-contaminated estimator available without a proper BenchmarkDotNet job.
Four full runs were taken; the tables above quote representative minima and the ranges observed.

`AstRewriter` was subclassed to replace every `Identifier` with a fresh instance, which forces `UpdateWith` to
rebuild every ancestor; the fraction of the tree actually rebuilt was then measured by walking the original and
the rebuilt trees in parallel and counting reference-identical subtrees, so the rebuild timing could be scaled to
a whole-tree figure.

Reflection over `Acornima.dll` / `Acornima.Extras.dll` (1.7.0, `net8.0`) produced the public-surface findings in
§3.2 — public-constructor coverage, the `init`-only setters on `Range`/`Location`, the `internal _labelSet` field,
and Acornima's `InternalsVisibleTo` list.

### 9.2 Preparation and parse (minima, ms)

```
===== handlebars.js  (184,515 chars) =====
  PrepareScript (StaticAnalysis=true) : min    12.27 ms  median    42.78 ms
  PrepareScript (StaticAnalysis=false): min     5.62 ms  median     5.87 ms
  bare Parser.ParseScript             : min     4.84 ms  median     5.77 ms
  tokenize only (no AST)              : min     3.32 ms  median     3.60 ms
  => analysis share of prepare        :   54.2 %
  => tokenizer share of bare parse    :   68.5 %
  AST nodes                           : 27,496   ( 6.7 source chars/node)
  nodes carrying UserData             : 17,132

===== linq-js.js  (34,289 chars) =====
  PrepareScript (StaticAnalysis=true) : min     2.84 ms
  PrepareScript (StaticAnalysis=false): min     1.03 ms
  bare Parser.ParseScript             : min     1.02 ms
  tokenize only (no AST)              : min     0.99 ms
  AST nodes                           : 11,155   ( 3.1 source chars/node)
  nodes carrying UserData             :  6,585

===== dromaeo-3d-cube.js  (9,008 chars) =====
  PrepareScript (StaticAnalysis=true) : min     0.48 ms
  PrepareScript (StaticAnalysis=false): min     0.22 ms
  bare Parser.ParseScript             : min     0.22 ms
  tokenize only (no AST)              : min     0.22 ms
  AST nodes                           :  2,361   ( 3.8 source chars/node)
  nodes carrying UserData             :  1,607
```

### 9.3 Serialization-shaped costs (minima, ms)

```
handlebars.js
  AstToJson size                       : 1,447,245 bytes ( 7.8x source)
  JsonDocument.Parse of that           :     7.72 - 12.99 ms   (builds no nodes)
  allocate 27,496 Identifier nodes     :     0.38 ms  (  3.0 % of full prepare)
  rebuild whole tree via public ctors  :     1.65 ms  ( 13.4 % of prepare; 65.4 % of the tree rebuilt)
  new Engine()                         :     0.003 ms
  new Engine() + Execute(prepared)     :     3.55 ms

linq-js.js
  AstToJson size                       :   571,120 bytes (16.7x source)
  JsonDocument.Parse of that           :     2.54 ms
  rebuild whole tree via public ctors  :     0.78 ms  ( 27.1 % of prepare; 95.5 % of the tree rebuilt)
  new Engine() + Execute(prepared)     :     0.38 ms

dromaeo-3d-cube.js
  AstToJson size                       :   116,150 bytes (12.9x source)
  JsonDocument.Parse of that           :     0.44 ms
  rebuild whole tree via public ctors  :     0.13 ms  ( 26.5 % of prepare; 78.6 % of the tree rebuilt)
```

### 9.3b Whole cold path (minima, ms; from the run that produced §4.2's table)

```
handlebars.js
  new Engine() + Execute(prepared)                : min     3.65 ms
  COLD prepare(full)+Engine+Execute               : min    14.85 ms  median   18.27 ms
  COLD prepare(StaticAnalysis=false)+Engine+Exec  : min    14.22 ms  median   16.14 ms  (  -4.2 % vs full)

linq-js.js
  new Engine() + Execute(prepared)                : min     0.38 ms
  COLD prepare(full)+Engine+Execute               : min     3.33 ms  median    4.34 ms
  COLD prepare(StaticAnalysis=false)+Engine+Exec  : min     1.71 ms  median    1.72 ms  ( -48.8 % vs full)
```

### 9.4 The label-set probe (the one suspected blocker, disproved)

Source: `outer: for (var i = 0; i < 3; i++) { inner: for (var j = 0; j < 3; j++) { if (j === 1) continue outer; } }`

```
  Statement.LabelSet present: original 2, rebuilt 2
  engine still evaluates the ORIGINAL tree correctly: i ends at 3
  rebuilt LabeledStatement is a new instance : True
  rebuilt body is a new instance             : True
  original body.LabelSet                     : outer
  UpdateWith-rebuilt body.LabelSet           : outer
  from-scratch ForStatement.LabelSet BEFORE wrapping : <null>
  from-scratch ForStatement.LabelSet AFTER  wrapping : outer
```

The last two lines are the finding: a `ForStatement` built from scratch through its public constructor has no
label set, and acquires the correct one the moment it is passed to `new LabeledStatement(label, body)`. A reader
therefore reconstructs labels correctly without touching Acornima internals — but it must build the
`LabeledStatement` *around* its body rather than patching the body afterwards, and that ordering constraint should
be captured in a test if this is ever built.

### 9.5 Acornima 1.7.0 public-surface findings

```
Acornima.Ast public types                 : 118   (includes abstract bases, interfaces and enums)
  with a public constructor               :  84
  concrete without a public constructor   :   2  (ChildNodes, NodeList<T> - neither is a Node; both have
                                                  public factories: NodeList.From/Create/Empty)
  concrete Node subtypes without one      :   0
Node.Range / Node.Location                : public getter, init-only setter (object initializer works)
Node.UserData                             : public getter and setter
Statement.LabelSet                        : public getter, no setter; backing field `_labelSet` is internal
Acornima InternalsVisibleTo               : Acornima.Benchmarks, Acornima.Extras, Acornima.Tests  (not Jint)
Acornima.Extras: AstToJson, AstToJavaScript, AstVisitor, AstRewriter   -- write/traverse only
Acornima.Extras: any JSON-or-binary -> AST reader                      -- absent
```
