using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Jint.Native.Intl.Data;

namespace Jint.Native.Intl;

/// <summary>
/// The range patterns of a format record (https://tc39.es/ecma402/#sec-datetimeformat-range-pattern-record), a component
/// bag's or a <c>dateStyle</c>/<c>timeStyle</c>'s:
/// what <c>formatRange</c> and <c>formatRangeToParts</c> write two dates with, built from CLDR's <c>intervalFormats</c>
/// the way ICU's <c>DateIntervalFormat</c> builds them as V8 drives it.
/// </summary>
/// <remarks>
/// <para>
/// It is a port of <c>tools/cldr-dates/reference/interval_format.py</c>, a model of ICU's <c>DateIntervalFormat</c>
/// (icu4c <c>dtitvfmt.cpp</c> and <c>dtitvinf.cpp</c>) which reproduces Node's <c>formatRange</c> and
/// <c>formatRangeToParts</c> on every row of the golden table <c>Jint.Tests</c> checks this class against. V8 creates
/// the interval format for the skeleton of the pattern a single date is written with — the one the format matcher
/// chose, or the style's — with the resolved hour cycle; the
/// skeleton is split into a date and a time, each normalized, and the nearest interval skeleton CLDR has is looked up,
/// its pattern widened to the request. A date and a time on one day are the date joined to the time's interval
/// pattern by the <c>medium</c> <c>dateTimeFormats</c>; any other range of a date and a time, and any field no
/// interval pattern covers, is the two whole dates in CLDR's <c>intervalFormatFallback</c>.
/// </para>
/// <para>
/// Each range pattern is the pattern's runs and a source for each: the fields that occur twice, and the text between
/// them, are the <c>startRange</c> and the <c>endRange</c> date as ICU's <c>FormattedDateInterval</c> spans mark them;
/// everything else is <c>shared</c>. A maximal stretch of one source is a DateTime Range Pattern Part Record
/// (https://tc39.es/ecma402/#sec-datetimeformat-range-pattern-part-record). CLDR writes U+2009 THIN SPACE around its
/// dashes, which a range keeps as V8 does, and U+202F in its times, which is U+0020 here as in every lane.
/// </para>
/// <para>
/// One instance serves every formatter whose format record it was built for (<see cref="DateTimePatternGenerator.GetIntervalFormat"/>);
/// the range patterns are built the first time a range needs them and are immutable.
/// </para>
/// </remarks>
internal sealed class DateTimeIntervalFormat
{
    // ICU's DateIntervalInfo::IntervalPatternIndex: the rows of https://tc39.es/ecma402/#sec-datetimeformat-range-pattern-record
    // after [[Default]], less [[DayPeriod]], which ICU reads as the am/pm (see JsDateTimeFormat.FormatRangeToParts).
    internal const int Era = 0;
    internal const int Year = 1;
    internal const int Month = 2;
    internal const int Day = 3;
    internal const int AmPm = 4;
    internal const int Hour = 5;
    internal const int Minute = 6;
    internal const int Second = 7;
    internal const int FractionalSecond = 8;
    internal const int FieldCount = 9;

    /// <summary>The fields CLDR keys an interval pattern by, in the order of the indexes above.</summary>
    private const string DataFields = "GyMdahm";

    /// <summary>ICU's <c>SimpleDateFormat::fgCalendarFieldToLevel</c>, by index.</summary>
    private static readonly int[] FieldLevels = [0, 10, 20, 30, 40, 50, 60, 70, 80];

    /// <summary>Stands for "the range is the single date" in <see cref="_rangePatterns"/>.</summary>
    private static readonly DateTimeRangePattern SingleDate = new([], []);

    private readonly DateTimePatternGenerator _generator;
    private readonly IntervalTable _table;
    private readonly string _hourCycle;
    private readonly char _decimalSeparator;
    private readonly string _skeleton;
    private readonly string _fallback;
    private readonly bool _laterDateFirst;
    private readonly string?[] _first = new string?[FieldCount];
    private readonly string?[] _second = new string?[FieldCount];
    private readonly DateTimeRangePattern?[] _rangePatterns = new DateTimeRangePattern?[FieldCount];
    private string? _datePattern;
    private string? _timePattern;
    private string? _dateTimeFormat;

