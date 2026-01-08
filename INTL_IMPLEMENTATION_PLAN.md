# Intl Implementation Plan

## Current Status (as of latest session)

- **Passed**: 2362 tests
- **Failed**: 0 tests
- **Skipped**: 194 tests (97 intl402 exclusions × 2 for strict/non-strict)

---

## Completed Work

### Phase 1: DurationFormat Pluralization Fix
- Fixed pluralization in `JsNumberFormat.cs` (FormatUnit, FormatUnitBigInt, FormatUnitToParts)
- Key change: `var isSingular = Math.Abs(value) == 1; var pattern = isSingular ? unitPatterns.One : unitPatterns.Other;`

### Phase 2: Unit Formatting Fixes
- Fixed short style abbreviations (removed periods: yr. → yr, mo. → mo, etc.)
- Fixed short style day (always singular)
- Fixed narrow style spacing in CLDR patterns

### Phase 3: Duration Validation
- Added integer validation for duration properties
- Fixed range validation for normalized seconds

### Phase 4: Numeric Time Separator Handling
- Added `IsNumericStyle` helper for numeric/2-digit detection
- Implemented colon-separated time formatting (e.g., "1:02:03")
- Fixed cascade rules for showing time units
- **Enabled test**: `mixed-short-and-numeric.js`

### Phase 5: Digital Style Formatting
- Fixed date units to use short form (yr, mo, wk, day)
- Fixed sub-second conversion to fractional seconds
- Handle large second values without 2-digit padding
- **Enabled tests**: `style-digital-en.js`, `negative-durationstyle-digital-en.js`

### Phase 6: Digital formatToParts and Sub-second Cascade
- Added date unit parts (year, month, week, day) to `FormatToPartsDigital()`
- Added sub-second cascade in numeric mode for `FormatNonDigital()`
  - When hours is numeric and there are sub-second units, cascade to show minutes/seconds
  - Convert sub-seconds to fractional seconds (e.g., "1:00:00.001")
- **Enabled tests**: `formatToParts-style-digital-en.js`, `negative-duration-formatToParts-style-digital-en.js`
- **Enabled tests**: `numeric-hour-with-zero-minutes-and-non-zero-seconds*.js` (3 tests)

### Phase 7: Fractional Sub-second Style
- Implemented fractional cascade for sub-second units in `FormatNonDigital()`
  - When `microseconds` is "numeric", milliseconds includes micro+nano as fractional part (e.g., "444.055006 ms")
  - When `nanoseconds` is "numeric", microseconds includes nano as fractional part (e.g., "55.006 μs")
- **Enabled test**: `fractions-of-subsecond-units-en.js`

### Phase 8: Rounding Mode Trunc for Seconds
- Added special case in `FormatNonDigital()` for `seconds: "numeric"` without hours/minutes
  - When hours and minutes are not displayed, seconds should output as just a number (e.g., "1" not "1 second")
  - Implements truncation rounding per spec (not rounding, just truncate fractional part)
  - Respects `fractionalDigits` option for sub-second precision
- **Enabled test**: `rounding-mode-trunc-for-seconds.js`

### Phase 9: Digital Style Large Number Formatting
- Fixed digital style date units to use:
  - Proper number grouping (thousand separators) for date unit values (e.g., "1,234,567 days")
  - Proper short style pluralization (e.g., "days" for plural, "day" for singular)
- Fixed `GetUnitName()` in DefaultCldrProvider to handle pluralization for "day" in short style
- Updated `FormatDigital()` to use CLDR patterns with grouping
- Updated `FormatToPartsDigital()` to use proper pluralization and grouping in parts
- **Enabled test**: `style-digital-largenumber-en.js`

---

## Remaining Work by Category

### Category A: DurationFormat (4 exclusions) - MEDIUM PRIORITY

#### A1: Out of Range Validation (2 tests)
**Files**: `duration-out-of-range-3.js`, `duration-out-of-range-4.js`
**Issue**: Requires precise BigInt calculations for edge cases
**Complexity**: HIGH - needs careful math for boundary conditions

#### A3: Other DurationFormat (2 tests)
- `mixed-non-numeric-styles-es.js` - Spanish locale support
- `precision-exact-mathematical-values.js` - BigInt precision

