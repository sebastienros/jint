# CLDR date and time patterns

`Jint.CldrDates.Generator` writes [`Jint/Native/Intl/Data/DateTimePatterns.bin`](../../Jint/Native/Intl/Data/DateTimePatterns.bin),
an embedded resource with CLDR's Gregorian date and time patterns and names for every locale cldr-json carries, and
[`DateTimePatternData.Data.cs`](../../Jint/Native/Intl/Data/DateTimePatternData.Data.cs), the header that says where
it came from. [`DateTimePatternData`](../../Jint/Native/Intl/Data/DateTimePatternData.cs) reads it. It is the
`[[LocaleData]]` [issue #4158](https://github.com/sebastienros/jint/issues/4158) moves `Intl.DateTimeFormat` onto;
until that lands, nothing formats with it.

**The build never runs the generator.** Both outputs are committed, and regenerating them is a manual step taken
when the pinned release changes.

## Inputs

[`pin.json`](pin.json) pins CLDR 48.2 (`release-48-2`):

| Input | What it gives | Pinned by |
| --- | --- | --- |
| [`cldr-dates-full-48.2.0.tgz`](https://www.npmjs.com/package/cldr-dates-full/v/48.2.0) | `main/<locale>/ca-gregorian.json` and `dateFields.json` for 766 locales, each already resolved through CLDR's inheritance | npm integrity `sha512-zMrKvjMO414HDftLr6vKH1fNQHakFNe+F1h4S0eg+zmL4WRi9pI41fxB3UrTJDY42MEM87jCuAw/qlqCgYKcAQ==` |
| [`cldr-core-48.2.0.tgz`](https://www.npmjs.com/package/cldr-core/v/48.2.0) | `availableLocales.json` (the locale list, cross-checked), `defaultContent.json`, `supplemental/likelySubtags.json` and `supplemental/parentLocales.json` (cross-checked) | npm integrity `sha512-zfmLothncSwfv2jlevoSrgI2VGgH8SDGHXst6jUEotHA8nq9Igeg9jzdJDFtBso9pgJuG89DK16TzrmZDdo2Bg==` |
| [`common/supplemental/supplementalData.xml`](https://github.com/unicode-org/cldr/blob/release-48-2/common/supplemental/supplementalData.xml) at `release-48-2` | `<parentLocales>`, the parent table, as the other CLDR tables under `Jint/Native/Intl/Data` are read from it | SHA-256 `cd2af39aef82fdbfba4d591c87548203350538ad2318486d104b3b38b8d62f1a` |

Each file is downloaded into a cache directory (by default `jint-cldr-dates` under the temporary directory; `--cache`
changes it) and checked against its pin before anything reads it. A cached copy that does not match is fetched again,
and a download that does not match fails the run: do not update the pin to whatever came back without finding out why
it changed.

## What is extracted

For each locale, from the `gregorian` calendar:

- `dateTimeFormats/availableFormats`, less the skeletons with a quarter (`Q`) or week (`w`, `W`) field, which ECMA-402
  has no option for, and CLDR's `-count-` and `-alt-` variants. That leaves 49 to 61 per locale.
- `dateTimeFormats` `full`/`long`/`medium`/`short`, and `dateTimeFormats-atTime/standard`, which ICU 72 and later join a
  date and a time with ("at", "um", "à").
- `dateFormats` and `timeFormats`, the four style patterns each.
- `dateTimeFormats/appendItems`, and the wide display name of each field they name (`dateFields.json`'s
  `fields/<field>/displayName`), which is what ICU puts in their `{2}`.
- `months` and `days`, `format` and `stand-alone`, every width; `eras` (`eraAbbr`, `eraNames`, `eraNarrow`); and the
  `format` `am`/`pm` day periods in three widths.

`intervalFormats` (for `formatRange`), the flexible day periods and the other calendars are not extracted yet.

The run fails, listing every problem at once, unless: cldr-dates-full and `availableLocales.json` name the same
locales; every value is present and non-empty, with twelve months, seven weekdays, two eras and am/pm; every pattern
parses (every quote closed) and uses only the letters ECMA-402's fields map to, `GyMLdEcabBhHKkmsSzvO`; every join
and appendItems pattern places `{0}` and `{1}` (and appendItems only `{2}` besides) with no letter outside quotes; the
XML's and cldr-core's parent tables agree; and every locale's parent chain reaches the root through locales cldr-json
carries. Then it decodes what it wrote, independently of the writer, and requires every locale to come back exactly.

CLDR 48.2 itself breaks the letter rule six times — `de-CH` `GyMEd` and `gd` `yMMM`, `ksh` `yM` and `sc` `yM` use the
week-numbering year `Y`, and `fa` and `fa-AF` have an `HHmmZ` skeleton — and writes one numbering-system override,
`haw`'s short date (`M=romanlow`). They are kept as CLDR writes them (the override is dropped: the resource has
nowhere to put it) and are listed by name in `CldrLocale.cs`, so a new one fails the run, and so does a listed one
that no longer occurs. `Jint.Tests` pins the same list.

## Regenerating

From the repository root:

```pwsh
dotnet run --project tools/cldr-dates/Jint.CldrDates.Generator -c Release
```

It prints the resource's size, in total and for the largest blocks. `--check` regenerates in memory and compares the content with
the committed header instead of writing anything.

The deflated bytes depend on the runtime's zlib, so a different .NET can write a different `.bin` from the same
inputs. The header therefore records two hashes: the file's, and the **payload's** — the inflated index followed by
every inflated block, which does not depend on the encoder. `--check` compares the payload, and
`IntlDateTimePatternDataTests` recomputes it from the embedded resource, so a `.bin` and a header from different runs
fail the test.

## The resource

All integers are unsigned LEB128 varints; a string is a varint byte length followed by UTF-8.

```text
file:   "JDTP"  version=1  varint indexLength  varint indexDeflatedLength  index (raw deflate)  blocks
index:  string cldrVersion
        varint slotCount, slotCount x string slotName
        varint blockCount, blockCount x { string language, varint offset, varint deflatedLength, varint length,
                                          varint localeCount, localeCount x string locale }
        varint parentCount, parentCount x { string locale, string parent }
block:  one record per locale, in the index's order (raw deflate; offset counted from the end of the index)
record: varint n, n x { varint gap, string value }        slots: the index is the previous one + gap + 1
        varint n, n x { string skeleton, string pattern } availableFormats entries new or different, in ordinal order
        varint n, n x string skeleton                     availableFormats entries the parent has and this locale drops
```

A **slot** is one fixed-position value; the 178 of them are listed in
[`SlotLayout.cs`](Jint.CldrDates.Generator/SlotLayout.cs), each named by its JSON path, and the index carries the
names so the loader can check them against its own offsets. Every locale but the root (`und`) stores only what
differs from its **CLDR parent** ([TR35](https://www.unicode.org/reports/tr35/#Parent_Locales)): the parent table's
entry, else the root for a language-and-script locale whose script is not the language's likely one, else the
locale with its last subtag removed (a language's parent is the root). cldr-json leaves out the default-content
locales, so a parent it does not carry is skipped for that parent's own (`ca-ES-valencia` inherits from `ca`, past
`ca-ES`). The index lists every parent that is not plain truncation; the loader truncates for the rest.

There is one deflated block per language (the locale's first subtag), so a process inflates only the languages it
uses — plus the root's, which every language's records are relative to, and a parent's in another language where
CLDR has one (`nb` and `nn` inherit from `no`, `hi-Latn` from `en-IN`, `ht` from `fr-HT`).

## Moving to a later CLDR release

1. Update `pin.json`: the versions and tag, each tarball's URL and npm `dist.integrity` (from
   `https://registry.npmjs.org/<package>/<version>`), and the XML's URL and SHA-256.
2. Run the generator. A validation failure is upstream data to read, not a list to widen: decide for each new entry
   whether it belongs in `KnownLetterExceptions` or `KnownNumberingOverrides`, with a comment saying what it is.
3. Update the expectations in `Jint.Tests/Runtime/IntlDateTimePatternDataTests.cs`: CLDR's values for a handful of
   locales, the locale count and the list of known letter exceptions.
4. Run `Jint.Tests`.

Update from a release, never entry by entry: the data is CLDR's own, under the Unicode License v3
([`CREDITS.txt`](../../CREDITS.txt)).
