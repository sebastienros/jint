# Sentence_Break data

`generate.py` writes [`Jint/Native/Intl/SentenceBreaker.Data.cs`](../../Jint/Native/Intl/SentenceBreaker.Data.cs), the
table of the Unicode Sentence_Break property that [`SentenceBreaker`](../../Jint/Native/Intl/SentenceBreaker.cs) uses for
`Intl.Segmenter` with `granularity: "sentence"` ([UAX #29, SB3 to SB11](https://www.unicode.org/reports/tr29/#Sentence_Boundary_Rules)).

**The build never runs the generator.** Its output is committed, and regenerating it is a manual step taken when the
pinned Unicode version changes. Do not edit the generated file by hand.

## The pin

**Unicode 17.0.0**, the version Node 26 (ICU 78) ships:
[`SentenceBreakProperty.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/SentenceBreakProperty.txt), SHA-256
`871c0c985ad95125e25b302414065a10839d068970bceb383ecec138f22a0a18`. The generator refuses any other file, so moving to
another version means changing the header line and the hash in `generate.py` on purpose.

## Running it

From the repository root, with Python 3:

```sh
curl -fLo /tmp/ucd-17/SentenceBreakProperty.txt --create-dirs https://www.unicode.org/Public/17.0.0/ucd/auxiliary/SentenceBreakProperty.txt
python3 tools/sentence-break/generate.py /tmp/ucd-17/SentenceBreakProperty.txt
```

It prints the number of runs it wrote (3 356 for 17.0.0) and leaves the working tree changed only if the data did.

## What it writes

The property of every code point as sorted runs, binary-searched by `SentenceBreaker.ClassOfNonAscii`, each run packed as
`first code point << 4 | class`. Characters that are not listed are `Other`, per the file's `@missing` line. CR, LF and
Sep are one class (`ParaSep`) and Format joins Extend, because the rules treat them alike (SB3 reads CR before it looks a
class up, and SB5 ignores both Extend and Format). The class numbers are the order of `SentenceBreaker.SentenceClass`.

It is the Unicode Character Database's table and not .NET's general categories on purpose: the Unicode data of
`net472` and `netstandard2.0` is far older than `net10.0`'s, and a category derivation differed from this property for
32 046 code points there (5 055 on `net8.0`), nearly all of them letters added after that data. The table makes a
sentence segment the same on every target framework.

Unicode data is under the [Unicode license](https://www.unicode.org/license.txt).