    /// <param name="generator">The generator of the formatter's CLDR locale.</param>
    /// <param name="format">The pattern a single date is written with: the format matcher's for a component bag, or the style's.</param>
    /// <param name="hourCycle">The resolved hour cycle, which V8 hands ICU as the locale's <c>-u-hc-</c> keyword.</param>
    /// <param name="decimalSeparator">What separates the seconds from their fraction.</param>
    internal DateTimeIntervalFormat(DateTimePatternGenerator generator, DateTimeFormatPattern format, string hourCycle, char decimalSeparator)
    {
        _generator = generator;
        _table = generator.IntervalTable;
        _hourCycle = hourCycle;
        _decimalSeparator = decimalSeparator;
        _fallback = generator.Data.IntervalFormatFallback;
        _laterDateFirst = _fallback.IndexOf("{1}", StringComparison.Ordinal) < _fallback.IndexOf("{0}", StringComparison.Ordinal);
        _skeleton = DateTimePatternGenerator.StaticSkeletonOf(format.Pattern);
        FormatPattern = BestPattern(_skeleton);
        Initialize();
    }

    /// <summary>
    /// The pattern ICU's own date format of the interval writes a whole date with in a fallback: the locale's best
    /// pattern for the skeleton, matched without V8's options (so a two-digit hour is the pattern's own).
    /// </summary>
    internal string FormatPattern { get; }

    /// <summary>
    /// The range pattern for a range whose largest differing field is <paramref name="field"/>, or
    /// <see langword="null"/> when the range is written as the single start date: the format shows no field that
    /// small, or the pattern ICU would write has no field it writes twice.
    /// </summary>
    internal DateTimeRangePattern? GetRangePattern(int field)
    {
        var pattern = Volatile.Read(ref _rangePatterns[field]);
        if (pattern is null)
        {
            var built = BuildRangePattern(field) ?? SingleDate;
            pattern = Interlocked.CompareExchange(ref _rangePatterns[field], built, null) ?? built;
        }

        return ReferenceEquals(pattern, SingleDate) ? null : pattern;
    }

    private string BestPattern(string skeleton) => _generator.GetBestPattern(skeleton, _hourCycle, _decimalSeparator, matchHourFieldLength: false);

    // === DateIntervalFormat::initializePattern ===

    private void Initialize()
    {
        var converted = NormalizeHourMetacharacters(_skeleton);
        SplitDateTime(converted, out var date, out var normalizedDate, out var time, out var normalizedTime);
        if (time.Length > 0 && date.Length > 0)
        {
            // DateTimePatterns[kDateTime], which is CLDR's medium dateTimeFormats and not its atTime variant.
            _dateTimeFormat = _generator.Data.GetDateTimeFormat(DateTimeStyleWidth.Medium);
        }

        var found = SetSeparateDateTimePatterns(normalizedDate, normalizedTime);
        if (!found)
        {
            if (time.Length > 0 && date.Length == 0)
            {
                SetTimeOnlyFallbacks(time);
            }

            return;
        }

        if (time.Length == 0)
        {
            return;
        }

        if (date.Length == 0)
        {
            SetTimeOnlyFallbacks(time);
            return;
        }

        // A date and a time: a range across days writes both whole dates, with the fields that differ added to the skeleton.
        var skeleton = _skeleton;
        foreach (var (field, letter) in new[] { (Day, 'd'), (Month, 'M'), (Year, 'y'), (Era, 'G') })
        {
            if (date.IndexOf(letter) < 0)
            {
                skeleton = letter + skeleton;
                SetFallback(field, BestPattern(skeleton));
            }
        }

        // One day: the date once, joined to the time's interval pattern.
        var datePattern = BestPattern(date);
        foreach (var field in new[] { AmPm, Hour, Minute })
        {
            if (!string.IsNullOrEmpty(_first[field]))
            {
                SetPattern(field, Substitute(_dateTimeFormat!, _first[field] + _second[field], datePattern));
            }
        }
    }

    /// <summary>
    /// A time without a date is written with the short date's fields (<c>yMd</c>) in front of it when the dates differ.
    /// </summary>
    private void SetTimeOnlyFallbacks(string time)
    {
        var pattern = BestPattern("yMd" + time);
        SetFallback(Day, pattern);
        SetFallback(Month, pattern);
        SetFallback(Year, pattern);
        SetFallback(Era, BestPattern("GyMd" + time));
    }

