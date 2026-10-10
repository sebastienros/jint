"""The reference model Jint's Intl.DateTimeFormat format matcher was ported from (sebastienros/jint#4158).

NOT shipped and NOT run by the build. A Python model of the ECMA-402 BestFitFormatMatcher as ICU's
DateTimePatternGenerator (which V8 and SpiderMonkey call) implements it, over CLDR 48.2's resolved JSON
(cldr-dates-full + cldr-core 48.2.0, the inputs pin.json names). Jint/Native/Intl/DateTimePatternGenerator.cs is
its C# port, and the golden table Jint.Tests checks that port against is this model's output:

  node probe-golden.js > icu-golden.tsv
  python format_matcher.py compare <cldr-json-root> icu-golden.tsv
  python format_matcher.py golden <cldr-json-root> icu-golden.tsv > golden.tsv

<cldr-json-root> holds `package/` (cldr-dates-full) and `core/package/` (cldr-core), unpacked. `compare` checks
the model against an ICU-based engine: Node 24.19 (ICU 78.3, CLDR 48.0) agreed with it on every row of the golden
table's text, and on 16,023 of 16,025 across the 641 cldr-json locales ICU resolves to themselves.

What it models, in ICU's own terms (icu4c/source/i18n/dtptngen.cpp):
  * skeleton construction from the options bag, the way V8's js-date-time-format.cc builds it;
  * DateTimeMatcher::set (field types, the implied 'a' of a 12-hour skeleton) and getDistance;
  * getBestRaw over availableFormats + the date/time style patterns + the canonical single fields, in the order
    ICU adds them (its ties go to the first added);
  * getBestAppending (date and time halves, appendItems, the fractional-second fix-up);
  * the dateTimeFormat choice by month width (the atTime variants, as ICU 72+ does);
  * adjustFieldTypes with UDATPG_MATCH_HOUR_FIELD_LENGTH (V8's option) and the specified-skeleton rule;
  * V8's hour-cycle replacement in the chosen pattern;
  * dateStyle and timeStyle as V8's DateTimeStylePattern gets them from ICU (style_pattern), and Temporal's
    AdjustDateTimeStyleFormat over them (adjust_style_format); the golden-styles table Jint.Tests reads:

  python format_matcher.py style-cases <cldr-json-root> > style-cases.tsv
  node probe-styles.js style-cases.tsv > icu-styles.tsv
  python format_matcher.py compare-styles <cldr-json-root> icu-styles.tsv
  python format_matcher.py golden-styles <cldr-json-root> icu-styles.tsv > golden-styles.tsv

basic_format_matcher is the ECMA-402 BasicFormatMatcher run literally over the same candidates, kept for contrast:
it matches ICU on only about half of the bags, which is why Jint treats formatMatcher 'basic' as best fit.
"""
import io
import json
import os
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', newline='\n')

# ---------------------------------------------------------------------------------------------------
# ICU field indices (UDateTimePatternField order) and the dtTypes table.
ERA, YEAR, QUARTER, MONTH, WOY, WOM, WEEKDAY, DOY, DOWIM, DAY, DAYPERIOD, HOUR, MINUTE, SECOND, FRACSEC, ZONE = range(16)
FIELD_COUNT = 16
DATE_MASK = (1 << DAYPERIOD) - 1
TIME_MASK = ((1 << FIELD_COUNT) - 1) & ~DATE_MASK
APPEND_KEYS = {ERA: 'Era', YEAR: 'Year', QUARTER: 'Quarter', MONTH: 'Month', WOY: 'Week', WOM: 'Week', WEEKDAY: 'Day-Of-Week',
               DOY: 'Day', DOWIM: 'Day', DAY: 'Day', DAYPERIOD: None, HOUR: 'Hour', MINUTE: 'Minute', SECOND: 'Second',
               FRACSEC: 'Second', ZONE: 'Timezone'}
FIELD_NAME_KEYS = {ERA: 'era', YEAR: 'year', QUARTER: 'quarter', MONTH: 'month', WOY: 'week', WOM: 'weekOfMonth',
                   WEEKDAY: 'weekday', DOY: 'dayOfYear', DOWIM: 'weekdayOfMonth', DAY: 'day', DAYPERIOD: 'dayperiod',
                   HOUR: 'hour', MINUTE: 'minute', SECOND: 'second', FRACSEC: 'second', ZONE: 'zone'}

DT_NARROW, DT_SHORTER, DT_SHORT, DT_LONG, DT_NUMERIC, DT_DELTA = -0x101, -0x102, -0x103, -0x104, 0x100, 0x10

