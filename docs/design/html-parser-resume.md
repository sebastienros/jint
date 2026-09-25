# Jint.HtmlParser pause and resume checkpoint

User requested another recoverable pause on 2026-09-25 because the token budget was running low.
The goal is **not complete**. Resume the original full scope, not just implemented subsets.
This checkpoint supersedes the September 23 state; Git history retains that earlier record.

## Verified common state

- Worktree `/Users/sebastienros/.codex/worktrees/bd4c/jint`, branch `codex/html-parser-integration`.
- Production implementation checkpoint `93c9e8e84`; subsequent `b892a78df` and this checkpoint
  preserve documentation. No unreviewed worker implementation was merged during wrap-up.
- Browser still uses AngleSharp. Fresh pause-time Release Browser build passed net8/net10 with
  zero warnings/errors: `/private/tmp/jint-html-parser-sept25-pause-browser-build.log`.
- Latest common non-corpus parser suite: **2,820 passed, zero failures/skips**, net8/net10 combined.
  `/private/tmp/jint-templates-integrated.log`. No production code changed after this test run.
- Latest common XML corpus: **4,000 total, 3,732 passed, 268 failed, zero skips**.
  Per framework: 1,820 conformance passes, 127 unresolved required cases, six optional adapter
  debts, 21 verified optional policies, zero harness failures/mismatches; OUTPUT 344 compared,
  42 pending and 52 no-fetch alternatives. `/private/tmp/jint-resumed-xml-twentyone-integrated.log`.
  These failures remain visible acceptance debt; XML conformance is not complete.
- No comparative timings or speedup claim. Primitive timing is net10 only, on an idle machine,
  with equivalent AngleSharp/native work; the existing harness is not a performance result.
- No PRs. Forty-three completed tasks were reviewed, merged, tested, archived and their worktrees
  removed. Unfinished worktrees below are retained, including failed/interrupted tasks.
- Active tasks were asked to checkpoint and stop. Frameset and trig tasks ended with authentication
  failures before their final commits; the coordinator preserved their exact remaining files in
  explicitly labelled WIP commits. No implementation changes were made by the coordinator.
  Process audit found no matching task-specific dotnet test/MSBuild processes still running.

The objective remains HTML/XML/SVG/CSS parsing, mutable native DOM and required Browser semantics,
complete production AngleSharp replacement, conformance and paired benchmarks. The architecture,
dependency inventory and `html-parser-progress.md` retain the full completion gates.

## Retained tasks

Directories below are under `/Users/sebastienros/.codex/worktrees/`, ending in `/jint`.
Recheck actual HEAD, clean status and task status before resuming. A checkpoint commit is not review
approval or proof that its final contents have passed tests. All listed worktrees were clean at pause.

| Task | Task ID | Directory / branch | Checkpoint and next action |
| --- | --- | --- | --- |
| H6e framesets | `01a0dab4-0ad4-7720-ada6-80c245e9c7ff` | `ad95`, `codex/html-framesets-tails` | WIP `150faca00`; build/test incomplete; separately fix template ownership defect below first |
| X4b3 XPath | `01a0d011-17de-7c01-92b6-cbb7cfcba6ed` | `ff28`, `codex/x4b3-xpath-identifiers` | `f795d1350`; review blocker in following-axis whitespace cancellation |
| V0b3b trigonometry | `01a0d029-93fb-7352-bf0c-ff2c92aec72d` | `ba36`, `codex/css-math-trigonometric-v0b3b` | WIP `3f7a25b58` after `db97abc02`; recover verification evidence, then full review |
| V0c1 substitutions | `01a0d029-9c47-7600-8b5b-e10aeb7b3b9e` | `4983`, `codex/css-substitution-v0c1` | WIP `fdf2acd46` after `36bac02df`; final four tests and guard change need fresh build/tests |
| XML corpus | `01a0cf6e-aa5f-7631-af1b-03f6136013a1` | `b0f9`, `codex/xml-conformance-notations` | `185837889`; prepared-input adapter implemented, independent review and common tests pending |
| Architecture | `01a0ceec-94ec-7f63-9bfb-189cac69df5f` | `dc58`, `codex/html-parser-design` | `24321b557`; numeric policy reviewed with source caveat below; two document commits unmerged |
| Benchmarks | `01a0ceee-7b09-7513-b507-a5c412eb4518` | `b0b0`, `codex/html-parser-benchmark-xml` | `00c7b7ec1`; harness integrated, timings unfinished |

Use GPT-6 Astra High for decisions/plans/reviews and dedicated GPT-6 Sol High local-worktree tasks
for implementation. Reuse retained tasks. Merge only after review and fresh relevant Release common
checks; then verify clean/idle/patch equivalence and archive/remove completed worktrees. No PRs.

## Resume priorities and review findings

### Fix common template ownership before more HTML integrations

