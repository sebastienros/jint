"""The reference model Jint's Intl.DateTimeFormat formatRange was ported from (sebastienros/jint#4158).

NOT shipped and NOT run by the build. A Python model of ICU's DateIntervalFormat (icu4c dtitvfmt.cpp, dtitvinf.cpp) as
V8 drives it (js-date-time-format.cc: the interval format is created for the skeleton of the pattern the format
matcher chose, with the resolved hour cycle as the locale's -u-hc- keyword), over CLDR 48.2's resolved JSON. It sits on
format_matcher.py, the model of ICU's DateTimePatternGenerator, and Jint/Native/Intl/DateTimeIntervalFormat.cs is its
C# port. The golden table Jint.Tests checks that port against is this model's output:

  node probe-range-golden.js > icu-range-golden.tsv
  python interval_format.py compare <cldr-json-root> icu-range-golden.tsv
  python interval_format.py golden <cldr-json-root> icu-range-golden.tsv > range-golden.tsv

What it models, in ICU's terms:
  * DateIntervalInfo: the locale's intervalFormats, a skeleton to a pattern per greatest-difference field, each
    (skeleton, field) taken from the nearest locale of the CLDR parent chain that has one (a letter the locale shares
    with its parent counts as its parent's), and within one locale the first letter in binary order ('B' before 'a');
    getBestSkeleton's distance (a missing or extra field 0x1000, numeric against text month 0x100, else the width
    difference), with z/K/k read as v/h/H and a/b dropped;
  * DateIntervalFormat::initializePattern: normalizeHourMetacharacters, getDateTimeSkeleton and its normalized
    skeletons, setSeparateDateTimePtn (the fallback date and time patterns, the extension of a date skeleton by the
    field that differs, adjustFieldWidth), the yMd prefix for a time-only skeleton, the fallbacks and the medium
    dateTimeFormat join of a date and a time range;
  * DateIntervalFormat::formatImpl: the largest calendar field that differs, a field the pattern does not show
    collapsing to a single date, the {0} - {1} fallback (with the date and a time range on the same day), and
    splitPatternInto2Part;
  * FormattedDateInterval's spans (addOverlapSpans: the extent of the fields that occur twice) and V8's parts, whose
    source is startRange or endRange inside a span and shared outside, a literal being the text between two fields.
Where Jint follows the specification instead (https://tc39.es/ecma402/#sec-partitiondatetimerangepattern), so does
this model: fractional seconds are compared at the digits the format writes, and a range that collapses is format()'s
output. Jint writes U+0020 for CLDR's U+2009 and U+202F in a range.
"""
import json
import os
import sys

import format_matcher as fm  # which writes stdout as UTF-8

# DateIntervalInfo's IntervalPatternIndex.
ERA, YEAR, MONTH, DATE, AM_PM, HOUR, MINUTE, SECOND, MILLISECOND = range(9)
INDEX_COUNT = 9
# validateAndProcessPatternLetter: the greatest-difference letters CLDR keys a pattern by.
LETTER_INDEX = {'G': ERA, 'y': YEAR, 'M': MONTH, 'd': DATE, 'a': AM_PM, 'B': AM_PM, 'h': HOUR, 'H': HOUR, 'm': MINUTE}
# fgCalendarFieldToPatternLetter for the fields a date skeleton is extended by.
FIELD_LETTER = {ERA: 'G', YEAR: 'y', MONTH: 'M', DATE: 'd'}
# SimpleDateFormat::fgCalendarFieldToLevel, by interval index.
FIELD_LEVEL = [0, 10, 20, 30, 40, 50, 60, 70, 80]
# SimpleDateFormat::getLevelFromChar.
CHAR_LEVEL = {'A': 40, 'D': 20, 'E': 30, 'F': 30, 'G': 0, 'H': 50, 'K': 50, 'L': 20, 'M': 20, 'O': 0, 'Q': 20, 'S': 80,
              'U': 10, 'V': 0, 'W': 30, 'X': 0, 'Y': 10, 'Z': 0, 'a': 40, 'c': 30, 'd': 30, 'e': 30, 'g': 0, 'h': 50,
              'k': 50, 'l': 0, 'm': 60, 'q': 20, 'r': 10, 's': 70, 'u': 10, 'v': 0, 'w': 20, 'x': 0, 'y': 10, 'z': 0}


# ---------------------------------------------------------------------------------------------------
# The data: intervalFormats resolved the way DateIntervalInfo's sink fills its table.
_raw_intervals = {}
_resolved_intervals = {}


def raw_intervals(root, locale):
    """cldr-json's resolved intervalFormats: (fallback, {skeleton: {letter: pattern}}), '-alt-' letters left out."""
    if locale not in _raw_intervals:
        g = json.load(open(os.path.join(root, 'package', 'main', locale, 'ca-gregorian.json'), encoding='utf-8'))['main'][locale]['dates']['calendars']['gregorian']
        iv = g['dateTimeFormats']['intervalFormats']
        table = {k: {letter: p for letter, p in v.items() if letter in LETTER_INDEX} for k, v in iv.items() if k != 'intervalFormatFallback'}
        _raw_intervals[locale] = (iv['intervalFormatFallback'], table)
    return _raw_intervals[locale]