# (char, field, type, minLen)
DT_TYPES = [
    ('G', ERA, DT_SHORT, 1), ('G', ERA, DT_LONG, 4), ('G', ERA, DT_NARROW, 5),
    ('y', YEAR, DT_NUMERIC, 1), ('Y', YEAR, DT_NUMERIC + DT_DELTA, 1), ('u', YEAR, DT_NUMERIC + 2 * DT_DELTA, 1),
    ('r', YEAR, DT_NUMERIC + 3 * DT_DELTA, 1), ('U', YEAR, DT_SHORT, 1), ('U', YEAR, DT_LONG, 4), ('U', YEAR, DT_NARROW, 5),
    ('Q', QUARTER, DT_NUMERIC, 1), ('Q', QUARTER, DT_SHORT, 3), ('Q', QUARTER, DT_LONG, 4), ('Q', QUARTER, DT_NARROW, 5),
    ('q', QUARTER, DT_NUMERIC + DT_DELTA, 1), ('q', QUARTER, DT_SHORT - DT_DELTA, 3), ('q', QUARTER, DT_LONG - DT_DELTA, 4),
    ('q', QUARTER, DT_NARROW - DT_DELTA, 5),
    ('M', MONTH, DT_NUMERIC, 1), ('M', MONTH, DT_SHORT, 3), ('M', MONTH, DT_LONG, 4), ('M', MONTH, DT_NARROW, 5),
    ('L', MONTH, DT_NUMERIC + DT_DELTA, 1), ('L', MONTH, DT_SHORT - DT_DELTA, 3), ('L', MONTH, DT_LONG - DT_DELTA, 4),
    ('L', MONTH, DT_NARROW - DT_DELTA, 5), ('l', MONTH, DT_NUMERIC + DT_DELTA, 1),
    ('w', WOY, DT_NUMERIC, 1), ('W', WOM, DT_NUMERIC, 1),
    ('E', WEEKDAY, DT_SHORT, 1), ('E', WEEKDAY, DT_LONG, 4), ('E', WEEKDAY, DT_NARROW, 5), ('E', WEEKDAY, DT_SHORTER, 6),
    ('c', WEEKDAY, DT_NUMERIC + 2 * DT_DELTA, 1), ('c', WEEKDAY, DT_SHORT - 2 * DT_DELTA, 3), ('c', WEEKDAY, DT_LONG - 2 * DT_DELTA, 4),
    ('c', WEEKDAY, DT_NARROW - 2 * DT_DELTA, 5), ('c', WEEKDAY, DT_SHORTER - 2 * DT_DELTA, 6),
    ('e', WEEKDAY, DT_NUMERIC + DT_DELTA, 1), ('e', WEEKDAY, DT_SHORT - DT_DELTA, 3), ('e', WEEKDAY, DT_LONG - DT_DELTA, 4),
    ('e', WEEKDAY, DT_NARROW - DT_DELTA, 5), ('e', WEEKDAY, DT_SHORTER - DT_DELTA, 6),
    ('d', DAY, DT_NUMERIC, 1), ('g', DAY, DT_NUMERIC + DT_DELTA, 1), ('D', DOY, DT_NUMERIC, 1), ('F', DOWIM, DT_NUMERIC, 1),
    ('a', DAYPERIOD, DT_SHORT, 1), ('a', DAYPERIOD, DT_LONG, 4), ('a', DAYPERIOD, DT_NARROW, 5),
    ('b', DAYPERIOD, DT_SHORT - DT_DELTA, 1), ('b', DAYPERIOD, DT_LONG - DT_DELTA, 4), ('b', DAYPERIOD, DT_NARROW - DT_DELTA, 5),
    ('B', DAYPERIOD, DT_SHORT - 3 * DT_DELTA, 1), ('B', DAYPERIOD, DT_LONG - 3 * DT_DELTA, 4), ('B', DAYPERIOD, DT_NARROW - 3 * DT_DELTA, 5),
    ('H', HOUR, DT_NUMERIC + 10 * DT_DELTA, 1), ('k', HOUR, DT_NUMERIC + 11 * DT_DELTA, 1), ('h', HOUR, DT_NUMERIC, 1),
    ('K', HOUR, DT_NUMERIC + DT_DELTA, 1),
    ('m', MINUTE, DT_NUMERIC, 1), ('s', SECOND, DT_NUMERIC, 1), ('A', SECOND, DT_NUMERIC + DT_DELTA, 1),
    ('S', FRACSEC, DT_NUMERIC, 1),
    ('v', ZONE, DT_SHORT - 2 * DT_DELTA, 1), ('v', ZONE, DT_LONG - 2 * DT_DELTA, 4), ('z', ZONE, DT_SHORT, 1), ('z', ZONE, DT_LONG, 4),
    ('Z', ZONE, DT_NARROW - DT_DELTA, 1), ('Z', ZONE, DT_LONG - DT_DELTA, 4), ('Z', ZONE, DT_SHORT - DT_DELTA, 5),
    ('O', ZONE, DT_SHORT - DT_DELTA, 1), ('O', ZONE, DT_LONG - DT_DELTA, 4),
    ('V', ZONE, DT_SHORT - DT_DELTA, 1), ('V', ZONE, DT_LONG - DT_DELTA, 2), ('V', ZONE, DT_LONG - 1 - DT_DELTA, 3),
    ('V', ZONE, DT_LONG - 2 - DT_DELTA, 4),
    ('X', ZONE, DT_NARROW - DT_DELTA, 1), ('X', ZONE, DT_SHORT - DT_DELTA, 2), ('X', ZONE, DT_LONG - DT_DELTA, 4),
    ('x', ZONE, DT_NARROW - DT_DELTA, 1), ('x', ZONE, DT_SHORT - DT_DELTA, 2), ('x', ZONE, DT_LONG - DT_DELTA, 4),
]


def canonical_row(ch, length):
    best = None
    for row in DT_TYPES:
        if row[0] == ch and row[3] <= length:
            if best is None or row[3] > best[3]:
                best = row
    return best


def tokenize(pattern):
    """Split an LDML pattern into ('field', ch, len) / ('lit', text) tokens; quotes are kept raw in lit."""
    out = []
    i = 0
    n = len(pattern)
    while i < n:
        c = pattern[i]
        if c == "'":
            j = i + 1
            text = "'"
            while j < n:
                if pattern[j] == "'":
                    if j + 1 < n and pattern[j + 1] == "'":
                        text += "''"
                        j += 2
                        continue
                    text += "'"
                    j += 1
                    break
                text += pattern[j]
                j += 1
            out.append(('lit', text))
            i = j
            continue
        if ('a' <= c <= 'z') or ('A' <= c <= 'Z'):
            j = i
            while j < n and pattern[j] == c:
                j += 1
            out.append(('field', c, j - i))
            i = j
            continue
        out.append(('lit', c))
        i += 1
    return out


class Skeleton:
    def __init__(self, text):
        self.text = text
        self.orig = {}   # field -> (char, len)
        self.type = [0] * FIELD_COUNT
        for tok in tokenize(text):
            if tok[0] != 'field':
                continue
            row = canonical_row(tok[1], tok[2])
            if row is None:
                continue
            f = row[1]
            self.orig[f] = (tok[1], tok[2])
            t = row[2]
            if t > 0:
                t += tok[2]
            self.type[f] = t
        # DateTimeMatcher::set: a 12-hour skeleton without a day period gets the default 'a'; a 24-hour one loses it.
        if HOUR in self.orig:
            hc = self.orig[HOUR][0]
            if hc in 'hK':
                if DAYPERIOD not in self.orig:
                    self.orig[DAYPERIOD] = ('a', 1)
                    self.type[DAYPERIOD] = DT_SHORT
            elif DAYPERIOD in self.orig:
                del self.orig[DAYPERIOD]
                self.type[DAYPERIOD] = 0

    def mask(self):
        m = 0
        for f in range(FIELD_COUNT):
            if self.type[f] != 0:
                m |= 1 << f
        return m


EXTRA_FIELD, MISSING_FIELD = 0x10000, 0x1000


def distance(req, include_mask, cand):
    result = 0
    missing = 0
    extra = 0
    for f in range(FIELD_COUNT):
        my = req.type[f] if include_mask & (1 << f) else 0
        other = cand.type[f]
        if my == other:
            continue
        if my == 0:
            result += EXTRA_FIELD
            extra |= 1 << f
        elif other == 0:
            result += MISSING_FIELD
            missing |= 1 << f
        else:
            result += abs(my - other)
    return result, missing, extra


# ---------------------------------------------------------------------------------------------------
# CLDR inheritance, as tools/cldr-dates/Jint.CldrDates.Generator resolves it (https://www.unicode.org/reports/tr35/#Parent_Locales):
# the explicit parentLocales table, then the root for a language-and-script locale whose script is not the language's
# likely one, then truncation; a parent cldr-json does not carry (a default-content locale) is skipped for its own.
_available_formats = {}
_parents = None
_likely_scripts = None


def available_formats(root, locale):
    if locale not in _available_formats:
        g = json.load(open(os.path.join(root, 'package', 'main', locale, 'ca-gregorian.json'), encoding='utf-8'))['main'][locale]['dates']['calendars']['gregorian']
        _available_formats[locale] = {k: v for k, v in g['dateTimeFormats']['availableFormats'].items() if '-alt-' not in k and '-count-' not in k}
    return _available_formats[locale]


