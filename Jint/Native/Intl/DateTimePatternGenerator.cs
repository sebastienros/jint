using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Native.Intl.Data;

namespace Jint.Native.Intl;

/// <summary>
/// The format matcher <c>Intl.DateTimeFormat</c> resolves a component bag with: the pattern a locale writes for a
/// set of fields, chosen from CLDR's <c>availableFormats</c> the way ICU's <c>DateTimePatternGenerator</c> chooses it.
/// https://tc39.es/ecma402/#sec-bestfitformatmatcher leaves the choice to the implementation, and this is the one the
/// engines built on ICU make.
/// </summary>
/// <remarks>
/// <para>
/// It is a port of <c>tools/cldr-dates/reference/format_matcher.py</c>, a model of ICU's
/// <c>DateTimePatternGenerator</c> (icu4c <c>dtptngen.cpp</c>) as V8 drives it, which reproduces Node's output on
/// every row of the golden table <c>Jint.Tests</c> checks this class against: the skeleton is built from the bag
/// (<see cref="Skeleton.FromOptions"/>), each candidate pattern is scored by ICU's field-type distance, fields no
/// candidate has are appended through <c>appendItems</c>, a date and a time are joined by the <c>atTime</c>
/// <c>dateTimeFormats</c>, and the chosen pattern's field widths are adjusted to the request.
/// </para>
/// <para>
/// https://tc39.es/ecma402/#sec-basicformatmatcher is not implemented. It presumes a <c>[[formats]]</c> list with
/// every width of every field already expanded, and run literally over CLDR's <c>availableFormats</c> it matched ICU
/// on 142 of 275 bags in the design's measurement. <c>formatMatcher: "basic"</c> is therefore answered by this
/// matcher too, which is what V8 does.
/// </para>
/// <para>
/// One generator serves every engine that formats in its CLDR locale; it is immutable apart from its caches, which
/// are concurrent and bounded.
/// </para>
/// </remarks>
internal sealed class DateTimePatternGenerator
{
    // ICU's UDateTimePatternField order.
    private const int Era = 0;
    private const int Year = 1;
    private const int Quarter = 2;
    private const int Month = 3;
    private const int WeekOfYear = 4;
    private const int WeekOfMonth = 5;
    private const int Weekday = 6;
    private const int DayOfYear = 7;
    private const int DayOfWeekInMonth = 8;
    private const int Day = 9;
    private const int DayPeriod = 10;
    private const int Hour = 11;
    private const int Minute = 12;
    private const int Second = 13;
    private const int FractionalSecond = 14;
    private const int Zone = 15;
    private const int FieldCount = 16;

    private const int AllFields = (1 << FieldCount) - 1;
    private const int DateFields = (1 << DayPeriod) - 1;
    private const int TimeFields = AllFields & ~DateFields;

    // ICU's field types: a numeric field's is positive and grows with its length, a textual one's is negative.
    private const int TypeNarrow = -0x101;
    private const int TypeShorter = -0x102;
    private const int TypeShort = -0x103;
    private const int TypeLong = -0x104;
    private const int TypeNumeric = 0x100;
    private const int TypeDelta = 0x10;

    private const int ExtraFieldPenalty = 0x10000;
    private const int MissingFieldPenalty = 0x1000;

    /// <summary>The single fields ICU adds as patterns of their own, so any field can be matched on its own.</summary>
    private const string CanonicalItems = "GyQMwWEDFdaHmsSv";

    /// <summary>How many resolved patterns a generator keeps; the rest are resolved again each time.</summary>
    private const int MaxCachedPatterns = 512;

    /// <summary>How many requested locale tags are remembered against the generator that serves them.</summary>
    private const int MaxCachedLocaleTags = 1024;

    private static readonly FieldTypeRow[] FieldTypes = BuildFieldTypes();
    private static readonly ConcurrentDictionary<string, DateTimePatternGenerator> Generators = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, DateTimePatternGenerator> GeneratorsByTag = new(StringComparer.Ordinal);
    private static int _generatorsByTagCount;

    private readonly DateTimePatternLocale _data;
    private readonly Candidate[] _candidates;
    private readonly ConcurrentDictionary<string, DateTimeFormatPattern> _patterns = new(StringComparer.Ordinal);
    private int _patternCount;
    private readonly string[]?[] _names = new string[]?[NameTableCount];

    private DateTimePatternGenerator(DateTimePatternLocale data)
    {
        _data = data;
        _candidates = BuildCandidates(data);
    }

    /// <summary>
    /// The generator for the CLDR locale that serves the BCP 47 tag <paramref name="locale"/>
    /// (<see cref="DateTimePatternData.ResolveLocale"/>).
    /// </summary>
    internal static DateTimePatternGenerator ForLocale(string locale)
    {
        if (GeneratorsByTag.TryGetValue(locale, out var generator))
        {
            return generator;
        }

        var data = DateTimePatternData.Shared.GetLocale(locale);
        if (!Generators.TryGetValue(data.Locale, out generator))
        {
            generator = Generators.GetOrAdd(data.Locale, new DateTimePatternGenerator(data));
        }

        // A tag is script-controlled, so the map from tags is bounded; the map from CLDR locales is bounded by the data.
        // The count is kept beside the map because ConcurrentDictionary.Count takes every lock it has.
        if (System.Threading.Volatile.Read(ref _generatorsByTagCount) < MaxCachedLocaleTags && GeneratorsByTag.TryAdd(locale, generator))
        {
            System.Threading.Interlocked.Increment(ref _generatorsByTagCount);
        }

        return generator;
    }

