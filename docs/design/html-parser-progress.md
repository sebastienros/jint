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
| A2/D1/D2 package and DOM foundation | `01a0cef5-4a7d-72e2-b551-74f03c9da7ec` | Astra requested attribute/tree/name-validation fixes to `5f7c3a854`; corrections in progress |
| H1/H2 resumable HTML tokenizer | `01a0cf2a-8177-7941-9413-ee60edc59454` | In progress |
| C1 CSS syntax | `01a0cf2a-8a51-76b1-a12b-ac57c7d2b594` | In progress |
| A2 shared limits/diagnostics/errors | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | In progress |

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
- No parser implementation or performance claim has been completed yet.
- Integrated corpus validation: all 12 fixtures pass, plus count-preserving HTML,
  XML and CSS corruption probes. This was correctness validation, not timing.
- Integrated inventory: 192 source/build consumers, 1,691 named generated members,
  17 WPT suites; lock check and all six Python regression tests pass.

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
