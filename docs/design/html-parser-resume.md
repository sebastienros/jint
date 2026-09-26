# Jint.HtmlParser resume checkpoint

The user requested a token-budget pause, then asked to finalize the six retained chats and integrate
their existing changes. That finite finalization pass is **complete**; all six chats are archived. The **original full project is
not complete** and remains a later effort. Do not mistake completed implementation slices or archived
chats for production AngleSharp replacement or full conformance/performance acceptance.

## Common state

- Worktree `/Users/sebastienros/.codex/worktrees/bd4c/jint`, branch `codex/html-parser-integration`.
- Final production implementation checkpoint: **`6d55e76ed`**; later finalization commits update docs.
- Browser still uses AngleSharp. No PRs or benchmark timings were created. Fresh common Browser
  Release build passed both TFMs with zero warnings/errors: `/private/tmp/jint-finalize-browser-build.log`.
- Reviewed HTML creation metadata is integrated as `ac2583318` (source `ef64460e2`).
- Reviewed CSS substitution analysis is integrated through `9fec0c126` (source full chain through
  `2896d3e3d`). Execution and consuming-property validity remain separate work.
- Source-reviewed XML optional policies are integrated as `3169c483b` (source `dd367c16a`).
  All three complete 6,283-node projections matched the independent source packet; all previous
  policies are unchanged. No production XML parser or conformance exclusions were changed.
- Latest common non-corpus Release parser tests: **3,270 passed**, zero failures/skips, net8/net10.
  `/private/tmp/jint-finalize-all-integrated.log`.
- Latest common XML corpus: **4,022 total, 3,766 passed, 256 expected debt failures, zero skips**.
  All failures are 127 pending required cases per TFM plus two census assertions. There are no
  harness/mismatch/optional-review failures. Optional policies: 27 verified, zero observed/adapter debt.
  OUTPUT remains 344 compared/42 pending, with 52 no-fetch alternatives.
  `/private/tmp/jint-finalize-xml-corpus.log`. These failures remain visible acceptance debt.
- Benchmark common correctness checks passed: 12 baseline fixtures; four native/AngleSharp XML/SVG
  comparisons and corruption probes; exhaustive primitive comparisons, all fresh Release net10.
  `/private/tmp/jint-finalize-benchmark-validation.log`. No timing or speedup evidence is implied.
- Exponential math source `f33b09c67` + `aa89a1743` + `83384fe13` is integrated through `6d55e76ed`
  after independent review/corrections. It fixes false finite hypot overflow, folded default log base
  omission and allocation cancellation checks. The finite math census is now 21 implemented functions.
  Independent 2,197-case sweeps per framework found no overflow in 1,177 representable cases. The
  106 beyond-MaxValue cases saturating instead of exact-reference infinity are an accepted binary64
  precision limitation, documented in the reviewed design. No correctly-rounded claim.

## Chat completion and cleanup

Architecture audit found all 29 existing design commits patch-equivalent in common. Benchmark audit
found all corpus/comparison implementation deliverables already integrated. Their further measurements
and full-project design/implementation work are parent-project obligations, not unfinished branch edits.
The XML harness chat is likewise finalized without pretending its remaining acceptance debt is resolved.

All six retained chats are finalized and archived; no unmerged task-owned implementation remains.
Fifty-three implementation chats are now archived, forty-four checkouts removed. Final filesystem
verification found architecture `dc58` removed after archival. Nine completed clean
checkouts remain because root/owner `list_artifacts` returns no managed worktree identity:

| Checkout | Chat ID | Completed slice |
| --- | --- | --- |
| `ff28` | `01a0d011-17de-7c01-92b6-cbb7cfcba6ed` | XPath |
| `a768` | `01a0dafe-8d1a-73b3-bc05-8fab6d2fdd92` | Native/XML IsValue |
| `ba36` | `01a0d029-93fb-7352-bf0c-ff2c92aec72d` | Trigonometry |
| `ad95` | `01a0dab4-0ad4-7720-ada6-80c245e9c7ff` | Framesets |
| `aebc` | `01a0db13-f920-7580-8392-b5798ebd7d93` | HTML IsValue |
| `4983` | `01a0d029-9c47-7600-8b5b-e10aeb7b3b9e` | CSS substitution analysis |
| `b0f9` | `01a0cf6e-aa5f-7631-af1b-03f6136013a1` | XML corpus harness/policies |
| `b0b0` | `01a0ceee-7b09-7513-b507-a5c412eb4518` | Benchmark corpus/harness |
| `4cb4` | `01a0db10-f9d2-7501-872f-fcfe719fdbbb` | Exponential math |

Directories are `/Users/sebastienros/.codex/worktrees/<checkout>/jint`. Use managed archival when
identities become available; do not bypass it with shell deletion. App computer-use access was also
denied, so do not attempt a UI workaround. Git branches and chat history remain recoverable.

