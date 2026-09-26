# Jint.HtmlParser resume checkpoint

**User resumed implementation (September 25):** continue until the replacement compiles and works;
the finite wrap-up below is historical. Production completion requires native Browser builds on both
supported TFMs, required Browser behavior/fixture verification, removal of production AngleSharp
references, standalone parsing entry points, and equivalent paired benchmark acceptance. Existing
conformance debt remains explicit; passing a missing-feature or checkpoint test is not completion.
No PRs. Astra High owns designs/reviews; Sol High owns implementation in local worktrees.

Current owners:

| Work | Chat | Checkout |
| --- | --- | --- |
| Browser DOM/generator/runtime/parser integration | `01a0db4d-a396-7e33-a770-ace95e2ad537` | `414c` |
| Browser Events, Page.Input, accessibility/extraction | `01a0db9d-701a-7752-8791-64eb54dd2d0c` | `68c5` |
| Native CSS sheets/rules/media/declarations | `01a0db8e-10ce-7671-ac02-2e224a13bb8d` | `16aa` |
| Native select/option state and shared native mutation hooks | `01a0dbbc-812f-77b2-9838-28183e25597d` | `eac8` |
| Internal input text/default value component | `01a0db8d-f2c1-7623-a908-49742dafdd77` | `757c` |
| Pure native input numeric/temporal algorithms | `01a0dbbc-8989-7002-a280-c16b0dfaf2c8` | `3c0a` |
| Contextual HTML fragments and parser form-pointer hookup | `01a0dbbf-2766-76f0-8065-4c7685e4a9cc` | `ceca` |

Events excludes shared `BrowserEventRealm.cs` and `DomHostHooks.cs`, retained by the Browser owner.
Select owns narrow Element/HtmlElementState/Attr/Node/CharacterNodes/NodeCloner hooks; numeric
helpers own new InputValues files only, and fragments own tokenizer/treebuilder/session paths.
CSS model work must preserve named unfinished-grammar blockers, rather than accepting invalid or
unimplemented declarations silently. Reviewed completed slices continue to land in common, and
Browser changes remain in `414c` until the package builds and works. Reopening CSS and input state leaves 66 completed
chats archived and 22 completed checkouts awaiting managed archive identities; previous counts below
refer to the finite checkpoint.

**Latest user-directed finalization (September 25):** reviewed native checkedness/radio state,
script source coordinates, and internal CSS declaration blocks are integrated through `c9925b18c`.
Fresh common Release non-corpus tests pass **4,482/4,482**, net8/net10, zero failures/skips:
`/private/tmp/jint-finalize-all-parser.log`. Production Browser still uses AngleSharp; its unfinished
native cutover stays isolated. No PRs or performance claims. Implementation expansion has stopped.

| Completed slice | Source commits | Common commits |
| --- | --- | --- |
| Native checkedness and incremental radio groups | `83ff093d8` | `ee422cefc` |
| Original script source-unit coordinates | `f874a0a42` | `9bc9157cf` |
| Internal validated declarations and serialization review fixes | `33ae2bb06`, `57d25d444`, `4f2391f8c` | `6e8915d7c`, `e8925567e`, `c9925b18c` |

All three slices passed independent GPT-6 Astra High review, were implemented in GPT-6 Sol High
local-worktree chats, and passed the combined common gate before those chats were archived.
Checkedness lifecycle changes also preserve the exact prior XML failure-name multiset:
4,022 total, 3,766 passed, 256 known debt failures, zero skips;
`/private/tmp/jint-finalize-checkedness-xml.log`, compared with `/private/tmp/jint-final-chats-xml.log`.

Explicit remaining seams: HTML tree construction does not yet call `AssociateFromParser` for
nonancestor form-pointer ownership; source attribution is unavailable for SVG scripts; CSS declarations
remain internal with 423 catalog grammars pending, and sheets/rules/cascade remain unfinished.
Script metadata distinguishes primary, inserted and mixed source units without changing expanded
input offsets. Browser still needs to consume the metadata in its native scheduler.

Earlier continuation integrations: compact CSS source ownership `ca376e0eb` → `c3256b793`, direct
attribute indexing `ea0d2010e` → `4f5d7109b`, Range read budgets `6795451c0` → `2b6cc11f2`, and
custom-property lexical provenance `7691e6680` → `ba5f3adcb`.

There are **68 archived completed chats and 24 retained completed checkouts** (44 previously
removed). Root and owning chats return empty managed-artifact lists, so these remaining completed
checkouts cannot be passed to `archive_worktree`. Do not invent identities or remove them by shell.
Unfinished Browser worktrees are preserved separately below.

## Earlier integrated checkpoints

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
- Earlier combined Release non-corpus parser validation: **4,158/4,158 passed**, zero failures/skips across net8/net10;
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
Its saved HEAD is **`1b723d891`**, including native textarea bindings, with the final handoff in
`docs/design/browser-native-cutover-progress.md` in that checkout. Fresh Release net8 build has
**932 errors, zero warnings**; Browser tests cannot run. Generator regeneration preserves 163
interfaces and 12 files with zero diagnostics. This is saved unfinished work, not common integration.

Accessibility/extraction chat `01a0db9d-701a-7752-8791-64eb54dd2d0c` in `68c5` owns a separate
unfinished native leaf checkpoint based on `c4005dea0`: `cdf59d402`, `b1725f7ee`, and
`90aa31b72`. Independent review cleared preservation after fixing foreign-input HTML semantics
and SVG-title leakage into HTML document naming; a reduced actual-source Release harness passed.
The leaf commits are now consolidated into isolated `414c` as `559fb89d8`, `3dc1e3cc7`,
and `fb853f441`; handoff commit `1b723d891` records the fresh failed build. Both implementation
turns have ended, their worktrees are clean, and no task-owned build/generator sessions remain.
It still needs input/select state, ContentEditing and actual native CssCascade. Full accessibility
and extraction golden tests are not verified. Retain this chat and checkout until that work is recovered.

Before resuming, merge the reviewed common prerequisites into the isolated Browser branch. Remaining
work includes native CSSOM/cascade, controls/validation, context-sensitive fragments, parser scheduling,
observers/events, runtime/DevTools consumers, generated bindings, host-budget seams and production
AngleSharp removal. Review and test the leaf before declaring it complete. Do not merge a broken
Browser build into common merely to close a chat.

The former D6r6 draft is fully recovered/superseded by the merged final implementation. Its stash
commit `386302865d1f5c92faf9e35bee5904b1e4edeaa9` and
`codex/native-live-traversal-d6r6-draft` branch are historical backups only.

## Chat completion and cleanup

Sixty-eight completed chats are archived. Forty-four checkouts were previously removed.
Twenty-four completed clean checkouts remain because root and owning chats expose empty `list_artifacts`
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
| `757c` | `01a0db8d-f2c1-7623-a908-49742dafdd77` | Native checkedness/radio groups |
| `631c` | `01a0dba6-8588-7262-8a1f-fdacf5bcc420` | Script source coordinates |
| `16aa` | `01a0db8e-10ce-7671-ac02-2e224a13bb8d` | Internal CSS declarations |

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
H7a supplies the per-read context and covers formatting reconstruction/CDATA. Native script handoff
H8 is integrated; Browser scheduling remains unfinished. Use the reviewed `html-parser-script-handoff.md` protocol after resumption. The internal HTML
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