def cldr_parent_of(root, locale):
    global _parents, _likely_scripts
    if _parents is None:
        _parents = json.load(open(os.path.join(root, 'core', 'package', 'supplemental', 'parentLocales.json'), encoding='utf-8'))['supplemental']['parentLocales']['parentLocale']
        likely = json.load(open(os.path.join(root, 'core', 'package', 'supplemental', 'likelySubtags.json'), encoding='utf-8'))['supplemental']['likelySubtags']
        _likely_scripts = {k: v.split('-')[1] for k, v in likely.items() if '-' not in k and len(v.split('-')) == 3}
    if locale == 'und':
        return None
    if locale in _parents:
        return _parents[locale]
    subtags = locale.split('-')
    if len(subtags) == 2 and len(subtags[1]) == 4 and subtags[1][0].isalpha() and _likely_scripts.get(subtags[0]) != subtags[1]:
        return 'und'
    return locale[:locale.rfind('-')] if '-' in locale else 'und'


def parent_of(root, locale):
    parent = cldr_parent_of(root, locale)
    while parent is not None and not os.path.isdir(os.path.join(root, 'package', 'main', parent)):
        parent = cldr_parent_of(root, parent)
    return parent


def own_formats_chain(root, locale):
    """The availableFormats keys each of locale, its parent, ..., the root holds itself: new, or different from its parent's."""
    chain = []
    while locale is not None:
        parent = parent_of(root, locale)
        formats = available_formats(root, locale)
        inherited = available_formats(root, parent) if parent is not None else {}
        chain.append([k for k, v in formats.items() if inherited.get(k) != v])
        locale = parent
    return chain


# ---------------------------------------------------------------------------------------------------
class LocaleData:
    def __init__(self, root, locale):
        self.root = root
        self.locale = locale
        main = os.path.join(root, 'package', 'main', locale)
        g = json.load(open(os.path.join(main, 'ca-gregorian.json'), encoding='utf-8'))['main'][locale]['dates']['calendars']['gregorian']
        self.g = g
        self.zone_names = json.load(open(os.path.join(main, 'timeZoneNames.json'), encoding='utf-8'))['main'][locale]['dates']['timeZoneNames']
        fields = json.load(open(os.path.join(root, 'package', 'main', locale, 'dateFields.json'), encoding='utf-8'))
        self.fields = fields['main'][locale]['dates']['fields']
        dtf = g['dateTimeFormats']
        self.append_items = dtf['appendItems']
        self.dt_formats = {k: dtf[k] for k in ('full', 'long', 'medium', 'short')}
        at = g.get('dateTimeFormats-atTime', {}).get('standard', {})
        self.dt_formats_at = {k: at.get(k, self.dt_formats[k]) for k in self.dt_formats}
        # Candidates: skeleton text -> pattern. ICU order: style patterns, canonical items, then availableFormats
        # overriding any skeleton already present.
        cands = {}
        bases = set()
        def add(pattern, skel=None, override=False):
            # DateTimePatternGenerator::addPatternWithSkeleton: a pattern added without a skeleton of its own
            # (a style pattern, a canonical item) is dropped when its BASE skeleton is already present, so the
            # medium date pattern shadows the short one; availableFormats entries replace a same-skeleton one.
            specified = skel is not None
            if skel is None:
                skel = skeleton_of(pattern)
            key = Skeleton(skel)
            k = canonical_key(key)
            base = base_key(key)
            if not specified and base in bases:
                return
            if k in cands and not override:
                return
            bases.add(base)
            cands[k] = (key, pattern)
        for style in ('full', 'long', 'medium', 'short'):
            for block in ('dateFormats', 'timeFormats'):
                p = g[block][style]
                if isinstance(p, dict):
                    p = p.get('_value')
                add(p)
        for ch in 'GyQMwWEDFdaHmsSv':
            add(ch, ch)
        # availableFormats in the order ICU adds them: the locale's own entries first, then each CLDR ancestor's in
        # turn up to the root, each bundle's keys in ASCII order. A locale's own entries are the ones that are new or
        # differ from its parent's. The order decides ties in get_best_raw (en-AU's MMMMEEEEd before en-001's MMMEd).
        resolved = {k: v for k, v in dtf['availableFormats'].items() if '-alt-' not in k and '-count-' not in k}
        seen = set()
        done = set()
        for own in own_formats_chain(root, locale):
            for k in sorted(own):
                if k not in resolved or k in done:
                    continue
                done.add(k)
                ck = canonical_key(Skeleton(k))
                add(resolved[k], k, override=ck not in seen)
                seen.add(ck)
        self.cands = cands


def skeleton_of(pattern):
    return ''.join(tok[1] * tok[2] for tok in tokenize(pattern) if tok[0] == 'field')


def canonical_key(sk):
    return tuple((f, sk.orig[f]) for f in sorted(sk.orig))


def base_key(sk):
    """ICU's baseOriginal: each field at its canonical row's minimum length, so d and dd share a base."""
    out = []
    for f in sorted(sk.orig):
        ch, ln = sk.orig[f]
        row = canonical_row(ch, ln)
        out.append((f, row[0], row[3]))
    return tuple(out)


def get_best_raw(ld, req, include_mask):
    best = None
    # ICU iterates its PatternMap by the base skeleton's first letter, upper case first, and within a letter in the
    # order the patterns were added (a replaced entry keeps its place); ties keep the first.
    def bucket(item):
        sk = item[1][0]
        first = sk.orig[min(sk.orig)][0] if sk.orig else '~'
        return (0 if first.isupper() else 1, first.lower())
    for key, (sk, pattern) in sorted(ld.cands.items(), key=bucket):
        d, missing, extra = distance(req, include_mask, sk)
        if best is None or d < best[0]:
            best = (d, missing, extra, sk, pattern)
            if d == 0:
                break
    return best


def adjust_field_types(pattern, req, specified, fix_fractional=False, decimal='.', match_hour=True):
    out = []
    for tok in tokenize(pattern):
        if tok[0] == 'lit':
            out.append(tok[1])
            continue
        ch, ln = tok[1], tok[2]
        row = canonical_row(ch, ln)
        if row is None:
            out.append(ch * ln)
            continue
        f = row[1]
        if fix_fractional and f == SECOND:
            out.append(ch * ln + decimal + 'S' * req.orig[FRACSEC][1])
            continue
        if req.type[f] == 0:
            out.append(ch * ln)
            continue
        rc, rl = req.orig[f]
        if rc == 'E' and rl < 3:
            rl = 3
        adj = rl
        if f in (MINUTE, SECOND) or (f == HOUR and not match_hour):
            adj = ln   # V8 passes UDATPG_MATCH_HOUR_FIELD_LENGTH only; ICU's own callers pass no option at all
        elif specified is not None and rc not in 'ce' and f in specified.orig:
            sl = specified.orig[f][1]
            pat_numeric = row[2] > 0
            skel_numeric = specified.type[f] > 0
            if sl == rl or pat_numeric != skel_numeric:
                adj = ln
        c = rc if (f not in (HOUR, MONTH, WEEKDAY) and (f != YEAR or rc == 'Y')) else ch
        out.append(c * adj)
    return ''.join(out)


