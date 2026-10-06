# Jint.HtmlParser resume checkpoint

**User resumed implementation (September 25):** continue until the replacement compiles and works;
the finite wrap-up below is historical. Production completion requires native Browser builds on both
supported TFMs, required Browser behavior/fixture verification, removal of production AngleSharp
references, standalone parsing entry points, and equivalent paired benchmark acceptance. Existing
conformance debt remains explicit; passing a missing-feature or checkpoint test is not completion.
No PRs. Astra High owns designs/reviews; Sol High owns implementation in local worktrees.

**Latest common integration: `674ae0fc2`.** The reviewed native Browser checkpoint `d5bac890c`
is merged. The two runtime conflicts resolve to the reviewed integration sources, preserving both
task-drain deferral and recovery-before-dequeue. Production Browser now references Jint.HtmlParser.
Fresh common Release/net10 evidence:

- Parser gate excluding `XmlConformanceTests`: **4,515 total, 4,511 passed, four failed, zero skipped**
  (`/private/tmp/jint-native-common-parser-checkpoint-net10.log`). One is the known XML corpus census;
  one is a stale unsupported-`:checked` fixture. Two are real substitution regressions: replacement
  children were truncated by source-gap pairing, and artificial boundary markers rejected a valid
  65,536-token expansion. Reviewed production repair `024f84e3d` preserves the original assertions
  and token/spelling/source limits; integration and rerun are pending.
- Browser excluding its WPT namespace: **3,701 total, 3,629 passed, 34 failed, 38 skipped**
  (`/private/tmp/jint-native-common-browser-checkpoint-net10.log`). Remaining work includes import
  loading, resolved box values, container/background-clip grammar, stylesheet request invalidation,
  blockified-link extraction, and individually reviewed fixture/binding corrections. This remains
  a failed acceptance gate; the prior full WPT-inclusive inventory is historical.
- Runtime task recovery: **18/18**, public automatic task draining: **3/3**, zero failures/skips
  (`/private/tmp/jint-native-common-task-recovery-net10.log`,
  `/private/tmp/jint-native-common-public-task-net10.log`). Public API baselines remain deferred as below.

Native META, individual transforms, transform lists, passive keyframes, and native import-model/
revision-snapshot chats are archived after common integration and their scoped passes. Current totals:
**77 archived completed chats, 33 retained completed clean checkouts**. Each of these five owners
returned an empty artifact list, so no managed worktree identity was available for archival; no manual
worktree deletion was performed. Active repair and Browser consumer owners remain open.

**Earlier independent common integration: `8b296d34d`.** The internal Browser task-drain deferral
(`49cf7e5c0`) is reviewed and integrated. Fresh common Release/net10 gates pass **11/11** internal
task-deferral cases and **3/3** unsigned public host-contract cases, zero failures/skips
(`/private/tmp/jint-common-task-drain-net10.log`, `/private/tmp/jint-common-public-task-drain-net10.log`).
The public focused command uses `-p:RunsPublicApiBaselines=false` to avoid its automatic all-TFM build;
the final full framework/API baseline gate must run normally. The feature chat is archived. Its clean
`6711` checkout is retained because its `list_artifacts` returned no managed archive identity.

**Build feedback amendment (user request):** routine implementation builds and tests now target
Release `net10.0` only. Validate every supported target framework at the final integration gate;
this defers the other framework legs, not their acceptance requirement. Never use `--no-build`.

**Latest isolated evidence, September 26:** the reviewed native import model/keyframes pass
**76/76** (`/private/tmp/native-import-keyframes-net10-test.log`), and transform lists/matrices pass
**272/272** (`/private/tmp/native-css-transform-list-matrix-net10-test.log`). Child resource ordering,
META recovery and seed preparation pass **70/70**
(`/private/tmp/native-browser-child-resource-boundary-net10-test.log`). Final frame preparation and
file-history recovery then pass their selected cases in a **71/72** Browser gate
(`/private/tmp/native-browser-frame-token-file-transform-net10-test.log`); the sole failure expects
one particular invalidation message although another native guard correctly rejects publication.
Reviewed correction `1040dcc22` accepts only those two exact messages and also asserts no matrix
was published. Its rerun is pending. The exact handler-count WPT variants pass **3/3**, zero skipped
(`/private/tmp/native-browser-handler-count-variants-net10-test.log`). These overlapping focused
gates do not supersede the failed full Browser acceptance run below.

