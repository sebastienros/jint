# RegExp on GregsEx — migration record

**Status: implemented on `sebros/jint-gregsex-regex-engine-a5ccbe`.** Every regular expression written in script runs
on the ECMAScript flavor of [GregsEx](https://github.com/sebastienros/gregsex), vendored by `tools/sync_gregsex.py`
from its `main` branch (first `d989dd84`, then `a12c3ce8`, now `dc455e97`). The QuickJS libregexp port and the Acornima-to-.NET `Regex`
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
  [`docs/guide/migrating-to-v5.md`](../guide/migrating-to-v5.md) §2 and §4.161.

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

### 4.1 In GregsEx (all fixed upstream in [sebastienros/gregsex#2](https://github.com/sebastienros/gregsex/pull/2) and [#3](https://github.com/sebastienros/gregsex/pull/3), each pinned by a test on both sides)

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

A second pass, after the first paired benchmark left split, `.*` and fresh-engine rows slower than `main`, went
upstream as [sebastienros/gregsex#3](https://github.com/sebastienros/gregsex/pull/3):

| # | Defect | Effect | Fix |
| --- | --- | --- | --- |
| G11 | `SearchCore` held the VM's `Machine`, a large ref struct whose frame is zeroed on entry. | Every search paid for a VM even when an accelerated plan answered: 12 ns of a 25 ns `split(/a/)` step. | The VM path is its own non-inlined method. |
| G12 | An absolute-deadline budget still read the clock at each search's zero-unit checkpoint. | 17 ns per search under Jint, which always sets a timeout. | Its checkpoints wait for the clock stride like any other charge. |
| G13 | The atomic-sequence plan refused nullable repeats. | `split(/\s*,\s*/)` ran the VM at every candidate. | Admitted under the plan's existing last-or-followed-by-a-foreign-literal rule. |
| G14 | Under `i` a class had no BMP bitmap, ASCII `Canonicalize` was a binary search, and every `i` literal blocked the plans. | `/[a-z]+@[a-z0-9.]+\.[a-z]{2,}/i` over 15 characters: 4.7 µs. | A folded bitmap under legacy `i`, an ASCII table, and caseless `i` literals planned as plain ones: 51 ns. |
| G15 | The FIRST-character prefilter stopped at a group or alternation. | `/(https?):\/\//` 500 ns, `/(?:abc|def|ghi)+\d*/` 2.5 µs on a 15-character subject. | It looks through groups, alternations and inner sequences: 37 ns and 119 ns. |
| G16 | A `.` run searched its four line terminators with a four-value `IndexOfAny`. | Twice .NET's `IndexOf('\n')` over 64 KiB. | One `IndexOfAnyExceptInRange('\u000E', '\u2027')`, verified, with an exact fallback after 8 false hits. |
| G17 | The inverse-fold cache behind the FIRST prefilters was per mode, not per Unicode version (introduced with G6). | After a Unicode 16 program built it, a Unicode 17 program missed the 17.0 case pairs: wrong `NoMatch`. Not reachable from Jint, which has the 17.0 tables only. | Keyed by its case-table instance. |

### 4.2 In Jint

| # | Defect | Effect | Fix |
| --- | --- | --- | --- |
| J1 | The generic `RegExp.prototype[@@replace]` turned an `undefined` capture into `""` before calling a replacer. | Wrong argument to a replacer function (`staging/sm/String/regress-369778.js`); latent before because non-`u` patterns took the .NET fast path. | Keep `undefined` (step 14.k). |
| J2 | The two regex-timeout tests used `^(a+)+$`, which is catastrophic for .NET and not for GregsEx. | They stopped exercising the timeout. | `^(?:a\|a)*b` against `a…a!b`, which is exponential for any backtracking matcher. |
| J3 | The fused lanes (global `match`, `split`, plain-string replace) do not update the legacy `RegExp.$1`… statics. | Pre-existing; unchanged by this work. | Not fixed — noted for a follow-up. |
| J4 | Each adapter search went through the validating entry's checks and sliced its scratch, and split searched one match per call. | About 15 ns per search above the executor. | A leaner prevalidated search, and split on a pattern without groups takes match ranges 32 at a time. |

## 5. Performance

`Jint.Benchmark/measure-paired.ps1`, 3 rounds in alternating order, `main` at `82b0efbaa` against this branch with
the vendored copy at GregsEx `dc455e97` (gregsex#3, measured at its branch head `008b4aa8`, whose
vendored files are byte-identical), on one Apple M4 Pro, .NET 10.0.11, macOS 15. The 41 rows are
every regex benchmark the suite has, plus SunSpider `regexp-dna` and Dromaeo `ObjectRegExp`. A row is a change only
when its 95% CI excludes zero; `control-indexOf` never reaches regex code and reads +0.9% [+0.1, +3.0], the run's
floor. Three rounds is enough for the large effects; a ±5% one wants six before it is believed
([`Jint.Benchmark/AGENTS.md`](../../Jint.Benchmark/AGENTS.md)). Another session was building on the same machine
for part of the run, which both sides shared.

**Faster** — 36 of the 41 rows (the others are the four below and the floor); the fourth column is the first paired run's figure, before gregsex#3 and J4.

| Row | Median Δ | 95% CI | Before the second pass |
| --- | ---: | --- | ---: |
| `RegExpShapeBenchmark.Run[template-replace-g]` | −97.9% | [−98.0, −97.8] | −97.8% |
| `RegExpSplitBenchmark.SplitUnicode` | −92.1% | [−92.2, −92.1] | −89.9% |
| `RegExpShapeBenchmark.Run[uprop-match-g]` | −92.0% | [−92.2, −92.0] | −92.1% |
| `RegExpCustomEngineBenchmark.VariableLength` | −83.8% | [−83.9, −82.8] | −67.5% |
| `SunSpiderBenchmark.Run[regexp-dna]` | −73.5% | [−74.4, −72.8] | −2.6% |
| `RegExpFreshEngineBenchmark.FreshEngineSource` | −73.0% | [−73.5, −71.2] | −61.1% |
| `RegExpSplitBenchmark.SplitWithCaptures` | −59.9% | [−60.0, −59.0] | −47.7% |
| `RegExpShapeBenchmark.Run[capture-match-g]` | −50.9% | [−51.9, −48.3] | −40.3% |
| `RegExpCustomEngineBenchmark.NamedCapture` | −46.3% | [−46.3, −45.7] | −44.5% |
| `RegExpShapeBenchmark.Run[class-match-g]` | −45.4% | [−45.5, −44.7] | −33.9% |
| `RegExpCustomEngineBenchmark.GlobalMatchAll` | −40.5% | [−41.1, −40.4] | −37.7% |
| `RegExpCustomEngineBenchmark.IsMatchOnly` | −40.2% | [−40.7, −39.7] | −38.1% |
| `RegExpMatchBenchmark.MatchGlobalUnicode` | −39.2% | [−39.5, −38.7] | −34.2% |
| `RegExpCustomEngineBenchmark.LiteralScan` | −38.2% | [−38.5, −37.1] | −36.2% |
| `RegExpShapeBenchmark.Run[nullable-split]` | −35.6% | [−38.5, −34.4] | +72.7% |
| `RegExpCustomEngineBenchmark.NoMatchScan` | −34.9% | [−35.9, −34.3] | −32.6% |
| `RegExpMatchBenchmark.MatchGlobal` | −31.9% | [−31.9, −30.5] | −23.5% |
| `RegExpShapeBenchmark.Run[greedy-dotstar-exec]` | −30.3% | [−30.6, −27.7] | +22.6% |
| `RegExpShapeBenchmark.Run[greedy-dotstar-test]` | −27.1% | [−29.5, −26.3] | +24.2% |
| `RegExpShapeBenchmark.Run[construct-100]` | −26.9% | [−30.3, −26.7] | −18.3% |
| `RegExpShapeBenchmark.Run[function-replace-g]` | −26.1% | [−26.7, −25.6] | −12.4% |
| `RegExpCustomEngineBenchmark.CaseInsensitiveScan` | −25.7% | [−25.7, −25.6] | −23.7% |
| `RegExpShapeBenchmark.Run[char-split]` | −21.9% | [−24.1, −20.9] | +49.2% |
| `RegExpExecLoopBenchmark.NamedGroupsAccess` | −21.2% | [−23.6, −18.8] | −14.5% |
| `RegExpExecLoopBenchmark.MatchAllIterate` | −18.6% | [−25.4, −17.8] | −9.9% |
| `RegExpExecLoopBenchmark.ExecWhileLoop` | −17.9% | [−18.2, −14.2] | −9.4% |
| `RegExpShapeBenchmark.Run[exec-loop]` | −17.1% | [−18.1, −16.6] | −12.8% |
| `RegExpExecLoopBenchmark.StickyExec` | −16.4% | [−18.8, −15.3] | −5.1% |
| `RegExpSplitBenchmark.Split` | −10.2% | [−10.6, −9.1] | +30.1% |
| Dromaeo `ObjectRegExp` (four rows) | −7.9% … −10.0% | each excludes zero | +1.8% … +6.5% |
| `RegExpFreshEngineBenchmark.FreshEnginePrepared` | −6.5% | [−15.0, −6.4] | +80.0% |
| `RegExpCustomEngineBenchmark.CharClassScan` | −2.9% | [−3.0, −0.3] | +0.9% |
| `RegExpCustomEngineBenchmark.DigitScan` | −2.0% | [−3.9, −1.6] | +3.5% |

Allocation fell with time on the capture-heavy rows: `template-replace-g` from 177 MB to 0.6 MB per operation,
`SplitUnicode` 25 MB to 1.6 MB, `capture-match-g` 10.5 MB to 2.3 MB, `MatchGlobal` 5.8 MB to 1.4 MB.

**Slower**

| Row | Median Δ | 95% CI | Absolute |
| --- | ---: | --- | --- |
| `RegExpCustomEngineBenchmark.AnchoredLiteral` | +50.6% | [+49.1, +61.8] | 13 ns → 19 ns |
| `RegExpShapeBenchmark.Run[literal-test]` | +17.6% | [+16.7, +21.6] | 3.2 µs → 3.7 µs |
| `RegExpShapeBenchmark.Run[literal-replace-g]` | +15.6% | [+13.5, +15.7] | 3.3 µs → 3.8 µs |
| `RegExpShapeBenchmark.Run[icase-literal-test]` | +4.7% | [+4.5, +5.7] | 3.8 µs → 4.0 µs |

The literal rows scan 64 KiB for a 10-character literal that is not there. GregsEx already searches with the fastest
of `IndexOf(string)`, `IndexOf(string, Ordinal)` and `SearchValues<string>` measured over that subject (3.1 µs against
4.9 µs); .NET Regex picks its anchor characters from a frequency table, which is what the remaining 0.5 µs would take.
`AnchoredLiteral` is the fixed cost of one call that does almost no work. `icase-literal-test` is below the
three-round bar.

## 6. Follow-ups

- **The literal scan** (+16…+18%, §5): anchor characters chosen by frequency, as .NET Regex does, and the fixed
  cost of a call that does almost no work (`AnchoredLiteral`, 13 ns → 19 ns). Re-measure `icase-literal-test` at six
  rounds.
- **Batches beyond split**: global `match` and plain-string replace still search once per match; the same
  `FindRanges` batch would serve them.
- **Acornima still validates every regex literal at parse time** (an early error is a parse-time error), so a
  literal is parsed twice. A parse-time validator on the GregsEx parser would remove one pass and one source of
  disagreement; error messages would then need their own text, since GregsEx diagnostics are codes.
- **`Jint/Runtime/RegExp/Unicode/`** survives only for `String.prototype.toUpperCase`/`toLowerCase`; those tables
  could move onto the GregsEx Unicode 17.0 data.
- The legacy statics in the fused lanes (J3), and the `/./uy`-inside-a-pair divergence from V8 (§3).
- `net472`/`netstandard2.0` were built and not measured; the downlevel `IndexOfAny(SearchValues)` and
  `SearchValues<string>` fallbacks are scalar.