def resolved_intervals(root, locale):
    """(fallback, {skeleton: [pattern or None per index ERA..MINUTE]}): a (skeleton, index) is the locale's own when it
    holds a letter for it that differs from its parent's, the first such letter in binary order; else its parent's."""
    if locale in _resolved_intervals:
        return _resolved_intervals[locale]
    fallback, table = raw_intervals(root, locale)
    parent = fm.parent_of(root, locale)
    inherited = resolved_intervals(root, parent)[1] if parent is not None else {}
    parent_raw = raw_intervals(root, parent)[1] if parent is not None else {}
    result = {k: list(v) for k, v in inherited.items()}
    for skeleton in table:
        own = [letter for letter, p in table[skeleton].items() if parent_raw.get(skeleton, {}).get(letter) != p]
        slots = result.setdefault(skeleton, [None] * 7)
        seen = set()
        for letter in sorted(own):
            index = LETTER_INDEX[letter]
            if index in seen:
                continue
            seen.add(index)
            slots[index] = table[skeleton][letter]
    _resolved_intervals[locale] = (fallback, result)
    return _resolved_intervals[locale]


def parse_skeleton(skeleton):
    widths = {}
    for ch in skeleton:
        widths[ch] = widths.get(ch, 0) + 1
    return widths


def get_best_skeleton(table, skeleton):
    """DateIntervalInfo::getBestSkeleton: (best skeleton, differenceInfo) over the table's skeletons, the first of the
    least distance winning. ICU walks its hash table; the table is walked in ordinal order here, and ties() says
    whether that can matter."""
    replaced = any(c in skeleton for c in 'zkKab')
    if replaced:
        skeleton = skeleton.replace('z', 'v').replace('k', 'H').replace('K', 'h').replace('a', '').replace('b', '')
    request = parse_skeleton(skeleton)
    best = None
    best_distance = None
    info = 0
    for candidate in sorted(table):
        widths = parse_skeleton(candidate)
        distance = 0
        difference = 1
        for ch in set(request) | set(widths):
            a = request.get(ch, 0)
            b = widths.get(ch, 0)
            if a == b:
                continue
            if a == 0 or b == 0:
                difference = -1
                distance += 0x1000
            elif ch == 'M' and ((a <= 2 < b) or (a > 2 >= b)):
                distance += 0x100
            else:
                distance += abs(a - b)
        if best_distance is None or distance < best_distance:
            best, best_distance, info = candidate, distance, difference
        if distance == 0:
            info = 0
            break
    if replaced and info != -1:
        info = 2
    return best, info, best_distance


def ties(table, skeleton, best_of=get_best_skeleton):
    """The skeletons that tie with the best one; ICU's hash order would decide between them."""
    best, info, distance = best_of(table, skeleton)
    if best is None or info == -1 or distance == 0:
        return []
    return [c for c in table if c != best and best_of({c: None}, skeleton)[2] == distance]


# ---------------------------------------------------------------------------------------------------
# Pattern utilities (ICU's own loops, quotes respected).
def pattern_fields(pattern):
    """(letter, length) of each field outside quotes."""
    return [(t[1], t[2]) for t in fm.tokenize(pattern) if t[0] == 'field']


def is_field_unit_ignored(pattern, index):
    """SimpleDateFormat::isFieldUnitIgnored: every field the pattern (or skeleton) writes is larger than the field."""
    level = FIELD_LEVEL[index]
    for letter, _ in pattern_fields(pattern):
        if level <= CHAR_LEVEL.get(letter, -1):
            return False
    return True


def split_pattern(pattern):
    """DateIntervalFormat::splitPatternInto2Part: the index of the first field whose letter repeats."""
    seen = set()
    i = 0
    n = len(pattern)
    in_quote = False
    prev = None
    count = 0
    found = False
    while i < n:
        ch = pattern[i]
        if ch != prev and count > 0:
            if prev in seen:
                found = True
                break
            seen.add(prev)
            count = 0
        if ch == "'":
            if i + 1 < n and pattern[i + 1] == "'":
                i += 1
            else:
                in_quote = not in_quote
        elif not in_quote and (('a' <= ch <= 'z') or ('A' <= ch <= 'Z')):
            prev = ch
            count += 1
        i += 1
    if count > 0 and not found and prev not in seen:
        count = 0
    return i - count


def find_replace(pattern, old, new):
    """DateIntervalFormat::findReplaceInPattern: outside quoted text only."""
    out = []
    source = pattern
    first = source.find("'")
    if first < 0:
        return source.replace(old, new)
    while first >= 0:
        second = source.find("'", first + 1)
        if second < 0:
            second = len(source) - 1
        out.append(source[:first].replace(old, new))
        out.append(source[first:second + 1])
        source = source[second + 1:]
        first = source.find("'")
    out.append(source.replace(old, new))
    return ''.join(out)