The final frame cancellation fix `50bede540` is source-clear and tested: a constraint callback that
cancels and returns cannot consume pending work or commit navigation. File-history queue identity
fix `cf4d99fdc` is reviewed, integrated, and its original regressions pass. Native import graph
revision snapshots (`f1e0897259`) and named transform accessors (`00c19cd10`) are source-clear;
consumer wiring and generated bindings remain separate gates. Browser import loading is assigned
to `16aa`, with shared scheduling/fetch hooks owned by `414c`; queries never initiate fetching.
Resolved box values are assigned to `902c`. Text-decoration production is reviewed with a narrow
inline-element fixture correction pending. Finalized slices will move into common at a reviewed
checkpoint before these later feature assignments finish.

**Demand boundary amendment:** [parsing and lazy behavior](html-parser-demand-boundary.md) follows the
user's latest direction. Input value initialization is lazy at `825eba36e`; select derived views and
inventories are lazy at `96827c3d9`, preserving intrinsic history. Numeric/temporal values and number
editing use that same demand-created state (`e9c95b864`, `fbc35417c`, `1ef6fb2c7`, `c4c71a95c`).
CSS cascade belongs to an on-demand Browser module. Ordinary parsing must not invoke
numeric/temporal conversions or style computation.

**Latest common gate: `053372065`, 3,452/3,452 net10.0 Release non-corpus tests**, zero failures/skips
(`/private/tmp/jint-resumed-checked-callbacks-net10.log`). Input, textarea, select and checkedness
invocation checkpoints are reviewed and integrated, including preallocation cancellation and repeated
counter fixes. Native form-owner revisions/FACE category/parser exclusions and qualified-name mutation
snapshots are integrated. Earlier combined gate passed 3,432/3,432 at `ac011d61e`
(`/private/tmp/jint-resumed-native-callbacks-net10.log`). The original textarea allocation regression
was corrected in production; its allocation assertion was preserved.

Paired HTML benchmarks (`ce7010d0b`) pass correctness-only validation for four complete trees,
count-preserving corruption probes, and 256 cold/first/warm control reads
(`/private/tmp/jint-resumed-html-comparison-common.log`). No timings or speedup claim yet.

Build contention prompted temporary serialization: implementation and source reviews continue, while
the coordinator grants one Release net10 build/test slot at a time. Final cross-framework validation
remains required. The isolated Browser builds successfully at `eedb918a5` and again at `8fc894c11`,
**zero warnings and errors** (`/private/tmp/native-browser-docs-net10-build.log` and
`/private/tmp/native-browser-request-identity-net10-build.log`). Its own restored production assets
contain no AngleSharp libraries; test-only provenance packages are separate. Earlier production
inventories of 259, 131, 80 and 29 errors are historical.

The Browser test-project inventory fell from 187 to 94 errors after the reviewed signed test-friend
grant (`/private/tmp/native-browser-test-friend-net10-build.log`). Most remaining errors belong to the
old cascade fixture. Its replacement preserves actual lazy-work measurements through optional internal
query diagnostics, corrects explicitly reviewed CSS inheritance expectations, and requires native
nested-rule support. These are separate source packets, not accepted runtime behavior yet.

That compile inventory is now historical: isolated `741bdb127` builds **Jint.Browser and its test
project with zero warnings/errors** (`/private/tmp/native-browser-root-watch-net10-build.log`).
The first core runtime gate passes **231/234**, with three failures and no skips
(`/private/tmp/native-browser-core-net10-test.log`): one reviewed declared-color fixture correction
and two frame cases exposing automatic timer draining during parser script evaluation. The focused
Events/extraction/observer gate passes **186/195**, nine failures and no skips
(`/private/tmp/native-browser-events-extraction-net10-test.log`): eight missing CSS association/query
paths and one reviewed Unicode-sets pattern fixture correction. These are incomplete runtime gates.
Dedicated fixes preserve generic Engine task behavior, establish actual document CSS realms, and
provide the existing native query to engine-free extraction without a fake realm or eager computation.