    /// <summary>
    /// The pattern the locale writes a component bag with, and the format record it resolves to.
    /// </summary>
    /// <param name="skeleton">The bag as a skeleton, <see cref="Skeleton.FromOptions"/>.</param>
    /// <param name="hourCycle">The resolved hour cycle; applied only when the skeleton has an hour.</param>
    /// <param name="decimalSeparator">What separates the seconds from their fraction when a fraction is asked for.</param>
    internal DateTimeFormatPattern GetPattern(string skeleton, string hourCycle, char decimalSeparator)
    {
        // The skeleton's hour letter already is the hour cycle, so the skeleton and the separator are the whole key.
        var key = decimalSeparator + skeleton;
        if (_patterns.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var request = Skeleton.Parse(skeleton);
        var pattern = BestPattern(request, decimalSeparator);
        if (request.Has(Hour))
        {
            pattern = ReplaceHourCycle(pattern, hourCycle);
        }

        var result = new DateTimeFormatPattern(pattern, this);
        if (System.Threading.Volatile.Read(ref _patternCount) < MaxCachedPatterns && _patterns.TryAdd(key, result))
        {
            System.Threading.Interlocked.Increment(ref _patternCount);
        }

        return result;
    }

    // === Candidates ===

    /// <summary>
    /// ICU's pattern map: the locale's style patterns, the canonical single fields and its <c>availableFormats</c>,
    /// in the order ICU adds them, then grouped by the letter of their first field the way ICU walks them.
    /// </summary>
    /// <remarks>
    /// The order is load-bearing: ICU gives a tie in distance to the pattern it added first. A style pattern (which has
    /// no skeleton of its own) is dropped when one with the same base skeleton is already there, so <c>medium</c>
    /// shadows <c>short</c>; an <c>availableFormats</c> entry replaces a same-skeleton pattern in place. Those entries
    /// are added the locale's own first, then each CLDR ancestor's in turn (<see cref="DateTimePatternLocale.OwnFormats"/>),
    /// each bundle in key order: <c>en-AU</c>'s own <c>MMMMEEEEd</c> goes before the <c>MMMEd</c> it inherits from
    /// <c>en-001</c>, and wins the tie <c>{ weekday: "short", month: "long", day: "numeric" }</c> scores them.
    /// </remarks>
    private static Candidate[] BuildCandidates(DateTimePatternLocale data)
    {
        var candidates = new List<Candidate>(96);
        var byKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var bases = new HashSet<string>(StringComparer.Ordinal);

        void Add(string pattern, string? skeletonText, bool replace)
        {
            var specified = skeletonText is not null;
            var skeleton = Skeleton.Parse(skeletonText ?? SkeletonOf(pattern));
            var key = skeleton.CanonicalKey();
            var baseKey = skeleton.BaseKey();
            if (!specified && bases.Contains(baseKey))
            {
                return;
            }

            var exists = byKey.TryGetValue(key, out var index);
            if (exists && !replace)
            {
                return;
            }

            bases.Add(baseKey);
            if (exists)
            {
                candidates[index] = new Candidate(skeleton, pattern, index);
            }
            else
            {
                byKey.Add(key, candidates.Count);
                candidates.Add(new Candidate(skeleton, pattern, candidates.Count));
            }
        }

        for (var width = DateTimeStyleWidth.Full; width <= DateTimeStyleWidth.Short; width++)
        {
            Add(data.GetDateFormat(width), null, replace: false);
            Add(data.GetTimeFormat(width), null, replace: false);
        }

        foreach (var item in CanonicalItems)
        {
            var text = item.ToString();
            Add(text, text, replace: false);
        }

        var added = new HashSet<string>(StringComparer.Ordinal);
        var replacedKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var level = data; level is not null; level = level.Parent)
        {
            foreach (var own in level.OwnFormats)
            {
                if (!data.TryGetAvailableFormat(own.Skeleton, out var pattern) || !added.Add(own.Skeleton))
                {
                    continue;
                }

                // An availableFormats entry replaces a style pattern or a canonical item with its skeleton, but not an
                // availableFormats entry added before it.
                var key = Skeleton.Parse(own.Skeleton).CanonicalKey();
                Add(pattern, own.Skeleton, replace: replacedKeys.Add(key));
            }
        }

        // ICU walks its map by the first letter of each base skeleton, upper case first, and within a letter in the
        // order the patterns were added.
        var result = candidates.ToArray();
        System.Array.Sort(result, static (x, y) =>
        {
            var byBucket = x.Bucket.CompareTo(y.Bucket);
            return byBucket != 0 ? byBucket : x.Order.CompareTo(y.Order);
        });
        return result;
    }

    /// <summary>
    /// A pattern's own skeleton: its field letters, in the order it writes them.
    /// </summary>
    private static string SkeletonOf(string pattern)
    {
        var builder = new StringBuilder(pattern.Length);
        foreach (var token in PatternToken.Tokenize(pattern))
        {
            if (token.IsField)
            {
                builder.Append(token.Letter, token.Length);
            }
        }

        return builder.ToString();
    }

    // === Matching ===

