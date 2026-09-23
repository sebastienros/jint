# Bounded HTML tree fixture census

`Html5libSubsetTests.cs` transcribes 14 complete-document `#data` / `#document`
cases from `html5lib/html5lib-tests/tree-construction` at commit
`9329e64694e7835d0dcff9811e22856ef6ad16f9` (the last revision before those
files moved to WPT). Case IDs name the source file and one-based `#data` ordinal.
The original license is copied in `UPSTREAM-LICENSE.txt`. The subset tests assert
the tree, while tokenizer and tree diagnostic tests assert their separate codes.
The independent H4 algorithm reference is the HTML Standard tree construction
snapshot updated 2026-09-22.

Executed: 14 of 14 selected historical document cases. Five later families remain
terminal `MissingFeature` branches: Formatting, Select, Templates, Framesets, and
ForeignContent. `HtmlTableStructureTests` and `HtmlTableTextTests` cover H5a/H5b
against the 2026-09-22 HTML Standard; the historical corpus pin above is comparison
evidence, not the rule for a changed algorithm. Table text tests cover pending runs,
foster locations, input splits, quota continuations, and coherent cancellation.
The original subset deliberately excludes historical PI cases whose bogus-comment
expectations conflict with the current Standard's processing-instruction tokens,
and select cases whose insertion rules changed. Current-spec PI behavior is
asserted independently in `ProcessingInstructionsRetainPlacementAndCase`;
select remains a named stop until H6.