def get_best_appending(ld, req, missing_fields, decimal, match_hour=True):
    if missing_fields == 0:
        return ''
    d, missing, extra, sk, pattern = get_best_raw(ld, req, missing_fields)
    result = adjust_field_types(pattern, req, sk, decimal=decimal, match_hour=match_hour)
    if missing == 0 and extra == 0:
        return result
    frac = (1 << SECOND) | (1 << FRACSEC)
    if (missing & frac) == (1 << FRACSEC) and (missing_fields & frac) == frac:
        result = adjust_field_types(pattern, req, sk, fix_fractional=True, decimal=decimal, match_hour=match_hour)
        missing &= ~(1 << FRACSEC)
    guard = 0
    while missing:
        guard += 1
        if guard > FIELD_COUNT:
            break
        start = missing
        d2, missing2, extra2, sk2, pattern2 = get_best_raw(ld, req, missing)
        temp = adjust_field_types(pattern2, req, sk2, decimal=decimal, match_hour=match_hour)
        found = start & ~missing2
        if found == 0:
            break
        top = found.bit_length() - 1
        key = APPEND_KEYS.get(top)
        if key and key in ld.append_items:
            name = (ld.fields.get(FIELD_NAME_KEYS[top], {}) or {}).get('displayName', '')
            result = ld.append_items[key].replace('{0}', result).replace('{1}', temp).replace('{2}', "'" + name + "'")
        missing = missing2
    return result


def best_pattern(ld, skeleton_text, decimal='.', match_hour=True):
    """getBestPattern; match_hour=False is ICU's default options (DateFormat::getBestPattern), True V8's."""
    req = Skeleton(skeleton_text)
    d, missing, extra, sk, pattern = get_best_raw(ld, req, -1)
    if missing == 0 and extra == 0:
        return adjust_field_types(pattern, req, sk, decimal=decimal, match_hour=match_hour)
    needed = req.mask()
    date = get_best_appending(ld, req, needed & DATE_MASK, decimal, match_hour)
    time = get_best_appending(ld, req, needed & TIME_MASK, decimal, match_hour)
    if not date:
        return time
    if not time:
        return date
    month_len = req.orig.get(MONTH, ('M', 0))[1]
    style = 'short'
    if month_len == 4:
        style = 'full' if WEEKDAY in req.orig else 'long'
    elif month_len == 3:
        style = 'medium'
    fmt = ld.dt_formats_at[style]
    # {1} is the date, {0} the time; the joining pattern's own literals are quoted already.
    return fmt.replace('{1}', '\u0001').replace('{0}', time).replace('\u0001', date)


# ---------------------------------------------------------------------------------------------------
# ECMA-402 BasicFormatMatcher over the same candidate list, for comparison.
TABLE16 = ['weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName']


def record_of(pattern):
    rec = {}
    for tok in tokenize(pattern):
        if tok[0] != 'field':
            continue
        ch, ln = tok[1], tok[2]
        if ch in 'Eec':
            rec['weekday'] = 'narrow' if ln == 5 else 'long' if ln == 4 else 'short'
        elif ch == 'G':
            rec['era'] = 'narrow' if ln == 5 else 'long' if ln == 4 else 'short'
        elif ch in 'yY':
            rec['year'] = '2-digit' if ln == 2 else 'numeric'
        elif ch in 'ML':
            rec['month'] = {1: 'numeric', 2: '2-digit', 3: 'short', 4: 'long', 5: 'narrow'}[min(ln, 5)]
        elif ch == 'd':
            rec['day'] = '2-digit' if ln == 2 else 'numeric'
        elif ch == 'B':
            rec['dayPeriod'] = 'narrow' if ln == 5 else 'long' if ln == 4 else 'short'
        elif ch in 'hHkK':
            rec['hour'] = '2-digit' if ln == 2 else 'numeric'
        elif ch == 'm':
            rec['minute'] = '2-digit' if ln == 2 else 'numeric'
        elif ch == 's':
            rec['second'] = '2-digit' if ln == 2 else 'numeric'
        elif ch == 'S':
            rec['fractionalSecondDigits'] = ln
        elif ch in 'zvO':
            rec['timeZoneName'] = 'short'
    return rec


def basic_format_matcher(ld, options):
    values = ['2-digit', 'numeric', 'narrow', 'short', 'long']
    best = None
    for key, (sk, pattern) in ld.cands.items():
        rec = record_of(pattern)
        score = 0
        for prop in TABLE16:
            o = options.get(prop)
            f = rec.get(prop)
            if o is None and f is not None:
                score -= 20
            elif o is not None and f is None:
                score -= 120
            elif o is not None and o != f:
                if prop == 'fractionalSecondDigits' or prop == 'timeZoneName':
                    score -= 120
                    continue
                delta = max(min(values.index(f) - values.index(o), 2), -2)
                score -= {2: 6, 1: 3, -1: 6, -2: 8}[delta]
        if best is None or score > best[0]:
            best = (score, pattern)
    return best[1]


# ---------------------------------------------------------------------------------------------------
# Skeleton from an options bag, as V8 builds it; hour letter from the resolved hour cycle.
def skeleton_from_options(o, hc):
    s = ''
    w = {'narrow': 'EEEEE', 'short': 'EEE', 'long': 'EEEE'}
    if 'weekday' in o: s += w[o['weekday']]
    if 'era' in o: s += {'narrow': 'GGGGG', 'short': 'G', 'long': 'GGGG'}[o['era']]
    if 'year' in o: s += {'2-digit': 'yy', 'numeric': 'y'}[o['year']]
    if 'month' in o: s += {'2-digit': 'MM', 'numeric': 'M', 'narrow': 'MMMMM', 'short': 'MMM', 'long': 'MMMM'}[o['month']]
    if 'day' in o: s += {'2-digit': 'dd', 'numeric': 'd'}[o['day']]
    if 'dayPeriod' in o: s += {'narrow': 'BBBBB', 'short': 'B', 'long': 'BBBB'}[o['dayPeriod']]
    if 'hour' in o:
        ch = {'h11': 'K', 'h12': 'h', 'h23': 'H', 'h24': 'k'}[hc]
        s += ch * (2 if o['hour'] == '2-digit' else 1)
    if 'minute' in o: s += 'mm' if o['minute'] == '2-digit' else 'm'
    if 'second' in o: s += 'ss' if o['second'] == '2-digit' else 's'
    if 'fractionalSecondDigits' in o: s += 'S' * o['fractionalSecondDigits']
    if 'timeZoneName' in o: s += {'short': 'z', 'long': 'zzzz'}[o['timeZoneName']]
    return s


def replace_hour_cycle(pattern, hc):
    ch = {'h11': 'K', 'h12': 'h', 'h23': 'H', 'h24': 'k'}[hc]
    out = []
    for tok in tokenize(pattern):
        if tok[0] == 'field' and tok[1] in 'hHkK':
            out.append(ch * tok[2])
        elif tok[0] == 'field':
            out.append(tok[1] * tok[2])
        else:
            out.append(tok[1])
    return ''.join(out)


# ---------------------------------------------------------------------------------------------------
# Rendering, for the two instants the golden table is written at (UTC, Gregorian, Latin digits).
DATES = [
    dict(year=2022, month=12, day=24, weekday='sat', hour=15, minute=7, second=9, ms=0),
    dict(year=2023, month=3, day=6, weekday='mon', hour=9, minute=4, second=5, ms=0),
]


