# Jint.HtmlParser resume checkpoint

**Active continuation from `0b9b921de`:** the full replacement goal resumed after the finite
wrap-up below. Browser cutover chat `01a0db4d-a396-7e33-a770-ace95e2ad537` is active again in
`414c`. New Sol High chats implement D7b2 checkedness/radio state (`757c`,
`01a0db8d-f2c1-7623-a908-49742dafdd77`) and validated CSS declaration blocks (`16aa`,
`01a0db8e-10ce-7671-ac02-2e224a13bb8d`). Range read-operation budget callbacks from
`e3b0` are now reviewed and integrated; chat `01a0db90-31aa-7a92-922a-b084def3fd98` is archived.
Astra High owns design and review. Completed D6/H8
owners released their files; D7b2 now exclusively owns the required Element/HtmlElementState/NodeCloner
hooks as well as the checkedness/form lifecycle files. CSS declarations do not own DOM, selectors,
Browser or public facade files. No PRs. Historical finalization/cleanup counts below remain unchanged.

**Script source metadata owner:** `631c`, chat `01a0dba6-8588-7262-8a1f-fdacf5bcc420`,
implements source-unit-aware positions for parser-created scripts. Normal input chunks share primary
coordinates; inserted input units retain separate coordinates, so inserted newlines cannot shift later
primary-script locations. The anchor is immediately after the start tag; tags and content crossing
units are explicitly mixed. Browser uses primary document lines and script-relative coordinates for
generated/mixed text. No D7-owned DOM files are reserved by this worker.

**Current continuation:** Browser checkpoint `92732c130` contains further native DOM adapters,
retained navigation target state and host-budget Range wiring, still isolated with an incomplete build.
Accessibility/extraction is delegated from that checkpoint to `68c5`, chat
`01a0db9d-701a-7752-8791-64eb54dd2d0c`; it exclusively owns those two implementation directories and
matching Browser test directories. The Browser owner keeps shared DOM, cascade, runtime and generator.
C1 custom-property lexical provenance source `7691e6680` is reviewed and integrated as `ba5f3adcb`.
Fresh common Release non-corpus parser tests: **4,262/4,262 passed**, both TFMs, zero failures/skips;
`/private/tmp/jint-css-lexical-span-common.log`. CSS declarations and native checkedness remain unmerged.
Review is correcting radio high-water storage and repeated attribute-scan costs, and verifying native
Browser target/slot budget semantics. No PRs or benchmark timings.

**Continuation integration:** compact CSS source ownership `ca376e0eb` → `c3256b793`, direct
attribute indexing `ea0d2010e` → `4f5d7109b`, and six read-only Range host-budget callbacks
`6795451c0` → `2b6cc11f2`. Fresh combined Release parser gate: **4,226/4,226 passed**, net8/net10,
zero failures/skips (`/private/tmp/jint-range-read-budget-common.log`). The Range chat is complete and
archived, with no owned processes. Its clean `e3b0` checkout is retained because its artifact list is
empty; there are now **65 archived completed chats and 21 retained completed checkouts** (44 previously
removed). Browser, checkedness and declaration implementation remain active. No PR or timing claim.

**Latest user-directed finalization (September 25):** the five remaining implementation chats
have reached saved checkpoints. Four reviewed slices are integrated through **`616bb320b`** in the
common worktree. The incomplete Browser cutover remains isolated. No new feature chats or PRs were
created during this finalization pass. The full replacement and performance objective is unfinished.

## Integrated checkpoints

| Completed slice | Source commits | Common commits |
| --- | --- | --- |
| D6r6 public Range/traversal, mutation subscriptions and abandoned endpoint cleanup | `b576210cd`, `a5a4dcb956` | `b55a70797`, `6e90de6e6` |
| H8 native parser script handoff and inserted-input sessions | `c926672c3` | `1001437d9` |
| Internal CSS catalog, initial validated property grammars and serialization corrections | `75999e8d0`, `c1b96b7c1`, `270d542ad` | `1763825d7`, `c45bb5671`, `0c05ccd37` |
| C3b selector interaction state and shared bounded work | `125605a71` | `616bb320b` |

