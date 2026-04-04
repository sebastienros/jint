# Regex Engine — Remaining Work

## Current Status

**97,570 test262 passed | 6 failures | ~38 excluded regex tests**

Architecture: Acornima validates syntax at parse time (`RegExpParseMode.Validate`). At runtime, `NeedCustomEngine()` routes to either .NET Regex (via `AdaptRegExp`) or our custom QuickJS-based engine. Patterns come raw from source — Acornima does not translate them.

---

## Active Failures (6 test cases, 3 files)

### 1. `\w`/`\W` case folding with v-flag (4 test cases)

**Files:**
- `built-ins/RegExp/CharacterClassEscapes/character-class-word-class-escape-negative-cases.js`
- `built-ins/RegExp/CharacterClassEscapes/character-class-non-word-class-escape-positive-cases.js`

**Issue:** U+0130 (LATIN CAPITAL LETTER I WITH DOT ABOVE) is incorrectly classified as a word character by `\w` in `/u` mode. The v-flag path (custom engine) is correct, but these tests also exercise the `/u` path which goes through .NET Regex. .NET's `\w` in ECMAScript mode includes U+0130 via case folding to 'I', which JS spec says should not happen.

**Root cause:** .NET Regex ECMAScript mode case folding includes U+0130 -> I mapping, JS spec does not.

**Fix:** Route `/\w/u` and `/\W/u` patterns to the custom engine (add `\w`/`\W` + `/u` flag detection to `NeedCustomEngine`), or accept as .NET limitation since the test exercises both `/u` and `/v` paths in one file.

**Files to modify:** `Jint/Native/RegExp/RegExpConstructor.cs` — expand `NeedCustomEngine()`.

### 2. matchAll surrogate pair advancement (2 test cases)

**File:** `built-ins/String/prototype/matchAll/regexp-prototype-matchAll-v-u-flag.js`

**Issue:** `doMatchAll(/(?:)/gu)` on a string with supplementary characters expects 12 empty matches but gets 18. The empty match advancement counts UTF-16 code units instead of Unicode code points for the `/u` flag path.

**Root cause:** The `/u` path goes through .NET Regex. When `AdvanceStringIndex` is called for empty matches, .NET-based matching doesn't correctly advance past surrogate pairs as single code points.

**Fix:** In `RegExpPrototype`, when advancing past an empty match with `fullUnicode=true`, ensure the advance skips 2 code units for surrogate pairs. Check `AdvanceStringIndex` in the .NET execution path (the custom engine path already handles this correctly).

**Files to modify:** `Jint/Native/RegExp/RegExpPrototype.cs` — check `AdvanceStringIndex` and the `.NET` exec path for empty match advancement.

---

## Excluded Tests — Fixable

### 3. Unicode case folding (2 files, 4 test cases)

**Files:**
- `language/literals/regexp/u-case-mapping.js`
- `built-ins/RegExp/unicode_full_case_folding.js`

**Issue:** Custom engine case folding tables don't match ES spec. Tests check:
- `u-case-mapping.js`: `/\u212a/iu` should match 'k' (KELVIN SIGN case fold), `/\u017f/iu` should match 's' (LONG S)
- `unicode_full_case_folding.js`: JS uses simple case folding only, not full — U+0390/U+1FD3, U+03B0/U+1FE3, U+FB05/U+FB06 should NOT match each other

**Root cause:** Custom engine `Canonicalize()` in `UnicodeProperties.cs` may not correctly implement simple/common Unicode case folding per ES spec section 22.2.2.8.2.

**Fix:** Audit `UnicodeProperties.Canonicalize()` and `CaseConv()` against the ES spec. The spec says: use simple case folding from CaseFolding.txt (status C and S), NOT full case folding (status F). Verify the QuickJS-ported case fold tables match.

**Files to modify:** `Jint/Runtime/RegExp/Unicode/UnicodeProperties.cs`

### 4. Duplicate named groups (6 files, 12 test cases)

**Files:**
- `built-ins/RegExp/named-groups/duplicate-names-exec.js`
- `built-ins/RegExp/named-groups/duplicate-names-match.js`
- `built-ins/RegExp/prototype/exec/duplicate-named-groups-properties.js`
- `built-ins/RegExp/prototype/exec/duplicate-named-indices-groups-properties.js`
- `built-ins/String/prototype/match/duplicate-named-groups-properties.js`
- `built-ins/String/prototype/match/duplicate-named-indices-groups-properties.js`