def render(ld, pattern, date):
    g = ld.g
    out = []
    tokens = tokenize(pattern)
    shows = {tok[1] for tok in tokens if tok[0] != 'lit'}
    for tok in tokens:
        if tok[0] == 'lit':
            t = tok[1]
            if t.startswith("'"):
                t = t[1:-1] if len(t) >= 2 and t.endswith("'") else t[1:]
                t = t.replace("''", "'") if t else "'"
            out.append(t)
            continue
        ch, ln = tok[1], tok[2]
        if ch == 'G':
            w = 'eraNarrow' if ln == 5 else 'eraNames' if ln == 4 else 'eraAbbr'
            out.append(g['eras'][w]['1'])
        elif ch in 'yY':
            out.append('%02d' % (date['year'] % 100) if ln == 2 else str(date['year']).zfill(ln))
        elif ch in 'ML':
            ctx = 'format' if ch == 'M' else 'stand-alone'
            if ln <= 2:
                out.append(('%02d' if ln == 2 else '%d') % date['month'])
            else:
                w = {3: 'abbreviated', 4: 'wide'}.get(ln, 'narrow')
                out.append(g['months'][ctx][w][str(date['month'])])
        elif ch in 'Ec':
            ctx = 'format' if ch == 'E' else 'stand-alone'
            w = {4: 'wide', 5: 'narrow', 6: 'short'}.get(ln, 'abbreviated')
            out.append(g['days'][ctx][w][date['weekday']])
        elif ch == 'd':
            out.append(('%02d' if ln == 2 else '%d') % date['day'])
        elif ch in 'ab':
            out.append(am_pm_day_period(ld, date, ln, ch == 'b' and shown_as_noon(date, shows)))
        elif ch == 'B':
            out.append(flexible_day_period(ld, date, ln, shows))
        elif ch == 'z':
            out.append(utc_zone_name(ld, ln))
        elif ch in 'hHkK':
            h = date['hour']
            v = {'h': (h % 12) or 12, 'K': h % 12, 'H': h, 'k': h or 24}[ch]
            out.append(('%02d' if ln == 2 else '%d') % v)
        elif ch == 'm':
            out.append(('%02d' if ln == 2 else '%d') % date['minute'])
        elif ch == 's':
            out.append(('%02d' if ln == 2 else '%d') % date['second'])
        elif ch == 'S':
            out.append('0' * ln)
        else:
            out.append('<' + ch * ln + '>')
    # Jint writes a plain space where CLDR 42+ has U+202F, in format() and formatToParts() alike.
    return ''.join(out).replace(' ', ' ')


def utc_zone_name(ld, length):
    """What ICU writes for the UTC zone: CLDR's Etc/UTC short (z) or long (zzzz) standard name, and where the locale has
    none, the localized GMT format — its zero form for z, and for zzzz the long form with the offset written out
    ("GMT+00:00"). Only the golden tables' timeZone: 'UTC' is modelled."""
    names = ld.zone_names
    utc = names.get('zone', {}).get('Etc', {}).get('UTC', {})
    width = 'long' if length >= 4 else 'short'
    name = utc.get(width, {}).get('standard')
    if name:
        return name
    if width == 'short':
        return names.get('gmtZeroFormat', 'GMT')
    # The long form writes the locale's hourFormat with two-digit hours, whatever it says ("+H:mm" in vmw, "+HH.mm" in nds).
    positive = names.get('hourFormat', '+HH:mm;-HH:mm').split(';')[0].replace('HH', 'H').replace('H', '00').replace('mm', '00')
    return names.get('gmtFormat', 'GMT{0}').replace('{0}', positive)


_day_period_rules = None


def day_period_rules(ld):
    """The locale's dayPeriodRuleSet as ICU's DayPeriodRules::getInstance (dayperiodrules.cpp) finds it: the locale's
    own, then each truncation of it, not its CLDR parent chain (zh-Hant has zh's, although its parent is the root). A
    language with none has none, and ICU writes am/pm, which the root's rule set (am and pm only) writes too."""
    global _day_period_rules
    if _day_period_rules is None:
        _day_period_rules = json.load(open(os.path.join(ld.root, 'core', 'package', 'supplemental', 'dayPeriods.json'), encoding='utf-8'))['supplemental']['dayPeriodRuleSet']
    name = ld.locale
    while name not in _day_period_rules:
        if '-' not in name:
            return _day_period_rules['und']
        name = name.rsplit('-', 1)[0]
    return _day_period_rules[name]


def shown_as_noon(date, shows):
    """Whether a pattern writing the letters `shows` shows the time as exactly noon: ICU's SimpleDateFormat writes noon
    only then, its minute and second zero where the pattern writes them (parsePattern's fHasMinute and fHasSecond)."""
    return date['hour'] == 12 and ('m' not in shows or date['minute'] == 0) and ('s' not in shows or date['second'] == 0)


def day_period_name(ld, width, period):
    """A format-context day period of the width; ICU fills a missing wide or narrow one with the abbreviated one."""
    names = ld.g['dayPeriods']['format']
    return names[width].get(period) or names['abbreviated'].get(period)


def am_pm_day_period(ld, date, length, noon=False):
    """An a field; and a b field, which is the locale's noon for a time shown as exactly noon where it has one."""
    width = {4: 'wide', 5: 'narrow'}.get(length, 'abbreviated')
    if noon and day_period_name(ld, width, 'noon'):
        return day_period_name(ld, width, 'noon')
    return ld.g['dayPeriods']['format'][width]['pm' if date['hour'] >= 12 else 'am']


def flexible_day_period(ld, date, length, shows):
    """CLDR's flexible day period (B) as ICU's SimpleDateFormat writes it: noon where the locale's rules have it and the
    pattern shows the time as exactly noon; otherwise the period whose [from, before) holds the hour (midnight is never
    written); named in the format context, and am/pm in the same width where the period is am or pm or the locale has
    no name for it."""
    rules = day_period_rules(ld)
    width = {4: 'wide', 5: 'narrow'}.get(length, 'abbreviated')
    if 'noon' in rules and shown_as_noon(date, shows) and day_period_name(ld, width, 'noon'):
        return day_period_name(ld, width, 'noon')
    hour = date['hour']
    period = None
    for name, rule in rules.items():
        if '_from' not in rule:
            continue
        start = int(rule['_from'][:2])
        end = int(rule['_before'][:2])
        if start <= hour < end if start < end else (hour >= start or hour < end):
            period = name
            break
    if period in (None, 'am', 'pm') or not day_period_name(ld, width, period):
        return am_pm_day_period(ld, date, length)
    return day_period_name(ld, width, period)


