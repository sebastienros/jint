# Parser-private shortest digits

Mechanical copy of `Jint/Native/Number/Dtoa/*.cs` at common commit
`56898c3d8` (2026-09-25). Existing source notices are retained verbatim.
The namespace is parser-private and engine Throw calls are replaced by
DtoaThrow, which raises the same CLR exception kinds. No arithmetic or
formatting algorithms were changed in the copy.

HtmlInputNumberFormatter calls only Shortest; precision/fixed modes remain
internal implementation details and have no parser facade. This reuse was
approved after the platform net10 R primitive failed independent V8
reference vectors (2^-958 and 2^-25 and their negative mirrors).