def adjust_field_width(input_skeleton, best_skeleton, pattern, difference):
    """DateIntervalFormat::adjustFieldWidth: a field the best skeleton has at the pattern's width is widened to the
    input's, and with differenceInfo 2 the letters the best skeleton stood in for are put back."""
    input_widths = parse_skeleton(input_skeleton)
    best_widths = parse_skeleton(best_skeleton)
    adjusted = pattern
    if difference == 2:
        if 'z' in input_skeleton:
            adjusted = find_replace(adjusted, 'v', 'z')
        if 'K' in input_skeleton:
            adjusted = find_replace(adjusted, 'h', 'K')
        if 'k' in input_skeleton:
            adjusted = find_replace(adjusted, 'H', 'k')
        if 'b' in input_skeleton:
            adjusted = find_replace(adjusted, 'a', 'b')
    if 'a' in adjusted and best_widths.get('a', 0) == 0:
        best_widths['a'] = 1
    if 'b' in adjusted and best_widths.get('b', 0) == 0:
        best_widths['b'] = 1
    out = []
    in_quote = False
    prev = None
    count = 0

    def flush():
        key = 'M' if prev == 'L' else prev
        field_count = best_widths.get(key, 0)
        input_count = input_widths.get(key, 0)
        if field_count == count and input_count > field_count:
            out.append(prev * (input_count - field_count))

    i = 0
    while i < len(adjusted):
        ch = adjusted[i]
        if ch != prev and count > 0:
            flush()
            count = 0
        if ch == "'":
            if i + 1 < len(adjusted) and adjusted[i + 1] == "'":
                out.append("''")
                i += 2
                continue
            in_quote = not in_quote
        elif not in_quote and (('a' <= ch <= 'z') or ('A' <= ch <= 'Z')):
            prev = ch
            count += 1
        out.append(ch)
        i += 1
    if count > 0:
        flush()
    return ''.join(out)


def static_get_skeleton(pattern):
    """DateTimePatternGenerator::staticGetSkeleton: the pattern's fields in canonical order at their own widths."""
    original = {}
    for letter, length in pattern_fields(pattern):
        row = fm.canonical_row(letter, length)
        if row is None:
            continue
        original[row[1]] = (letter, length)
    if fm.MINUTE in original and fm.FRACSEC in original and fm.SECOND not in original:
        original[fm.SECOND] = ('s', 1)
    if fm.HOUR in original:
        if original[fm.HOUR][0] not in 'hK':
            original.pop(fm.DAYPERIOD, None)
    return ''.join(letter * length for f, (letter, length) in sorted(original.items()))


