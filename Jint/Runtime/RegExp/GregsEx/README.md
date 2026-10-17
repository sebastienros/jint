# GregsEx ECMAScript engine (copied sources)

The files under this directory are the ECMAScript flavor of
[GregsEx](https://github.com/sebastienros/gregsex), copied from commit
`d989dd8455b0b7e591d48f04d149c8c2f0a9eaea` under its MIT license (`GregsEx-LICENSE.txt`). The Unicode
tables are generated from Unicode 17.0.0 data under the Unicode License V3
(`Flavors/EcmaScript/Unicode/Unicode-LICENSE.txt`).

Jint consumes them through `Jint/Runtime/RegExp/JintRegExpEngine.cs` and nothing else. The GregsEx package
is not referenced because it ships `net10.0` only and its public surface stops above what a JavaScript
engine needs (group names before a match, the executor below the `lastIndex` session); the rest of GregsEx —
the other flavors, the runtime compiler, the source generator — is not copied.

## Keeping it syncable

The folder layout mirrors `src/GregsEx/`, so a sync is a `diff -r` against the upstream tree. Every
divergence is one of the mechanical edits below, or is marked with a `// Jint:` comment at the site.

Mechanical, applied to every file:

- `namespace GregsEx…` → `namespace Jint.Runtime.RegExp.GregsEx…` (and the matching `using`s);
- top-level `public` types → `internal`;
- `#nullable enable` at the top, because `.editorconfig` marks the folder `generated_code` to keep Jint's
  style analyzers off it, and generated code is otherwise outside the nullable context.

Structural:

- `Contracts/LaneContracts.cs` keeps only `SearchOperation`, `WorkspaceBuffers` and `RunState`; the
  RegularCore descriptor, the `static abstract` text-reader interfaces and the PCRE2 pair buffer are gone.
- `IR/SemanticProgram.cs` is reduced to `IR/SourceRange.cs`.
- `Flavors/EcmaScript/Unicode` carries only the Unicode 17.0 tables (the version the pinned test262
  asserts); `EcmaUnicodeData` refuses any other version.
- `EcmaAccelerator` keeps its .NET 9 `SearchValues<string>` lane under `#if NET9_0_OR_GREATER` and scans the
  branch strings downlevel. Other downlevel gaps (`ThrowIfNull`, `ThrowIfNegative[OrZero]`, generic
  `Enum.IsDefined`, `span.Contains(char)`, `IndexOfAny(SearchValues<char>)`) are polyfilled in
  `Jint/Extensions/Polyfills.cs`, not here.

Behavioural (each also a candidate upstream fix; see `docs/design/regexp-gregsex-migration.md`):

- `EcmaCompiler` clamps quantifier bounds above `int.MaxValue` instead of failing `ProgramLimitExceeded`.
- `EcmaAccelerator` skips its literal plans for a Unicode-aware pattern holding a lone surrogate literal.
- `EcmaAccelerator` declines the atomic-sequence plan when its minimum length would overflow.
- `EcmaAccelerator` builds `FirstChars`, a FIRST-set prefilter for a case-insensitive first atom or a nullable prefix
  (with inverted case-folding tables), and the literal plan searches with `SearchValues<string>` on .NET 9+.
- `EcmaExecutor` runs a greedy single-character repeat as one counted choice (`ChoiceKind.GreedyRun`, which is why
  `ChoiceWidth` is 4), passes the accelerated plans an unbounded frame count, and never starts a `FirstChars`
  candidate inside a surrogate pair under u/v.
- `RunBudget` takes the fast charge path until its next scheduled clock read when a timeout is set, and has an
  absolute-deadline constructor that reads no clock (`RunState` gains the matching overload).
