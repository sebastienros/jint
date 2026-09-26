# Jint.HtmlParser pause and resume checkpoint

Paused at the user's request on 2026-09-25 to conserve the remaining token budget.
The full goal is **not complete**. This checkpoint supersedes the earlier September 25 pause;
Git history retains its older state. Resume the full original objective, not only finite slices.

## Verified common state

- Worktree `/Users/sebastienros/.codex/worktrees/bd4c/jint`, branch `codex/html-parser-integration`.
- Production implementation checkpoint **`e4e72d0e9`**. Subsequent checkpoint changes are documentation.
  No new worker implementation was integrated during this wrap-up.
- Latest fresh Release non-corpus parser suite: **3,022 passed, zero failures/skips**, net8/net10 combined.
  Log `/private/tmp/jint-framesets-integrated.log`. Production code has not changed since this run.
- Latest common XML corpus: **4,016 total, 3,754 passed, 262 expected debt failures, zero skips**.
  Per framework: 1,820 required passes, 127 pending required cases, three optional observed,
  zero adapter debt, 24 verified optional policies, zero harness failures/mismatches;
  OUTPUT 344 compared/42 pending and 52 no-fetch alternatives.
  Log `/private/tmp/jint-weekly-policy-guard-integrated.log`.
  Root classified every failure: 254 pending required cases + six optional observed + two census.
  Worker-only newer policies are not common acceptance evidence.
- Browser still uses AngleSharp. Fresh pause-time Release Browser build passed net8/net10 with zero
  warnings/errors: `/private/tmp/jint-html-parser-final-pause-browser-build.log`.
- No PRs or comparative timings; no speedup claim. Primitive benchmarks remain net10 only on an
  idle machine, with equivalent native/AngleSharp work. An implemented harness is not a result.
- Forty-seven completed tasks are archived; forty-three worktrees were removed. Four clean completed
  checkouts (`ff28`, `a768`, `ba36`, `ad95`) remain pending managed cleanup: `list_artifacts` exposes
  no attached identity in root or worker chats. Do not bypass managed archival with shell deletion.
- Active implementation tasks were asked to finish only their bounded checkpoint, commit, and stop.
  Unmerged and unfinished worktrees are retained. Do not dispatch textarea or another feature now.

The objective remains HTML/XML/SVG/CSS parsing, mutable native DOM and required Browser semantics,
complete production AngleSharp replacement, correctness acceptance and equivalent paired benchmarks.
The architecture, dependency inventory and [implementation record](html-parser-progress.md) preserve
full completion gates. The current library is a partial implementation; Browser remains functional
through its existing dependency while migration is unfinished.

## Retained tasks

Directories are under `/Users/sebastienros/.codex/worktrees/`, ending in `/jint`.
Recheck actual HEAD, clean state and chat status before resuming. A worker checkpoint is not review
approval or evidence of passing common tests. Keep these unmerged/unfinished tasks open.

| Task | Task ID | Directory / branch | Checkpoint and next action |
| --- | --- | --- | --- |
| V0c1 substitutions | `01a0d029-9c47-7600-8b5b-e10aeb7b3b9e` | `4983`, `codex/css-substitution-v0c1` | `2896d3e3d`; final provisional-wrapper correction needs independent re-review; full chain below |
| V0b3c exponential math | `01a0db10-f9d2-7501-872f-fcfe719fdbbb` | `4cb4`, `codex/css-math-exponential-v0b3c` | `f33b09c67`; clean; worker reports 564 focused/3,042 non-corpus tests combined; all five functions need independent review |
| HTML creation-time IsValue | `01a0db13-f920-7580-8392-b5798ebd7d93` | `aebc`, `codex/html-element-is-value` | `ef64460e2`; clean, worker reports 1,514 non-corpus tests per TFM; review then integrate |
| XML corpus | `01a0cf6e-aa5f-7631-af1b-03f6136013a1` | `b0f9`, `codex/xml-conformance-notations` | `dd367c16a`; compare all new pr-xml policy expectations with independently derived packet, then integrate/test |
| Architecture | `01a0ceec-94ec-7f63-9bfb-189cac69df5f` | `dc58`, `codex/html-parser-design` | `24321b557`; reviewed numeric design integrated; numeric runtime remains undispatched |
| Benchmarks | `01a0ceee-7b09-7513-b507-a5c412eb4518` | `b0b0`, `codex/html-parser-benchmark-xml` | `00c7b7ec1`; harness integrated; measurements unfinished |