class IntervalFormat:
    """One DateIntervalFormat: the locale, the skeleton V8 hands it and the hour cycle."""

    def __init__(self, root, ld, skeleton, hc, decimal='.'):
        self.root = root
        self.ld = ld
        self.hc = hc
        self.decimal = decimal
        self.fallback, self.table = resolved_intervals(root, ld.locale)
        self.later_first = self.fallback.index('{1}') < self.fallback.index('{0}')
        self.skeleton = skeleton
        self.format_pattern = self.best_pattern(skeleton)
        self.first = [None] * INDEX_COUNT
        self.second = [None] * INDEX_COUNT
        self.date_pattern = None
        self.time_pattern = None
        self.date_time_format = None
        self.initialize()

    def best_pattern(self, skeleton):
        """DateFormat::getBestPattern for a locale with the -u-hc- keyword: no match options, the hour cycle's letter."""
        pattern = fm.best_pattern(self.ld, skeleton, self.decimal, match_hour=False)
        if any(c in 'hHkK' for c in skeleton):
            pattern = fm.replace_hour_cycle(pattern, self.hc)
        return pattern

    def set_pattern(self, index, pattern):
        split = split_pattern(pattern)
        self.first[index] = pattern[:split]
        self.second[index] = pattern[split:]

    def set_fallback(self, index, pattern):
        self.first[index] = ''
        self.second[index] = pattern

    def normalize_hour_metacharacters(self, skeleton):
        hour_char = None
        day_period = None
        hour_start = hour_length = period_start = period_length = 0
        for i, c in enumerate(skeleton):
            if c in 'jJChHkK':
                if hour_char is None:
                    hour_char, hour_start = c, i
                hour_length += 1
            elif c in 'abB':
                if day_period is None:
                    day_period, period_start = c, i
                period_length += 1
            elif hour_char is not None and day_period is not None:
                break
        if hour_char is None:
            return skeleton
        converted = self.best_pattern(hour_char)
        while "'" in converted:
            a = converted.index("'")
            b = converted.find("'", a + 1)
            if b < 0:
                b = a
            converted = converted[:a] + converted[b + 1:]
        letter = 'H'
        if 'h' in converted:
            letter = 'h'
        elif 'K' in converted:
            letter = 'K'
        elif 'k' in converted:
            letter = 'k'
        if 'b' in converted:
            day_period = 'b'
        elif 'B' in converted:
            day_period = 'B'
        elif day_period is None:
            day_period = 'a'
        replacement = letter
        if letter not in 'Hk':
            width = 5 if period_length >= 5 or hour_length >= 5 else 3 if period_length >= 3 or hour_length >= 3 else 1
            replacement += day_period * width
        result = skeleton[:hour_start] + replacement + skeleton[hour_start + hour_length:]
        if period_start > hour_start:
            period_start += len(replacement) - hour_length
        return result[:period_start] + result[period_start + period_length:]

    @staticmethod
    def date_time_skeleton(skeleton):
        date = []
        norm_date = []
        time = []
        norm_time = []
        counts = {'E': 0, 'd': 0, 'M': 0, 'y': 0, 'm': 0, 'v': 0, 'z': 0}
        hour_char = None
        for ch in skeleton:
            if ch in 'EdMy':
                date.append(ch)
                counts[ch] += 1
            elif ch in 'GYuQqLlWwDFgecUr':
                norm_date.append(ch)
                date.append(ch)
            elif ch in 'hHkK':
                time.append(ch)
                if hour_char is None:
                    hour_char = ch
            elif ch in 'mzv':
                time.append(ch)
                counts[ch] += 1
            elif ch in 'aVZjsSAbB':
                time.append(ch)
                norm_time.append(ch)
        norm_date.append('y' * counts['y'])
        if counts['M']:
            norm_date.append('M' if counts['M'] < 3 else 'M' * min(counts['M'], 5))
        if counts['E']:
            norm_date.append('E' if counts['E'] <= 3 else 'E' * min(counts['E'], 5))
        if counts['d']:
            norm_date.append('d')
        if hour_char:
            norm_time.append(hour_char)
        if counts['m']:
            norm_time.append('m')
        if counts['z']:
            norm_time.append('z')
        if counts['v']:
            norm_time.append('v')
        return ''.join(date), ''.join(norm_date), ''.join(time), ''.join(norm_time)

    def initialize(self):
        converted = self.normalize_hour_metacharacters(self.skeleton)
        date, norm_date, time, norm_time = self.date_time_skeleton(converted)
        if time and date:
            self.date_time_format = self.ld.dt_formats['medium']
        found = self.set_separate_date_time(norm_date, norm_time)
        if not found:
            if time and not date:
                self.set_time_only_fallbacks(time)
            return
        if not time:
            return
        if not date:
            self.set_time_only_fallbacks(time)
            return
        skeleton = self.skeleton
        for index in (DATE, MONTH, YEAR, ERA):
            if FIELD_LETTER[index] not in date:
                skeleton = FIELD_LETTER[index] + skeleton
                self.set_fallback(index, self.best_pattern(skeleton))
        date_pattern = self.best_pattern(date)
        for index in (AM_PM, HOUR, MINUTE):
            if self.first[index]:
                time_interval = self.first[index] + self.second[index]
                self.set_pattern(index, self.date_time_format.replace('{1}', '\u0001').replace('{0}', time_interval).replace('\u0001', date_pattern))

    def set_time_only_fallbacks(self, time):
        pattern = self.best_pattern('yMd' + time)
        for index in (DATE, MONTH, YEAR):
            self.set_fallback(index, pattern)
        self.set_fallback(ERA, self.best_pattern('GyMd' + time))

    def set_separate_date_time(self, date, time):
        skeleton = time if time else date
        best, difference, _ = get_best_skeleton(self.table, skeleton)
        if best is None:
            return False
        if date:
            self.date_pattern = self.best_pattern(date)
        if time:
            self.time_pattern = self.best_pattern(time)
        if difference == -1:
            return False
        if not time:
            # ICU hands the date calls one extendedSkeleton and extendedBestSkeleton, which persist between them, and
            # once the month's pattern came from an extension the year's and era's calls read and extend those same
            # strings in place.
            state = {'skeleton': skeleton, 'best': best, 'ext': '', 'ext_best': '', 'aliased': False}
            self.set_date_interval(DATE, state, difference)
            if self.set_date_interval(MONTH, state, difference):
                state['aliased'] = True
                state['skeleton'] = state['ext']
                state['best'] = state['ext_best']
            self.set_date_interval(YEAR, state, difference)
            self.set_date_interval(ERA, state, difference)
        else:
            self.set_time_interval(MINUTE, skeleton, best, difference)
            self.set_time_interval(HOUR, skeleton, best, difference)
            self.set_time_interval(AM_PM, skeleton, best, difference)
        return True

    def set_time_interval(self, index, skeleton, best, difference):
        """DateIntervalFormat::setIntervalPattern for a time field: an am/pm difference with no pattern of its own takes
        the hour's."""
        pattern = self.table[best][index]
        if not pattern:
            if is_field_unit_ignored(best, index):
                return
            if index == AM_PM:
                pattern = self.table[best][HOUR]
                if pattern:
                    self.set_pattern(index, adjust_field_width(skeleton, best, pattern, difference))
            return
        if difference != 0:
            pattern = adjust_field_width(skeleton, best, pattern, difference)
        self.set_pattern(index, pattern)

    def set_date_interval(self, index, state, difference):
        """DateIntervalFormat::setIntervalPattern for a date field: a field the best skeleton has no pattern for is looked
        up on the skeleton extended by its letter ("MMMd" to "yMMMd"); answers whether an extension is on record."""
        skeleton, best = state['skeleton'], state['best']
        pattern = self.table[best][index]
        if not pattern:
            if is_field_unit_ignored(best, index):
                return False
            letter = FIELD_LETTER[index]
            state['ext'] = letter + skeleton
            state['ext_best'] = letter + best
            if state['aliased']:
                state['skeleton'] = state['ext']
                state['best'] = state['ext_best']
                skeleton, best = state['skeleton'], state['best']
            pattern = self.table.get(state['ext_best'], [None] * 7)[index]
            if not pattern and difference == 0:
                better, difference, _ = get_best_skeleton(self.table, state['ext_best'])
                if better is not None and difference != -1:
                    pattern = self.table[better][index]
                    best = better
        if not pattern:
            return False
        if difference != 0:
            pattern = adjust_field_width(skeleton, best, pattern, difference)
        self.set_pattern(index, pattern)
        return state['ext'] != ''