    /// <summary>
    /// ICU's <c>setSeparateDateTimePtn</c>: the interval patterns of the time if there is one, else of the date, from the
    /// nearest interval skeleton; false when that skeleton has other fields, which leaves every range to a fallback.
    /// </summary>
    private bool SetSeparateDateTimePatterns(string date, string time)
    {
        var skeleton = time.Length > 0 ? time : date;
        var best = _table.GetBestSkeleton(skeleton, out var difference);
        if (best is null)
        {
            return false;
        }

        if (date.Length > 0)
        {
            _datePattern = BestPattern(date);
        }

        if (time.Length > 0)
        {
            _timePattern = BestPattern(time);
        }

        if (difference == -1)
        {
            return false;
        }

        if (time.Length == 0)
        {
            // ICU hands the four calls one extended skeleton and extended best skeleton, which persist between them;
            // once the month's pattern came from an extension, the year's and the era's calls extend those in place.
            var state = new Extension(skeleton, best);
            SetDateInterval(Day, ref state, difference);
            if (SetDateInterval(Month, ref state, difference))
            {
                state.Aliased = true;
                state.Skeleton = state.Extended;
                state.Best = state.ExtendedBest;
            }

            SetDateInterval(Year, ref state, difference);
            SetDateInterval(Era, ref state, difference);
        }
        else
        {
            SetTimeInterval(Minute, skeleton, best, difference);
            SetTimeInterval(Hour, skeleton, best, difference);
            SetTimeInterval(AmPm, skeleton, best, difference);
        }

        return true;
    }

    /// <summary>
    /// ICU's <c>setIntervalPattern</c> for a time field: an am/pm difference with no pattern of its own takes the hour's,
    /// as a 24-hour skeleton has none.
    /// </summary>
    private void SetTimeInterval(int field, string skeleton, string best, int difference)
    {
        var pattern = _table.GetPattern(best, field);
        if (pattern is null)
        {
            if (IsFieldUnitIgnored(best, field))
            {
                return;
            }

            if (field == AmPm && _table.GetPattern(best, Hour) is { } hourPattern)
            {
                SetPattern(field, AdjustFieldWidth(skeleton, best, hourPattern, difference));
            }

            return;
        }

        SetPattern(field, difference != 0 ? AdjustFieldWidth(skeleton, best, pattern, difference) : pattern);
    }

