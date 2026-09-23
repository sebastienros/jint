# Bounded H4 tree fixture census

`Html5libSubsetTests.cs` transcribes 14 complete-document `#data` / `#document`
cases from `html5lib/html5lib-tests/tree-construction` at commit
`9329e64694e7835d0dcff9811e22856ef6ad16f9` (the last revision before those
files moved to WPT). Case IDs name the source file and one-based `#data` ordinal.
The original license is copied in `UPSTREAM-LICENSE.txt`. The subset tests assert
the tree, while tokenizer and tree diagnostic tests assert their separate codes.
The independent H4 algorithm reference is the HTML Standard tree construction
snapshot updated 2026-09-22.

Executed: 14 of 14 selected historical document cases. Unsupported by H4:
6 separate literal cases in `UnsupportedBranchStopsBeforeItsMutation`, one for
each named H5-H7 family; each asserts `MissingFeature` and is not a passing parse.
The H4 subset deliberately excludes historical PI cases whose bogus-comment
expectations conflict with the current Standard's processing-instruction tokens,
and select cases whose insertion rules changed. Current-spec PI behavior is
asserted independently in `ProcessingInstructionsRetainPlacementAndCase`;
select remains a named stop until H6.