# ---------------------------------------------------------------------------------------------------
# Formatting. A date is a dict: year (proleptic, may be <= 0), month, day, weekday ('sun'..'sat'), hour, minute,
# second, ms.
WEEKDAYS = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat']
PART_TYPES = {'G': 'era', 'y': 'year', 'Y': 'year', 'M': 'month', 'L': 'month', 'd': 'day', 'E': 'weekday', 'c': 'weekday',
              'a': 'dayPeriod', 'b': 'dayPeriod', 'B': 'dayPeriod', 'h': 'hour', 'H': 'hour', 'k': 'hour', 'K': 'hour',
              'm': 'minute', 's': 'second', 'S': 'fractionalSecond', 'z': 'timeZoneName', 'v': 'timeZoneName', 'O': 'timeZoneName'}
EN_DAY_PERIODS = [(0, 6, 'at night'), (6, 12, 'in the morning'), (12, 13, 'noon'), (13, 18, 'in the afternoon'),
                  (18, 21, 'in the evening'), (21, 24, 'at night')]


def render_field(ld, letter, length, date):
    g = ld.g
    if letter == 'G':
        width = 'eraNarrow' if length == 5 else 'eraNames' if length == 4 else 'eraAbbr'
        return g['eras'][width]['0' if date['year'] <= 0 else '1']
    if letter in 'yY':
        year = date['year'] if date['year'] > 0 else 1 - date['year']
        return '%02d' % (year % 100) if length == 2 else str(year).zfill(length)
    if letter in 'ML':
        if length <= 2:
            return ('%02d' if length == 2 else '%d') % date['month']
        context = 'format' if letter == 'M' else 'stand-alone'
        width = {3: 'abbreviated', 4: 'wide'}.get(length, 'narrow')
        return g['months'][context][width][str(date['month'])]
    if letter in 'Ec':
        context = 'format' if letter == 'E' else 'stand-alone'
        width = {4: 'wide', 5: 'narrow', 6: 'short'}.get(length, 'abbreviated')
        return g['days'][context][width][date['weekday']]
    if letter == 'd':
        return ('%02d' if length == 2 else '%d') % date['day']
    if letter in 'ab':
        width = {4: 'wide', 5: 'narrow'}.get(length, 'abbreviated')
        return g['dayPeriods']['format'][width]['pm' if date['hour'] >= 12 else 'am']
    if letter == 'B':
        # Jint has CLDR's flexible day periods for English only; elsewhere it writes am/pm (PR-6 of #4158).
        if ld.locale.split('-')[0] == 'en':
            return next(name for start, end, name in EN_DAY_PERIODS if start <= date['hour'] < end)
        return g['dayPeriods']['format']['abbreviated']['pm' if date['hour'] >= 12 else 'am']
    if letter in 'hHkK':
        h = date['hour']
        value = {'h': (h % 12) or 12, 'K': h % 12, 'H': h, 'k': h or 24}[letter]
        return ('%02d' if length == 2 else '%d') % value
    if letter == 'm':
        return ('%02d' if length == 2 else '%d') % date['minute']
    if letter == 's':
        return ('%02d' if length == 2 else '%d') % date['second']
    if letter == 'S':
        return ('%03d' % date['ms'])[:min(length, 3)].ljust(length, '0')
    if letter in 'zvO':
        return 'UTC'
    return '<' + letter * length + '>'


def runs_of(pattern, date_index):
    """A pattern as runs: ('lit', text, date) or ('field', letter, length, date), quotes resolved."""
    runs = []
    for tok in fm.tokenize(pattern):
        if tok[0] == 'field':
            runs.append(('field', tok[1], tok[2], date_index))
        else:
            text = tok[1]
            if text.startswith("'"):
                text = text[1:-1] if len(text) >= 2 and text.endswith("'") else text[1:]
                text = text.replace("''", "'") if text else "'"
            runs.append(('lit', text, date_index))
    return runs