All implementation used GPT-6 Sol High dedicated local-worktree chats and passed independent
GPT-6 Astra High review. Common sources match the reviewed commits. Review corrections release dead
range endpoint buckets, preserve nested SVG parser pause, preserve escaped CSS token contents and
exact integer serialization, and avoid quadratic root walks for nested active labels. Existing
selector cancellation assertions remain intact; the mixed unsupported-branch fixture now uses
`:checked`, while new tests cover the explicitly supported headless `:hover` policy.

## Common state and validation

- Worktree `/Users/sebastienros/.codex/worktrees/bd4c/jint`, branch `codex/html-parser-integration`.
- Production Browser still uses AngleSharp. No timing run or speedup claim was made in this pass.
- Final combined Release non-corpus parser validation: **4,158/4,158 passed**, zero failures/skips across net8/net10;
  `/private/tmp/jint-final-chats-combined.log`.
- Browser binding/prototype/identity plus generated-code staleness: **44/44 passed**, net8/net10;
  `/private/tmp/jint-final-chats-browser.log`, `/private/tmp/jint-final-chats-staleness.log`.
- D6 unsigned PackageReference consumer of the signed package passed on net8/net10 in common:
  `/private/tmp/jint-final-chats-consumer.log`.
- A1 dependency inventory matches and all six inventory tests pass.
- XML corpus after D6/H8 integration: **4,022 total, 3,766 passed, 256 existing failures**, zero skips;
  `/private/tmp/jint-final-chats-xml.log`. The entire failure-name multiset equals
  `/private/tmp/jint-wrapup-combined-xml.log`. This remains 127 pending required rows per TFM plus
  the census assertion on each TFM. Optional policies remain 27 verified; OUTPUT 344 compared/42 pending.
- Earlier benchmark correctness evidence remains `/private/tmp/jint-finalize-benchmark-validation.log`.
  Full conformance and equivalent paired performance acceptance are still required.

## Preserved unfinished Browser chat

Chat `01a0db4d-a396-7e33-a770-ace95e2ad537` remains open in
`/Users/sebastienros/.codex/worktrees/414c/jint`, branch `codex/browser-native-dom-cutover`.
Its clean saved HEAD is **`763a3ae3633e1ae0cf66aec857060bb1221570b9`**; production progress is
`eb9379071bfeee38c9f4fd1b550b5a07634327bf`. Read that checkout's
`docs/design/browser-native-cutover-progress.md` for the current handoff; its older handoff document
predates this continuation. Fresh Release net8 build has **1,058 errors, zero warnings**; Browser tests
were not run. Regeneration preserves 163 interfaces with zero generator diagnostics. No owned process
needs the checkout. Do not integrate this incomplete cutover merely to close its chat.

Before resuming, merge the reviewed common prerequisites into that isolated branch. Remaining work
includes native input/control state, CSSOM/cascade, runtime/parser scheduling, generated binding member
implementations and the remaining production consumers. The selector environment and H8 host protocol
are now implemented internally; Browser wiring is not. D7 checkable dispatch is approved in
`html-parser-checkable-dispatch.md` but was not started. CSS property-core follow-up is recorded in
`html-parser-cssom-core-checkpoint.md`; declaration blocks, sheets/rules/media and 423 pending catalog
registrations remain future work, not implemented CSSOM.

The former D6r6 draft is fully recovered/superseded by the merged final implementation. Its stash
commit `386302865d1f5c92faf9e35bee5904b1e4edeaa9` and
`codex/native-live-traversal-d6r6-draft` branch are historical backups only.

## Chat completion and cleanup

Sixty-four completed chats are archived. Forty-four checkouts were previously removed.
Twenty completed clean checkouts remain because root and owning chats expose empty `list_artifacts`
results; no managed identity is available for `archive_worktree`. Use managed archival when identities
become available; do not bypass it with shell deletion. The unfinished Browser checkout above is
retained separately.

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
| `7b39` | `01a0db36-97d7-7b90-a3a0-bfaaa7404cf6` | Browser binding contract |
| `dc57` | `01a0db38-75c8-72b2-a01f-8fdeddf59284` | Tokenizer context prerequisite |
| `3aad` | `01a0db32-26b3-7b90-a040-1a0f969affbc` | Native textarea and shadow stamps |
| `5b44` | `01a0db35-8c1b-7f03-9fa1-5009f6ba68f5` | Internal HTML serializer |
| `0c31` | `01a0db4e-2f7a-7e52-bc1f-000fcb158552` | H7a foreign-content parsing |
| `7331` | `01a0db52-0817-7031-a745-86ed8aa618ba` | Tokenizer inserted-input boundaries |
| `2d15` | `01a0db4c-ee87-7523-acc5-04bed9cd6e6e` | C6s CSS substitution execution |

