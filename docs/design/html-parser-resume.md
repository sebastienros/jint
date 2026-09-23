# Jint.HtmlParser pause and resume checkpoint

User requested a recoverable pause on 2026-09-23 because the token budget was running low.
The goal is **not complete**. Resume the original full scope, not just the implemented subsets.

## Common worktree and working state

- Common worktree: `/Users/sebastienros/.codex/worktrees/bd4c/jint`.
- Common branch: `codex/html-parser-integration`; implementation checkpoint `9d5b3fb1c`.
  Later checkpoint commits preserve documentation only.
- Browser still uses AngleSharp. No unfinished native parser has been substituted into Browser.
- Fresh pause-time `dotnet build Jint.Browser/Jint.Browser.csproj -c Release` passed both net8.0
  and net10.0 with zero warnings/errors. Log: `/private/tmp/jint-html-parser-pause-browser-build.log`.
- Latest common non-corpus HtmlParser tests: **2,730 passed, zero failures/skips**, both frameworks.
  Log: `/private/tmp/jint-form-states-sign-integrated.log`. Subsequent common changes are reviewed
  corpus expectations and documentation, not production code.
- Latest common XML corpus: **4,000 total, 3,690 passed, 310 failed, zero skips**.
  Per framework: 1,799 conformance passes, 148 unresolved required cases, six optional adapter
  debts, 21 verified optional policies, zero harness failures/mismatches. OUTPUT 323 compared,
  63 pending; 52 no-fetch alternatives. Log: `/private/tmp/jint-xml-sixteen-integrated.log`.
  These are visible acceptance failures, not a completed XML conformance claim.
- No comparative timing result or speedup claim exists. Benchmarks must compare equivalent
  AngleSharp/native work; the reviewed primitive timing lane is net10 only on an idle machine.
- No PR was created. Forty-one reviewed, integrated, tested tasks were archived and their
  worktrees removed. Every unfinished task below is retained, including WIP that may not build.

The original goal remains HTML/XML/SVG/CSS parsing, mutable native DOM and required Browser
semantics, complete AngleSharp replacement in production, conformance, and comparative benchmarks.
The existing architecture, dependency inventory and `html-parser-progress.md` retain full gates.

## Retained tasks and branches

All directories below are under `/Users/sebastienros/.codex/worktrees/`, ending in `/jint`.
Recheck actual HEAD, working status and task status before resuming; never infer completion from a
commit or a previously printed test result.

| Task | Task ID | Directory / branch | Checkpoint and next action |
| --- | --- | --- | --- |
| D6s2/D6s3 slots | `01a0cff7-448b-73e0-81de-577b76d90bd8` | `32f8`, `codex/d6s2-slot-assignment` | `736085642`; re-review final cancellation fix before integrating full source chain |
| H6d templates | `01a0d01a-09e1-7312-b099-2afb6efc86b7` | `2a63`, `codex/h6d-template-parsing` | `9b8136469`; independent full review, then common tests |
| X4b3 XPath | `01a0d011-17de-7c01-92b6-cbb7cfcba6ed` | `ff28`, `codex/x4b3-xpath-identifiers` | WIP `2c61763d8`; final Release build passes both TFMs, new evaluation tests unrun |
| V0b3b trigonometry | `01a0d029-93fb-7352-bf0c-ff2c92aec72d` | `ba36`, `codex/css-math-trigonometric-v0b3b` | WIP `db97abc02`; finish compile/tests before review |
| V0c1 substitutions | `01a0d029-9c47-7600-8b5b-e10aeb7b3b9e` | `4983`, `codex/css-substitution-v0c1` | WIP `36bac02df`; not yet compile/test verified |
| XML corpus | `01a0cf6e-aa5f-7631-af1b-03f6136013a1` | `b0f9`, `codex/xml-conformance-notations` | `0e703650d`; two approved expectation commits await integration and common tests |
| Architecture | `01a0ceec-94ec-7f63-9bfb-189cac69df5f` | `dc58`, `codex/html-parser-design` | `d1cfff5b9`; numeric policy review has two unresolved decisions |
| Benchmarks | `01a0ceee-7b09-7513-b507-a5c412eb4518` | `b0b0`, `codex/html-parser-benchmark-xml` | `00c7b7ec1`; harness already integrated, measurements unfinished |

Use GPT-6 Astra High for decisions, plans and reviews; dedicated GPT-6 Sol High local-worktree
tasks for implementation. Do not start replacement tasks for the retained work. Merge only after
review, run relevant fresh Release common-worktree tests, verify clean/idle/patch equivalence,
then archive/remove the completed worktree while preserving branch and task history. No PRs.

## Immediate review and recovery details

### Slots and native file ownership

