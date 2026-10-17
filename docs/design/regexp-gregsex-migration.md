# RegExp on GregsEx — migration record

**Status: implemented on `sebros/jint-gregsex-regex-engine-a5ccbe`.** Every regular expression written in script runs
on the ECMAScript flavor of [GregsEx](https://github.com/sebastienros/gregsex), vendored by `tools/sync_gregsex.py`
from its `main` branch (first `d989dd84`, now `a12c3ce8`). The QuickJS libregexp port and the Acornima-to-.NET `Regex`
adaptation are gone. This document records the decision, how compliance was held, what the work surfaced in
either project, and what performance it bought.

## 1. Which of the three options, and why

| Option | Verdict |
| --- | --- |
| Reference the `GregsEx` package | **Not viable.** The package ships `lib/net10.0` only, and Jint targets `net472`, `netstandard2.0`, `netstandard2.1`, `net8.0` and `net10.0`. Its public surface also stops above what a JavaScript engine needs: no group names before a match, and no executor entry below the mutable `EcmaScriptMatcher` session, which owns `lastIndex` itself where Jint's `RegExpBuiltinExec` must. |
| Vendor GregsEx whole | **Not worth it.** 77K lines across six flavors plus FastExpressionCompiler, almost none of it ECMAScript. |
| **Copy the ECMAScript closure** | **Chosen.** About 9.6K lines of logic plus the Unicode 17.0 tables, under `Jint/Runtime/RegExp/GregsEx/` with GregsEx's folder layout kept. A sync is mechanical: `tools/sync_gregsex.py` takes the pinned upstream files, renames the namespace, makes the types internal, then applies `tools/gregsex-sync/jint.patch`. It keeps no state, so whether there is anything to take is just whether `git diff` is empty: `.github/workflows/gregsex-sync.yml` regenerates weekly and opens a pull request only then. What the patch holds is in that folder's `README.md`. |

The downlevel port needed only polyfills (`ThrowIfNull`, `ThrowIfNegative[OrZero]`, generic `Enum.IsDefined`,
`span.Contains(char)`, `IndexOfAny(SearchValues<char>)`) and one `#if NET9_0_OR_GREATER` around
`SearchValues<string>`. The `static abstract` reader interfaces that block `netstandard2.0` belong to the generic
RegularCore path, which the ECMAScript executor never touches, so they were dropped rather than ported.

## 2. What changed in Jint

- `JintRegExpEngine` wraps the GregsEx executor. Matchers are immutable and shared: `RegExpParseCache` holds them
  process-wide (keyed by pattern and flags), literals memoize theirs on the AST node, and mutable executor storage is
  per-thread scratch. A backtracking reservation starts at 64 frames and grows on demand up to 4M.
- The timeout is read at match time from the source's conversion options or `Constraints.RegexTimeout`, as before;
  a match that overruns it throws `RegexMatchTimeoutException`, as before.
- `RegExpConstructor.NeedCustomEngine`, the case-folding hazard analysis, the per-subject fallback engine and
  `RegexCompilation` are deleted: there is one engine.
- Fused lanes over capture spans: `exec` builds its result array directly (captures are slices of the subject, as
  the .NET lane did), replace with a function or a `$`-template collects every match before calling a replacer (the
  specification's order), split follows `SplitMatcher` as a forward search, and the range-only lanes (`test`,
  `search`, global `match`, plain-string replace) ask the executor for no captures at all.
- **Breaking:** `JsRegExp.Value`, `JsRegExp.ParseResult` and `CompileRegex` on the parsing options are removed; a
  script `RegExp` converts to the CLR as the `JsRegExp` itself; a host `Regex` keeps .NET semantics unless
  `Options.Interop.RegexConversion = RegexConversionMode.EcmaScript`. All of it is in
  [`docs/guide/migrating-to-v5.md`](../guide/migrating-to-v5.md) §2 and §4.155.

## 3. Compliance

| Suite | `main` (`82b0efbaa`) | this branch |
| --- | --- | --- |
| test262 (`7ab7fafa`, all five sub-directories, both modes) | 102,653 passed, 0 failed, 87 skipped | 102,653 passed, 0 failed, 87 skipped |
| `Jint.Tests` (net10.0) | all pass | all pass, plus 20 new regression cases (`RegExpEngineRegressionTests`) |
| `Jint.Tests.PublicInterface` (net10.0) | all pass | all pass; baselines accepted for the intended API delta |
| GregsEx ECMAScript corpora, run through Jint against their Node 24.4.1 / V8 13.6 oracle (oracle 939, imported test262 exec 1,381, contract 27) | 2,342 of 2,346 run, 1 skipped | 2,342 of 2,346 run, 1 skipped — the same four failures |

None of the 87 skipped test262 cases is regex-related, and test262's settings carry no RegExp exclusion, so there was
no skipped Jint regex corpus to re-enable. The closest thing is GregsEx's own test262 dispositions, which exclude 76
files as "JS object boundary" (legacy static accessors, `compile`, Symbol dispatch, coercions): Jint runs all of them
in its own test262 suite, and they pass.

The four failures and the skip are the same on both builds:

- `ES-R-inside-pair-u`, `ES-R-inside-pair-v`, `ECMA-C018`: `/./uy` with `lastIndex` pointing inside a surrogate pair.
  V8 matches at index 0; Jint returns `null`, before and after. GregsEx's corpus calls the case disputed.
- `ECMA-C024`: a harness artefact (`String(-0)` prints `0`).
- One contract case is skipped because it sets GregsEx resource limits that have no JavaScript meaning.

## 4. What the work surfaced

### 4.1 In GregsEx (all fixed upstream in [sebastienros/gregsex#2](https://github.com/sebastienros/gregsex/pull/2), and pinned by a test on both sides)

The same pull request made every GregsEx source and generator output `#nullable enable`, so the copied files no
longer need it added (the sync tool still asserts it), and added `JintRegressionTests` and `NullableContextTests`
to GregsEx's suite. What stays Jint's own after it is structural only: the trimmed `LaneContracts`, the Unicode 17.0
tables alone, and the `#if NET9_0_OR_GREATER` guards (the folder's `README.md`).

| # | Defect | Effect | Fix |
| --- | --- | --- | --- |
| G1 | The literal plans assemble consecutive literal nodes into a UTF-16 string, so under `u`/`v` an escaped lead surrogate followed by a raw trail (`/\uD83D` + U+DC38`/u`) becomes a valid pair. | Wrong match of U+1F438 (`staging/sm/RegExp/unicode-raw.js`). | Skip the literal plans for a Unicode-aware program holding a lone-surrogate literal. |
| G2 | Quantifier bounds above `int.MaxValue` fail `ProgramLimitExceeded` by policy. | ECMAScript admits any DecimalDigits (`built-ins/RegExp/quantifier-integer-limit.js`). | Clamp: a larger minimum is as unsatisfiable as `int.MaxValue`, a larger maximum is unbounded. |
| G3 | The atomic-sequence plan sums step minimums in `int`. | `/.{2147483647}11/` wraps negative and throws `ArgumentOutOfRangeException` (`staging/sm/RegExp/regress-yarr-regexp.js`). | Decline the plan when the sum exceeds `int.MaxValue / 2`. |
| G4 | A greedy repeat of a single-character atom saves one choice per character, each a copy of the task stack and registers. | `/a.*a/` over 64 KiB: ~1.9 ms, against 5 µs for .NET. | One counted choice per run that gives a character (code point under `u`/`v`) back per retry; runs of exact case-sensitive classes are measured with the vectorized `CountRun`. |
| G5 | The accelerated plans refuse a repeat longer than the VM's admitted frames, although they use no frames. | With JavaScript's unbounded backtracking the host grows the reservation and reruns the whole search until it fits (five reruns on a 64 KiB `.*`). | Pass the plans an unbounded frame count. |
| G6 | There is no prefilter for a case-insensitive first atom, or for a pattern starting with nullable atoms. | `/ZZQ/i` over 64 KiB: 4.6 ms against 4 µs for .NET; `split(/\s*,\s*/)` 3× .NET. | A FIRST-set prefilter built from inverted case-folding tables; it never starts a `u`/`v` match inside a surrogate pair. |
| G7 | `RunBudget` takes the slow charge path on every charge whenever a timeout is set, and reads the clock on construction. | 30–50% of a timed scan, and a clock read per search (Jint always sets a timeout). | Fast path until the next scheduled clock read; an absolute-deadline constructor that reads nothing. |
| G8 | The literal plan searches with `IndexOf(string)`. | About 2× slower than .NET Regex over common-letter text. | `SearchValues<string>` on .NET 9+. |
| G9 | Repository: the benchmark comparator `jint-quickjs` compiles `$(JintRoot)/Jint/Runtime/RegExp/**`. | It stops compiling against this branch, and would be benchmarking GregsEx against a copy of itself. | Removed upstream (comparator, `JINT_QUICKJS` blocks and manifest entry); the release report's earlier `jint-quickjs` columns stay as history. |
| G10 | Defaults: Unicode 16.0, a 4096-frame cap, a 10M work limit and ~1.7 MB reserved per matcher session. | Right for a bounded library and wrong for a JavaScript engine (test262 asserts Unicode 17.0). | Not a defect: Jint binds the 17.0 tables, starts at 64 frames, removes the work limit and relies on the regex timeout. |

### 4.2 In Jint

| # | Defect | Effect | Fix |
| --- | --- | --- | --- |
| J1 | The generic `RegExp.prototype[@@replace]` turned an `undefined` capture into `""` before calling a replacer. | Wrong argument to a replacer function (`staging/sm/String/regress-369778.js`); latent before because non-`u` patterns took the .NET fast path. | Keep `undefined` (step 14.k). |
| J2 | The two regex-timeout tests used `^(a+)+$`, which is catastrophic for .NET and not for GregsEx. | They stopped exercising the timeout. | `^(?:a\|a)*b` against `a…a!b`, which is exponential for any backtracking matcher. |
| J3 | The fused lanes (global `match`, `split`, plain-string replace) do not update the legacy `RegExp.$1`… statics. | Pre-existing; unchanged by this work. | Not fixed — noted for a follow-up. |

## 5. Performance

`Jint.Benchmark/measure-paired.ps1`, 3 rounds in alternating order, `main` at `82b0efbaa` against this branch at
`e4d62e219` (the vendored copy is byte-identical at `d16b83758`), on one Apple M4 Pro, .NET 10.0.11, macOS 15. The
41 rows are every regex benchmark the suite has, plus SunSpider `regexp-dna` and Dromaeo `ObjectRegExp`. A row is a
change only when its 95% CI excludes zero; `control-indexOf` never reaches regex code and reads +1.0%, the run's
floor. Three rounds is enough for the large effects; the ±5% ones (Dromaeo below) want six before they are believed
([`Jint.Benchmark/AGENTS.md`](../../Jint.Benchmark/AGENTS.md)).

**Faster**

| Row | Median Δ | 95% CI |
| --- | ---: | --- |
| `RegExpShapeBenchmark.Run[template-replace-g]` | −97.8% | [−97.8, −97.7] |
| `RegExpShapeBenchmark.Run[uprop-match-g]` | −92.1% | [−92.2, −92.0] |
| `RegExpSplitBenchmark.SplitUnicode` | −89.9% | [−90.4, −89.8] |
| `RegExpCustomEngineBenchmark.VariableLength` | −67.5% | [−71.6, −66.0] |
| `RegExpFreshEngineBenchmark.FreshEngineSource` | −61.1% | [−63.0, −61.0] |
| `RegExpSplitBenchmark.SplitWithCaptures` | −47.7% | [−49.1, −47.5] |
| `RegExpCustomEngineBenchmark.NamedCapture` | −44.5% | [−45.0, −43.9] |
| `RegExpShapeBenchmark.Run[capture-match-g]` | −40.3% | [−41.0, −38.4] |
| `RegExpCustomEngineBenchmark.IsMatchOnly` | −38.1% | [−41.7, −37.5] |
| `RegExpCustomEngineBenchmark.GlobalMatchAll` | −37.7% | [−39.9, −37.0] |
| `RegExpCustomEngineBenchmark.LiteralScan` | −36.2% | [−39.2, −35.8] |
| `RegExpMatchBenchmark.MatchGlobalUnicode` | −34.2% | [−36.2, −31.7] |
| `RegExpShapeBenchmark.Run[class-match-g]` | −33.9% | [−35.9, −33.2] |
| `RegExpCustomEngineBenchmark.NoMatchScan` | −32.6% | [−33.3, −32.1] |
| `RegExpCustomEngineBenchmark.CaseInsensitiveScan` | −23.7% | [−24.8, −22.0] |
| `RegExpMatchBenchmark.MatchGlobal` | −23.5% | [−24.9, −22.3] |
| `RegExpShapeBenchmark.Run[construct-100]` | −18.3% | [−21.4, −17.3] |
| `RegExpExecLoopBenchmark.NamedGroupsAccess` | −14.5% | [−18.8, −13.6] |
| `RegExpShapeBenchmark.Run[exec-loop]` | −12.8% | [−14.0, −11.4] |
| `RegExpShapeBenchmark.Run[function-replace-g]` | −12.4% | [−14.3, −12.0] |
| `RegExpExecLoopBenchmark.MatchAllIterate` | −9.9% | [−10.6, −9.2] |
| `RegExpExecLoopBenchmark.ExecWhileLoop` | −9.4% | [−10.2, −3.9] |
| `RegExpExecLoopBenchmark.StickyExec` | −5.1% | [−6.9, −4.1] |

Allocation fell with time on the capture-heavy rows: `template-replace-g` from 177 MB to 0.6 MB per operation,
`SplitUnicode` 25 MB to 1.6 MB, `capture-match-g` 10.5 MB to 2.3 MB, `MatchGlobal` 5.8 MB to 1.4 MB.

**No change:** SunSpider `regexp-dna` −2.6% [−5.8, +0.1] (it was +2,679% before the `FirstStrings` prefilter),
`CharClassScan` +0.9% [−4.3, +5.5], `DigitScan` +3.5% [−0.8, +3.8], Dromaeo `ObjectRegExp[True,False]` +1.8%
[−8.1, +5.7].

**Slower**

| Row | Median Δ | 95% CI | Absolute |
| --- | ---: | --- | --- |
| `RegExpCustomEngineBenchmark.AnchoredLiteral` | +169.1% | [+166.0, +180.1] | 12 ns → 33 ns |
| `RegExpFreshEngineBenchmark.FreshEnginePrepared` | +80.0% | [+79.1, +82.5] | 5.7 µs → 10.1 µs |
| `RegExpShapeBenchmark.Run[nullable-split]` | +72.7% | [+72.1, +79.7] | 406 µs → 701 µs |
| `RegExpShapeBenchmark.Run[char-split]` | +49.2% | [+43.5, +51.5] | 99 µs → 142 µs |
| `RegExpSplitBenchmark.Split` | +30.1% | [+27.7, +35.4] | 615 µs → 786 µs |
| `RegExpShapeBenchmark.Run[greedy-dotstar-test]` | +24.2% | [+22.6, +25.0] | 4.7 µs → 5.7 µs |
| `RegExpShapeBenchmark.Run[greedy-dotstar-exec]` | +22.6% | [+18.8, +24.3] | 4.9 µs → 5.9 µs |
| `RegExpShapeBenchmark.Run[literal-test]` | +18.2% | [+14.0, +20.7] | 3.3 µs → 3.7 µs |
| `RegExpShapeBenchmark.Run[literal-replace-g]` | +17.6% | [+11.4, +17.9] | 3.4 µs → 3.8 µs |
| `RegExpShapeBenchmark.Run[icase-literal-test]` | +6.8% | [+6.5, +8.2] | 3.7 µs → 4.0 µs |
| Dromaeo `ObjectRegExp` `[False,False]` / `[False,True]` / `[True,True]` | +6.5% / +5.0% / +5.8% | [+3.9, +6.6] / [+0.7, +9.8] / [+4.9, +6.4] | ~30 ms |

Every slower row is one where a single search does little work, so a fixed cost per search dominates: the
executor's workspace and run state are set up per call, and split pays it once per piece (`char-split` makes ~2,600
searches). `AnchoredLiteral` is 21 ns of that cost on its own. `FreshEnginePrepared` (+4.4 µs per fresh engine running a
prepared script) has not been profiled yet. The matching itself is faster wherever a search does real
work. The Dromaeo rows are below the three-round bar and need a six-round run before they count.

## 6. Follow-ups

- **Per-search overhead in split and other many-match loops** (`split(/a/)` +49%, `split(/\s*,\s*/)` +73%,
  an anchored literal 12 ns → 33 ns; §5): workspace setup per search, and the VM started per candidate where a plan
  could run the whole loop. Then re-measure Dromaeo `ObjectRegExp` at six rounds.
- **Acornima still validates every regex literal at parse time** (an early error is a parse-time error), so a
  literal is parsed twice. A parse-time validator on the GregsEx parser would remove one pass and one source of
  disagreement; error messages would then need their own text, since GregsEx diagnostics are codes.
- **`Jint/Runtime/RegExp/Unicode/`** survives only for `String.prototype.toUpperCase`/`toLowerCase`; those tables
  could move onto the GregsEx Unicode 17.0 data.
- The legacy statics in the fused lanes (J3), and the `/./uy`-inside-a-pair divergence from V8 (§3).
- `net472`/`netstandard2.0` were built and not measured; the downlevel `IndexOfAny(SearchValues)` and
  `SearchValues<string>` fallbacks are scalar.
