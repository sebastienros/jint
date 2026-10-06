# Bounded HTML tree fixture census

`Html5libSubsetTests.cs` transcribes 14 complete-document `#data` / `#document`
cases from `html5lib/html5lib-tests/tree-construction` at commit
`9329e64694e7835d0dcff9811e22856ef6ad16f9` (the last revision before those
files moved to WPT). Case IDs name the source file and one-based `#data` ordinal.
The original license is copied in `UPSTREAM-LICENSE.txt`. The subset tests assert
the tree, while tokenizer and tree diagnostic tests assert their separate codes.
The independent H4 algorithm reference is the HTML Standard tree construction
snapshot updated 2026-09-22.

Executed: 14 of 14 selected historical document cases. H7a foreign dispatch is
covered by authored native-tree fixtures in `HtmlForeignTreeTests` and the complete
37 SVG tag, 58 SVG attribute, and 11 foreign namespace adjustment rows in
`HtmlForeignAdjustmentTests`. Their primary source is the
[WHATWG HTML source at `2f441941fc523877bd9d5cd7de3b91a81a00ca2e`](https://github.com/whatwg/html/blob/2f441941fc523877bd9d5cd7de3b91a81a00ca2e/source),
inspected 2026-09-25, specifically the tree-construction dispatcher, creating and
inserting nodes, and foreign-content rules. These authored fixtures are not WPT
passes and do not increase the historical corpus census. They cover namespace
identity, integration exceptions and accepted-token encoding facts, breakouts,
foreign scripts, diagnostics, CDATA context, every short split at quotas 1/3/large,
destination ownership/IsValue, and charged resumable long-name scans. No
`ForeignContent` stop remains reachable; contextual fragments remain H7b and
active parser scripting remains H8. Frameset and tail modes are covered in
`HtmlFramesetTreeTests` against the HTML Standard updated 2026-09-25. Ordinary templates,
content patching, and applicable declarative-shadow parsing are covered in
`HtmlTemplateTreeTests` and `HtmlTemplatePatchingTests` against the current HTML Standard.
No Templates stop remains reachable. Default standalone declarative-shadow permission is false;
internal calling algorithms can select it and supply cached engine-free Browser host facts.
Patch coverage includes native PI pseudo-attributes, ordinary descendant and sibling scope,
actual fragment/template ownership, captured removals from live parents, moved markers, close
versus EOF, quota-one work scaling, every short fragment split, and coherent cancellation or
notification failure around atomic native removal. Declarative coverage uses real shadow roots,
flags/registry ownership, actual fragment hosts, permission/host/existing-root fallback, and
host-provider lookup gates and original infrastructure exceptions. This is authored native
coverage, not an expansion of the historical WPT census. Atomic native work boundaries and the
public HTML promotion amendment are recorded in
[the promotion checkpoint](../../../docs/design/html-parser-html-promotion.md).
`HtmlTableStructureTests` and `HtmlTableTextTests` cover H5a/H5b
against the 2026-09-22 HTML Standard; the historical corpus pin above is comparison
evidence, not the rule for a changed algorithm. Table text tests cover pending runs,
foster locations, input splits, quota continuations, and coherent cancellation.
The original subset deliberately excludes historical PI cases whose bogus-comment
expectations conflict with the current Standard's processing-instruction tokens,
and select cases whose insertion rules changed. Current-spec PI behavior is
asserted independently in `ProcessingInstructionsRetainPlacementAndCase`;
current select trees are asserted in `HtmlSelectTreeTests`. Fragment-context
select cases remain with H7b.

H7b contextual fragments use the same native session/tokenizer/tree builder with a
private parser document, a synthetic HTML stack root, and a target-owned detached
root insertion fragment. `HtmlFragmentTreeTests` contains 57 authored cases against
HTML Standard §13.4 and the tree-construction rules inspected 2026-09-25. This is
an internal seam; it does not promote `MarkupParser` or complete Browser cutover.
The matrix covers HTML/table/select/template contexts, initial text states without
an appropriate end tag, SVG/MathML integration and CDATA, all document modes,
form ancestry, template/shadow targets, script flags/source metadata, ownership and
formatting reconstruction/adoption including `IsValue`. Short contexts/text/foreign
cases run every split at quotas 1/3/large. Seven structural differential cases use
pinned AngleSharp 1.8.2 only for conforming intersection behavior; current select,
PI and patch rules retain independent expectations. No existing corpus pin or
exclusion changed. Document form-pointer association now resolves roots and commits
fresh insertion cooperatively; quota-one, deterministic depth-plus-control scaling
and host-adoption cases cover the cache invalidation contract.