    /// <summary>
    /// ICU's <c>setIntervalPattern</c> for a date field: a field the best skeleton has no pattern for is looked up on the
    /// skeletons extended by its letter (<c>MMMd</c> to <c>yMMMd</c>); answers whether an extension is on record.
    /// </summary>
    private bool SetDateInterval(int field, ref Extension state, int difference)
    {
        var skeleton = state.Skeleton;
        var best = state.Best;
        var pattern = _table.GetPattern(best, field);
        if (pattern is null)
        {
            if (IsFieldUnitIgnored(best, field))
            {
                return false;
            }

            var letter = DataFields[field];
            state.Extended = letter + skeleton;
            state.ExtendedBest = letter + best;
            if (state.Aliased)
            {
                state.Skeleton = skeleton = state.Extended;
                state.Best = best = state.ExtendedBest;
            }

            pattern = _table.GetPattern(state.ExtendedBest, field);
            if (pattern is null && difference == 0)
            {
                var nearer = _table.GetBestSkeleton(state.ExtendedBest, out difference);
                if (nearer is not null && difference != -1)
                {
                    pattern = _table.GetPattern(nearer, field);
                    best = nearer;
                }
            }
        }

        if (pattern is null)
        {
            return false;
        }

        SetPattern(field, difference != 0 ? AdjustFieldWidth(skeleton, best, pattern, difference) : pattern);
        return state.Extended.Length > 0;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Extension
    {
        internal Extension(string skeleton, string best)
        {
            Skeleton = skeleton;
            Best = best;
            Extended = "";
            ExtendedBest = "";
            Aliased = false;
        }

        internal string Skeleton;
        internal string Best;
        internal string Extended;
        internal string ExtendedBest;
        internal bool Aliased;
    }

    private void SetPattern(int field, string pattern)
    {
        var split = SplitPoint(pattern);
        _first[field] = pattern.Substring(0, split);
        _second[field] = pattern.Substring(split);
    }

    /// <summary>A fallback's own pattern, which ICU keeps as the second part with an empty first one.</summary>
    private void SetFallback(int field, string pattern)
    {
        _first[field] = "";
        _second[field] = pattern;
    }

    /// <summary>
    /// ICU's <c>normalizeHourMetacharacters</c>: the hour field written with the letter the locale's pattern for that
    /// hour letter uses, and the day period CLDR's pattern for it writes (<c>zh-Hant</c>'s <c>Bh時</c> makes it
    /// <c>B</c>), or <c>a</c>.
    /// </summary>
    private string NormalizeHourMetacharacters(string skeleton)
    {
        var hourLetter = '\0';
        var dayPeriod = '\0';
        int hourStart = 0, hourLength = 0, periodStart = 0, periodLength = 0;
        for (var i = 0; i < skeleton.Length; i++)
        {
            var c = skeleton[i];
            if (c is 'j' or 'J' or 'C' or 'h' or 'H' or 'k' or 'K')
            {
                if (hourLetter == '\0')
                {
                    hourLetter = c;
                    hourStart = i;
                }

                hourLength++;
            }
            else if (c is 'a' or 'b' or 'B')
            {
                if (dayPeriod == '\0')
                {
                    dayPeriod = c;
                    periodStart = i;
                }

                periodLength++;
            }
            else if (hourLetter != '\0' && dayPeriod != '\0')
            {
                break;
            }
        }

        if (hourLetter == '\0')
        {
            return skeleton;
        }

        var converted = WithoutQuotedText(BestPattern(hourLetter.ToString()));
        var letter = 'H';
        if (converted.Contains('h'))
        {
            letter = 'h';
        }
        else if (converted.Contains('K'))
        {
            letter = 'K';
        }
        else if (converted.Contains('k'))
        {
            letter = 'k';
        }

        if (converted.Contains('b'))
        {
            dayPeriod = 'b';
        }
        else if (converted.Contains('B'))
        {
            dayPeriod = 'B';
        }
        else if (dayPeriod == '\0')
        {
            dayPeriod = 'a';
        }

        var replacement = new StringBuilder().Append(letter);
        if (letter is not ('H' or 'k'))
        {
            var width = periodLength >= 5 || hourLength >= 5 ? 5 : periodLength >= 3 || hourLength >= 3 ? 3 : 1;
            replacement.Append(dayPeriod, width);
        }

        var result = skeleton.Substring(0, hourStart) + replacement + skeleton.Substring(hourStart + hourLength);
        if (periodStart > hourStart)
        {
            periodStart += replacement.Length - hourLength;
        }

        return result.Remove(periodStart, periodLength);
    }

    private static string WithoutQuotedText(string pattern)
    {
        int first;
        while ((first = pattern.IndexOf('\'')) >= 0)
        {
            var second = pattern.IndexOf('\'', first + 1);
            if (second < 0)
            {
                second = first;
            }

            pattern = pattern.Remove(first, second - first + 1);
        }

        return pattern;
    }

    /// <summary>
    /// ICU's <c>getDateTimeSkeleton</c>: the skeleton's date and time fields, and their normalized forms, which keep one
    /// <c>d</c>, one hour, one <c>m</c> and one zone, fold <c>MM</c> into <c>M</c> and <c>E</c> to <c>EEE</c> into <c>E</c>.
    /// </summary>
    private static void SplitDateTime(string skeleton, out string date, out string normalizedDate, out string time, out string normalizedTime)
    {
        var dateBuilder = new StringBuilder();
        var normalizedDateBuilder = new StringBuilder();
        var timeBuilder = new StringBuilder();
        var normalizedTimeBuilder = new StringBuilder();
        int e = 0, d = 0, months = 0, y = 0, minutes = 0, v = 0, z = 0;
        var hourLetter = '\0';
        foreach (var c in skeleton)
        {
            switch (c)
            {
                case 'E':
                    dateBuilder.Append(c);
                    e++;
                    break;
                case 'd':
                    dateBuilder.Append(c);
                    d++;
                    break;
                case 'M':
                    dateBuilder.Append(c);
                    months++;
                    break;
                case 'y':
                    dateBuilder.Append(c);
                    y++;
                    break;
                case 'G' or 'Y' or 'u' or 'Q' or 'q' or 'L' or 'l' or 'W' or 'w' or 'D' or 'F' or 'g' or 'e' or 'c' or 'U' or 'r':
                    normalizedDateBuilder.Append(c);
                    dateBuilder.Append(c);
                    break;
                case 'h' or 'H' or 'k' or 'K':
                    timeBuilder.Append(c);
                    if (hourLetter == '\0')
                    {
                        hourLetter = c;
                    }

                    break;
                case 'm':
                    timeBuilder.Append(c);
                    minutes++;
                    break;
                case 'z':
                    z++;
                    timeBuilder.Append(c);
                    break;
                case 'v':
                    v++;
                    timeBuilder.Append(c);
                    break;
                case 'a' or 'V' or 'Z' or 'j' or 's' or 'S' or 'A' or 'b' or 'B':
                    timeBuilder.Append(c);
                    normalizedTimeBuilder.Append(c);
                    break;
            }
        }

        normalizedDateBuilder.Append('y', y);
        if (months > 0)
        {
            normalizedDateBuilder.Append('M', months < 3 ? 1 : System.Math.Min(months, 5));
        }

        if (e > 0)
        {
            normalizedDateBuilder.Append('E', e <= 3 ? 1 : System.Math.Min(e, 5));
        }

        if (d > 0)
        {
            normalizedDateBuilder.Append('d');
        }

        if (hourLetter != '\0')
        {
            normalizedTimeBuilder.Append(hourLetter);
        }

        if (minutes > 0)
        {
            normalizedTimeBuilder.Append('m');
        }

        if (z > 0)
        {
            normalizedTimeBuilder.Append('z');
        }

        if (v > 0)
        {
            normalizedTimeBuilder.Append('v');
        }

        date = dateBuilder.ToString();
        normalizedDate = normalizedDateBuilder.ToString();
        time = timeBuilder.ToString();
        normalizedTime = normalizedTimeBuilder.ToString();
    }

    /// <summary>
    /// ICU's <c>adjustFieldWidth</c>: a field the best skeleton has at the pattern's width is widened to the one the
    /// skeleton asks for (<c>MMM</c> to <c>MMMM</c>, <c>E</c> to <c>EEEE</c>); with a difference of 2 the letters the
    /// best skeleton stood in for (<c>z</c>, <c>K</c>, <c>k</c>, <c>b</c>) are put back.
    /// </summary>
    private static string AdjustFieldWidth(string inputSkeleton, string bestSkeleton, string pattern, int difference)
    {
        var inputWidths = IntervalTable.Widths(inputSkeleton);
        var bestWidths = IntervalTable.Widths(bestSkeleton);
        if (difference == 2)
        {
            if (inputSkeleton.Contains('z'))
            {
                pattern = ReplaceOutsideQuotes(pattern, 'v', 'z');
            }

            if (inputSkeleton.Contains('K'))
            {
                pattern = ReplaceOutsideQuotes(pattern, 'h', 'K');
            }

            if (inputSkeleton.Contains('k'))
            {
                pattern = ReplaceOutsideQuotes(pattern, 'H', 'k');
            }

            if (inputSkeleton.Contains('b'))
            {
                pattern = ReplaceOutsideQuotes(pattern, 'a', 'b');
            }
        }

        // ICU looks for an 'a' or 'b' anywhere in the pattern, quoted text included.
        if (pattern.Contains('a') && bestWidths['a' - 'A'] == 0)
        {
            bestWidths['a' - 'A'] = 1;
        }

        if (pattern.Contains('b') && bestWidths['b' - 'A'] == 0)
        {
            bestWidths['b' - 'A'] = 1;
        }

        var builder = new StringBuilder(pattern.Length + 4);
        var quoted = false;
        var previous = '\0';
        var count = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c != previous && count > 0)
            {
                Widen(builder, previous, count, inputWidths, bestWidths);
                count = 0;
            }

            if (c == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    builder.Append("''");
                    i++;
                    continue;
                }

                quoted = !quoted;
            }
            else if (!quoted && IsAsciiLetter(c))
            {
                previous = c;
                count++;
            }