Use **GPT-6 Astra High** for decisions, plans and reviews; **GPT-6 Sol High** dedicated local-worktree
chats for implementation. Reuse retained tasks and preserve exclusive file ownership. Merge only after
review and fresh relevant Release common checks, then verify clean/idle/patch equivalence before
archiving the task and its managed worktree. Do not create PRs without the user's request.

## Completed since the previous pause

- XPath full source chain through `50bfc1350` integrated through `c131b6a9f`. Cancellation now polls
  within arbitrarily long legal following-axis whitespace; independent re-review clear.
- Native/XML immutable IsValue source `eeb5b0c1a` integrated as `190f59aa9`.
  Public factories keep null; XML captures accepted null-namespace lowercase is attributes;
  clones/import preserve the creation value and adoption preserves identity.
- Template adoption source `a0ab062cc` + `326513ea4` integrated as `4f971ae0f` + `f3e6d0c6a`.
  **Inner recreation uses the stack common ancestor** (HTML step 13.6); **final recreation uses the
  furthest block** (step 17). Do not reinstate the earlier blanket inert-owner recommendation.
  Native reparenting between parser turns makes these distinct destinations observable.
- Trig source `db97abc02`, `3f7a25b58`, `6c552a0fd` integrated through `d43915da9`.
  Final correction preserves exact turn/grad quarter turns before canonical conversion; no epsilon.
  Astra re-review included all special-value matrix entries and 3,656 parse-level probes per TFM.
- Framesets source `150faca00`, `7cb1f9922`, `95519f45d` integrated through `e4e72d0e9`.
  Reviewed/tested/archived. TreeConstruction ownership transferred explicitly to HTML IsValue task.
- Japanese exact-six prepared inputs source `185837889` integrated as `2a41e82a4`; explicit common
  preparation and integrity checks passed. Three weekly source policies `3d6926625` and exact-key
  guardian correction `7ca8ae471` integrated as `fa6a79fd4` and `cca66f703`.
- Numeric design source `d1cfff5b9`, `24321b557` integrated as `9eeecd86e`, `5b6253947`, with approval
  clarification `51c6c3120`. Root verified pinned WPT huge-local empty-result assertion independently.

## Review handoffs

### CSS substitution analysis

Unmerged chain: `36bac02df`, `fdf2acd46`, `69d73b681`, `c36116fa6`, `2896d3e3d`.
Astra first review found argument-boundary declaration-value restrictions, free-form wrapper handling,
nested early-spread discovery and reserved env-name metadata defects. `c36116fa6` addresses those.
Second review found one remaining provisional-wrapper defect: `var(...var(--args),{red;blue})` and
`{!}` must remain Deferred when the immediate header spread can supply an effective comma.
`2896d3e3d` is the owner correction, not yet independently re-reviewed. Worker reports 84 focused
and 1,449 non-corpus cases per TFM passing, zero build warnings. Nested spreads unable to change the
outer comma must retain malformed-wrapper rejection. No substitution execution/property-validity or
WPT pass claim. Reuse reviewer `/root/substitution_review` if still available.

### Exponential math

Implements pow/sqrt/hypot/log/exp only. Reviewed dispatch is in `html-parser-css-math-followups.md`.
Log policy order: NaN, invalid base (negative or one), negative value, explicit value endpoints, then
ln(value)/ln(base); default base e. Bases signed zero/infinity use the documented quotient completion.
Pow NaN dominates exponent zero; large odd-integer detection cannot cast to Int64. Hypot uses scaled
O(n) work and NaN wins infinity. Preserve signed sqrt zero and exp endpoints. Function census should
be 21 implemented/zero pending; that does not mean CSS is complete. Fixtures are authored, not WPT.
Review all code/edge cases/cancellation independently before common integration. Source checkpoint
is `f33b09c67`; root verified its direct parent is common `d43915da9` (worker final’s different base
was stale). All retained worker checkouts were clean when inspected at this pause.

### HTML IsValue

Native/XML half is already common. `ef64460e2` captures accepted token metadata and preserves immutable
formatting-entry values through reconstruction/adoption, with split-input/quota/live-edit tests.
Owns `HtmlTreeBuilder.cs`, `.Formatting.cs`, `.AdoptionAgency.cs`, and `HtmlIsValueTests.cs` only.
Implied elements remain null and later html/body attribute merges cannot change creation metadata.
Preserve the distinct adoption allocation destinations above. Future foreign-content allocations must
pass metadata too. X3c HTML serialization requires both halves reviewed/integrated.

### Three pr-xml optional policies