No implementation/review task remains running. No new feature chats were started during finalization.
The larger project remains paused for a later effort; its goal must not be marked complete.

## Continuing the original project

Use GPT-6 Astra High for design/planning/reviews and GPT-6 Sol High dedicated local-worktree chats
for implementation. Review, integrate, and test in common before archiving finalized chats/worktrees.
No PRs until requested. The [implementation record](html-parser-progress.md), dependency inventory and
reviewed design documents preserve the full objective and finite dispatches.

Required future work includes HTML foreign content/fragments/patch-shadow branches, HTML serialization
and public facades, all remaining form/input state families, CSS substitution execution/colors/property
registry/CSSOM/cascade, Browser bindings/generator/events/scheduling and complete production AngleSharp
cutover, full conformance/consumer verification, and equivalent paired performance acceptance.

Completed foundation invariants to preserve:

- HTML adoption inner recreation allocates through the stack common ancestor (step 13.6); final
  recreation uses the furthest block (step 17). The earlier blanket inert-owner recommendation was
  corrected. Native reparenting between parser turns makes these distinct destinations observable.
- Immutable IsValue captures accepted creation-time attributes; clones/import preserve it, adoption
  preserves identity, and live attribute changes/merged attributes never rewrite it. Future foreign
  allocations must pass it too. X3c serialization now needs the completed metadata contract preserved.
- Exact turn/grad quarter turns must survive canonical conversion without epsilon matching.
- Source-preserving substitution analysis is not substitution execution or property validation.
- The three pr-xml originals contain `<!ENTITY lt "<">`, an ordinary XML 4.6 error, not a WFC.
  Explicit optional recovery preserves mandatory predefined lt semantics per XML 1.2/4.6. Do not call
  sources error-free, generalize to blanket DTD lenience or count optional cases as required passes.

Numeric input design is approved in `html-parser-input-numeric-policy.md`: exact shortest-decimal
remainders and uncapped finite-double calendar conversion. The 1,024-bit bound applies only to calendar
integers; remainder alignment can exceed 2,098 bits. Root verified the pinned WPT huge-local empty-result
assertion; keep the documented spec/WPT discrepancy explicit. No numeric runtime task was dispatched.

Textarea D7b1c preflight is complete, no runtime implementation dispatched. Before dispatch, clarify the
reviewed `html-parser-text-control-state.md` contract with these points:

- Stable lazy `HtmlElementState.TextArea`/`HtmlTextAreaState` only; defer Input/general facade to b1d.
- Native file scope: textarea/selection/mutation helpers plus HtmlElementState, Node, CharacterNodes,
  NodeCloner and narrow Element access. Parser lifecycle/notifications stay in b1e.
- Semantic children-changed hooks are independent of mutation-record suppression. Fragment destination
  insertion hooks once after the entire sequence; individual removals retain intermediate steps.
  Direct Comment/PI data replacement also hooks, although only Text/CDATA contributes child text.
- Copy raw/dirty state before cloning descendants. Shallow clean clones initially retain copied raw
  text despite an empty default; their next child change resumes child-derived projection. Adoption
  retains identity. Parsed append invalidates in O(1) without flattening or reading Text.Data.
- Separate automatic clamping and explicit selection steps for later notifications. Preserve destructive
  intermediate clamps; avoid quadratic replace-all behavior. No placeholder event transport.

## Evidence and validation

Private source-review packet is retained in ignored common `artifacts/html-parser-review/pr-xml/`
and `/private/tmp/jint-pr-xml-source-review/`. It contains original-source derivation, pins, omissions,
complete expected projections and citations. Do not commit/publish corpus-derived private artifacts.
The independent exponential review packet is also retained in ignored
`artifacts/html-parser-review/exponential/`, including generators, exact reference vectors and results.
Preserve needed ignored files before any common-worktree archival; managed snapshots exclude them.

Pinned archive: `Jint.Tests.HtmlParser/Xml/Conformance/Cache/xmlts20130923.tar.gz`, SHA
`9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f`.
Explicit decoded preparation when needed:
`python3 Jint.Tests.HtmlParser/Xml/Conformance/Tools/import_corpus.py prepare-decoded`.
Missing corpus/prepared inputs must fail visibly, never become silent skips.

Always fresh Release, never `--no-build`; Git uses `git -c core.fsmonitor=false`.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName!~Xml.Conformance'
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName~Xml.Conformance'
```

Benchmarks retain normal gate configuration and verified idle-machine requirements, with net10-only
primitive runs, equivalent AngleSharp/native work, Op/s and allocations. No short-job claims or idle
bypasses. HTML/CSS comparison needs matching complete native APIs; tokenizer work is not equal to DOM
construction. Exact commands and comparison limitations are in both benchmark corpus READMEs.

App list_threads may omit newer/archived tasks. Use recorded IDs and wait_threads for task state;
do not recreate missing-looking chats. Worker cross-chat reports can be automatically rejected; read
local final/status rather than request retries. The original large goal remains incomplete.