            builder.Append(c);
        }

        if (count > 0)
        {
            Widen(builder, previous, count, inputWidths, bestWidths);
        }

        return builder.ToString();

        static void Widen(StringBuilder builder, char letter, int count, int[] inputWidths, int[] bestWidths)
        {
            var key = (letter == 'L' ? 'M' : letter) - 'A';
            var fieldCount = bestWidths[key];
            var inputCount = inputWidths[key];
            if (fieldCount == count && inputCount > fieldCount)
            {
                builder.Append(letter, inputCount - fieldCount);
            }
        }
    }

    /// <summary>ICU's <c>findReplaceInPattern</c> for one letter: outside quoted text only.</summary>
    private static string ReplaceOutsideQuotes(string pattern, char from, char to)
    {
        var builder = new StringBuilder(pattern.Length);
        var quoted = false;
        foreach (var c in pattern)
        {
            if (c == '\'')
            {
                quoted = !quoted;
            }

            builder.Append(!quoted && c == from ? to : c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// ICU's <c>splitPatternInto2Part</c>: an interval pattern is the first date up to the first field whose letter was
    /// written before, and the second from there on.
    /// </summary>
    internal static int SplitPoint(string pattern)
    {
        // One bit per letter from 'A' to 'z'.
        var seen = 0UL;
        var quoted = false;
        var previous = '\0';
        var count = 0;
        var found = false;
        var i = 0;
        for (; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c != previous && count > 0)
            {
                var bit = 1UL << (previous - 'A');
                if ((seen & bit) != 0)
                {
                    found = true;
                    break;
                }

                seen |= bit;
                count = 0;
            }

            if (c == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (!quoted && IsAsciiLetter(c))
            {
                previous = c;
                count++;
            }
        }

        if (count > 0 && !found && (seen & (1UL << (previous - 'A'))) == 0)
        {
            count = 0;
        }

        return i - count;
    }

    /// <summary>
    /// ICU's <c>SimpleDateFormat::isFieldUnitIgnored</c>: whether every field <paramref name="pattern"/> (or a skeleton)
    /// writes is larger than <paramref name="field"/>, so that two dates differing only there write the same text.
    /// </summary>
    internal static bool IsFieldUnitIgnored(string pattern, int field)
    {
        var level = FieldLevels[field];
        var quoted = false;
        var previous = '\0';
        var count = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c != previous && count > 0)
            {
                if (level <= LevelOf(previous))
                {
                    return false;
                }

                count = 0;
            }

            if (c == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (!quoted && IsAsciiLetter(c))
            {
                previous = c;
                count++;
            }
        }

        return count == 0 || level > LevelOf(previous);
    }

    /// <summary>ICU's <c>SimpleDateFormat::getLevelFromChar</c>: the larger, the smaller the unit; -1 for no calendar field.</summary>
    internal static int LevelOf(char letter) => letter switch
    {
        'G' or 'O' or 'V' or 'X' or 'Z' or 'g' or 'l' or 'v' or 'x' or 'z' => 0,
        'U' or 'Y' or 'r' or 'u' or 'y' => 10,
        'D' or 'L' or 'M' or 'Q' or 'q' or 'w' => 20,
        'E' or 'F' or 'W' or 'c' or 'd' or 'e' => 30,
        'A' or 'a' => 40,
        'H' or 'K' or 'h' or 'k' => 50,
        'm' => 60,
        's' => 70,
        'S' => 80,
        _ => -1,
    };

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    /// <summary>
    /// <paramref name="joiner"/> (a <c>dateTimeFormats</c> pattern) with <c>{0}</c> the time and <c>{1}</c> the date.
    /// </summary>
    private static string Substitute(string joiner, string time, string date)
    {
        var builder = new StringBuilder(joiner.Length + time.Length + date.Length);
        for (var i = 0; i < joiner.Length; i++)
        {
            if (joiner[i] == '{' && i + 2 < joiner.Length && joiner[i + 2] == '}' && joiner[i + 1] is '0' or '1')
            {
                builder.Append(joiner[i + 1] == '0' ? time : date);
                i += 2;
                continue;
            }

            builder.Append(joiner[i]);
        }

        return builder.ToString();
    }

    // === The range patterns ===

    /// <summary>
    /// ICU's <c>DateIntervalFormat::formatImpl</c> for a largest differing field, as runs: the interval pattern's two
    /// parts, or a fallback, or null for the single date.
    /// </summary>
    private DateTimeRangePattern? BuildRangePattern(int field)
    {
        var runs = new List<(DateTimePatternRun Run, int Date)>();
        var first = _first[field];
        var second = _second[field];
        var sameDay = field >= AmPm;
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(second))
        {
            if (IsFieldUnitIgnored(FormatPattern, field))
            {
                return null;
            }

            AppendFallback(runs, FormatPattern, sameDay);
        }
        else if (string.IsNullOrEmpty(first))
        {
            AppendFallback(runs, second!, sameDay);
        }
        else
        {
            AppendPattern(runs, first!, _laterDateFirst ? 1 : 0);
            AppendPattern(runs, second!, _laterDateFirst ? 0 : 1);
        }

        return DateTimeRangePattern.FromRuns(runs, FieldLevels[field]);
    }

    /// <summary>
    /// ICU's <c>fallbackFormat</c>: on one day, the date once and the time's two values in the fallback, joined by the
    /// medium <c>dateTimeFormats</c>; otherwise the two whole dates in the fallback.
    /// </summary>
    private void AppendFallback(List<(DateTimePatternRun Run, int Date)> runs, string pattern, bool sameDay)
    {
        if (!sameDay || _datePattern is null || _timePattern is null || _dateTimeFormat is null)
        {
            AppendFallbackDates(runs, pattern);
            return;
        }

        var joiner = _dateTimeFormat;
        var literal = new StringBuilder();
        for (var i = 0; i < joiner.Length; i++)
        {
            if (joiner[i] == '{' && i + 2 < joiner.Length && joiner[i + 2] == '}' && joiner[i + 1] is '0' or '1')
            {
                AppendLiteral(runs, literal);
                if (joiner[i + 1] == '0')
                {
                    AppendFallbackDates(runs, _timePattern);
                }
                else
                {
                    AppendPattern(runs, _datePattern, 0);
                }

                i += 2;
                continue;
            }

            literal.Append(joiner[i]);
        }

        AppendLiteral(runs, literal);
    }

    /// <summary>
    /// CLDR's <c>intervalFormatFallback</c> around two dates: the start at <c>{0}</c> and the end at <c>{1}</c>, its own
    /// text written as it stands.
    /// </summary>
    private void AppendFallbackDates(List<(DateTimePatternRun Run, int Date)> runs, string pattern)
    {
        var literal = new StringBuilder();
        for (var i = 0; i < _fallback.Length; i++)
        {
            if (_fallback[i] == '{' && i + 2 < _fallback.Length && _fallback[i + 2] == '}' && _fallback[i + 1] is '0' or '1')
            {
                AppendLiteral(runs, literal);
                AppendPattern(runs, pattern, _fallback[i + 1] - '0');
                i += 2;
                continue;
            }

            literal.Append(_fallback[i]);
        }

        AppendLiteral(runs, literal);
    }

    private void AppendPattern(List<(DateTimePatternRun Run, int Date)> runs, string pattern, int date)
    {
        foreach (var run in new DateTimeFormatPattern(pattern, _generator).Runs)
        {
            runs.Add((run, date));
        }
    }

    private static void AppendLiteral(List<(DateTimePatternRun Run, int Date)> runs, StringBuilder literal)
    {
        if (literal.Length > 0)
        {
            runs.Add((new DateTimePatternRun('\0', 0, DateTimeFormatPattern.NormalizeSpaces(literal.ToString()), null), -1));
            literal.Clear();
        }
    }

    /// <summary>
    /// A locale's interval patterns by skeleton, as ICU's <c>DateIntervalInfo</c> holds them: read once per locale.
    /// </summary>
    internal sealed class IntervalTable
    {
        private readonly string[] _skeletons;
        private readonly int[][] _widths;
        private readonly string?[][] _patterns;
        private readonly Dictionary<string, int> _index;

        internal IntervalTable(DateTimePatternLocale data)
        {
            var skeletons = new List<string>();
            var patterns = new List<string?[]>();
            foreach (var entry in data.IntervalFormats)
            {
                if (skeletons.Count == 0 || !string.Equals(skeletons[skeletons.Count - 1], entry.Skeleton, StringComparison.Ordinal))
                {
                    skeletons.Add(entry.Skeleton);
                    patterns.Add(new string?[DataFields.Length]);
                }

                var field = DataFields.IndexOf(entry.Field);
                if (field >= 0)
                {
                    patterns[patterns.Count - 1][field] = entry.Pattern;
                }
            }

            _skeletons = skeletons.ToArray();
            _patterns = patterns.ToArray();
            _widths = new int[_skeletons.Length][];
            _index = new Dictionary<string, int>(_skeletons.Length, StringComparer.Ordinal);
            for (var i = 0; i < _skeletons.Length; i++)
            {
                _widths[i] = Widths(_skeletons[i]);
                _index.Add(_skeletons[i], i);
            }
        }

        /// <summary>ICU's <c>parseSkeleton</c>: how many times each letter occurs, by letter from <c>A</c>.</summary>
        internal static int[] Widths(string skeleton)
        {
            var widths = new int[58];
            foreach (var c in skeleton)
            {
                if (IsAsciiLetter(c))
                {
                    widths[c - 'A']++;
                }
            }

            return widths;
        }

        /// <summary>The pattern CLDR has for <paramref name="skeleton"/> when <paramref name="field"/> differs, if any.</summary>
        internal string? GetPattern(string skeleton, int field)
        {
            return field < DataFields.Length && _index.TryGetValue(skeleton, out var i) ? _patterns[i][field] : null;
        }

        /// <summary>
        /// ICU's <c>DateIntervalInfo::getBestSkeleton</c>: the interval skeleton nearest <paramref name="skeleton"/>, with
        /// <paramref name="difference"/> 0 for the same fields at the same widths, 1 for other widths, 2 when <c>z</c>,
        /// <c>K</c>, <c>k</c>, <c>a</c> or <c>b</c> had to be read as <c>v</c>, <c>h</c>, <c>H</c> or nothing, and -1
        /// for other fields.
        /// </summary>
        /// <remarks>
        /// A field one has and the other lacks costs 0x1000, a numeric month against a textual one 0x100, and otherwise
        /// the difference of the widths. ICU walks its hash table and keeps the first of the least distance; this walks
        /// the skeletons in ordinal order, and no locale's table has two skeletons at the least distance from any
        /// skeleton the golden table asks for (<c>interval_format.py ties</c>).
        /// </remarks>
        internal string? GetBestSkeleton(string skeleton, out int difference)
        {
            var replaced = skeleton.AsSpan().IndexOfAny("zkKab".AsSpan()) >= 0;
            if (replaced)
            {
                var builder = new StringBuilder(skeleton.Length);
                foreach (var c in skeleton)
                {
                    switch (c)
                    {
                        case 'z':
                            builder.Append('v');
                            break;
                        case 'k':
                            builder.Append('H');
                            break;
                        case 'K':
                            builder.Append('h');
                            break;
                        case 'a' or 'b':
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }

                skeleton = builder.ToString();
            }

            var request = Widths(skeleton);
            string? best = null;
            var bestDistance = int.MaxValue;
            difference = 0;
            for (var i = 0; i < _skeletons.Length; i++)
            {
                var candidate = _widths[i];
                var distance = 0;
                var fieldDifference = 1;
                for (var letter = 0; letter < request.Length; letter++)
                {
                    var requested = request[letter];
                    var offered = candidate[letter];
                    if (requested == offered)
                    {
                        continue;
                    }

                    if (requested == 0 || offered == 0)
                    {
                        fieldDifference = -1;
                        distance += 0x1000;
                    }
                    else if (letter == 'M' - 'A' && (requested <= 2) != (offered <= 2))
                    {
                        distance += 0x100;
                    }
                    else
                    {
                        distance += System.Math.Abs(requested - offered);
                    }
                }

                if (distance < bestDistance)
                {
                    best = _skeletons[i];
                    bestDistance = distance;
                    difference = fieldDifference;
                }

                if (distance == 0)
                {
                    difference = 0;
                    break;
                }
            }

            if (replaced && difference != -1)
            {
                difference = 2;
            }

            return best;
        }
    }
}