def fallback_runs(fallback, pattern):
    """A {0} - {1} fallback: the pattern for the start at {0} and for the end at {1}, the fallback's own text around
    them."""
    runs = []
    i = 0
    while i < len(fallback):
        if fallback.startswith('{0}', i):
            runs.extend(runs_of(pattern, 0))
            i += 3
        elif fallback.startswith('{1}', i):
            runs.extend(runs_of(pattern, 1))
            i += 3
        else:
            runs.append(('lit', fallback[i], None))
            i += 1
    return runs


class Range:
    def __init__(self, ld, itv, v8_pattern, x, y):
        self.ld = ld
        self.itv = itv
        self.v8_pattern = v8_pattern
        self.x = x
        self.y = y

    def largest_difference(self):
        x, y = self.x, self.y
        if (x['year'] <= 0) != (y['year'] <= 0):
            return ERA
        for index, key in ((YEAR, 'year'), (MONTH, 'month'), (DATE, 'day')):
            if x[key] != y[key]:
                return index
        if (x['hour'] >= 12) != (y['hour'] >= 12):
            return AM_PM
        if x['hour'] % 12 != y['hour'] % 12:
            return HOUR
        if x['minute'] != y['minute']:
            return MINUTE
        if x['second'] != y['second']:
            return SECOND
        # https://tc39.es/ecma402/#sec-partitiondatetimerangepattern compares fractional seconds at the digits the
        # format writes (ICU compares milliseconds).
        digits = sum(length for letter, length in pattern_fields(self.v8_pattern) if letter == 'S') or 3
        if x['ms'] // 10 ** (3 - min(digits, 3)) != y['ms'] // 10 ** (3 - min(digits, 3)):
            return MILLISECOND
        return None

    def runs(self):
        """The output as runs and the index of the date each is written with, or None for format()'s single date."""
        itv = self.itv
        index = self.largest_difference()
        self.index = index
        if index is None:
            return None
        same_day = index >= AM_PM
        first, second = (1, 0) if itv.later_first else (0, 1)
        if not itv.first[index] and not itv.second[index]:
            if is_field_unit_ignored(itv.format_pattern, index):
                return None
            return self.fallback(itv.format_pattern, same_day)
        if not itv.first[index]:
            return self.fallback(itv.second[index], same_day)
        return runs_of(itv.first[index], first) + runs_of(itv.second[index], second)

    def fallback(self, pattern, same_day):
        itv = self.itv
        if same_day and itv.date_pattern and itv.time_pattern:
            runs = []
            fmt = itv.date_time_format
            i = 0
            while i < len(fmt):
                if fmt.startswith('{0}', i):
                    runs.extend(fallback_runs(itv.fallback, itv.time_pattern))
                    i += 3
                elif fmt.startswith('{1}', i):
                    runs.extend(runs_of(itv.date_pattern, 0))
                    i += 3
                else:
                    runs.append(('lit', fmt[i], None))
                    i += 1
            return runs
        return fallback_runs(itv.fallback, pattern)

    def parts(self):
        """(type, value, source) from ICU's spans, each part written from the date its source names; None when the range
        is a single date.

        ICU's spans are the extent of the first and of the second occurrence of each field that occurs twice, and V8
        reports every part outside them as shared, although ICU writes the pattern's second part from the end date.
        https://tc39.es/ecma402/#sec-partitiondatetimerangepattern writes a shared part from the start date, so Jint keeps
        V8's sources where the two agree and names the date where they do not, which CLDR's data makes happen twice:
          * fa's "d LLL - d MMM y" writes the month stand-alone before the dash and in the format context after it, two
            UDateFormatFields to ICU; Jint pairs L with M (and c with E), as one calendar field;
          * sw's "d - d MMM y" (a month difference) writes the month once, after the second day, from the end date; a
            field ICU writes once from the end date, no larger than the field that differs, is the end's.
        """
        runs = self.runs()
        if runs is None:
            return None
        fields = [i for i, r in enumerate(runs) if r[0] == 'field']
        spans = [None, None]
        s1 = [None, None]
        s2 = [None, None]
        for n, i in enumerate(fields):
            for j in fields[n + 1:]:
                if same_field(runs[j][1], runs[i][1]):
                    s1 = [i if s1[0] is None else min(s1[0], i), i if s1[1] is None else max(s1[1], i)]
                    s2 = [j if s2[0] is None else min(s2[0], j), j if s2[1] is None else max(s2[1], j)]
                    break
        if s1[0] is None:
            return None
        # The span of the first occurrences belongs to whichever date is written first.
        first_index = runs[s1[0]][3]
        spans[first_index] = s1
        spans[1 - first_index] = s2
        out = []
        for i, run in enumerate(runs):
            if spans[0][0] <= i <= spans[0][1]:
                source = 'startRange'
            elif spans[1][0] <= i <= spans[1][1]:
                source = 'endRange'
            elif run[0] == 'field' and run[3] == 1 and CHAR_LEVEL.get(run[1], -1) >= FIELD_LEVEL[self.index]:
                source = 'endRange'
            else:
                source = 'shared'
            if run[0] == 'lit':
                part, value = 'literal', run[1]
            else:
                date = self.y if source == 'endRange' else self.x
                part, value = PART_TYPES.get(run[1], 'unknown'), render_field(self.ld, run[1], run[2], date)
            value = value.replace('\u2009', ' ').replace('\u202f', ' ')
            if part == 'literal' and out and out[-1][0] == 'literal':
                out[-1] = ('literal', out[-1][1] + value, out[-1][2])
            else:
                out.append((part, value, source))
        return out


