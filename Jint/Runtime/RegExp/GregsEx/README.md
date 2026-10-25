# GregsEx ECMAScript engine (vendored sources)

The files under this directory are the ECMAScript flavor of
[GregsEx](https://github.com/sebastienros/gregsex) under its MIT license (`GregsEx-LICENSE.txt`), regenerated from GregsEx `main`; the commit that last
changed them names the upstream revision. The Unicode tables are generated from Unicode 17.0.0 data under the Unicode License V3
(`Flavors/EcmaScript/Unicode/Unicode-LICENSE.txt`).

Jint consumes them through `Jint/Runtime/RegExp/JintRegExpEngine.cs` and nothing else. The GregsEx package
is not referenced because it ships `net10.0` only and its public surface stops above what a JavaScript
engine needs (group names before a match, the executor below the `lastIndex` session); the rest of GregsEx —
the other flavors, the runtime compiler, the source generator — is not vendored.

## Do not edit these files by hand

They are produced by `tools/sync_gregsex.py`, which keeps no state: it regenerates every file from upstream, so an
unchanged upstream gives an empty `git diff`, and a hand edit is overwritten by the next sync.

```bash
python3 tools/sync_gregsex.py                       # regenerate from GregsEx main
python3 tools/sync_gregsex.py --revision <sha>
```

A sync is the upstream files, the mechanical transforms below, then `tools/gregsex-sync/jint.patch`.
`.github/workflows/gregsex-sync.yml` runs it every Monday and, when the diff is not empty, builds and runs the regex
tests in a job with a read-only token, then a second job opens a pull request from `gregsex-sync/<revision>` (one
branch per upstream revision, never force-pushed).

**A fix to the matcher belongs upstream.** Make it in GregsEx, then sync. Change the patch only for what stays
Jint's own (the list below).

### Changing or rebasing the patch

When the patch no longer applies to a new upstream revision, or a Jint-only edit is needed:

```bash
python3 tools/sync_gregsex.py --no-patch --revision <sha>   # upstream + transforms only
git add Jint/Runtime/RegExp/GregsEx                         # stage that as the base
# re-apply the Jint edits (git apply --3way tools/gregsex-sync/jint.patch, then resolve), or make new ones
git diff -- Jint/Runtime/RegExp/GregsEx > tools/gregsex-sync/jint.patch
python3 tools/sync_gregsex.py --revision <sha>              # must reproduce the working tree exactly
```

## What differs from upstream

Mechanical, applied by the tool to every C# file:

- `namespace GregsEx…` → `namespace Jint.Runtime.RegExp.GregsEx…` (and the matching `using`s);
- top-level `public` types → `internal`;
- `#nullable enable` at the top, because `.editorconfig` marks the folder `generated_code` to keep Jint's
  style analyzers off it, and generated code is otherwise outside the nullable context;
- `using System.Threading;` where the file names a type from it, since Jint's implicit usings do not import it.

In `jint.patch`, every one of them structural or a downlevel port; the behavioural fixes found by this migration
are upstream since [sebastienros/gregsex#2](https://github.com/sebastienros/gregsex/pull/2):

- `Contracts/LaneContracts.cs` keeps only `SearchOperation`, `WorkspaceBuffers` and `RunState` (with an
  absolute-deadline overload); the RegularCore descriptor, the `static abstract` text-reader interfaces and the
  PCRE2 pair buffer are gone, which is what lets it compile for `netstandard2.0` and `net472`.
- `Flavors/EcmaScript/Unicode` carries only the Unicode 17.0 tables (the version the pinned test262 asserts);
  `EcmaUnicodeData` refuses any other version and `EcmaScriptProfile` defaults to 17.0.
- `Polyfills/StringSearchValues.cs` declares its `StringSearchValues` alias per file rather than `global`, and
  `EcmaAccelerator` repeats it: a global alias would shadow Jint's own `StringSearchValues` (`Jint/Native/Intl`).
- `ValueStringBuilder` guards the APIs `net472` lacks.

Jint-owned files the tool never touches: `.editorconfig`, this `README.md` and `IR/SourceRange.cs` (the one type of upstream's `IR/SemanticProgram.cs` the flavor needs). Other downlevel gaps
(`ThrowIfNull`, `ThrowIfNegative[OrZero]`, generic `Enum.IsDefined`, `span.Contains(char)`,
`IndexOfAny(SearchValues<char>)`, `LastIndexOfAny[Except](SearchValues<char>)`) are polyfilled in `Jint/Extensions/Polyfills.cs`, not here.
