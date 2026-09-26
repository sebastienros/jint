# Jint.HtmlParser resume checkpoint

**Latest user-directed wrap-up (September 25):** all five remaining chats have stopped at saved
checkpoints. Reviewed, coherent changes are integrated through **`10f8ee2ac`** in the common worktree.
Three completed chats are archived; two chats retain unfinished work. No new feature chats were started
in this finalization pass. The full parser/Browser replacement goal remains incomplete.

## Common state and validation

- Worktree `/Users/sebastienros/.codex/worktrees/bd4c/jint`, branch `codex/html-parser-integration`.
- Production Browser still uses AngleSharp. The unfinished native runtime migration is isolated.
  No PRs or benchmark timings were created; there is no speedup claim.
- H7a foreign-content construction: source `1a70fa588`, common `c32b9ba2d`.
- Tokenizer inserted-input boundaries: source `35770d0e2`, common `6dda5f7d7`.
- CSS substitution execution: sources `a91bcdd29`, `4c9997c2b`, common `b65500bbc`, `198870baf`.
- Generator contract type selection: source `632db0093`, common `60e4454e5`. Independent Release
  generation against the existing contract produced all 12 files byte-for-byte unchanged.
- Live Range/character data/iterator/TreeWalker/content operations: sources `26614c167`, `2608427ed`,
  `a0cecf889`, `2ab8c12cb`, `d4ac3df8e`, `798958a6a`; common `fbef2a152`, `9ff205c4b`, `099755b11`,
  `c458996f4`, `3372c1013`, `10f8ee2ac`. D6r6 notification/publication draft is excluded.
- All integrated implementation commits passed independent Astra High source review. Common source trees
  match the reviewed worker trees. Review fixed quadratic normalization/weak-registration work and
  bounded expanded CSS headers before flattening.
- Fresh common Release parser non-corpus tests: **3,864/3,864 passed**, zero failures/skips across
  net8/net10: `/private/tmp/jint-wrapup-combined-parser.log`.
- Fresh common Browser contract/staleness/prototype/identity tests: **44/44 passed**, both TFMs:
  `/private/tmp/jint-wrapup-browser-contract.log`.
- Fresh common XML corpus: **4,022 total, 3,766 passed, 256 existing debt failures**, zero skips:
  `/private/tmp/jint-wrapup-combined-xml.log`. All 256 failing test names exactly match
  `/private/tmp/jint-finalize-four-chats-xml.log`; there are no added or removed failures.
  This remains acceptance debt: 127 pending required cases per TFM plus two census assertions.
  Optional policies remain 27 verified; OUTPUT remains 344 compared/42 pending.
- Earlier benchmark correctness evidence remains in `/private/tmp/jint-finalize-benchmark-validation.log`:
  12 baseline fixtures, four native/AngleSharp XML/SVG comparisons and corruption probes, and primitive
  comparisons. This wrap-up did not rerun timings or establish performance acceptance.

## Preserved unfinished chats

| Chat | Checkout / branch | Saved work and next step |
| --- | --- | --- |
| `01a0db4d-a396-7e33-a770-ace95e2ad537` | `414c`, `codex/browser-native-dom-cutover` | WIP `233d0aa6a`; incomplete native Browser switch, not merged. Read that checkout's `docs/design/browser-native-cutover-handoff.md`. Last Release net8 build stops at four declaration errors and masks more gaps; no passing Browser tests for this branch. |
| `01a0db52-af75-7971-9e0f-682e63ae96b0` | `e333`, `codex/native-live-traversal` | Reviewed D6r2–r5 through `798958a6a` integrated. D6r6 scheduling-notification draft preserved as stash commit `386302865d1f5c92faf9e35bee5904b1e4edeaa9`, also anchored by branch `codex/native-live-traversal-d6r6-draft`. |

Both working trees are clean and their implementation turns are finished. Keep these chats/worktrees.
The D6r6 draft is a stash-shaped commit (including an untracked-files parent), based before the final
performance corrections. Recover deliberately and reconcile with current common; do not cherry-pick
it as a finished implementation. Remaining D6 work includes notifications, public XML docs/API snapshots,
unsigned packed-consumer verification and broader contract acceptance. Native PI pseudo-attribute
metadata remains absent. Browser still needs script/session handoff, fragments, live selector state,
CSSOM/cascade, forms and the remaining production consumer migration. Do not merge the WIP runtime
merely to close its chat.

## Chat completion and cleanup

Sixty completed implementation chats are archived, including the three finalized here. Forty-four
checkouts were previously removed. Sixteen completed clean checkouts remain because root and owning
chats return empty `list_artifacts` results, so no managed identity is available for `archive_worktree`:

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

Directories are `/Users/sebastienros/.codex/worktrees/<checkout>/jint`. Use managed archival when
identities become available; do not bypass it with shell deletion. App computer-use access was also
previously denied. The two unfinished checkouts above are retained separately from this cleanup list.
No implementation or review task remains running at the end of this pass. No PRs were created.

## Continuing the original project

Use GPT-6 Astra High for design/planning/reviews and GPT-6 Sol High dedicated local-worktree chats
for implementation. Review, integrate, and test in common before archiving finalized chats/worktrees.
No PRs until requested. The [implementation record](html-parser-progress.md), dependency inventory and
reviewed design documents preserve the full objective and finite dispatches.

Required future work includes HTML fragments/script handoff/patch-shadow branches, serialization
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