Ordinary templates landed in `93c9e8e84` and passed existing common tests. A subsequent independent
probe found an uncovered defect in `HtmlTreeBuilder.AdoptionAgency.cs`, `CreateFromFormattingEntry`
(around line 492): it allocates via `_document` instead of the actual insertion destination's owner.
Parse `<template><b><p>x`, then feed `</b>` at quota 1. Existing text `x` briefly acquires the active
owner during child transfer, then returns to the inert template owner during replacement insertion.
This violates destination-owner-before-allocation and adds spurious adoption. Fix both recreation
sites: inner recreation must use the actual old node parent destination; final replacement uses the
furthest block's owner. Verify the algorithm destinations rather than blindly changing one field.
Assert element/attribute/existing-descendant ownership at every yield for both paths, including
no extra adoption effects. Probe directory: `/tmp/h6d-review-cqyk2qju`.

H6e owns TreeConstruction and was assigned this as a **separate additive commit**. It is not fixed in
its paused frameset WIP. Preserve frameset edits while isolating the fix; review it independently.
Framesets, foreign content, fragments, and newer template patch/shadow branches remain incomplete.
Slots' full reviewed chain is integrated through `1c4c4cfbb`; its task was archived after common tests.

### XPath

Unmerged chain: `3e00d8717`, `2c61763d8`, `f795d1350`.
Astra exact-source Release probes passed net8/net10 for ID/session freshness, detached Attr axes,
original syntax fidelity, all 13 opaque BCL fences, immutable results, publication cancellation and
10 differential following-axis cases. **One blocker remains** at `NativeXPathExpression.cs:82`:
`while (after < source.Length && IsXPathSpace(source[after])) after++;` scans arbitrarily long legal
whitespace without charging/polling. Repro: `following` + 100,000 spaces + `::node()`.
Add polling inside lookahead and a deterministic test proving cancellation there, before the later
outer-loop checkpoint. Re-review before integrating. No public promotion; namespace/Browser mapping
are later slices. Preserve truthful parentless Attr behavior and owned expression/result contracts.

### CSS retained work

Trig owner reported fresh Release builds, focused checks and 1,413 non-corpus cases per framework
passing after the canonical absolute-unit fix, before authentication interrupted the final commit.
Coordinator saved that exact three-line diff as `3f7a25b58`; it is still WIP pending evidence/review.
V0c1's earlier content passed 49 focused and 1,416 non-corpus cases per framework, but the final four
tests and guard edit in `fdf2acd46` are unverified. Its full net8 run lacked the required XML archive;
that is not parser conformance evidence. The completed task's local final records this limitation.
The common Log special-value policy was approved in `b892a78df`; exponential functions remain
undispatched until trig integration. Substitution execution/property validation remains separate.

### XML prepared inputs and remaining corpus

Both reviewed IBM expectation packets (21 cases) are now integrated in `86eda440f` and `57e93f939`.
The exact-six Japanese adapter design was approved in `b892a78df`. Worker `185837889` implements it
only in harness/tooling/manifest. Review independently before merge. After merge explicitly run:

```sh
python3 Jint.Tests.HtmlParser/Xml/Conformance/Tools/import_corpus.py prepare-decoded
```

This prepares ignored decoded artifacts; tests/build never invoke Python or network themselves.
Worker reports 3/3 Python checks and 60/60 selected fresh Release tests combined net8/net10.
Worker census per TFM: 1,820 passing, 127 required pending, zero harness, six optional observed,
zero adapter debt, 21 optional verified, OUTPUT 344 compared/42 pending. Census intentionally red.
Six decoded rows become **observed/unreviewed**, not automatically verified policies. Preserve raw,
decoded hashes, original line endings/declarations, fixed registry and HarnessFailure corruption path.
Do not add a production CodePages decoder. Common does not yet contain this adapter implementation.

### Designs preserved for later dispatch

Numeric-policy source commits `d1cfff5b9` and `24321b557` remain on architecture branch. Final revision
received Astra review clear: exact shortest-decimal remainder, no artificial calendar ceiling,
Euclidean Gregorian/ISO arithmetic, 384-character output buffer. **1024 bits is only the finite-double
calendar integer bound**; decimal remainder arithmetic can exceed 2098 bits and must not inherit that
cap. Reviewer could not retrieve the pinned WPT source for the stated huge-local discrepancy; verify
that specific source assertion from `/tmp/jint-b3p-evidence/` or upstream before closing evidence.
No numeric runtime implementation was dispatched.

`html-parser-element-is-value.md` is an independently reviewed contract, saved in this checkpoint.
No task has been created. Dispatch native/XML immutable metadata first, limited to its listed files;
after review/integration, H6e or explicitly transferred HTML owner adds token/formatting capture in a
separate commit. X3c serialization gate requires both. Do not overlap TreeConstruction ownership.
Textarea/checkable state, HTML serialization/public facade, CSS property/CSSOM/cascade, Browser
migration, full acceptance and performance measurements all remain in the original scope.

## Validation commands

Always fresh Release, never `--no-build`; use `git -c core.fsmonitor=false` for Git operations.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName!~Xml.Conformance'
MSBUILDDISABLENODEREUSE=1 dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -p:BuildInParallel=false --filter 'FullyQualifiedName~Xml.Conformance'
```

The corpus command remains red until reviewed debt is resolved. Do not widen exclusions to obtain a
green checkpoint. Browser remains independently buildable while migration is incomplete.
