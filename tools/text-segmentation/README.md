# Text segmentation data

`generate.py` writes the three Unicode tables of `Intl.Segmenter`:

- [`Jint/Native/Intl/GraphemeBreaker.Data.cs`](../../Jint/Native/Intl/GraphemeBreaker.Data.cs), the Grapheme_Cluster_Break,
  Extended_Pictographic and Indic_Conjunct_Break properties that [`GraphemeBreaker`](../../Jint/Native/Intl/GraphemeBreaker.cs)
  uses for `granularity: "grapheme"` ([UAX #29, GB1 to GB999](https://www.unicode.org/reports/tr29/#Grapheme_Cluster_Boundary_Rules));
- [`Jint/Native/Intl/SentenceBreaker.Data.cs`](../../Jint/Native/Intl/SentenceBreaker.Data.cs), the Sentence_Break property that
  [`SentenceBreaker`](../../Jint/Native/Intl/SentenceBreaker.cs) uses for `granularity: "sentence"`
  ([UAX #29, SB3 to SB11](https://www.unicode.org/reports/tr29/#Sentence_Boundary_Rules));
- [`Jint/Native/Intl/WordBreaker.Data.cs`](../../Jint/Native/Intl/WordBreaker.Data.cs), the Word_Break property as ICU's root
  word rules tailor it, which [`WordBreaker`](../../Jint/Native/Intl/WordBreaker.cs) uses for `granularity: "word"`
  ([UAX #29, WB1 to WB999](https://www.unicode.org/reports/tr29/#Word_Boundary_Rules)).

**The build never runs the generator.** Its output is committed, and regenerating it is a manual step taken when the
pinned Unicode version changes. Do not edit the generated files by hand.

## The pin

**Unicode 17.0.0**, the version Node 26 (ICU 78) ships, for all three tables. The generator checks the first line and the
SHA-256 of every file it reads against `PINNED` and refuses any other, so moving to another version means changing all
of them on purpose:

| File | Used for | SHA-256 |
| --- | --- | --- |
| [`auxiliary/SentenceBreakProperty.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/SentenceBreakProperty.txt) | sentences | `871c0c985ad95125e25b302414065a10839d068970bceb383ecec138f22a0a18` |
| [`auxiliary/WordBreakProperty.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/WordBreakProperty.txt) | words | `72274cac1e6b919507db35655c3e175aa27274668a1ece95c28d2069f2ad9852` |
| [`emoji/emoji-data.txt`](https://www.unicode.org/Public/17.0.0/ucd/emoji/emoji-data.txt) | graphemes and words: Extended_Pictographic (GB11, WB3c) | `2cb2bb9455cda83e8481541ecf5b6dfda66a3bb89efa3fa7c5297eccf607b72b` |
| [`Scripts.txt`](https://www.unicode.org/Public/17.0.0/ucd/Scripts.txt) | words: Han and Hiragana (ICU) | `9f5e50d3abaee7d6ce09480f325c706f485ae3240912527e651954d2d6b035bf` |
| [`PropList.txt`](https://www.unicode.org/Public/17.0.0/ucd/PropList.txt) | words: Ideographic (ICU) | `130dcddcaadaf071008bdfce1e7743e04fdfbc910886f017d9f9ac931d8c64dd` |
| [`LineBreak.txt`](https://www.unicode.org/Public/17.0.0/ucd/LineBreak.txt) | words: Complex_Context (ICU) | `e6a18fa91f8f6a6f8e534b1d3f128c21ada45bfe152eb6b1bcc5e15fd8ac92e6` |
| [`auxiliary/GraphemeBreakProperty.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/GraphemeBreakProperty.txt) | graphemes; words: Control (ICU) | `d6b51d1d2ae5c33b451b7ed994b48f1f4dc62b2272a5831e7fd418514a6bae89` |
| [`DerivedCoreProperties.txt`](https://www.unicode.org/Public/17.0.0/ucd/DerivedCoreProperties.txt) | graphemes: Indic_Conjunct_Break (GB9c) | `24c7fed1195c482faaefd5c1e7eb821c5ee1fb6de07ecdbaa64b56a99da22c08` |

The official conformance tests of the same version are vendored here unchanged, and `Jint.Tests` runs every line of them
(`IntlSegmenterGraphemeTests.TheUnicodeGraphemeBreakTestPasses`, `IntlSegmenterWordTests.TheUnicodeWordBreakTestPasses`,
`IntlSegmenterSentenceTests.TheUnicodeSentenceBreakTestPasses`):
[`GraphemeBreakTest.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/GraphemeBreakTest.txt) (SHA-256
`e2d134d2c52919bace503ebb6a551c1855fe1a1faec18478c78fff254a1793ec`, 766 cases),
[`WordBreakTest.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/WordBreakTest.txt) (SHA-256
`1de23a75f37904abc7d206239ee8d34f8fdf0fb4ab32a7174dfbabbde25419b2`, 1 944 cases) and
[`SentenceBreakTest.txt`](https://www.unicode.org/Public/17.0.0/ucd/auxiliary/SentenceBreakTest.txt) (SHA-256
`12cb47d028ded0c1cb8a28558f95479cbcd24559c46977015c82f3b50a1cc6e4`, 512 cases). Node 26 passes all of them too.

## Running it

From the repository root, with Python 3:

```sh
for f in auxiliary/SentenceBreakProperty.txt auxiliary/WordBreakProperty.txt emoji/emoji-data.txt Scripts.txt \
         PropList.txt LineBreak.txt auxiliary/GraphemeBreakProperty.txt DerivedCoreProperties.txt; do
  curl -fLo "/tmp/ucd-17/$(basename $f)" --create-dirs "https://www.unicode.org/Public/17.0.0/ucd/$f"
done
python3 tools/text-segmentation/generate.py /tmp/ucd-17
```

It prints the number of runs it wrote (2 168 grapheme runs, 3 356 sentence runs and 2 305 word runs for 17.0.0) and leaves the working tree
changed only if the data did.

## What it writes

The property of every code point as sorted runs, binary-searched by `GraphemeBreaker.PropertiesOfNonAscii`,
`SentenceBreaker.ClassOfNonAscii` and `WordBreaker.PropertiesOfNonAscii`. Characters that are not listed are `Other`,
per each file's `@missing` line.

- **Graphemes:** each run is `first code point << 8 | properties`, the `GraphemeBreaker.GraphemeClass` in the low four
  bits, Extended_Pictographic above it and Indic_Conjunct_Break (None, Consonant, Extend, Linker) in the two bits above
  that. These are ICU's own classes: its [`char.txt`](https://github.com/unicode-org/icu/blob/release-78.3/icu4c/source/data/brkitr/rules/char.txt)
  is UAX #29 without a tailoring. A second table holds the ASCII characters, and the generator asserts what
  `GraphemeBreaker` assumes: no control has a flag, every InCB Linker is Extend, every InCB Extend is Extend or ZWJ,
  and every InCB consonant and Extended_Pictographic character is Other.

- **Sentences:** each run is `first code point << 4 | class`. CR, LF and Sep are one class (`ParaSep`) and Format joins
  Extend, because the rules treat them alike (SB3 reads CR before it looks a class up, and SB5 ignores both Extend and
  Format). The class numbers are the order of `SentenceBreaker.SentenceClass`.
- **Words:** each run is `first code point << 8 | properties`, the `WordBreaker.WordClass` in the low five bits and three
  flags above it: Extended_Pictographic, ICU's `$KanaKanji` (Han, Hiragana and Word_Break=Katakana) and ICU's
  `$Hiragana`/`$Ideographic`, whose words are word-like. The classes are those of ICU's
  [`word.txt`](https://github.com/unicode-org/icu/blob/release-78.3/icu4c/source/data/brkitr/rules/word.txt):
  `$ALetterPlus` is ALetter without the Han, kana and Hangul syllables plus the Complex_Context letters of Thai, Lao,
  Khmer and Myanmar, `$Extend` has no Han character, and the Hangul syllables are a class of their own. A second table
  holds the ASCII characters, read without a search. The generator also asserts what `WordBreaker` assumes of the
  flags, so a Unicode version that breaks an assumption stops the generator rather than the segmentation.

ICU hands a run of Han, kana, Thai, Lao, Khmer or Myanmar characters to a dictionary that splits it into words. Jint has
no such dictionary, so `WordBreaker` leaves the run whole, the segment ICU's rules give before the dictionary runs.

The tables are the Unicode Character Database's and not .NET's general categories on purpose: the Unicode data of
`net472` and `netstandard2.0` is far older than `net10.0`'s, and a category derivation of the sentence classes differed
from this property for 32 046 code points there (5 055 on `net8.0`), nearly all of them letters added after that data.
The tables make a segment the same on every target framework.

Unicode data is under the [Unicode license](https://www.unicode.org/license.txt).
