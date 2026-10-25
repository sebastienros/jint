# RegExp on GregsEx — migration record

**Status: implemented on `sebros/jint-gregsex-regex-engine-a5ccbe`.** Every regular expression written in script runs
on the ECMAScript flavor of [GregsEx](https://github.com/sebastienros/gregsex), vendored by `tools/sync_gregsex.py`
from its `main` branch (first `d989dd84`, then `a12c3ce8` and `dc455e97`, now `c48524b1`). The QuickJS libregexp port and the Acornima-to-.NET `Regex`
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

- `JintRegExpEngine` wraps the GregsEx executor. A matcher's compiled program is immutable and shared:
  `RegExpParseCache` holds matchers process-wide (keyed by pattern and flags, never by timeout: a compile's budget
  bounds the work an engine does, not which matchers it may be handed), and literals memoize theirs on the AST node.
  The cache admits a pattern on its second compile and retires full generations instead of clearing, so a script
  compiling patterns from data never fills it with matchers nothing asks for again - that filling was most of what a
  cold compile cost (a miss through the first cache took 8.5 µs against 1.4 µs for the bare compile). Its one mutable field is a frame-count hint, published without ordering because a stale read costs only a
  growth retry. A search's backtracking storage starts at 64 frames and grows ×4 per retry, bounded by size rather
  than by frame count, since a frame's size depends on the pattern:
  - up to 1 MiB, a thread keeps the storage between searches, and growth is published as the matcher's hint;
  - beyond that, the storage is the search's own and dropped when it returns, so one large input does not make every
    later search of the pattern, on any thread or engine, allocate it;
  - at 64 MiB the match throws a `RangeError`, as V8 does when its backtracking stack is exhausted.
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

### 4.1 In GregsEx (all fixed upstream in [sebastienros/gregsex#2](https://github.com/sebastienros/gregsex/pull/2), [#3](https://github.com/sebastienros/gregsex/pull/3) and [#4](https://github.com/sebastienros/gregsex/pull/4), each pinned by a test on both sides)

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

A third pass answered the win-x64 gate on #4300 (SunSpider `tagcloud`, `unpack`, `validate-input`, Dromaeo
`ObjectRegExp` and cold compiles) and went upstream as
[sebastienros/gregsex#5](https://github.com/sebastienros/gregsex/pull/5):

| # | Defect | Effect | Fix |
| --- | --- | --- | --- |
| G18 | The FIRST-character prefilter gave up on a pattern starting with a non-multiline `^`. | Every candidate position ran the VM, although only position 0 can match. | Try the start only, and only when its character is in the FIRST set. |
| G19 | An alternation tried each alternative at every position. | `tagcloud`/`unpack`-style alternations saved a choice per alternative that cannot start there. | Per-alternative ASCII start masks dispatch to the alternatives the current character can start. |
| G20 | `\b` around a literal, and `\b\w+\b`, ran the VM. | Word-boundary scans were several times .NET's. | A bounded-literal plan, and boundary checks folded into the single-set repeat plan. |
| G21 | Every compile built its own ASCII-only fast sets, parser buffers and case-fold bitmaps. | Cold compiles allocated several times `main`. | Shared ASCII sets (a 4096-entry cache), pooled parser buffers, shared bitmaps and fold table. |
| G22 | Patterns whose next unit decides every choice still ran the general VM. | One VM dispatch and one saved choice per unit. | A Glushkov position matcher: one-pass where the moves are disjoint, backtracking over the automaton where they overlap, with greedy single-unit runs measured once and given back per retry as in G4. Opt-in through the host-sized search (`hostSized: true`), so GregsEx's own frame-capacity contract is unchanged. |

### 4.2 In Jint

| # | Defect | Effect | Fix |
| --- | --- | --- | --- |
| J1 | The generic `RegExp.prototype[@@replace]` turned an `undefined` capture into `""` before calling a replacer. | Wrong argument to a replacer function (`staging/sm/String/regress-369778.js`); latent before because non-`u` patterns took the .NET fast path. | Keep `undefined` (step 14.k). |
| J2 | The two regex-timeout tests used `^(a+)+$`, which is catastrophic for .NET and not for GregsEx. | They stopped exercising the timeout. | `^(?:a\|a)*b` against `a…a!b`, which is exponential for any backtracking matcher. |
| J3 | The fused lanes (global `match`, `split`, plain-string replace) do not update the legacy `RegExp.$1`… statics. | Pre-existing for those lanes; for `search` it was new, since non-`u` patterns used to reach it through `RegExpExec`. | `search` updates them (review of #4300); the other lanes are a follow-up. Replace now sets them from its last match. |
| J4 | Each adapter search went through the validating entry's checks and sliced its scratch, and split searched one match per call. | About 15 ns per search above the executor. | A leaner prevalidated search, and split on a pattern without groups takes match ranges 32 at a time. |
| J5 | A search's backtracking storage grew on the shared matcher and stayed in thread scratch, and frames were capped by count. | One 1 MB input made every later search of the pattern, on every thread and engine, allocate up to gigabytes, kept for the thread's life; exhaustion threw an internal exception type. | Bounded by size (1 MiB retained, 64 MiB per search), with a `RangeError` at the ceiling (review of #4300). |
| J6 | Compiling a script-supplied pattern ran with every GregsEx budget at its maximum. | A `new RegExp(src)` whose compile is expensive was unbounded, where `main` compiled the custom engine's patterns under the regex timeout. | The timeout becomes a compile-work budget; exhausting it throws `RegexMatchTimeoutException`. |

## 5. Performance

`Jint.Benchmark/measure-paired.ps1`, 3 rounds in alternating order, `main` at `82b0efbaa` against this branch with
the vendored copy at GregsEx `dc455e97` (gregsex#3, measured at its branch head `008b4aa8`, whose
vendored files are byte-identical), on one Apple M4 Pro, .NET 10.0.11, macOS 15. The 41 rows are
every regex benchmark the suite has, plus SunSpider `regexp-dna` and Dromaeo `ObjectRegExp`. A row is a change only
when its 95% CI excludes zero; `control-indexOf` never reaches regex code and reads +0.9% [+0.1, +3.0], the run's
floor. Three rounds is enough for the large effects; a ±5% one wants six before it is believed
([`Jint.Benchmark/AGENTS.md`](https://github.com/sebastienros/jint/blob/main/Jint.Benchmark/AGENTS.md)). The machine was
not idle for part of the run (a concurrent build); both sides shared that.

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

### 5.1 After gregsex#5

The tables above predate the third pass (G18–G22). The gate that prompted it ran on Windows x64; so does the
re-measure: `measure-paired.ps1`, 3 rounds in alternating order, `main` at `c553a7870` against this branch at
`d949514fa` (GregsEx `f0e49f63`, identical to its squash-merge `651dc2b2`), on an AMD EPYC 7763 (devbox1). The 47
rows are the gate's. `control-indexOf` never reaches regex code and read +42.6% here, against +43.5% and −0.1% in
the two earlier runs on this machine: a bimodal floor, so a row inside about ±40% that only that run moved is not
believed.

| Row | The gate | G18–G21 only | Now | 95% CI |
| --- | ---: | ---: | ---: | --- |
| SunSpider `string-unpack-code` | +14% | −11.1% | −9.4% | [−13.3, −7.4] |
| SunSpider `string-tagcloud` | +23% | +3.6% | +0.8% | [−0.5, +2.2], no change |
| SunSpider `string-validate-input` | +7% | +4.0% | −1.5% | [−5.8, −1.1] |
| SunSpider `regexp-dna` | | | −55.4% | [−56.6, −55.4] |
| Dromaeo `ObjectRegExp` (four rows) | | +1 … +6% | −1.0 … +1.6% | each includes zero |
| `grouped-alternation-repeat` | | | −93.5% | [−93.5, −93.3] |
| `greedy-dotstar-test` | | +74.2% | +7.9% | [+6.8, +10.5] |
| `char-split` | | +18.3% | +4.5% | [+3.3, +8.0] |
| `literal-test` / `literal-replace-g` | | | +16.5% / +14.0% | both exclude zero |
| Cold compile `classes-i` | | | −25.1% | [−26.3, −23.4] |
| Cold compile `literal`, `email`, `email-i`, `uprop-u`, `alternation`, `tokenizer` | | | +15 … +39% | each excludes zero |

The other rows are faster or unchanged: `SplitUnicode` −93%, `FreshEngineSource` −57%, `CaseInsensitiveScan` −39%,
`MatchGlobal` −32%, `construct-100` −30%, `Split` −22%, `ExecWhileLoop` −14%. On the Apple M4 Pro (in process,
4 alternating rounds) the same build read `greedy-dotstar` −39% and `tagcloud` −4.2%.

### 5.2 After gregsex#19

[sebastienros/gregsex#19](https://github.com/sebastienros/gregsex/pull/19) (a required-unit bound on the last
candidate start, an ASCII first-unit filter, a follow-filtered greedy give-back) is vendored at GregsEx `4b665a46`.
Measured on the Apple M4 Pro only (`measure-paired.ps1`, 3 alternating rounds, `main` at `59cea2de4` against
`65c742a36`, 68 rows: every SunSpider script plus the rows above). `control-indexOf` read −0.2% [−0.2, +0.5].

| Row | Median Δ | 95% CI | Absolute (round 1) |
| --- | ---: | --- | --- |
| SunSpider `regexp-dna` | −76.7% | [−76.7, −76.3] | |
| SunSpider `string-unpack-code` | −8.6% | [−9.5, −6.6] | |
| SunSpider `string-tagcloud` | −3.0% | [−3.9, −2.9] | |
| SunSpider `string-validate-input` | +1.3% | [+0.0, +1.8] | |
| SunSpider, the other 22 scripts | −3.1 … +0.7% | three above zero by under 1% | |
| Dromaeo `ObjectRegExp` (four rows) | −2.9 … −3.9% | each excludes zero | |
| `greedy-dotstar-test` | −41.1% | [−41.2, −41.1] | |
| `char-split` | −26.0% | [−27.0, −25.0] | |
| `literal-test` / `literal-replace-g` | +16.3% / +13.8% | both exclude zero | |
| `AnchoredLiteral` | +64.3% | [+64.1, +65.0] | 12.3 ns → 20.2 ns |
| Cold compile `classes-i` / `uprop-u` | −39.3% / −36.0% | both exclude zero | 6.1 MB → 0.7 MB / 2.2 MB → 1.0 MB allocated |
| Cold compile `literal`, `email`, `alternation`, `tokenizer`, `email-i` | +20 … +64% | each excludes zero | `email-i` 496 µs → 804 µs, 299 KB → 691 KB |

`email-i` (+64%) and `tokenizer` (+41%) read about twice what the in-process Mac harness measured before gregsex#19
(+30%, +21%). They were #19's compile-time filters: see §5.3.

### 5.3 After gregsex#22–#24 and the cache rework

[sebastienros/gregsex#22](https://github.com/sebastienros/gregsex/pull/22) keeps the unanchored literal scan out of
`ExecuteLiteral`'s frame. [#23](https://github.com/sebastienros/gregsex/pull/23) builds #19's unit filters from the fast
sets' ASCII words and shares their `SearchValues`. [#24](https://github.com/sebastienros/gregsex/pull/24) charges the
compile budget inline and range-checks node kinds. `RegExpParseCache` (§2) caches a pattern only on its second compile,
and retires generations instead of clearing: it had cost about 7 µs per new pattern, five times compiling a short one.

Measured with `measure-paired.ps1`, 3 alternating rounds on the Apple M4 Pro, 68 rows: `main` at `59cea2de4` against
`7bc4c7929`, with GregsEx `63f41069` vendored (identical to `main` `e5ea55b3`). One `new RegExp` of a pattern never seen
before, cache included, is the cold-compile row minus `control-concat`, over 50:

| Pattern | `main` | this branch | Δ (median of rounds) | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `needle` + n | 7.21 µs | 1.43 µs | −80.1% | 1.8 KB → 5.0 KB |
| email | 8.26 µs | 2.66 µs | −67.8% | 3.2 KB → 8.4 KB |
| email, `i` | 9.57 µs | 4.99 µs | −48.0% | 5.8 KB → 10.3 KB |
| eight-word alternation | 9.22 µs | 4.14 µs | −55.4% | 5.2 KB → 13.0 KB |
| tokenizer, `g` | 11.28 µs | 6.26 µs | −44.5% | 7.6 KB → 18.0 KB |
| `classes-i` | 35.22 µs | 10.14 µs | −71.3% | 121 KB → 11.7 KB |
| `uprop-u` | 32.86 µs | 10.58 µs | −67.6% | 44 KB → 19 KB |

The same run: `AnchoredLiteral` −2.9%, `literal-test` / `literal-replace-g` +15.3% / +14.1%, `DigitScan` +3.1%,
`regexp-dna` −75.6%, `string-unpack-code` −6.8%, `string-tagcloud` −2.1%, `string-validate-input` +3.0% (no change),
Dromaeo `ObjectRegExp` −2.8 … −5.5%, `construct-100` (cache hits) −24.2%. The 22 SunSpider scripts without regexes read
+0.4 … +5.9% with wide CIs from one slow round; only `math-cordic` (+5.9%, which no change here reaches) excludes zero.

## 6. Follow-ups

- **The backtracking ceiling (J5).** [sebastienros/gregsex#4](https://github.com/sebastienros/gregsex/pull/4) made
  frames variable-length: a choice now saves a fixed-width frame and trails only the task and register slots it
  changes, where it used to copy the whole task stack and the registers. The longest run the 64 MiB ceiling admits went
  from about 82,000 to about 466,000 characters for `/"(?:[^"\\]|\\.)*"/`, from 121,573 to 372,823 for `/(a|b)*c/`
  and from 143,393 to 621,375 for `/(?:a|b)*c/`. Skipping an alternative that cannot start at the current character
  (no choice saved for it) then took them to 932,061, 559,237 and 932,063. Since gregsex#5 (G22) all three run on the
  position matcher and need no frames; the ceiling remains for the patterns it declines (lookarounds,
  backreferences, `u`/`v`, more than 256 positions), where QuickJS and V8 still match longer runs.
- **Cold compile** (review of #4300, item 5). gregsex#4 also shares fast sets, walks bitmap words instead of all 65,536
  code units, and caches the cased-character set and resolved property intervals. Against `main`, 50 unique patterns
  per operation (`RegExpColdCompileBenchmark`'s shapes, alternating runs on a loaded machine) now read: `literal` −26%,
  `\p{L}` under `u` +1% (it was +1,596%), `email` +57%, an alternation +59%, an `i` class +101% (was +461%), the email
  pattern under `i` +131% (was +1,225%). gregsex#5 (G21) brought them to +15 … +39% on Windows x64 and `classes-i` to −25% (§5.1), still allocating 2 to
  2.5 times `main`; what is left is per-pattern setup in the compiler and accelerator. After gregsex#22–#24 and the
  cache rework a one-shot pattern costs 44–80% less than on `main` (§5.3), but still allocates more than `main`'s
  .NET `Regex` adaptation (5.0 KB against 1.8 KB for a short literal): a smaller program and accelerator layout.
- **`greedy-dotstar-test` and `char-split`** stay +8% and +4.5% on Windows x64 (§5.1), where the Apple M4 Pro reads
  both faster than `main`.
- **The literal scan** (+14…+15%, §5.3): 3.26 µs over 64 KiB against 2.76 µs for .NET's compiled `Regex`, which
  `main` upgrades a reused literal to; a bare `SearchValues<string>` is 3.14 µs, and a rare-character search and a
  two-character `Vector128` search both measured slower. `AnchoredLiteral` is fixed (gregsex#22).
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