    /// <summary>
    /// ICU's <c>getBestPattern</c>: the best single candidate when one has every field, and otherwise the date and
    /// the time assembled separately and joined.
    /// </summary>
    private string BestPattern(Skeleton request, char decimalSeparator)
    {
        var best = GetBestRaw(request, AllFields);
        if (best.Missing == 0 && best.Extra == 0)
        {
            return AdjustFieldTypes(best.Candidate.Pattern, request, best.Candidate.Skeleton, fixFractionalSeconds: false, decimalSeparator);
        }

        var needed = request.Mask;
        var date = GetBestAppending(request, needed & DateFields, decimalSeparator);
        var time = GetBestAppending(request, needed & TimeFields, decimalSeparator);
        if (date.Length == 0)
        {
            return time;
        }

        if (time.Length == 0)
        {
            return date;
        }

        // ICU picks the join by the width of the month asked for, and since ICU 72 from the atTime variants.
        var monthLength = request.Lengths[Month];
        var style = DateTimeStyleWidth.Short;
        if (monthLength == 4)
        {
            style = request.Has(Weekday) ? DateTimeStyleWidth.Full : DateTimeStyleWidth.Long;
        }
        else if (monthLength == 3)
        {
            style = DateTimeStyleWidth.Medium;
        }

        return Substitute(_data.GetAtTimeFormat(style), time, date, null);
    }

    /// <summary>
    /// ICU's <c>getBestAppending</c>: the best candidate for <paramref name="missingFields"/>, and every field it
    /// still lacks appended through the locale's <c>appendItems</c>.
    /// </summary>
    private string GetBestAppending(Skeleton request, int missingFields, char decimalSeparator)
    {
        if (missingFields == 0)
        {
            return "";
        }

        var best = GetBestRaw(request, missingFields);
        var result = AdjustFieldTypes(best.Candidate.Pattern, request, best.Candidate.Skeleton, fixFractionalSeconds: false, decimalSeparator);
        var missing = best.Missing;
        if (missing == 0 && best.Extra == 0)
        {
            return result;
        }

        // A fraction of a second the pattern lacks is written straight after its seconds.
        const int SecondAndFraction = (1 << Second) | (1 << FractionalSecond);
        if ((missing & SecondAndFraction) == 1 << FractionalSecond && (missingFields & SecondAndFraction) == SecondAndFraction)
        {
            result = AdjustFieldTypes(best.Candidate.Pattern, request, best.Candidate.Skeleton, fixFractionalSeconds: true, decimalSeparator);
            missing &= ~(1 << FractionalSecond);
        }

        for (var guard = 0; missing != 0 && guard < FieldCount; guard++)
        {
            var start = missing;
            var next = GetBestRaw(request, missing);
            var appended = AdjustFieldTypes(next.Candidate.Pattern, request, next.Candidate.Skeleton, fixFractionalSeconds: false, decimalSeparator);
            var found = start & ~next.Missing;
            if (found == 0)
            {
                break;
            }

            var topField = HighestBit(found);
            if (AppendFieldOf(topField) is { } appendField)
            {
                var name = "'" + _data.GetFieldDisplayName(appendField) + "'";
                result = Substitute(_data.GetAppendItem(appendField), result, appended, name);
            }

            missing = next.Missing;
        }

        return result;
    }

    /// <summary>
    /// ICU's <c>getBestRaw</c>: the candidate nearest the request over the fields in <paramref name="includeMask"/>.
    /// A field the candidate lacks costs <see cref="MissingFieldPenalty"/>, one it has that was not asked for
    /// <see cref="ExtraFieldPenalty"/>, and one of another width or kind the difference of their types. The first
    /// candidate of the least distance wins.
    /// </summary>
    private BestMatch GetBestRaw(Skeleton request, int includeMask)
    {
        var best = default(BestMatch);
        var bestDistance = int.MaxValue;
        foreach (var candidate in _candidates)
        {
            var distance = Distance(request, includeMask, candidate.Skeleton, out var missing, out var extra);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new BestMatch(candidate, missing, extra);
                if (distance == 0)
                {
                    break;
                }
            }
        }