def same_field(a, b):
    """Whether two pattern letters write one calendar field: the format and stand-alone month and weekday are one."""
    fold = {'L': 'M', 'c': 'E'}
    return fold.get(a, a) == fold.get(b, b)


# ---------------------------------------------------------------------------------------------------
# The golden table.
def instant(year, month, day, hour, minute, second, ms=0):
    import calendar
    import datetime
    d = datetime.date(year, month, day)
    epoch = calendar.timegm((year, month, day, hour, minute, second)) * 1000 + ms
    return dict(year=year, month=month, day=day, weekday=WEEKDAYS[(d.weekday() + 1) % 7], hour=hour, minute=minute, second=second, ms=ms,
                epoch=epoch)


# Each pair differs first in the field its name says, in UTC.
PAIRS = [
    ('minute', instant(2022, 12, 24, 15, 7, 9), instant(2022, 12, 24, 15, 42, 31)),
    ('hour', instant(2022, 12, 24, 13, 4, 5), instant(2022, 12, 24, 17, 42, 31)),
    ('ampm', instant(2022, 12, 24, 9, 4, 5), instant(2022, 12, 24, 15, 7, 9)),
    ('day', instant(2022, 12, 24, 15, 7, 9), instant(2022, 12, 27, 9, 4, 5)),
    ('month', instant(2023, 3, 6, 9, 4, 5), instant(2023, 5, 9, 15, 7, 9)),
    ('year', instant(2022, 12, 24, 15, 7, 9), instant(2023, 3, 6, 9, 4, 5)),
]

BAGS = [
    ('E_d_MMMM', {'weekday': 'short', 'day': 'numeric', 'month': 'long'}),
    ('EEEE_y_MMMM_d', {'weekday': 'long', 'year': 'numeric', 'month': 'long', 'day': 'numeric'}),
    ('y_MMMM', {'year': 'numeric', 'month': 'long'}),
    ('y_MMM_d', {'year': 'numeric', 'month': 'short', 'day': 'numeric'}),
    ('MMM_d', {'month': 'short', 'day': 'numeric'}),
    ('y_M_d', {'year': 'numeric', 'month': 'numeric', 'day': 'numeric'}),
    ('yy_MM_dd', {'year': '2-digit', 'month': '2-digit', 'day': '2-digit'}),
    ('MMMM', {'month': 'long'}),
    ('d', {'day': 'numeric'}),
    ('G_y', {'era': 'short', 'year': 'numeric'}),
    ('j_mm', {'hour': 'numeric', 'minute': '2-digit'}),
    ('H_mm_h23', {'hour': 'numeric', 'minute': '2-digit', 'hourCycle': 'h23'}),
    ('j_mm_ss', {'hour': 'numeric', 'minute': 'numeric', 'second': 'numeric'}),
    ('y_MMMM_d_j_mm', {'year': 'numeric', 'month': 'long', 'day': 'numeric', 'hour': 'numeric', 'minute': '2-digit'}),
    ('y_M_d_j_mm_ss', {'year': 'numeric', 'month': 'numeric', 'day': 'numeric', 'hour': 'numeric', 'minute': 'numeric', 'second': 'numeric'}),
]

def pairs_for(bag):
    """A bag without a time field writes the minute and hour pairs as it writes the am/pm one, as a single date."""
    has_time = any(k in bag for k in ('hour', 'minute', 'second'))
    return [p for p in PAIRS if has_time or p[0] not in ('minute', 'hour')]


TYPE_CODES = {'literal': 'l', 'era': 'G', 'year': 'y', 'month': 'M', 'day': 'd', 'weekday': 'E', 'dayPeriod': 'a',
              'hour': 'h', 'minute': 'm', 'second': 's', 'fractionalSecond': 'S', 'timeZoneName': 'z'}
SOURCE_CODES = {'shared': 's', 'startRange': '1', 'endRange': '2'}