| `e333` | `01a0db52-af75-7971-9e0f-682e63ae96b0` | D6r6 public traversal and mutation subscriptions |
| `af00` | `01a0db6e-c116-72e3-80cb-626149bab447` | H8 native script handoff |
| `c123` | `01a0db7b-ad3e-7a32-b64e-8584c8fa89eb` | Internal validated CSS property core |
| `19f0` | `01a0db7a-eb94-7f42-8443-92412a91810d` | C3b selector interaction state |

| `e3b0` | `01a0db90-31aa-7a92-922a-b084def3fd98` | Native Range read budgets |

Directories are `/Users/sebastienros/.codex/worktrees/<checkout>/jint`.

## Continuing the original project

Use GPT-6 Astra High for design/planning/reviews and GPT-6 Sol High dedicated local-worktree chats
for implementation. Review, integrate, and test in common before archiving finalized chats/worktrees.
No PRs until requested. The [implementation record](html-parser-progress.md), dependency inventory and
reviewed design documents preserve the full objective and finite dispatches.

Required future work includes HTML fragments/Browser script scheduling/patch-shadow branches, serialization
public facades and Browser consumers, textarea parser/event hooks, all remaining form/input state families, CSS colors/property
registry/CSSOM/cascade, Browser bindings/generator/events/scheduling and complete production AngleSharp
cutover, full conformance/consumer verification, and equivalent paired performance acceptance.

Completed foundation invariants to preserve:

- HTML adoption inner recreation allocates through the adjusted insertion location derived from
  the stack common ancestor (step 13.6), including template-target adjustment; final recreation uses
  the furthest block (step 17). The earlier blanket inert-owner recommendation was corrected. Native
  reparenting between parser turns makes these distinct destinations observable.
- Immutable IsValue captures accepted creation-time attributes; clones/import preserve it, adoption
  preserves identity, and live attribute changes/merged attributes never rewrite it. H7a foreign
  allocations and X3c serialization preserve this completed metadata contract.
- Exact turn/grad quarter turns must survive canonical conversion without epsilon matching.
- Source-preserving substitution analysis is not substitution execution or property validation.
- The three pr-xml originals contain `<!ENTITY lt "<">`, an ordinary XML 4.6 error, not a WFC.
  Explicit optional recovery preserves mandatory predefined lt semantics per XML 1.2/4.6. Do not call
  sources error-free, generalize to blanket DTD lenience or count optional cases as required passes.

Numeric input design is approved in `html-parser-input-numeric-policy.md`: exact shortest-decimal
remainders and uncapped finite-double calendar conversion. The 1,024-bit bound applies only to calendar
integers; remainder alignment can exceed 2,098 bits. Root verified the pinned WPT huge-local empty-result
assertion; keep the documented spec/WPT discrepancy explicit. No numeric runtime task was dispatched.

Textarea D7b1c is implemented and reviewed through `398f37085`. Preserve lazy raw freezing on an
invalid range edit, clean-clone raw/child alignment, null-namespace readonly checks, automatic versus
explicit selection steps, and intermediate replace-all clamps. The suffix algorithm is linear and
handles CR/LF across adjacent text nodes and excluded children. Independent review checked 4,000
mixed-child cases and 36,000 suffix lengths. Parser lifecycle and selection-event transport remain b1e.

The tokenizer context and inserted-input primitives and H7a foreign tree construction are complete.
H7a supplies the per-read context and covers formatting reconstruction/CDATA. Active script handoff
remains H8; use the reviewed `html-parser-script-handoff.md` protocol after resumption. The internal HTML
serializer is complete for its reviewed slice, including template/shadow traversal and IsValue;
public facade and Browser wiring remain separate. No performance claim follows from these tests.

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