Full unintegrated chain: `8486c378c`, `832ef8025`, `0a651e79e`, `598de6522`, `736085642`.
The first review found semantic assignment/signal-order errors, clearing stale stored slot pointers,
quadratic manual transfer and flattened fallback work. `598de6522` cleared those findings in review,
including 1,000 independent randomized named mutation sequences. Its remaining blockers were
unpolled attribute scans and the first wide-root index enumeration. `736085642` addresses those;
**independent re-review is still required**. Owner reports 1,077 non-corpus tests per TFM, no failures.
Shared Element/Node/Attr/Document/clone files remain reserved until this handoff finishes.
Next native consumers include D7b1c textarea, D7b2 checkable state and X3c IsValue prerequisite.

### HTML templates

Owner reports fresh Release builds both TFMs and 1,343 non-corpus tests each via directly run freshly
built MTP assemblies; the dotnet test wrapper stalled locally. Eleven changed files implement ordinary
templates, inert destinations, mode stack, EOF and scope behavior. Applicable `for` patching remains a
deliberate MissingFeature stop. Root preliminary inspection is not a complete independent review.
After H6d, framesets, foreign content, fragments and newer template branches remain required.

### XPath owned evaluation

`html-parser-xpath.md` contains the reviewed amendment, integrated in `89ab6fa62`.
.NET 8/10 BCL following-axis evaluation loops on a truthful parentless Attr; lying about cursor
movement or accepting cancellation as the result is forbidden. Own original-source validation,
token-aware following-axis guard and private compiled expressions; expose native materialized
results with captured first-node string-value, retain all 13 opaque BCL fences. No public promotion
yet. Finish the checkpoint's tests, remove any skipped valid-query gate, then review full source chain.
Owner's final checkpoint is clean and has no running task process. Before final test additions,
the focused XPath suite passed 24/24 on net8 with no skips; that does not verify the final tests.
V0c1's canceled test session 45502 did not report exit to its task. The coordinator's subsequent
authorized process audit found no running HtmlParser dotnet test or MSBuild process. Do not treat
this WIP's canceled run as test evidence. All implementation tasks acknowledged checkpointing.

### XML corpus and prepared input design

Unintegrated source-approved expectation commits: `14f76309e` (17 IBM P61–P65 cases) and `0e703650d`
(four IBM P68/P69 cases). All exact source packets were independently reviewed; compare committed
JSON entries against packets before integrating, preserving prior entries. Focused tests pass each
on both TFMs. Latest worker full census after both is net8 only: 1,820 passes /127 unresolved,
OUTPUT 344 compared/42 pending; six adapter debts remain. Net10 full census after final four was
not run due pause. Packet paths: `/tmp/jint-ibm-p61-p65-original-output17-source-review.json` and
`/tmp/jint-ibm-p68-p69-original-output4-source-review.json`. Common corpus archive is cached and pinned.

`html-parser-xml-japanese-byte-adapter.md` is a saved design direction, **not implemented or finally
reviewed**. Prepare six exact decoded fixtures explicitly with strict Python codecs and fixed raw/
decoded hashes; tests consume verified UTF-8 storage without rewriting original XML declarations or
line endings. Do not add a general CodePages decoder: negative probes show ExceptionFallback is not
strict enough. Missing/corrupt prepared artifacts must fail the harness. Six optional cases become
observed/unreviewed, not automatically approved parser outcomes.

### Remaining policy reviews

- `d1cfff5b9` numeric-policy proposal is **not approved**. Rounding-cell lattice makes exact integer
  2^53 with base0/step3 matched despite the normative nonintegral quotient. Resolve toward faithful
  matching or document an explicitly chosen deviation; do not call it browser consensus. Its common
  date/week/local ceiling is also unsupported: Blink permits instants above 8.64e15 still mapping to
  week275760-W37. Prefer source-backed family-specific domains; no implementation dispatch yet.
  Evidence: `/tmp/jint-b3p-evidence/date_components.cc:469` and the retained design task.
- `html-parser-css-math-followups.md` now saves a **Log decision draft**, including its full special
  value/base matrix. Endpoint-first handling conflicts with mathematical base<1 limits; pinned WPT
  does not resolve this. Coordinator review is required before V0b3c. Trig's earlier atan2 -180/table
  decision is reviewed and already part of the active implementation contract.

## Validation commands and precautions

Always fresh Release; never `--no-build`. Use `git -c core.fsmonitor=false` for repository operations.
Local feed mapping requires `-p:RestoreSources=https://api.nuget.org/v3/index.json`.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName!~Xml.Conformance'
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName~Xml.Conformance'
```

The second command intentionally remains red until reviewed corpus debt is resolved. Do not widen
exclusions to obtain a green checkpoint. The existing Browser build remains usable independently.