### Category B: NumberFormat (10 exclusions) - LOW PRIORITY

#### B1: formatRange Methods (3 tests)
**Files**: `formatRange/en-US.js`, `formatRange/pt-PT.js`, `formatRangeToParts/en-US.js`
**Issue**: `formatRange()` and `formatRangeToParts()` methods not implemented
**Scope**: Add new methods to NumberFormatPrototype

#### B2: formatToParts for Non-English Units (4 tests)
**Files**: `unit-de-DE.js`, `unit-ja-JP.js`, `unit-ko-KR.js`, `unit-zh-TW.js`
**Issue**: Unit patterns only available for English
**Scope**: Expand UnitPatternsData.txt with more locale data

#### B3: Other NumberFormat (3 tests)
- `format-significant-digits.js` - Significant digits precision
- `useGrouping-extended-en-IN.js` - Indian numbering grouping
- `value-decimal-string.js` - Decimal string handling

### Category C: DateTimeFormat (27 exclusions) - LOW PRIORITY
Most require advanced CLDR data:
- Calendar systems (Chinese, Dangi, lunisolar)
- Timezone canonicalization
- Era formatting
- formatRange support

### Category D: Collator (5 exclusions) - LOW PRIORITY
- German phonebook collation
- Unicode extension handling
- Requires ICU-level collation data

### Category E: String Case Conversion (6 exclusions) - LOW PRIORITY
- Turkish/Azeri dotted I handling
- Lithuanian special casing
- Requires locale-specific case mapping tables

### Category F: Other (10 exclusions) - LOW PRIORITY
- Locale canonicalization (CLDR alias data)
- PluralRules (CLDR plural data)
- supportedValuesOf consistency
- DisplayNames edge cases

---

## Recommended Next Steps

### Immediate (Quick Wins) - ALL COMPLETED
~~1. **Fix digital formatToParts fractional parts** - DONE (Phase 6)~~
~~2. **Fix sub-second cascade in numeric mode** - DONE (Phase 6)~~
~~3. **Fix fractional sub-second style** - DONE (Phase 7)~~
~~4. **Fix rounding mode trunc for seconds** - DONE (Phase 8)~~
~~5. **Fix digital style large number formatting** - DONE (Phase 9)~~

### Medium Term (Requires Significant Work)
1. **BigInt Precision for DurationFormat** (A1)
   - `duration-out-of-range-3/4.js`, `precision-exact-mathematical-values.js`
   - Requires implementing arbitrary precision math for edge cases
   - Would enable 4 tests

2. **Improve formatRange for NumberFormat** (B1)
   - Requires locale-specific CLDR range patterns and part collapsing
   - Would enable 3 tests

### Long Term (Major Features)
1. **Add non-English locale data** for Spanish, German, etc.
2. **Implement calendar systems** for DateTimeFormat
3. **Add Temporal support** for DateTimeFormat

---

## Key Files

- `Jint/Native/Intl/JsDurationFormat.cs` - DurationFormat implementation
- `Jint/Native/Intl/JsNumberFormat.cs` - NumberFormat implementation
- `Jint/Native/Intl/DefaultCldrProvider.cs` - CLDR data provider
- `Jint/Native/Intl/DurationFormatPrototype.cs` - Duration validation
- `Jint.Tests.Test262/Test262Harness.settings.json` - Test exclusions

---

## Verification Commands

```bash
# Build
dotnet build --configuration Release --verbosity q

# Run intl402 tests
dotnet test --configuration Release --verbosity m Jint.Tests.Test262/Jint.Tests.Test262.csproj --filter "FullyQualifiedName~intl402"

# Quick REPL test
echo 'new Intl.DurationFormat("en", {style:"digital"}).format({hours:1,minutes:2,seconds:3})' | dotnet run --project Jint.Repl --configuration Release -- -t 10
```

---

## Notes

- Always delete `Jint.Tests.Test262/Generated` folder after modifying `Test262Harness.settings.json`
- Tests run in both strict and non-strict mode (×2 count)
- Test262 harness functions (`formatDurationFormatPattern`, `partitionDurationFormatPattern`) compute expected values dynamically