# ---------------------------------------------------------------------------------------------------
# The option bags. Each is formatted with { timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' } added,
# so that a locale's own calendar (th, fa) or digits (bn, fa) do not stand between the matcher and the comparison.
BAGS = [
    ('E_d_MMMM', {'weekday': 'short', 'day': 'numeric', 'month': 'long'}),
    ('EEEE_d_MMMM', {'weekday': 'long', 'day': 'numeric', 'month': 'long'}),
    ('E_y_MMM_d', {'weekday': 'short', 'year': 'numeric', 'month': 'short', 'day': 'numeric'}),
    ('EEEE_y_MMMM_d', {'weekday': 'long', 'year': 'numeric', 'month': 'long', 'day': 'numeric'}),
    ('y_MMMM', {'year': 'numeric', 'month': 'long'}),
    ('y_MMM', {'year': 'numeric', 'month': 'short'}),
    ('y_MM', {'year': 'numeric', 'month': '2-digit'}),
    ('MMMM_d', {'month': 'long', 'day': 'numeric'}),
    ('MMM_d', {'month': 'short', 'day': 'numeric'}),
    ('M_d', {'month': 'numeric', 'day': 'numeric'}),
    ('y_M_d', {'year': 'numeric', 'month': 'numeric', 'day': 'numeric'}),
    ('yy_MM_dd', {'year': '2-digit', 'month': '2-digit', 'day': '2-digit'}),
    ('MMMM', {'month': 'long'}),
    ('EEEE', {'weekday': 'long'}),
    ('d', {'day': 'numeric'}),
    ('G_y', {'era': 'short', 'year': 'numeric'}),
    ('GGGG_y_MMMM_d', {'era': 'long', 'year': 'numeric', 'month': 'long', 'day': 'numeric'}),
    ('j_mm', {'hour': 'numeric', 'minute': '2-digit'}),
    ('H_mm_h23', {'hour': 'numeric', 'minute': '2-digit', 'hourCycle': 'h23'}),
    ('h_mm_h12', {'hour': 'numeric', 'minute': '2-digit', 'hour12': True}),
    ('j_mm_ss', {'hour': 'numeric', 'minute': 'numeric', 'second': 'numeric'}),
    ('E_j_mm', {'weekday': 'short', 'hour': 'numeric', 'minute': '2-digit'}),
    ('y_MMMM_d_j_mm', {'year': 'numeric', 'month': 'long', 'day': 'numeric', 'hour': 'numeric', 'minute': '2-digit'}),
    ('MMM_d_j_mm', {'month': 'short', 'day': 'numeric', 'hour': 'numeric', 'minute': '2-digit'}),
    ('y_M_d_j_mm_ss', {'year': 'numeric', 'month': 'numeric', 'day': 'numeric', 'hour': 'numeric', 'minute': 'numeric', 'second': 'numeric'}),
]

# The locales of the design's 52-locale run (sebastienros/jint#4158).
LOCALES = ['ar', 'bg', 'bn', 'ca', 'cs', 'da', 'de', 'el', 'en', 'en-AU', 'en-GB', 'en-IN', 'es', 'es-MX', 'et', 'fa', 'fi',
           'fil', 'fr', 'fr-CA', 'he', 'hi', 'hr', 'hu', 'id', 'it', 'ja', 'ko', 'lt', 'lv', 'ms', 'nb', 'nl', 'pl', 'pt',
           'pt-PT', 'ro', 'ru', 'sk', 'sl', 'sr', 'sv', 'sw', 'ta', 'th', 'tr', 'uk', 'ur', 'vi', 'zh', 'zh-Hant']

LIKELY_REGION = {'en': 'US', 'en-GB': 'GB', 'de': 'DE', 'fr': 'FR', 'es': 'ES', 'ru': 'RU', 'pl': 'PL', 'ja': 'JP', 'zh': 'CN',
                 'ko': 'KR', 'ar': 'EG'}
CYCLE = {'H': 'h23', 'h': 'h12', 'K': 'h11', 'k': 'h24'}


_likely = None


def likely_region(root, locale):
    global _likely
    parts = locale.split('-')
    for p in parts[1:]:
        if len(p) == 2 and p.isalpha() or (len(p) == 3 and p.isdigit()):
            return p.upper()
    if _likely is None:
        _likely = json.load(open(os.path.join(root, 'core', 'package', 'supplemental', 'likelySubtags.json'), encoding='utf-8'))['supplemental']['likelySubtags']
    full = _likely.get(locale) or _likely.get(parts[0]) or 'und-Latn-001'
    return full.split('-')[-1]


_time_data = None


def load_time_data(root):
    """timeData from supplementalData.xml when it is beside the JSON (cldr-json drops the language_region keys,
    fr_CA among them, which the XML and Jint's own TimeData table carry), the lossy JSON otherwise."""
    global _time_data
    if _time_data is not None:
        return _time_data
    xml = os.path.join(root, 'supplementalData.xml')
    if os.path.exists(xml):
        import xml.etree.ElementTree as ET
        _time_data = {}
        for hours in ET.parse(xml).getroot().iter('hours'):
            for region in hours.get('regions').split():
                _time_data[region] = {'_preferred': hours.get('preferred'), '_allowed': hours.get('allowed')}
    else:
        _time_data = json.load(open(os.path.join(root, 'core', 'package', 'supplemental', 'timeData.json'), encoding='utf-8'))['supplemental']['timeData']
    return _time_data


def hour_cycles(root, locale):
    td = load_time_data(root)
    region = likely_region(root, locale)
    lang = locale.split('-')[0]
    entry = td.get(lang + '_' + region) or td.get(region) or td['001']
    preferred = CYCLE[entry['_preferred'][0]]
    allowed = [CYCLE[a[0]] for a in entry['_allowed'].split()]
    h12 = next((c for c in allowed if c in ('h11', 'h12')), 'h12')
    h24 = next((c for c in allowed if c in ('h23', 'h24')), 'h23')
    return preferred, h12, h24




def resolve(root, locale, bag):
    """The pattern, hour cycle and resolved record the model chooses for one (locale, bag)."""
    ld = LocaleData(root, locale)
    preferred, h12, h24 = hour_cycles(root, locale)
    o = {k: v for k, v in bag.items() if k not in ('hourCycle', 'hour12')}
    hc = bag.get('hourCycle') or (h12 if bag.get('hour12') is True else h24 if bag.get('hour12') is False else preferred)
    pattern = best_pattern(ld, skeleton_from_options(o, hc))
    if 'hour' in o:
        pattern = replace_hour_cycle(pattern, hc)
    record = record_of(pattern)
    keys = ['weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName']
    ro = ','.join('%s=%s' % (k, record[k]) for k in keys if k in record)
    if 'hour' in record:
        ro += ',hourCycle=' + hc
    return ld, pattern, hc, ro


def escape(s):
    return ''.join(c if 0x20 <= ord(c) < 0x7f and c != '\\' else '\\u%04x' % ord(c) for c in s)


def load_icu(path):
    """An ICU reference written by probe-golden.js: (locale, bag) -> [text@DATES[0], text@DATES[1], resolvedOptions]."""
    import re
    ref = {}
    for line in open(path, encoding='utf-8'):
        line = re.sub(r'\\u([0-9a-f]{4})', lambda m: chr(int(m.group(1), 16)), line.rstrip('\r\n'))
        # JavaScript escaped UTF-16 code units: pair the surrogates back up.
        line = line.encode('utf-16-le', 'surrogatepass').decode('utf-16-le')
        cols = line.split('\t')
        if len(cols) >= 4:
            ref[(cols[0], cols[1])] = cols[2:]
    return ref