Source packet is preserved in ignored common `artifacts/html-parser-review/pr-xml/` as well as
`/private/tmp/jint-pr-xml-source-review/`: `README.md`, `review-packet.json`, `derive.py`,
three `pr-xml-{euc-jp,iso-2022-jp,shift_jis}.projection.json` files and `unread-resource.json`.
These private corpus-derived files are deliberately not committed or published. Preserve them before
any future common-worktree archival, because managed snapshots exclude ignored files.
Root independently parsed each original verified decoded string using Python minidom and matched
all 6,283 projection nodes: 2,252 elements, 3,899 text, 116 comments, 14 CDATA, one PI, one doctype;
1,105 specified attributes, empty notations. Bodies and projections match across encodings.
Sole unread ExternalSubset: name empty, PublicId null, SystemId `spec.dtd`, original UTF-16 offsets
41/46/44 respectively; doctype PublicId is empty string. Pretty-JSON projection SHA
`ac02cb4c0267f7217734d092eb7dee58710408dedfe92e998150489c9034467c` is NOT harness compact-JSON digest.

All three originals contain `<!ENTITY lt "<">`, violating XML 4.6's declaration prescription
(an ordinary error, not a well-formedness constraint). Root explicitly approved exact optional recovery
that preserves mandatory predefined lt behavior per XML 1.2/4.6. Do not call the sources error-free,
generalize this to broad DTD lenience, or count these rows as required conformance passes.
Review `dd367c16a` against every packet field, preserve previous policy entries, update the exact-six
prepared-input guardian to these three Verified policies, and retain hash/integrity failures.
Expected corpus change only: optional observed three→zero, verified 24→27; required 127 and OUTPUT42
stay pending. Worker reports 4,022 total/3,766 passing/256 expected failures; root common run is pending.

### Later dispatches, not started

Numeric policy uses exact shortest-decimal remainders and uncapped finite-double calendar conversion.
The 1,024-bit bound applies only to calendar integers; decimal remainder arithmetic can exceed
2,098 bits. No numeric runtime task has been created.

Textarea D7b1c preflight is complete, with no implementation dispatched. Use a stable lazy
`HtmlElementState.TextArea`/`HtmlTextAreaState` slot; defer Input/general HtmlTextControl to b1d.
Native file scope: new textarea/selection/mutation helpers plus HtmlElementState, Node, CharacterNodes,
NodeCloner and narrow Element access. Keep parser lifecycle/notifications in b1e, no TreeConstruction
ownership overlap. Before dispatch, clarify these reviewed details in the source contract:

- Semantic children-changed hooks run independently of mutation-record suppression. Destination fragment
  insertion hooks once after the entire sequence; individual removals keep specified intermediate steps.
  Direct Comment/PI data replacement also triggers the hook although only Text/CDATA contributes text.
- Copy raw/dirty state before cloning descendants. A shallow clean clone initially retains copied raw
  text despite an empty default; its next child change resumes child-derived projection. Adopt keeps identity.
- Parsed append invalidates in O(1) without reading Text.Data or flattening/normalizing prefixes.
  Preserve destructive intermediate selection clamps; avoid quadratic replace-all behavior.
- Centralize automatic clamping and explicit selection operations separately for future notifications;
  add no placeholder event transport. Parser completion's Reset seam remains future b1e.

Other required work remains: HTML foreign content/fragments/patch-shadow branches, serialization/public
facades, all input-state families, CSS execution/colors/property registry/CSSOM/cascade, Browser bindings,
eventing/scheduling/AngleSharp cutover, full conformance/consumer verification and paired benchmarks.

## Validation and tooling

Always fresh Release, never `--no-build`; use `git -c core.fsmonitor=false` for Git operations.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName!~Xml.Conformance'
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName~Xml.Conformance'
```

Pinned archive common cache: `Jint.Tests.HtmlParser/Xml/Conformance/Cache/xmlts20130923.tar.gz`, SHA
`9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f`.
Explicit decoded preparation when needed:
`python3 Jint.Tests.HtmlParser/Xml/Conformance/Tools/import_corpus.py prepare-decoded`.
Do not turn missing corpus/prepared inputs into silent skips or claim a green full corpus.

App list_threads can omit newer tasks; use exact IDs above, wait_threads cursors, and inspect retained
worktrees instead of recreating tasks. Worker cross-task reports may be automatically rejected; read
local final/status rather than ask workers to retry rejected sends. Managed cleanup currently lacks
artifact identities; app computer-use access was denied too, so do not attempt a UI workaround.
