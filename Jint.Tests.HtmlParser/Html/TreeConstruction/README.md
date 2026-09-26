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
`HtmlFramesetTreeTests` against the HTML Standard updated 2026-09-25. Templates stops only where the current
`for` content-patching branch applies; ordinary template parsing is covered in
`HtmlTemplateTreeTests` against the HTML Standard updated 2026-09-22. The document
parser's default declarative-shadow flag is false, so valid `shadowrootmode` tokens
follow the Standard's ordinary-template fallback. H6f owns active declarative
shadow parsing and content patching once their native state is integrated.
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
