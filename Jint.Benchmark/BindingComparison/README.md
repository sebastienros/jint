# DOM binding comparison spike

This is the reproducible harness for [#3575 D0.3](https://github.com/sebastienros/jint/issues/3575),
tracked by [#3898](https://github.com/sebastienros/jint/issues/3898). It contains no performance results.
An idle-machine paired run and an evidence-based conclusion are still needed to complete D0.3.

Two **separate executables** use the same offline AngleSharp DOM and the same prepared JavaScript:

| Arm | Binding | Interpreter | DOM |
| --- | --- | --- | --- |
| Baseline | AngleSharp.Js 1.1.0 reflection | NuGet Jint 4.15.3 | AngleSharp 1.8.0 |
| Candidate | Hand-shaped Node/Element/Document prototypes, public APIs only | This checkout's Jint | AngleSharp 1.8.0 |

Each project has its own dependency graph and enforced lock file (use `dotnet restore -p:RestoreLockedMode=false`
only when deliberately updating that project's pins). Exact direct package pins deliberately bypass
central package management for this experiment; the Jint project reference retains its normal repository
configuration. The baseline's incompatible Jint version never loads into the candidate process.
`Jint.Benchmark.csproj` excludes these sources; run these projects separately.

The candidate is a deliberately small binding spike: two Node attributes, one Element attribute and two
operations, one Document attribute and one operation, and identity-preserving wrappers. It asserts that
its prototypes use the shared shape representation. It does **not** implement WebIDL conversion and
error semantics, the complete DOM prototype family, Window, or the production generated browser binding.

## Verify without measuring

From the repository root, with Python 3 and the repository's .NET SDK:

```sh
python3 Jint.Benchmark/BindingComparison/run.py --output /tmp/binding-verification
python3 -m unittest discover -s Jint.Benchmark/BindingComparison -p test_run.py
```

`--dotnet /absolute/path/to/dotnet` selects a particular SDK executable, including for child builds.
Choose a new output directory per run. Every invocation freshly builds in Release. Verification runs the
actual benchmark methods with two independent cold instances and repeated calls to independently warmed
instances for each workload. It checks 24 results per arm against independent expected values, then
rejects differences in workload hashes, runtime, architecture, OS, or DOM/BDN assembly identity.

| Workload | What it exercises | Expected checksum |
| --- | --- | --- |
| NodeRead | Document and Element inherited Node getters | 13000 |
| ElementReadWrite | Alternating attribute writes, reads and `id` | 8500 |
| DocumentLookup | `getElementById`, wrapper identity, `documentElement` | 5000 |
| InterpreterControl | Integer loop without DOM calls | 3500 |

The fixed 1000-iteration workloads reset their state each invocation. Alternating attribute values make
a missing setter fail. The control exposes interpreter differences; it is not a correction factor that
can be subtracted from other rows. `verification.json` records the runtime, SDK, git head and dirty state,
assembly informational versions and SHA-256 hashes, and the HTML/script hash. Logs are retained on failure.
No clocks are sampled and no timing claims are produced by verification. The `Binding comparison verification`
workflow runs these checks on relevant pull requests and main pushes; CI never measures performance.

List the benchmark methods without running them:

```sh
dotnet run -c Release --project Jint.Benchmark/BindingComparison/Baseline -- --list flat
dotnet run -c Release --project Jint.Benchmark/BindingComparison/Candidate -- --list flat
```

## Collect measurements later

Read [the benchmark instructions](../AGENTS.md) before measuring. On an otherwise idle machine:

```sh
python3 Jint.Benchmark/BindingComparison/run.py --measure --rounds 6 --output /tmp/binding-paired
```

The driver verifies first, selects the repository's **gate** configuration (three BDN launches, idle
validation, normal production tiering), and collects alternating baseline/candidate then
candidate/baseline rounds. It refuses an idle-check override and preserves each round's raw BDN CSV,
logs, memory diagnostics and provenance. An absent, duplicate, failed or incomplete report fails the run.
It does not calculate or publish a ratio. Pair rows by method and workload within each round, and analyze
paired differences with confidence intervals as described in [measure-paired.ps1](../measure-paired.ps1).
That script's worktree runner is not directly interchangeable with these two project runners.
After an interrupted measurement, follow the power-plan restoration instructions in the benchmark guide.

**Cold** includes DOM parsing, engine and binding setup, first prepared-script execution and disposal on
every operation; script parsing is excluded. Process initialization, first-ever reflection and static
shape construction are warmed by BDN: this is document-cold, not process startup.
**Warm** excludes DOM/engine setup, script parsing and disposal, and warms each engine only with its own
row's workload. Checksums are checked inside both methods and memory diagnostics cover both lanes.

These arms differ in interpreter/parser version and binding surface. In particular, baseline cold setup
builds its complete Window/DOM environment while the candidate builds three small prototypes. A cold
ratio cannot be presented as equivalent-browser startup savings, and a warm ratio cannot isolate the
causal effect of shapes. Use the control, report all provenance, and describe these as end-to-end costs
of the two explicitly defined embedding paths. Comparing the production generated bindings or attributing
a pure binding speedup requires another matched experiment. No numbers from a busy machine or a short job
belong in a PR or release note.