**Issue:** `HasDuplicateNamedGroups()` routing was disabled (commit 9d1158b29) because it caused CI hangs. The custom engine's capture reset for duplicate named groups in alternation (`(?<x>a)|(?<x>b)`) has an infinite loop.

**Root cause:** The compiler's `CheckAdvance` emission may be missing for certain duplicate named group patterns, allowing the interpreter to loop forever on zero-width matches.

**Fix:**
1. Re-enable `HasDuplicateNamedGroups()` in `NeedCustomEngine()`
2. Debug the specific pattern that hangs — add a step counter limit to the interpreter for debugging
3. Check compiler's `NeedCheckAdvAndCaptureInit` for duplicate group patterns
4. The full-snapshot backtracking refactor may have already fixed the underlying capture state issue

**Files to modify:**
- `Jint/Native/RegExp/RegExpConstructor.cs` — re-enable routing
- `Jint/Runtime/RegExp/RegExpCompiler.cs` — verify CheckAdvance for dup groups
- `Jint/Runtime/RegExp/RegExpInterpreter.cs` — if needed

### 5. Annex B malformed named groups (1 file, 2 test cases)

**File:** `annexB/built-ins/RegExp/named-groups/non-unicode-malformed.js`

**Issue:** Per Annex B.1.2, in non-unicode mode, malformed `\k<name>` syntax should be accepted as literal characters. E.g., `/\k<a>/` matches literal string "k<a>".

**Root cause:** Acornima parser rejects malformed `\k<>` at parse time even in non-unicode mode. The custom engine compiler also rejects it.

**Fix:** Either:
- Update Acornima to accept Annex B syntax in non-unicode mode (external dependency)
- Or add Annex B fallback in the custom engine's compiler: when `\k<name>` fails in non-unicode mode, treat `\k` as literal 'k' and reparse

**Files to modify:** `Jint/Runtime/RegExp/RegExpCompiler.cs` — add AnnexB fallback for `\k`

---

## Excluded Tests — Blocked on Acornima

### 6. Unicode 17 script names (22 files, 44 test cases)

**Files:** 11 Script + 11 Script_Extensions files for: Beria_Erfe, Garay, Gurung_Khema, Kirat_Rai, Ol_Onal, Sidetic, Sunuwar, Tai_Yo, Todhri, Tolong_Siki, Tulu_Tigalari

**Issue:** These tests use regex literals like `/\p{Script=Garay}/u`. Acornima rejects "Garay" as an unknown script name at parse time, so the test file can't even be parsed.

**Root cause:** Acornima's Unicode property name tables only go up to Unicode 16. Unicode 17 added these 11 new scripts.

**Status:** Blocked on Acornima update. Our custom engine already has the correct script data (Unicode 17 tables). Once Acornima recognizes these names, the tests should pass immediately — the pattern would either go through .NET (which would fail) and then fall back to custom engine via `NeedCustomEngine` (detects `\p{}`), or Acornima could return `Regex=null` and we'd route directly.

**Fix:** Update Acornima's `UnicodeProperties` to include Unicode 17 script names. This is an Acornima-side change.

---

## Summary Table

| # | Category | Files | Tests | Status | Blocker |
|---|----------|-------|-------|--------|---------|
| 1 | `\w`/`\W` case fold with /u | 2 | 4 | **Failing** | .NET case fold |
| 2 | matchAll surrogate advance /u | 1 | 2 | **Failing** | .NET advance |
| 3 | Unicode case folding | 2 | 4 | Excluded | Custom engine tables |
| 4 | Duplicate named groups | 6 | 12 | Excluded | Custom engine hang |
| 5 | Annex B malformed `\k` | 1 | 2 | Excluded | Parser + compiler |
| 6 | Unicode 17 script names | 22 | 44 | Excluded | **Acornima** |
| | **Total remaining** | **34** | **68** | | |

### Not fixable without external changes

| Category | Tests | Reason |
|----------|-------|--------|
| Unicode 17 scripts | 44 | Acornima needs Unicode 17 property names |

### Fixable in Jint

| Category | Tests | Effort |
|----------|-------|--------|
| `\w`/`\W` + matchAll /u path | 6 | Low — expand NeedCustomEngine or fix .NET path |
| Unicode case folding | 4 | Medium — audit Canonicalize() tables |
| Duplicate named groups | 12 | Medium — debug hang, re-enable routing |
| Annex B `\k` fallback | 2 | Low — add compiler fallback |
| **Total fixable** | **24** | |