def encode_parts(parts):
    """Each part as its type's letter, its source (s, 1 or 2) and its length; the values are the text's slices."""
    return ' '.join('%s%s%d' % (TYPE_CODES.get(t, '?'), SOURCE_CODES[s], len(v.encode('utf-16-le')) // 2) for t, v, s in parts)


def model_row(root, locale, bag, x, y):
    """The model's (text, parts) for one range, the parts encoded; a single date is format()'s text, all shared."""
    ld, pattern, hc, ro = fm.resolve(root, locale, bag)
    itv = IntervalFormat(root, ld, static_get_skeleton(pattern), hc)
    parts = Range(ld, itv, pattern, x, y).parts()
    if parts is None:
        single = []
        for run in runs_of(pattern, 0):
            if run[0] == 'lit':
                value = run[1].replace('\u202f', ' ')
                if single and single[-1][0] == 'literal':
                    single[-1] = ('literal', single[-1][1] + value, 'shared')
                else:
                    single.append(('literal', value, 'shared'))
            else:
                single.append((PART_TYPES.get(run[1], 'unknown'), render_field(ld, run[1], run[2], x).replace('\u202f', ' '), 'shared'))
        parts = single
    return ''.join(v for _, v, _ in parts), encode_parts(parts)


def load_icu(path):
    """probe-range-golden.js's output: (locale, bag, pair) -> (text, parts)."""
    import re
    ref = {}
    for line in open(path, encoding='utf-8'):
        line = re.sub(r'\\u([0-9a-f]{4})', lambda m: chr(int(m.group(1), 16)), line.rstrip('\r\n'))
        line = line.encode('utf-16-le', 'surrogatepass').decode('utf-16-le')
        cols = line.split('\t')
        if len(cols) >= 5:
            ref[(cols[0], cols[1], cols[2])] = (cols[3], cols[4])
    return ref


def locales():
    """format_matcher.py's 51 locales, or the comma-separated PROBE_LOCALES (as probe-range-golden.js reads it)."""
    listed = os.environ.get('PROBE_LOCALES')
    return listed.split(',') if listed else fm.LOCALES


def golden(root, icu_tsv):
    """Writes the golden table Jint.Tests reads: the bags and the pairs, then one row per (locale, bag, pair) with the
    model's text and parts; and last, what ICU writes where it differs from the model, empty where it agrees."""
    icu = load_icu(icu_tsv)
    print('# Generated by tools/cldr-dates/reference/interval_format.py golden from cldr-json 48.2.0; do not edit.')
    print('# Each bag is formatted with { timeZone: "UTC", calendar: "gregory", numberingSystem: "latn" } added, and each pair')
    print('# (start and end, in milliseconds since the epoch) differs first in the field it is named after.')
    print('# parts: one per part, its type letter (l literal, G era, y year, M month, d day, E weekday, a dayPeriod, h hour,')
    print('# m minute, s second, S fractionalSecond, z timeZoneName), its source (s shared, 1 startRange, 2 endRange) and its')
    print('# length; the values are the text sliced in order. The last column is what ICU (Node 24.19, ICU 78.3) writes where')
    print('# it differs from the model: text|parts, with U+2009 and U+202F written as U+0020 in a range, as Jint writes them.')
    for name, bag in BAGS:
        print('\t'.join(['bag', name, json.dumps(bag, separators=(',', ':'))]))
    for pair, x, y in PAIRS:
        print('\t'.join(['pair', pair, str(x['epoch']), str(y['epoch'])]))
    print('# locale\tbag\tpair\ttext\tparts\ticu')
    for locale in fm.LOCALES:
        for name, bag in BAGS:
            for pair, x, y in pairs_for(bag):
                text, parts = model_row(root, locale, bag, x, y)
                reference = icu[(locale, name, pair)]
                differs = '' if reference == (text, parts) else '|'.join(reference)
                print('\t'.join([locale, name, pair, fm.escape(text), parts, fm.escape(differs)]))


def compare(root, icu_tsv):
    icu = load_icu(icu_tsv)
    same = diff = 0
    for locale in locales():
        for name, bag in BAGS:
            for pair, x, y in pairs_for(bag):
                text, parts = model_row(root, locale, bag, x, y)
                reference = icu.get((locale, name, pair))
                if reference == (text, parts):
                    same += 1
                else:
                    diff += 1
                    print('!!\t%s\t%s\t%s\tmodel=%r %s\ticu=%r %s' % (locale, name, pair, text, parts, reference and reference[0], reference and reference[1]))
    print('same', same, 'diff', diff)


def report_ties(root):
    """Every getBestSkeleton call building the golden table's interval formats that finds two skeletons at the least
    distance, where ICU's hash order would decide: none, so walking the skeletons in ordinal order is exact here."""
    global get_best_skeleton
    plain = get_best_skeleton
    found = []

    def checked(table, skeleton):
        result = plain(table, skeleton)
        tied = ties(table, skeleton) if table and next(iter(table.values()), None) is not None else []
        if tied:
            found.append((skeleton, result[0], tied))
        return result

    get_best_skeleton = checked
    try:
        for locale in locales():
            for name, bag in BAGS:
                ld, pattern, hc, ro = fm.resolve(root, locale, bag)
                before = len(found)
                IntervalFormat(root, ld, static_get_skeleton(pattern), hc)
                for skeleton, best, tied in found[before:]:
                    print(locale, name, skeleton, best, tied)
    finally:
        get_best_skeleton = plain
    print('ties', len(found))


if __name__ == '__main__':
    if len(sys.argv) >= 4 and sys.argv[1] == 'golden':
        golden(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 4 and sys.argv[1] == 'compare':
        compare(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 3 and sys.argv[1] == 'ties':
        report_ties(sys.argv[2])
    else:
        print(__doc__)
        sys.exit(2)