        return best;
    }

    private static int Distance(Skeleton request, int includeMask, Skeleton candidate, out int missing, out int extra)
    {
        var result = 0;
        missing = 0;
        extra = 0;
        for (var field = 0; field < FieldCount; field++)
        {
            var requested = (includeMask & (1 << field)) != 0 ? request.Types[field] : 0;
            var offered = candidate.Types[field];
            if (requested == offered)
            {
                continue;
            }

            if (requested == 0)
            {
                result += ExtraFieldPenalty;
                extra |= 1 << field;
            }
            else if (offered == 0)
            {
                result += MissingFieldPenalty;
                missing |= 1 << field;
            }
            else
            {
                result += System.Math.Abs(requested - offered);
            }
        }

        return result;
    }

    /// <summary>
    /// ICU's <c>adjustFieldTypes</c>, with <c>UDATPG_MATCH_HOUR_FIELD_LENGTH</c> as V8 passes it: each field of the
    /// chosen pattern takes the width the request asked for, except where the pattern's own width is the point.
    /// </summary>
    /// <remarks>
    /// Minutes and seconds keep the pattern's width. A field keeps it too when the candidate's skeleton already had
    /// the requested width (German's <c>HH 'Uhr'</c> for a numeric hour) or when one is numeric and the other text
    /// (Japanese <c>M月</c> for a long month). The month, weekday and hour letters stay as the pattern has them, so
    /// <c>L</c> and <c>c</c> keep the stand-alone form; a weekday is never narrower than three letters.
    /// </remarks>
    private static string AdjustFieldTypes(string pattern, Skeleton request, Skeleton specified, bool fixFractionalSeconds, char decimalSeparator)
    {
        var builder = new StringBuilder(pattern.Length + 4);
        foreach (var token in PatternToken.Tokenize(pattern))
        {
            if (!token.IsField)
            {
                builder.Append(token.Raw);
                continue;
            }

            var letter = token.Letter;
            var length = token.Length;
            var row = FindRow(letter, length);
            if (row < 0)
            {
                builder.Append(letter, length);
                continue;
            }

            var field = FieldTypes[row].Field;
            if (fixFractionalSeconds && field == Second)
            {
                builder.Append(letter, length);
                builder.Append(decimalSeparator);
                builder.Append('S', request.Lengths[FractionalSecond]);
                continue;
            }

            if (request.Types[field] == 0)
            {
                builder.Append(letter, length);
                continue;
            }

            var requestedLetter = request.Letters[field];
            var requestedLength = request.Lengths[field];
            if (requestedLetter == 'E' && requestedLength < 3)
            {
                requestedLength = 3;
            }

            var adjustedLength = requestedLength;
            if (field is Minute or Second)
            {
                adjustedLength = length;
            }
            else if (requestedLetter != 'c' && requestedLetter != 'e' && specified.Has(field))
            {
                var patternIsNumeric = FieldTypes[row].Type > 0;
                var skeletonIsNumeric = specified.Types[field] > 0;
                if (specified.Lengths[field] == requestedLength || patternIsNumeric != skeletonIsNumeric)
                {
                    adjustedLength = length;
                }
            }

            var keepsLetter = field is Hour or Month or Weekday || (field == Year && requestedLetter != 'Y');
            builder.Append(keepsLetter ? letter : requestedLetter, adjustedLength);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes the resolved hour cycle's letter over every hour field of <paramref name="pattern"/>, as V8 does after
    /// the match: <c>K</c> for h11, <c>h</c> for h12, <c>H</c> for h23, <c>k</c> for h24.
    /// </summary>
    private static string ReplaceHourCycle(string pattern, string hourCycle)
    {
        var letter = HourLetter(hourCycle);
        var builder = new StringBuilder(pattern.Length);
        foreach (var token in PatternToken.Tokenize(pattern))
        {
            if (!token.IsField)
            {
                builder.Append(token.Raw);
            }
            else
            {
                builder.Append(token.Letter is 'h' or 'H' or 'k' or 'K' ? letter : token.Letter, token.Length);
            }
        }

        return builder.ToString();
    }

    internal static char HourLetter(string hourCycle) => hourCycle switch
    {
        "h11" => 'K',
        "h23" => 'H',
        "h24" => 'k',
        _ => 'h',
    };

    /// <summary>
    /// Replaces <c>{0}</c>, <c>{1}</c> and <c>{2}</c> in one pass, so a value that happens to contain a placeholder
    /// is never substituted into.
    /// </summary>
    private static string Substitute(string template, string zero, string one, string? two)
    {
        var builder = new StringBuilder(template.Length + zero.Length + one.Length + (two?.Length ?? 0));
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{' && i + 2 < template.Length && template[i + 2] == '}')
            {
                var value = template[i + 1] switch
                {
                    '0' => zero,
                    '1' => one,
                    '2' => two,
                    _ => null,
                };

                if (value is not null)
                {
                    builder.Append(value);
                    i += 2;
                    continue;
                }
            }

            builder.Append(template[i]);
        }

        return builder.ToString();
    }

    private static int HighestBit(int value)
    {
        var bit = -1;
        while (value != 0)
        {
            value >>= 1;
            bit++;
        }

        return bit;
    }

    /// <summary>The <c>appendItems</c> entry, and the display name in its <c>{2}</c>, for a field.</summary>
    private static DateTimeAppendField? AppendFieldOf(int field) => field switch
    {
        Era => DateTimeAppendField.Era,
        Year => DateTimeAppendField.Year,
        Quarter => DateTimeAppendField.Quarter,
        Month => DateTimeAppendField.Month,
        WeekOfYear or WeekOfMonth => DateTimeAppendField.Week,
        Weekday => DateTimeAppendField.Weekday,
        DayOfYear or DayOfWeekInMonth or Day => DateTimeAppendField.Day,
        Hour => DateTimeAppendField.Hour,
        Minute => DateTimeAppendField.Minute,
        Second or FractionalSecond => DateTimeAppendField.Second,
        Zone => DateTimeAppendField.Zone,
        _ => null,
    };

    // === Names ===

    private const int NameTableCount = 20;

    /// <summary>
    /// The locale's CLDR names for a text field, one array per context and width, built the first time each is asked
    /// for. U+202F, which CLDR 42 and later write in some names and patterns, is a plain space here as everywhere in
    /// the formatter.
    /// </summary>
    internal string[] GetMonthNames(DateTimeNameContext context, DateTimeNameWidth width)
        => GetNames((int) context * 3 + (int) width, static (data, c, w) => data.GetMonthNames(c, w), context, width);

    internal string[] GetWeekdayNames(DateTimeNameContext context, DateTimeNameWidth width)
        => GetNames(6 + (int) context * 4 + (int) width, static (data, c, w) => data.GetWeekdayNames(c, w), context, width);

    internal string[] GetEraNames(DateTimeNameWidth width)
        => GetNames(14 + (int) width, static (data, _, w) => data.GetEraNames(w), DateTimeNameContext.Format, width);

    internal string[] GetDayPeriodNames(DateTimeNameWidth width)
        => GetNames(17 + (int) width, static (data, _, w) => data.GetDayPeriodNames(w), DateTimeNameContext.Format, width);

    private delegate ReadOnlySpan<string> NameReader(DateTimePatternLocale data, DateTimeNameContext context, DateTimeNameWidth width);

    private string[] GetNames(int table, NameReader read, DateTimeNameContext context, DateTimeNameWidth width)
    {
        var names = System.Threading.Volatile.Read(ref _names[table]);
        if (names is not null)
        {
            return names;
        }

        var span = read(_data, context, width);
        names = new string[span.Length];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = DateTimeFormatPattern.NormalizeSpaces(span[i]);
        }

        return System.Threading.Interlocked.CompareExchange(ref _names[table], names, null) ?? names;
    }

    // === Field types ===

    /// <summary>
    /// The row of ICU's <c>dtTypes</c> table a field letter of a given length falls in: the one for that letter with
    /// the greatest minimum length not above it, or -1 for a letter the table does not know.
    /// </summary>
    private static int FindRow(char letter, int length)
    {
        var found = -1;
        for (var i = 0; i < FieldTypes.Length; i++)
        {
            var row = FieldTypes[i];
            if (row.Letter == letter && row.MinLength <= length && (found < 0 || row.MinLength > FieldTypes[found].MinLength))
            {
                found = i;
            }
        }

        return found;
    }

    /// <summary>
    /// ICU's <c>dtTypes</c>: each field letter, the field it writes and its type by minimum length.
    /// </summary>
    private static FieldTypeRow[] BuildFieldTypes() =>
    [
        new('G', Era, TypeShort, 1), new('G', Era, TypeLong, 4), new('G', Era, TypeNarrow, 5),
        new('y', Year, TypeNumeric, 1), new('Y', Year, TypeNumeric + TypeDelta, 1), new('u', Year, TypeNumeric + 2 * TypeDelta, 1),
        new('r', Year, TypeNumeric + 3 * TypeDelta, 1), new('U', Year, TypeShort, 1), new('U', Year, TypeLong, 4), new('U', Year, TypeNarrow, 5),
        new('Q', Quarter, TypeNumeric, 1), new('Q', Quarter, TypeShort, 3), new('Q', Quarter, TypeLong, 4), new('Q', Quarter, TypeNarrow, 5),
        new('q', Quarter, TypeNumeric + TypeDelta, 1), new('q', Quarter, TypeShort - TypeDelta, 3), new('q', Quarter, TypeLong - TypeDelta, 4),
        new('q', Quarter, TypeNarrow - TypeDelta, 5),
        new('M', Month, TypeNumeric, 1), new('M', Month, TypeShort, 3), new('M', Month, TypeLong, 4), new('M', Month, TypeNarrow, 5),
        new('L', Month, TypeNumeric + TypeDelta, 1), new('L', Month, TypeShort - TypeDelta, 3), new('L', Month, TypeLong - TypeDelta, 4),
        new('L', Month, TypeNarrow - TypeDelta, 5), new('l', Month, TypeNumeric + TypeDelta, 1),
        new('w', WeekOfYear, TypeNumeric, 1), new('W', WeekOfMonth, TypeNumeric, 1),
        new('E', Weekday, TypeShort, 1), new('E', Weekday, TypeLong, 4), new('E', Weekday, TypeNarrow, 5), new('E', Weekday, TypeShorter, 6),
        new('c', Weekday, TypeNumeric + 2 * TypeDelta, 1), new('c', Weekday, TypeShort - 2 * TypeDelta, 3), new('c', Weekday, TypeLong - 2 * TypeDelta, 4),
        new('c', Weekday, TypeNarrow - 2 * TypeDelta, 5), new('c', Weekday, TypeShorter - 2 * TypeDelta, 6),
        new('e', Weekday, TypeNumeric + TypeDelta, 1), new('e', Weekday, TypeShort - TypeDelta, 3), new('e', Weekday, TypeLong - TypeDelta, 4),
        new('e', Weekday, TypeNarrow - TypeDelta, 5), new('e', Weekday, TypeShorter - TypeDelta, 6),
        new('d', Day, TypeNumeric, 1), new('g', Day, TypeNumeric + TypeDelta, 1), new('D', DayOfYear, TypeNumeric, 1), new('F', DayOfWeekInMonth, TypeNumeric, 1),
        new('a', DayPeriod, TypeShort, 1), new('a', DayPeriod, TypeLong, 4), new('a', DayPeriod, TypeNarrow, 5),
        new('b', DayPeriod, TypeShort - TypeDelta, 1), new('b', DayPeriod, TypeLong - TypeDelta, 4), new('b', DayPeriod, TypeNarrow - TypeDelta, 5),
        new('B', DayPeriod, TypeShort - 3 * TypeDelta, 1), new('B', DayPeriod, TypeLong - 3 * TypeDelta, 4), new('B', DayPeriod, TypeNarrow - 3 * TypeDelta, 5),
        new('H', Hour, TypeNumeric + 10 * TypeDelta, 1), new('k', Hour, TypeNumeric + 11 * TypeDelta, 1), new('h', Hour, TypeNumeric, 1),
        new('K', Hour, TypeNumeric + TypeDelta, 1),
        new('m', Minute, TypeNumeric, 1), new('s', Second, TypeNumeric, 1), new('A', Second, TypeNumeric + TypeDelta, 1),
        new('S', FractionalSecond, TypeNumeric, 1),
        new('v', Zone, TypeShort - 2 * TypeDelta, 1), new('v', Zone, TypeLong - 2 * TypeDelta, 4), new('z', Zone, TypeShort, 1), new('z', Zone, TypeLong, 4),
        new('Z', Zone, TypeNarrow - TypeDelta, 1), new('Z', Zone, TypeLong - TypeDelta, 4), new('Z', Zone, TypeShort - TypeDelta, 5),
        new('O', Zone, TypeShort - TypeDelta, 1), new('O', Zone, TypeLong - TypeDelta, 4),
        new('V', Zone, TypeShort - TypeDelta, 1), new('V', Zone, TypeLong - TypeDelta, 2), new('V', Zone, TypeLong - 1 - TypeDelta, 3),
        new('V', Zone, TypeLong - 2 - TypeDelta, 4),
        new('X', Zone, TypeNarrow - TypeDelta, 1), new('X', Zone, TypeShort - TypeDelta, 2), new('X', Zone, TypeLong - TypeDelta, 4),
        new('x', Zone, TypeNarrow - TypeDelta, 1), new('x', Zone, TypeShort - TypeDelta, 2), new('x', Zone, TypeLong - TypeDelta, 4),
    ];

    [StructLayout(LayoutKind.Auto)]
    private readonly struct FieldTypeRow
    {
        internal FieldTypeRow(char letter, int field, int type, int minLength)
        {
            Letter = letter;
            Field = field;
            Type = type;
            MinLength = minLength;
        }

        internal char Letter { get; }

        internal int Field { get; }

        internal int Type { get; }

        internal int MinLength { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BestMatch
    {
        internal BestMatch(Candidate candidate, int missing, int extra)
        {
            Candidate = candidate;
            Missing = missing;
            Extra = extra;
        }

        internal Candidate Candidate { get; }

        internal int Missing { get; }

        internal int Extra { get; }
    }

    private sealed class Candidate
    {
        internal Candidate(Skeleton skeleton, string pattern, int order)
        {
            Skeleton = skeleton;
            Pattern = pattern;
            Order = order;
            Bucket = skeleton.Bucket();
        }

        internal Skeleton Skeleton { get; }

        internal string Pattern { get; }

        /// <summary>When the pattern was added; a replaced pattern keeps its predecessor's.</summary>
        internal int Order { get; }

        internal int Bucket { get; }
    }

    /// <summary>
    /// ICU's <c>DateTimeMatcher</c>: a skeleton as the letter, length and type of each of its fields.
    /// </summary>
    internal sealed class Skeleton
    {
        private Skeleton()
        {
        }

        internal char[] Letters { get; } = new char[FieldCount];

        internal int[] Lengths { get; } = new int[FieldCount];

        internal int[] Types { get; } = new int[FieldCount];

        internal bool Has(int field) => Letters[field] != '\0';

        internal int Mask
        {
            get
            {
                var mask = 0;
                for (var index = 0; index < FieldCount; index++)
                {
                    if (Types[index] != 0)
                    {
                        mask |= 1 << index;
                    }
                }

                return mask;
            }
        }

        /// <summary>
        /// The skeleton of an options bag, built the way V8 builds it: one field per option present, the hour's letter
        /// from the resolved hour cycle.
        /// </summary>
        internal static string FromOptions(
            string? weekday,
            string? era,
            string? year,
            string? month,
            string? day,
            string? dayPeriod,
            string? hour,
            string? minute,
            string? second,
            int? fractionalSecondDigits,
            string? timeZoneName,
            string hourCycle)
        {
            var builder = new StringBuilder(24);
            AppendTextual(builder, 'E', weekday, shortLength: 3);
            AppendTextual(builder, 'G', era, shortLength: 1);
            AppendNumeric(builder, 'y', year);
            if (month is not null)
            {
                builder.Append('M', month switch
                {
                    "2-digit" => 2,
                    "narrow" => 5,
                    "short" => 3,
                    "long" => 4,
                    _ => 1,
                });
            }

            AppendNumeric(builder, 'd', day);
            AppendTextual(builder, 'B', dayPeriod, shortLength: 1);
            AppendNumeric(builder, HourLetter(hourCycle), hour);
            AppendNumeric(builder, 'm', minute);
            AppendNumeric(builder, 's', second);
            if (fractionalSecondDigits is > 0)
            {
                builder.Append('S', fractionalSecondDigits.Value);
            }

            switch (timeZoneName)
            {
                case "short":
                    builder.Append('z');
                    break;
                case "long":
                    builder.Append('z', 4);
                    break;
                case "shortOffset":
                    builder.Append('O');
                    break;
                case "longOffset":
                    builder.Append('O', 4);
                    break;
                case "shortGeneric":
                    builder.Append('v');
                    break;
                case "longGeneric":
                    builder.Append('v', 4);
                    break;
            }

            return builder.ToString();

            static void AppendTextual(StringBuilder builder, char letter, string? value, int shortLength)
            {
                if (value is not null)
                {
                    builder.Append(letter, value switch
                    {
                        "narrow" => 5,
                        "long" => 4,
                        _ => shortLength,
                    });
                }
            }

            static void AppendNumeric(StringBuilder builder, char letter, string? value)
            {
                if (value is not null)
                {
                    builder.Append(letter, string.Equals(value, "2-digit", StringComparison.Ordinal) ? 2 : 1);
                }
            }
        }

        /// <summary>
        /// ICU's <c>DateTimeMatcher::set</c>: each field's letter, length and type; a 12-hour skeleton without a day
        /// period gets the implied <c>a</c>, and a 24-hour one loses any day period it has.
        /// </summary>
        internal static Skeleton Parse(string text)
        {
            var skeleton = new Skeleton();
            foreach (var token in PatternToken.Tokenize(text))
            {
                if (!token.IsField)
                {
                    continue;
                }

                var row = FindRow(token.Letter, token.Length);
                if (row < 0)
                {
                    continue;
                }

                var field = FieldTypes[row].Field;
                var type = FieldTypes[row].Type;
                skeleton.Letters[field] = token.Letter;
                skeleton.Lengths[field] = token.Length;
                skeleton.Types[field] = type > 0 ? type + token.Length : type;
            }

            if (skeleton.Has(Hour))
            {
                if (skeleton.Letters[Hour] is 'h' or 'K')
                {
                    if (!skeleton.Has(DayPeriod))
                    {
                        skeleton.Letters[DayPeriod] = 'a';
                        skeleton.Lengths[DayPeriod] = 1;
                        skeleton.Types[DayPeriod] = TypeShort;
                    }
                }
                else if (skeleton.Has(DayPeriod))
                {
                    skeleton.Letters[DayPeriod] = '\0';
                    skeleton.Lengths[DayPeriod] = 0;
                    skeleton.Types[DayPeriod] = 0;
                }
            }

            return skeleton;
        }

        /// <summary>Each field's letter at its length, in field order: equal for two skeletons ICU treats as one.</summary>
        internal string CanonicalKey()
        {
            var builder = new StringBuilder(16);
            for (var field = 0; field < FieldCount; field++)
            {
                if (Has(field))
                {
                    builder.Append(Letters[field], Lengths[field]);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// ICU's base skeleton: each field at the minimum length of its type's row, so <c>d</c> and <c>dd</c> share one
        /// and <c>MMM</c> and <c>MMMM</c> do not.
        /// </summary>
        internal string BaseKey()
        {
            var builder = new StringBuilder(16);
            for (var field = 0; field < FieldCount; field++)
            {
                if (Has(field))
                {
                    var row = FindRow(Letters[field], Lengths[field]);
                    builder.Append(Letters[field], FieldTypes[row].MinLength);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Where ICU's map files the skeleton: by the letter of its first field, upper case before lower case.
        /// </summary>
        internal int Bucket()
        {
            for (var field = 0; field < FieldCount; field++)
            {
                if (Has(field))
                {
                    var letter = Letters[field];
                    return letter is >= 'A' and <= 'Z' ? letter - 'A' : 26 + (letter - 'a');
                }
            }

            return 52;
        }
    }
}

/// <summary>
/// One piece of an LDML pattern: a run of one field letter, or literal text as the pattern writes it (quotes and all).
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct PatternToken
{
    private PatternToken(char letter, int length, string? raw)
    {
        Letter = letter;
        Length = length;
        Raw = raw;
    }

    internal bool IsField => Raw is null;

    internal char Letter { get; }

    internal int Length { get; }

    /// <summary>The literal as the pattern writes it, quotes included; null for a field.</summary>
    internal string? Raw { get; }

    /// <summary>
    /// Splits an LDML pattern (https://www.unicode.org/reports/tr35/tr35-dates.html#Date_Format_Patterns): a run of
    /// one ASCII letter is a field, a quoted stretch (<c>''</c> being a quote) and any other character are literals.
    /// </summary>
    internal static List<PatternToken> Tokenize(string pattern)
    {
        var tokens = new List<PatternToken>();
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                var j = i + 1;
                while (j < pattern.Length)
                {
                    if (pattern[j] == '\'')
                    {
                        if (j + 1 < pattern.Length && pattern[j + 1] == '\'')
                        {
                            j += 2;
                            continue;
                        }

                        j++;
                        break;
                    }

                    j++;
                }

                tokens.Add(new PatternToken('\0', 0, pattern.Substring(i, j - i)));
                i = j;
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                var j = i + 1;
                while (j < pattern.Length && pattern[j] == c)
                {
                    j++;
                }

                tokens.Add(new PatternToken(c, j - i, null));
                i = j;
                continue;
            }

            tokens.Add(new PatternToken('\0', 0, c.ToString()));
            i++;
        }

        return tokens;
    }
}

/// <summary>
/// A pattern the format matcher chose, split once into what the renderer walks, and the format record
/// (https://tc39.es/ecma402/#sec-datetimeformat-format-record) its field letters make: what
/// <c>resolvedOptions()</c> reports for a component bag (https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.resolvedoptions).
/// </summary>
/// <remarks>
/// The record is read off the letters of the pattern, after its quoted literals are taken out, so the Spanish
/// <c>MMMM 'de' y</c> has no day in it; V8 reads the <c>d</c> of <c>'de'</c> as one. Immutable, and shared by every
/// formatter that resolves to it.
/// </remarks>
internal sealed class DateTimeFormatPattern
{
    internal DateTimeFormatPattern(string pattern, DateTimePatternGenerator generator)
    {
        Pattern = pattern;
        var runs = new List<DateTimePatternRun>();
        var literal = new StringBuilder();
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    literal.Append('\'');
                    i += 2;
                    continue;
                }

                i++;
                while (i < pattern.Length)
                {
                    if (pattern[i] == '\'')
                    {
                        if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                        {
                            literal.Append('\'');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    literal.Append(pattern[i]);
                    i++;
                }

                continue;
            }

            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                var j = i + 1;
                while (j < pattern.Length && pattern[j] == c)
                {
                    j++;
                }

                if (literal.Length > 0)
                {
                    runs.Add(new DateTimePatternRun('\0', 0, NormalizeSpaces(literal.ToString()), null));
                    literal.Clear();
                }

                var length = j - i;
                runs.Add(new DateTimePatternRun(c, length, null, NamesFor(generator, c, length)));
                Record(c, length);
                i = j;
                continue;
            }

            literal.Append(c);
            i++;
        }

        if (literal.Length > 0)
        {
            runs.Add(new DateTimePatternRun('\0', 0, NormalizeSpaces(literal.ToString()), null));
        }

        Runs = runs.ToArray();
    }

    /// <summary>The LDML pattern, as the matcher chose it.</summary>
    internal string Pattern { get; }

    /// <summary>The pattern split into field runs and unquoted literal runs.</summary>
    internal DateTimePatternRun[] Runs { get; }

    internal string? Weekday { get; private set; }
    internal string? Era { get; private set; }
    internal string? Year { get; private set; }
    internal string? Month { get; private set; }
    internal string? Day { get; private set; }
    internal string? DayPeriod { get; private set; }
    internal string? Hour { get; private set; }
    internal string? Minute { get; private set; }
    internal string? Second { get; private set; }
    internal int? FractionalSecondDigits { get; private set; }
    internal string? TimeZoneName { get; private set; }

    /// <summary>
    /// CLDR 42 and later write U+202F NARROW NO-BREAK SPACE in time patterns; the formatter writes a plain space in
    /// its place everywhere, as V8 does in <c>format()</c>, so that <c>format()</c> is always the concatenation of
    /// <c>formatToParts()</c>.
    /// </summary>
    internal static string NormalizeSpaces(string value) => value.IndexOf('\u202F') < 0 ? value : value.Replace('\u202F', ' ');

    private static string TextualStyle(int length) => length switch
    {
        4 => "long",
        5 => "narrow",
        _ => "short",
    };

    private static string NumericStyle(int length) => length == 2 ? "2-digit" : "numeric";

    private void Record(char letter, int length)
    {
        switch (letter)
        {
            case 'E' or 'c' or 'e':
                Weekday = TextualStyle(length);
                break;
            case 'G':
                Era = TextualStyle(length);
                break;
            case 'y' or 'Y':
                Year = NumericStyle(length);
                break;
            case 'M' or 'L':
                Month = length switch
                {
                    1 => "numeric",
                    2 => "2-digit",
                    3 => "short",
                    4 => "long",
                    _ => "narrow",
                };
                break;
            case 'd':
                Day = NumericStyle(length);
                break;
            case 'B':
                DayPeriod = TextualStyle(length);
                break;
            case 'h' or 'H' or 'k' or 'K':
                Hour = NumericStyle(length);
                break;
            case 'm':
                Minute = NumericStyle(length);
                break;
            case 's':
                Second = NumericStyle(length);
                break;
            case 'S':
                FractionalSecondDigits = length;
                break;
            case 'z':
                TimeZoneName = length >= 4 ? "long" : "short";
                break;
            case 'O':
                TimeZoneName = length >= 4 ? "longOffset" : "shortOffset";
                break;
            case 'v':
                TimeZoneName = length >= 4 ? "longGeneric" : "shortGeneric";
                break;
            case 'Z' or 'V' or 'X' or 'x':
                TimeZoneName = "short";
                break;
        }
    }

    /// <summary>The locale's CLDR names a text field writes, or null for a numeric field.</summary>
    private static string[]? NamesFor(DateTimePatternGenerator generator, char letter, int length)
    {
        switch (letter)
        {
            case 'M' or 'L' when length >= 3:
                return generator.GetMonthNames(letter == 'M' ? DateTimeNameContext.Format : DateTimeNameContext.StandAlone, NameWidth(length, allowsShort: false));
            case 'E' or 'c' or 'e':
                // A one- or two-letter c or e is ICU's numeric day of the week, which no ECMA-402 option asks for and
                // no CLDR pattern this engine carries uses; it is written as the abbreviated name.
                return generator.GetWeekdayNames(letter == 'E' ? DateTimeNameContext.Format : DateTimeNameContext.StandAlone, NameWidth(length, allowsShort: true));
            case 'G':
                return generator.GetEraNames(NameWidth(length, allowsShort: false));
            case 'a' or 'b':
                return generator.GetDayPeriodNames(NameWidth(length, allowsShort: false));
            default:
                return null;
        }
    }

    /// <summary>
    /// The CLDR width a text field's length asks for: up to three letters abbreviated, four wide, five narrow, six short
    /// (weekdays only).
    /// </summary>
    internal static DateTimeNameWidth NameWidth(int length, bool allowsShort) => length switch
    {
        4 => DateTimeNameWidth.Wide,
        5 => DateTimeNameWidth.Narrow,
        >= 6 when allowsShort => DateTimeNameWidth.Short,
        >= 6 => DateTimeNameWidth.Narrow,
        _ => DateTimeNameWidth.Abbreviated,
    };
}

/// <summary>
/// One run of a chosen pattern: a field letter and its length, or literal text; a text field carries the CLDR names it
/// writes.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct DateTimePatternRun
{
    internal DateTimePatternRun(char field, int length, string? literal, string[]? names)
    {
        Field = field;
        Length = length;
        Literal = literal;
        Names = names;
    }

    internal char Field { get; }

    internal int Length { get; }

    internal string? Literal { get; }

    internal string[]? Names { get; }

    internal bool IsLiteral => Literal is not null;
}
