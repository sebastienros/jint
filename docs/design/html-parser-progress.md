# Jint.HtmlParser implementation record

## Scope and workflow

The requested package replaces AngleSharp in Jint.Browser with a new API for HTML,
SVG, XML, CSS, DOM mutation tracking, and browser integration. Performance must be
demonstrated against AngleSharp on equivalent work. The user explicitly supersedes
the earlier repository direction to retain AngleSharp.

- Integration worktree: `/Users/sebastienros/.codex/worktrees/bd4c/jint`.
- Integration branch: `codex/html-parser-integration`.
- Starting revision: `d78d25137526b4a7b35ccdcfc8522730acd392d1`.
- Design, planning, and reviews: GPT-6 Astra, high reasoning.
- Feature implementations: dedicated GPT-6 Sol, high reasoning tasks in local worktrees.
- Merge finalized feature commits into the integration worktree; no pull requests.

## Tasks

| Task | Identity | State |
| --- | --- | --- |
| Architecture and migration design | `01a0ceec-94ec-7f63-9bfb-189cac69df5f` | Reviewed design and feature contracts integrated |
| Comparison corpus and benchmark harness | `01a0ceee-7b09-7513-b507-a5c412eb4518` | Reviewed and integrated |
| A1 dependency and binding inventory | `01a0cef5-3c12-7d22-926c-e2aeefe58949` | Reviewed and integrated |
| A2/D1/D2 package and DOM foundation | `01a0cef5-4a7d-72e2-b551-74f03c9da7ec` | Reviewed fixes integrated; 52 combined tests pass |
| H1/H2 resumable HTML tokenizer | `01a0cf2a-8177-7941-9413-ee60edc59454` | In progress |
| C1 CSS syntax | `01a0cf2a-8a51-76b1-a12b-ac57c7d2b594` | In progress |
| A2 shared limits/diagnostics/errors and API snapshots | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed and integrated |
| D3a native cloning and import | `01a0cf34-dc42-75d0-960e-735bef6eb68a` | Reviewed and integrated |
| XML shared contracts and provenance | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Follow-up in progress |
| X1 native XML/SVG parsing | `01a0cf3f-9380-7513-a351-4078c30d2241` | Internal scanner milestone in progress |

## Initial repository evidence

At the starting revision, Jint.Browser targets `net8.0;net10.0` and references
AngleSharp 1.8.2, AngleSharp.Css 1.1.2, AngleSharp.Xml 1.2.0, and
AngleSharp.XPath 2.0.6. There are 194 browser C# files containing AngleSharp
references. This includes generated bindings, DOM storage, mutation observation,
CSSOM, parsing, and script/resource scheduling.

`Runtime/DocumentFetch.cs` reads a bounded response body, decodes it, and supplies
the resulting string to `Runtime/Parsing/ParserDriver.cs`. Script suspension and
`document.write` insertion are separate requirements from transport streaming.

## Validation

- Baseline Release build: passed on .NET 8 and .NET 10, zero warnings/errors.
- Baseline document-loading, DOMParser, and mutation-observer tests: 88 passed,
  zero failed/skipped across both frameworks. Filter:
  `FullyQualifiedName~DocumentLoadTests|FullyQualifiedName~MutationObserverTests|FullyQualifiedName~DomParserTests`.
- Local restore requires `-p:RestoreSources=https://api.nuget.org/v3/index.json`
  because the user-level additional feed has no source mapping (`NU1507`).
- Native DOM and shared parser contracts are implemented; parsing algorithms and
  browser migration remain in progress. No performance claim has been established.
- Integrated corpus validation: all 12 fixtures pass, plus count-preserving HTML,
  XML and CSS corruption probes. This was correctness validation, not timing.
- Integrated inventory: 192 source/build consumers, 1,691 named generated members,
  17 WPT suites; lock check and all six Python regression tests pass.
- Integrated DOM/shared-contract/API-snapshot suite: 52 tests passed on net8.0/net10.0,
  no failures/skips. Author separately verified signed packing and a Native AOT
  consumer of the packed package; this establishes only the current native surface.
- After integrating clone/import, the suite passes 64 tests across both frameworks,
  with zero failures/skips. HTML and CSS implementation commits remain under review.

## Integrated commits

- `30f9343eb`: initial Astra architecture and dependency plan, from `e47db2a0d`.
  Independent Astra review requested tighter configuration scope, complete standalone
  construct APIs, explicit ownership of intrinsic element semantics, and completion
  gates covering the standalone package as well as browser replacement.
- `b859c4098`: all four design review findings addressed; Astra recheck clear.
- `df30e1793`, `4e90d1b3b`: corpus/baseline and semantic validation corrections;
  Astra recheck clear. Source commits `a86964ce2`, `2bf2537cd`.
- `813e51988`: inventory plus coverage/CRLF corrections; Astra recheck clear.
  Source commit `26297b86e`. Lock refreshed for the integrated benchmark consumer.
- `4ebd36845`: exact independent HTML, CSS and shared-contract feature scopes,
  from Astra commit `6d8df1d10`.
- `6bc4057b3`, `97edc7997`, `b4c61bbe8`: native DOM plus all Astra correctness and
  document-append performance fixes (source `5f7c3a854`, `61d1fbe20`, `2f97a615c`).
  Final Astra recheck clear.
- `a8a3aeb66`, `d1cf75f02`: shared contracts and generated public API snapshots
  (source `e20551f4a`, `1e0da12b`); both Astra reviews clear.
- `3ca355b77`: iterative clone/import and generated API snapshot additions
  (source `21387e9fb`); Astra review clear.
- `30ab14f99`, `7e2a94adb`, `307ad2b43`: XML/SVG design and corrections
  (source `8f275e24b`, `8f18f92bc`, `862696a2c`). The final reviewed policy supersedes
  the intermediate rejection proposal: no-fetch external entity omissions remain
  successful parses and are exposed through immutable document provenance.