**Latest isolated runtime gates:** reviewed parser style completion, CSS nesting, stylesheet sets,
and query diagnostics pass **55/55** fresh Release/net10 tests at `98b2b75c5`
(`/private/tmp/native-style-completion-nesting-net10-test.log`). Browser watch/adoption, request identity,
frame scheduling, manufactured-document CSS realms and configuration pass **23/23**, zero failures/skips
(`/private/tmp/native-browser-watch-adoption-net10-test.log`). The frame failures above are fixed;
these focused passes do not establish full Browser acceptance. The full fresh net10 Browser run at
`72809d818` finished with **3,964 total: 3,644 passed, 279 failed, 41 skipped**
(`/private/tmp/native-browser-full-net10-test.log`; complete grouped inventory
`/private/tmp/native-browser-full-net10-failures-final.json`). Failures include binding, event,
resource, selector and CSS grammar issues plus stale WPT exclusions. This is a failed acceptance gate.
In particular, unsupported native `:checked` and other state predicates cause many layout tests to fail
before reaching cache assertions. Fix production behavior and review expectation corrections individually.

Subsequent isolated `00658cf48` focused Browser gate: **462 total, 410 passed, 52 failed, zero skipped**
(`/private/tmp/native-browser-state-image-ce-inert-net10-test.log`). All selected image-loading,
custom-element reaction, accessibility/golden, inert CSS, text-extraction, and event-timing cases pass.
Most remaining failures are missing validity/editability selector facts; imports, child target selection,
resource-only cache coverage, and separately reviewed legacy literals remain. Native query factory /
state-selector / work gate passes **33/35**, with two new fixture-initialization assumptions under repair;
all factory scaling/cancellation cases pass (`/private/tmp/native-queryfactory-elementstate-net10-test.log`).

Subsequent isolated gates supersede those focused failure inventories:

- Native query factory, selector state and font weight: **87/87 passed** at `40ff526e9`
  (`/private/tmp/native-queryfactory-state-fontweight-net10-test.log`).
- Native supports, lexical serialization and media: **217/219 passed**, zero skipped, at `cd920e5b7`
  (`/private/tmp/native-supports-lexical-media-net10-test.log`). The two new lexical fixtures used invalid
  declaration inputs. Reviewed test repair `715d4762c` preserves exact token-boundary assertions with
  valid function-contained delimiters; its rerun is pending.
- Browser supports, media, reflection, collection counts, loaded stylesheet moves and geometry:
  **280/286 passed**, zero skipped, at `9e1195ba2`
  (`/private/tmp/native-browser-supports-media-reflection-count-net10-test.log`). Remaining failures
  are font-size computation, physical padding support and explicitly reviewed SVG reflection / detached
  document readiness expectations. The latter focused fixture gate now passes **18/18**
  (`/private/tmp/native-browser-svg-ready-fixtures-net10-test.log`).
- The internal healthy task-start hook passes **16/16** focused core cases and **3/3** public automatic
  task-drain cases, net10 (`/private/tmp/native-core-task-start-net10-test.log`,
  `/private/tmp/native-core-public-automatic-net10-test.log`). An additional original-unwind regression
  is source-clear; its net10 rerun remains pending. Browser recovery wiring is separate.

Native META capture through `a79485ce4` is now source-clear, including preallocated failed-prefix
publication, textarea invalidation and deferred cross-document Range targets; its isolated net10 gate
passes **195/195**, zero skipped (`/private/tmp/native-meta-mutation-range-net10-test.log`).
Native host-fact wiring through `79438c221` is source-clear;
ordinary candidates share document witnesses rather than accumulating quadratic observations. Browser
file-history reconciliation must finish before seed capture and retain its cursor across cancellation;
the producer through `cced7eb71` is source-clear, while DOM/CSS callsite wiring remains unfinished.
Font-size finite scaling is source-clear through `7f180d98a`; the consolidated native control-facts,
lexical and font-size gate passes **152/152**, zero skipped
(`/private/tmp/native-control-facts-lexical-fontsize-net10-test.log`). Physical spacing, individual
transforms, text alignment and passive classic keyframes are reviewed and integrating in `414c`.

The first Browser META consumer gate passes **35/35**, zero skipped, at `157768a3b`
(`/private/tmp/native-browser-meta-consumer-net10-test.log`). Subsequent source review found two
uncovered boundary defects: unbracketed idle recovery needs a short recovery-only page budget, and
task-start recovery must succeed before consuming a queued task. Repair `936bebcc8` is under review;
that focused pass is not final consumer acceptance. Task FIFO and original failure recovery must be
verified again after the repair.

Transform lists/resolved matrices and native import rules now have dedicated Sol High worktrees.
The approved import design uses incremental `MayContainImport` scanning and materializes a single real
stylesheet only for candidate-bearing sources before dependency loading. Declaration values remain
lazy, zero-import resources remain unmaterialized, and CSS queries never initiate network requests.
The Browser import loading graph remains a separate consumer assignment.

