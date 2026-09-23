# HTML parser replacement inventory (A1)

This is a locked description of the **current** AngleSharp dependency surface on the integration branch. It is an input to the replacement work, not a claim that the replacement already implements these contracts. Run from the repository root:

```sh
python3 tools/html-parser-inventory/inventory.py
python3 -m unittest discover -s tools/html-parser-inventory -p 'test_*.py'
```

When an intentional source, binding or baseline change makes the check fail, run `python3 tools/html-parser-inventory/inventory.py --update` and review the JSON diff. Every newly listed use needs a migration owner before accepting the lock. The tool needs only Python's standard library. It scans all checked-out C# and project files, so a new AngleSharp reference outside the known projects fails with `No owner` instead of disappearing. `bin`, `obj`, generated output and vendored WPT files are separate inputs or excluded from source scanning. Text hashes normalize CRLF to LF, so the same committed tree has the same inventory on Windows, Linux and macOS.

## What is locked

| JSON section | Source and meaning |
| --- | --- |
| `source` | Every hand-written C#, project, props or targets file with an AngleSharp token outside a comment, with runtime/generator/test/benchmark classification, replacement task family, exact reference lines and a SHA-256 for production and generator files. The full-file hash is intentional: `using AngleSharp.Dom` makes unqualified `INode` calls invisible to a token search, so any edit to a native consumer gets reviewed. |
| `generated` | Every generated shape method, accessor, constant, constructor and symbol slot, plus the four iterable methods installed through `DomIterableMembers.ValueIterator`, with interface and owner; registry interfaces, interfaces with no emitted member, override and extension-source hashes, and hashes of all checked-in `.g.cs` files. The existing `DomBindingsStalenessTests` remains the authority that those files match the pinned assemblies. |
| `packages` | Direct project/props/targets references (including inline versions), central package versions and the two-assembly binding generator pin. The four Browser runtime packages are AngleSharp, AngleSharp.Css, AngleSharp.Xml and AngleSharp.XPath. |
| `api` | The five approved Jint public API snapshots by hash, and top-level Browser public type and source member declarations. The latter are source references for migration review; Browser has no compiled public API snapshot yet, so A2 must add one for the new package. |
| `wpt` | The shared vendored WPT commit, the browser lane's measured census table and the exclusions source hash. The census is the existing Windows baseline and must be remeasured by its own gate; this script does not run WPT or infer new pass/fail outcomes. |

The lock currently records **191** AngleSharp-bearing source/build files, **1,691** generated shape members across **163** registered interfaces, and the browser WPT census of **392** vendored documents, **9** synthesized wrappers, **66,916** registrations and **239** not passing. These numbers are derived by the tool and will change if the checked-in sources change.

## Replacement ownership

`owner` values are migration work, not current code ownership. They follow the finite task IDs in `docs/design/html-parser.md` (held in the integration worktree). The file and member entries make these splits reviewable:

| Family | Bounded slices and current regression anchors |
| --- | --- |
| `B1` | Core DOM shapes, native binding calls, the emitter and staleness/pin tests (`DomBindingsStalenessTests`, `DomPrototypeTests`). |
| `B2` | Form controls, table interfaces, other HTML element reflection, and live collections (`Forms/*`, `DomCollectionTests`, `DomReflectionTests`). |
| `B3` | Events, custom elements and observers (`Events/*`, `CustomElements/*`, `Observers/*`). |
| `B4` | Detached documents, SVG, XML, DOMParser, XPath and serialization (`Views/DomParserTests`, `Views/XPathTests`, `Dom/HtmlSerializationTests`). |
| `B5` | CSS bindings and CSSOM projection (`Views/ComputedStyleTests`, `Layout/CascadeTraversalTests`). `C5` subdivides `CSSStyleDeclaration` accessors into color/display/visibility, box/layout, font/text, motion/interaction and legacy/SVG/page properties. The lock names each accessor in its group; later C5 work must give each group its own grammar and regression set. |
| `R1`/`R2`/`R3` | Parser coordination; classic/module script ordering; dynamic scripts/styles/resources/images/frames (`Parsing/DocumentLoadTests`, `ScriptLoadingTests`, `ImageLoadingTests`, `ChildFrameTests`). `ParserDriver` spans multiple slices, so its full-file lock requires review by each affected owner on migration. |
| `R4` | Page runtime, extraction, accessibility, layout, protocol and API leaf consumers (`Extraction/*`, `Accessibility/*`, `Layout/*`, `DevTools/*`). |
| `G1`/`G2` | Browser/WPT/tool parity and eventual production package removal. Benchmark-only comparisons belong to `A3`. |

The generated surface is grouped by interface and member. For example, `HTMLInputElement` belongs to `B2:form-controls`, `CSSStyleDeclaration.color` to `C5:color-display-visibility`, and `HTMLIFrameElement` to `R3:frames`. A broad file such as `ParserDriver.cs` is assigned a lead owner and remains a shared integration seam; the lock's reference lines and SHA keep later edits visible.

This is a conservative dependency inventory. A file hash does not prove a particular method's semantics, and the Browser public declarations are not a compiled API compatibility test. The existing Release build, binding staleness test, WPT census and future public API snapshot provide those separate gates.
