# Encoding Standard data

`indexes.json` and `encodings.json` are vendored verbatim from the
[Encoding Standard's repository](https://github.com/whatwg/encoding), at commit

    a985b62a9b45c17da3e17a9f0a0b4e30c34c4a8a

`encodings.json` is the machine-readable form of the standard's
[names-and-labels table](https://encoding.spec.whatwg.org/#names-and-labels); `indexes.json` holds the
[indexes](https://encoding.spec.whatwg.org/#indexes) the legacy encodings decode through. `whatwg-encoding-LICENSE.txt`
is that commit's `LICENSE`: the standard is licensed under Creative Commons Attribution 4.0 International and,
in its own words, "to the extent portions of it are incorporated into source code, such portions in the source
code are licensed under the BSD 3-Clause License instead" — which is what the generated tables are.

Both files are checked in unmodified, including their line endings (`.gitattributes` pins them to LF), so
verifying them is `curl` plus `git diff` and nothing else. Only the single-byte indexes are consumed today;
the multi-byte ones are carried along because trimming the file would make that check a judgement call
instead of a comparison.

## Regenerating the tables

`generate-encoding-tables.ps1` reads both files and writes
[`Jint/WebApi/Encoding/EncodingTables.Data.cs`](../../Jint/WebApi/Encoding/EncodingTables.Data.cs) — the label
table `TextDecoder` resolves a label through, and the `ushort[128]` index each single-byte encoding decodes
through.

```pwsh
pwsh tools/whatwg-encoding/generate-encoding-tables.ps1
```

**The build never runs it.** The generated file is committed, regeneration is a manual step, and its header
records the commit above along with the SHA-256 of the exact bytes it was generated from. The script validates
what it reads — 128 entries per index, every code point inside the BMP and never a surrogate, U+0000 unused so
it can mean "unmapped", labels unique and ASCII-lowercase, every encoding's lowercased name among its own
labels — and fails rather than emitting something subtly wrong.

`Jint.Tests` embeds both JSON files (from here, not from a copy) and checks the generated tables against them:
every label the standard lists resolves to the encoding it names, and all 28 single-byte encodings decode all
256 byte values exactly as their index says. So a regeneration that changes anything is visible either in the
diff or in a failing test.

## Refreshing the data

1. Resolve `main` in `whatwg/encoding` to a concrete commit.
2. Download `indexes.json`, `encodings.json` and `LICENSE` at that commit and replace the files here.
3. Update the commit in this file and in `$sourceCommit` at the top of the script.
4. Re-run the script and commit its output together with the data.
5. Run `Jint.Tests`. New labels or encodings show up as failures in `LegacyEncodingTests` — the counts it
   asserts are deliberate, so that an encoding added upstream has to be looked at rather than absorbed.