def golden(root, icu_tsv):
    """Writes the golden table Jint.Tests reads: locale, bag, options, pattern, the text at each of DATES and the resolved
    options, all the model's; and last, what ICU writes where it differs from the model, empty where it agrees."""
    icu = load_icu(icu_tsv)
    print('# Generated by tools/cldr-dates/reference/format_matcher.py golden from cldr-json 48.2.0; do not edit.')
    print('# The last column is what ICU (Node 24.19, ICU 78.3) writes where it differs from the model: text@1|text@2|resolvedOptions.')
    print('# locale\tbag\toptions\tpattern\t' + '\t'.join('text@%04d-%02d-%02dT%02d:%02d:%02dZ' % (d['year'], d['month'], d['day'], d['hour'], d['minute'], d['second']) for d in DATES) + '\tresolvedOptions\ticu')
    for locale in LOCALES:
        for name, bag in BAGS:
            ld, pattern, hc, ro = resolve(root, locale, bag)
            texts = [render(ld, pattern, d) for d in DATES]
            reference = icu[(locale, name)]
            differs = '' if reference == texts + [ro] else '|'.join(reference)
            options = json.dumps(bag, separators=(',', ':'), sort_keys=False)
            print('\t'.join([locale, name, options, escape(pattern)] + [escape(t) for t in texts] + [ro, escape(differs)]))


def compare(root, node_tsv):
    """Compares the golden table's model with an ICU reference written by probe-golden.js."""
    ref = load_icu(node_tsv)
    same = diff = 0
    for locale in LOCALES:
        for name, bag in BAGS:
            ld, pattern, hc, ro = resolve(root, locale, bag)
            texts = [render(ld, pattern, d) for d in DATES]
            expected = ref.get((locale, name))
            ok = expected is not None and expected[:len(texts)] == texts
            ro_ok = expected is not None and len(expected) > len(texts) and expected[len(texts)] == ro
            same += ok
            diff += not ok
            if not ok or not ro_ok:
                print(('!!' if not ok else 'ro') + '\t%s\t%s\t%r\t%r\ticu=%r' % (locale, name, pattern, texts + [ro], expected))
    print('same', same, 'diff', diff)


# ---------------------------------------------------------------------------------------------------
# dateStyle and timeStyle, as V8's DateTimeStylePattern (js-date-time-format.cc) drives ICU: the locale's dateFormats
# and timeFormats (ECMA-402 DateTimeStyleFormat), a date and a time joined by the atTime dateTimeFormats of the date's
# width (ICU 72+'s SimpleDateFormat for a date and a time style), and, where the resolved hour cycle is not the one the
# style pattern writes, the pattern's skeleton (DateTimePatternGenerator::staticGetSkeleton) with its day period dropped
# and its hour letter replaced (V8's ReplaceSkeleton), matched again by best_pattern and given the cycle's hour letter.
HOUR_LETTER = {'h11': 'K', 'h12': 'h', 'h23': 'H', 'h24': 'k'}
STYLES = ('full', 'long', 'medium', 'short')


def style_format(ld, block, style):
    p = ld.g[block][style]
    return p.get('_value') if isinstance(p, dict) else p


def pattern_hour_cycle(pattern):
    """V8's HourCycleFromPattern: the cycle of the pattern's first hour letter outside quotes, or None."""
    in_quote = False
    for c in pattern:
        if c == "'":
            in_quote = not in_quote
        elif not in_quote and c in 'hHkK':
            return CYCLE[c]
    return None


def style_skeleton(pattern, hc):
    """staticGetSkeleton, then ReplaceSkeleton: each field of the pattern once, in field order, without a day period,
    and with the resolved cycle's hour letter."""
    sk = Skeleton(pattern)
    out = ''
    for f in sorted(sk.orig):
        ch, ln = sk.orig[f]
        if ch in 'abB':
            continue
        out += (HOUR_LETTER[hc] if ch in 'hHkK' else ch) * ln
    return out


def style_pattern(ld, date_style, time_style, hc):
    """The pattern a dateStyle and/or timeStyle formatter writes with in the resolved hour cycle hc."""
    if date_style is None:
        pattern = style_format(ld, 'timeFormats', time_style)
    else:
        date = style_format(ld, 'dateFormats', date_style)
        if time_style is None:
            return date
        time = style_format(ld, 'timeFormats', time_style)
        pattern = ld.dt_formats_at[date_style].replace('{1}', '\u0001').replace('{0}', time).replace('\u0001', date)
    if pattern_hour_cycle(pattern) == hc:
        return pattern
    return replace_hour_cycle(best_pattern(ld, style_skeleton(pattern, hc)), hc)


# Temporal's AdjustDateTimeStyleFormat (https://tc39.es/proposal-temporal/#sec-adjustdatetimestyleformat): the style's
# format when it has no field the Temporal type lacks, and otherwise the format matcher run over the fields it has that
# the type allows. The formatter drops the style the type cannot have first (a PlainDate keeps the dateStyle, a
# PlainTime the timeStyle), so "timeStyle is ignored when dateStyle is present" holds in every locale.
TEMPORAL_ALLOWED = {
    'PlainDate': ('weekday', 'era', 'year', 'month', 'day'),
    'PlainYearMonth': ('era', 'year', 'month'),
    'PlainMonthDay': ('month', 'day'),
    'PlainTime': ('dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits'),
    'PlainDateTime': ('weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits'),
}
RECORD_KEYS = ['weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName']


def adjust_style_format(ld, base, hc, allowed):
    """The adjusted pattern, and the component bag it was matched from (None where the style's format stands)."""
    record = record_of(base)
    if all(k in allowed for k in record):
        return base, None
    bag = {k: record[k] for k in RECORD_KEYS if k in record and k in allowed}
    pattern = best_pattern(ld, skeleton_from_options(bag, hc))
    if 'hour' in bag:
        pattern = replace_hour_cycle(pattern, hc)
    return pattern, bag