The benchmark project freshly compiles with zero warnings/errors at `1e6049eda`
(`/private/tmp/native-benchmark-build.log`). AngleSharp dependencies are explicit benchmark-only
references; no timing comparison has run. These isolated results do not replace the failed full Browser
gate, common-worktree validation or final all-framework/API/package checks.

Engine-free CSS registration (`72d88eda1` + `ec7e37fcc`) is source-clear and integrated in isolated
Browser: verified-root association avoids repeated ancestor walks; deep-style scaling is tested.
Custom-property lexical serialization (`5e7313c21` + `324533eda`) is source-clear and integrated.
The native META blockers described above are repaired; Browser delivery remains in progress.
Typography/media and host control facts have dedicated implementation owners. No full Browser,
final framework, or speedup acceptance yet.

Native two-phase mutation notifications and frozen attribute values pass **15/15** focused fresh
Release net10 tests at isolated `39e027033`, zero failures/skips
(`/private/tmp/native-mutation-order-fixed-net10-test.log`). The reviewed stylesheet producer through
`f8483210f` preserves FIFO disabled transitions, element-owned enablement history, association ordering,
and bounded updates. Parser completion, removal/adoption and shadow-tree lifecycle wiring remain a
separate consumer gate. Request identities and stale success/failure/event guards are source-reviewed
through `6283ba315`; their Browser regressions remain unrun. These isolated gates are not a functional
replacement or common-worktree pass.

Subsequent isolated source progress: token-list caching, native receiver mappings and production
package-reference removal are reviewed; stylesheet association/history still has open findings.
The reviewed CSS demand chain through `5440b533a` passes **1,275/1,275** fresh Release net10 CSS tests
(`/tmp/jint-css-saturated-publication.log`), zero failures/skips. This is an isolated focused gate,
not a common-worktree or Browser runtime pass. Native `CSS.supports` is being implemented separately;
its query helper does not imply support for `@supports` stylesheet execution.

The shared DOM/PI changes preserve the XML corpus's exact prior failure-name multiset:
4,022 total, 3,766 passing, 256 known failures, zero skips, both TFMs
(`/private/tmp/jint-resumed-h6f-xml.log`). This is unchanged debt, not a passing conformance gate.

Earlier common Release validation at `0f7291a92`: **6,446/6,446**, zero failures/skips,
net8/net10 (`/private/tmp/jint-resumed-lazy-checked-common.log`). This includes reviewed lazy
non-radio checkedness, PI attribute/data coherence and specified CSS color grammar/fixes.
Parsed inputs and cold clones retain
no value sidecar; raw attributes, ordinary selectors and serialization remain cold. First semantic
access initializes once, while type/multiple transitions preserve observable history. Radios retain
the state needed for peer-exclusion history. Select derived-view laziness remains in progress.

Browser binding checkpoint `d30175b2d` reduces its local build to 414 unique errors; it is under
review and is not a passing native Browser build. Events source checkpoint `6de325e084` is reviewed,
but runtime tests remain blocked by shared compilation. CSS cascade, native parser scheduling and
public HTML entry points are still being integrated. Do not claim replacement or speedup acceptance.

**Earlier completed test gate: `d196ec650`.** CSS sheets/media and sizing/flex/alignment grammars,
input value state, and pure numeric/temporal helpers pass **5,898/5,898** fresh Release non-corpus tests
across net8/net10 (`/private/tmp/jint-resumed-temporal-common.log`). Shared atomic user-edit accounting
and combined select/input clone hooks at `7d5acec6a` then pass **6,026/6,026**, zero failures/skips,
both frameworks (`/private/tmp/jint-resumed-select-common.log`).
Numeric-helper chat `01a0dbbc-8989-7002-a280-c16b0dfaf2c8` is archived after review/integration/tests;
its clean `3c0a` checkout is retained because both root and owner return no managed archive identity.

**Previous integrated checkpoint: `6e3273234`.** Reviewed number parsing/shortest formatting
(`c9885646d`) and contextual HTML fragments with bounded parser form association
(`df35c6263`, `6e3273234`) pass **5,224/5,224** fresh Release non-corpus tests across net8/net10,
zero failures/skips (`/private/tmp/jint-resumed-fragments-common.log`). Browser work remains isolated;
its latest build stops at early declaration errors that mask later diagnostics. No passing native
Browser build or speedup claim yet. CSS media, input value state and select state are under review.

Current owners:

| Work | Chat | Checkout |
| --- | --- | --- |
| Browser shared DOM/runtime/parser integration | `01a0db4d-a396-7e33-a770-ace95e2ad537` | `414c` |
| Browser contract/generator/native binding consumers | `01a0dbf5-ad50-79d2-9253-109314f39e60` | `b78d` |
| Browser Events, Page.Input, accessibility/extraction | `01a0db9d-701a-7752-8791-64eb54dd2d0c` | `68c5` |
| Browser demand-driven CSS cascade and style consumers | `01a0db8e-10ce-7671-ac02-2e224a13bb8d` | `16aa` |
| Native CSS nesting and demand-driven selector state/fact contracts | `01a0dcba-a79a-78c3-bf32-08d08a406acb` | `cc99` |
| Native CSS supports grouping model and bindings | `01a0dcf9-5974-7d13-9ebf-e82b0d4ebf40` | `2187` |
| Native individual transforms and Browser computation | `01a0dd07-2a1f-7d82-860e-46276a70de8a` | `529a` |
| Native passive classic keyframes and CSSOM bindings | `01a0dd0d-ea51-7291-a2e6-4e0c5addd14a` | `280c` |
| Native transform lists and Browser resolved matrices | `01a0dd22-35ab-7fa1-80ff-7151a2075073` | `6776` |
| Native import rules and incremental cold discovery | `01a0dd23-5753-7e03-8c45-93553ca097cb` | `79cd` |
| Browser-only task-drain deferral (common verified, chat archived) | `01a0dccf-f06a-7fc0-8b93-34b98f3e110e` | `6711`, retained without managed archive identity |
| Native select/option state (complete, chat archived) | `01a0dbbc-812f-77b2-9838-28183e25597d` | `eac8` |
| Lazy input/value/checkedness producers (complete, chat archived) | `01a0db8d-f2c1-7623-a908-49742dafdd77` | `757c` |
| Pure numeric/temporal helpers (complete, chat archived) | `01a0dbbc-8989-7002-a280-c16b0dfaf2c8` | `3c0a` |
| CSS layout/color grammars (complete, chat archived) | `01a0dbc8-9d9a-73f0-a487-5027ce7b3501` | `08f4` |
| HTML facade/option completion/paired benchmarks (complete, chat archived) | `01a0dbbf-2766-76f0-8065-4c7685e4a9cc` | `ceca` |
| Browser lazy media/canvas/dialog capabilities | `01a0dc24-22c8-79d1-a3fd-f6a671706229` | `7778` |
| Native CSS.supports queries, typography and media features | `01a0dc8b-46e1-7570-a17f-0c709bc93fa9` | `71b3` |

Events excludes shared `BrowserEventRealm.cs` and `DomHostHooks.cs`, retained by the Browser owner.
Select owns narrow Element/HtmlElementState/Attr/Node/CharacterNodes/NodeCloner hooks; numeric
helpers own new InputValues files only, and fragments own tokenizer/treebuilder/session paths.
CSS model work must preserve named unfinished-grammar blockers, rather than accepting invalid or
unimplemented declarations silently. Reviewed completed slices continue to land in common, and
Browser changes remain isolated until the package builds and works. After numeric-helper, CSS color
and HTML facade/benchmark completion, 72 completed chats are archived and 28 completed checkouts await
managed archive identities. Root and the completed owners returned empty artifact lists; no invented identity or shell
removal was used. Unfinished worktrees remain active. Previous counts below are historical.

**Historical user-directed finalization (September 25, before resumption):** reviewed native checkedness/radio state,
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
`9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f`. The tests download it on first
use and produce the decoded Japanese inputs beside it; nothing in `Cache/` is committed.
Missing corpus/prepared inputs must fail visibly, never become silent skips.

Always fresh Release, never `--no-build`; Git uses `git -c core.fsmonitor=false`.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -f net10.0 -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName!~Xml.Conformance'
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -f net10.0 -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName~Xml.Conformance'
```

Benchmarks retain normal gate configuration and verified idle-machine requirements, with net10-only
primitive runs, equivalent AngleSharp/native work, Op/s and allocations. No short-job claims or idle
bypasses. HTML/CSS comparison needs matching complete native APIs; tokenizer work is not equal to DOM
construction. Exact commands and comparison limitations are in both benchmark corpus READMEs.

App list_threads may omit newer/archived tasks. Use recorded IDs and wait_threads for task state;
do not recreate missing-looking chats. Worker cross-chat reports can be automatically rejected; read
local final/status rather than request retries. The original large goal remains incomplete.