STYLE_CASES = [('d_' + s, {'dateStyle': s}) for s in STYLES] + [('t_' + s, {'timeStyle': s}) for s in STYLES] + [
    ('dt_%s_%s' % (d, t), {'dateStyle': d, 'timeStyle': t}) for d in STYLES for t in STYLES] + [
    ('t_short_h23', {'timeStyle': 'short', 'hourCycle': 'h23'}),
    ('t_short_h12', {'timeStyle': 'short', 'hourCycle': 'h12'}),
    ('t_short_h11', {'timeStyle': 'short', 'hourCycle': 'h11'}),
    ('t_short_h24', {'timeStyle': 'short', 'hourCycle': 'h24'}),
    ('t_medium_12', {'timeStyle': 'medium', 'hour12': True}),
    ('t_medium_24', {'timeStyle': 'medium', 'hour12': False}),
    ('t_full_h23', {'timeStyle': 'full', 'hourCycle': 'h23'}),
    ('t_long_h12', {'timeStyle': 'long', 'hourCycle': 'h12'}),
    ('dt_full_short_h23', {'dateStyle': 'full', 'timeStyle': 'short', 'hourCycle': 'h23'}),
    ('dt_short_medium_h12', {'dateStyle': 'short', 'timeStyle': 'medium', 'hourCycle': 'h12'}),
    ('dt_long_long_12', {'dateStyle': 'long', 'timeStyle': 'long', 'hour12': True}),
    ('dt_medium_full_24', {'dateStyle': 'medium', 'timeStyle': 'full', 'hour12': False}),
] + [('PYM_' + s, {'temporal': 'PlainYearMonth', 'dateStyle': s}) for s in STYLES] + [
    ('PMD_' + s, {'temporal': 'PlainMonthDay', 'dateStyle': s}) for s in STYLES] + [
    ('PD_full', {'temporal': 'PlainDate', 'dateStyle': 'full'}),
    ('PD_short', {'temporal': 'PlainDate', 'dateStyle': 'short'}),
    ('PT_full', {'temporal': 'PlainTime', 'timeStyle': 'full'}),
    ('PT_long', {'temporal': 'PlainTime', 'timeStyle': 'long'}),
    ('PT_medium', {'temporal': 'PlainTime', 'timeStyle': 'medium'}),
    ('PDT_full_full', {'temporal': 'PlainDateTime', 'dateStyle': 'full', 'timeStyle': 'full'}),
    ('PDT_long_long', {'temporal': 'PlainDateTime', 'dateStyle': 'long', 'timeStyle': 'long'}),
    ('PDT_short_full', {'temporal': 'PlainDateTime', 'dateStyle': 'short', 'timeStyle': 'full'}),
    ('PDT_medium_medium', {'temporal': 'PlainDateTime', 'dateStyle': 'medium', 'timeStyle': 'medium'}),
]


def resolve_style(root, locale, options):
    """The pattern, the component bag an ICU engine can be asked for the same text with, and the resolvedOptions string,
    for one style case. A Temporal case's bag is the one AdjustDateTimeStyleFormat matched, and its resolvedOptions are
    that bag's format record, which is what the bag reports when an ICU engine is asked for it."""
    ld = LocaleData(root, locale)
    preferred, h12, h24 = hour_cycles(root, locale)
    hc = options.get('hourCycle') or (h12 if options.get('hour12') is True else h24 if options.get('hour12') is False else preferred)
    date_style = options.get('dateStyle')
    time_style = options.get('timeStyle')
    pattern = style_pattern(ld, date_style, time_style, hc)
    probe = {k: v for k, v in options.items() if k != 'temporal'}
    ro = ','.join('%s=%s' % (k, options[k]) for k in ('dateStyle', 'timeStyle') if k in options)
    if time_style is not None:
        ro += ',hourCycle=' + hc
    temporal = options.get('temporal')
    if temporal is not None:
        allowed = TEMPORAL_ALLOWED[temporal]
        pattern, bag = adjust_style_format(ld, pattern, hc, allowed)
        if bag is not None:
            record = record_of(pattern)
            ro = ','.join('%s=%s' % (k, record[k]) for k in RECORD_KEYS if k in record)
            probe = dict(bag)
            if 'hour' in record:
                ro += ',hourCycle=' + hc
                probe['hourCycle'] = hc
    return ld, pattern, probe, ro


def read_locales(path):
    """The locales a run covers: the golden table's, or one per line of a file (the design's wider set)."""
    if path is None:
        return LOCALES
    return [line.strip() for line in open(path, encoding='utf-8') if line.strip()]


def style_cases(root, locales_path=None):
    """What probe-styles.js asks ICU for: locale, case and the Intl.DateTimeFormat options, one line each."""
    for locale in read_locales(locales_path):
        for name, options in STYLE_CASES:
            ld, pattern, probe, ro = resolve_style(root, locale, options)
            print('\t'.join([locale, name, json.dumps(probe, separators=(',', ':'))]))


def golden_styles(root, icu_tsv):
    """Writes the style golden table: locale, case, options, pattern, the text at each of DATES, the UTC zone name the
    text writes (empty for none), resolvedOptions, and last what ICU writes where it differs from the model."""
    icu = load_icu(icu_tsv)
    print('# Generated by tools/cldr-dates/reference/format_matcher.py golden-styles from cldr-json 48.2.0; do not edit.')
    print('# The last column is what ICU (Node 24.19, ICU 78.3) writes where it differs from the model: text@1|text@2|resolvedOptions.')
    print('# A Temporal case (options.temporal) is compared with ICU through the component bag AdjustDateTimeStyleFormat matched.')
    print('# locale\tcase\toptions\tpattern\t' + '\t'.join('text@%04d-%02d-%02dT%02d:%02d:%02dZ' % (d['year'], d['month'], d['day'], d['hour'], d['minute'], d['second']) for d in DATES) + '\tzone\tresolvedOptions\ticu')
    for locale in LOCALES:
        for name, options in STYLE_CASES:
            ld, pattern, probe, ro = resolve_style(root, locale, options)
            texts = [render(ld, pattern, d) for d in DATES]
            zones = [tok for tok in tokenize(pattern) if tok[0] == 'field' and tok[1] == 'z']
            zone = utc_zone_name(ld, zones[0][2]) if zones else ''
            reference = icu[(locale, name)]
            differs = '' if reference == texts + [ro] else '|'.join(reference)
            print('\t'.join([locale, name, json.dumps(options, separators=(',', ':')), escape(pattern)] + [escape(t) for t in texts] + [escape(zone), ro, escape(differs)]))


def compare_styles(root, node_tsv, locales_path=None):
    """Compares the style golden table's model with an ICU reference written by probe-styles.js."""
    ref = load_icu(node_tsv)
    same = diff = 0
    for locale in read_locales(locales_path):
        for name, options in STYLE_CASES:
            ld, pattern, probe, ro = resolve_style(root, locale, options)
            texts = [render(ld, pattern, d) for d in DATES]
            expected = ref.get((locale, name))
            ok = expected is not None and expected[:len(texts)] == texts
            ro_ok = expected is not None and len(expected) > len(texts) and expected[len(texts)] == ro
            same += ok
            diff += not ok
            if not ok or not ro_ok:
                print(('!!' if not ok else 'ro') + '\t%s\t%s\t%r\t%r\ticu=%r' % (locale, name, pattern, texts + [ro], expected))
    print('same', same, 'diff', diff)


if __name__ == '__main__':
    if len(sys.argv) >= 4 and sys.argv[1] == 'golden':
        golden(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 4 and sys.argv[1] == 'compare':
        compare(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 3 and sys.argv[1] == 'style-cases':
        style_cases(sys.argv[2], sys.argv[3] if len(sys.argv) >= 4 else None)
    elif len(sys.argv) >= 4 and sys.argv[1] == 'golden-styles':
        golden_styles(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 4 and sys.argv[1] == 'compare-styles':
        compare_styles(sys.argv[2], sys.argv[3], sys.argv[4] if len(sys.argv) >= 5 else None)
    else:
        print(__doc__)
        sys.exit(2)
