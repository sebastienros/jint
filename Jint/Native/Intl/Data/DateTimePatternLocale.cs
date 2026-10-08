using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.Native.Intl.Data;

/// <summary>
/// One locale's CLDR Gregorian date and time data, resolved through its inheritance chain: what
/// <see cref="DateTimePatternData"/> hands out, built once per locale and never changed afterwards.
/// </summary>
/// <remarks>
/// The values sit in one array at the positions the generator's <c>SlotLayout</c> gives them
/// (<c>tools/cldr-dates/Jint.CldrDates.Generator/SlotLayout.cs</c>); <see cref="DateTimePatternData"/> checks the
/// resource's own list of slot names against the offsets below before it reads a record. The spans handed out
/// point into that array, which nothing writes after construction and which a derived locale shares by copying.
/// </remarks>
internal sealed class DateTimePatternLocale
{
    internal const int DateTimeFormatsStart = 0;
    internal const int AtTimeFormatsStart = 4;
    internal const int DateFormatsStart = 8;
    internal const int TimeFormatsStart = 12;
    internal const int AppendItemsStart = 16;
    internal const int FieldDisplayNamesStart = 27;
    internal const int MonthsStart = 38;
    internal const int WeekdaysStart = 110;
    internal const int ErasStart = 166;
    internal const int DayPeriodsStart = 172;
    internal const int IntervalFallbackSlot = 178;
    internal const int SlotCount = 179;

    private const int AppendFieldCount = 11;
    private const int MonthWidthCount = 3;
    private const int WeekdayWidthCount = 4;

    private readonly string[] _slots;
    private readonly DateTimeSkeletonPattern[] _availableFormats;
    private readonly DateTimeSkeletonPattern[] _ownFormats;
    private readonly DateTimeIntervalPattern[] _intervalFormats;

    /// <param name="locale">The CLDR locale.</param>
    /// <param name="slots">Every slot, resolved.</param>
    /// <param name="availableFormats">Every availableFormats entry, resolved, in ordinal order of the skeleton.</param>
    /// <param name="parent">The locale's CLDR parent, or <see langword="null"/> for the root.</param>
    /// <param name="ownFormats">
    /// The entries the locale holds itself, in ordinal order of the skeleton: new, or different from its parent's.
    /// For the root, every entry.
    /// </param>
    /// <param name="intervalFormats">Every interval pattern, resolved, in ordinal order of the skeleton and then the field.</param>
    internal DateTimePatternLocale(
        string locale,
        string[] slots,
        DateTimeSkeletonPattern[] availableFormats,
        DateTimePatternLocale? parent,
        DateTimeSkeletonPattern[] ownFormats,
        DateTimeIntervalPattern[] intervalFormats)
    {
        Locale = locale;
        _slots = slots;
        _availableFormats = availableFormats;
        Parent = parent;
        _ownFormats = ownFormats;
        _intervalFormats = intervalFormats;
    }

    /// <summary>
    /// The CLDR locale the data belongs to, as cldr-json names it (<c>de-AT</c>, <c>zh-Hant-HK</c>, <c>und</c>).
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The locale this one inherits from under CLDR's parent-locale rules, or <see langword="null"/> for the root.
    /// </summary>
    internal DateTimePatternLocale? Parent { get; }

    /// <summary>
    /// The <c>availableFormats</c> skeletons and their patterns, in ordinal order of the skeleton, without the
    /// skeletons with a quarter or week field and without CLDR's <c>-count-</c> and <c>-alt-</c> variants.
    /// </summary>
    internal ReadOnlySpan<DateTimeSkeletonPattern> AvailableFormats => _availableFormats;

    /// <summary>
    /// The <c>availableFormats</c> entries this locale holds itself rather than inherits — new, or different from
    /// its <see cref="Parent"/>'s — in ordinal order of the skeleton; for the root, all of them.
    /// </summary>
    /// <remarks>
    /// ICU adds a locale's own entries before its parent's, and its format matcher gives a tie to whichever it added
    /// first, so the order a locale's entries were inherited in is part of what it formats with.
    /// </remarks>
    internal ReadOnlySpan<DateTimeSkeletonPattern> OwnFormats => _ownFormats;

    internal bool TryGetAvailableFormat(string skeleton, [NotNullWhen(true)] out string? pattern)
    {
        var low = 0;
        var high = _availableFormats.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var comparison = string.CompareOrdinal(_availableFormats[middle].Skeleton, skeleton);
            if (comparison == 0)
            {
                pattern = _availableFormats[middle].Pattern;
                return true;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        pattern = null;
        return false;
    }

    /// <summary>
    /// CLDR's <c>intervalFormats</c>, one pattern per skeleton and greatest-difference field, in ordinal order of the
    /// skeleton and then the field: what <c>formatRange</c> writes two dates with.
    /// </summary>
    /// <remarks>
    /// Each (skeleton, field) is the one ICU's <c>DateIntervalInfo</c> reads: the nearest locale of the CLDR parent chain
    /// that has a pattern for it, and CLDR's <c>B</c> before its <c>a</c> where one locale has both. The generator
    /// resolves this (<c>tools/cldr-dates/Jint.CldrDates.Generator/CldrLocale.cs</c>).
    /// </remarks>
    internal ReadOnlySpan<DateTimeIntervalPattern> IntervalFormats => _intervalFormats;

    /// <summary>
    /// CLDR's <c>intervalFormatFallback</c>: the text two whole dates are joined with when no interval pattern fits,
    /// <c>{0}</c> the start and <c>{1}</c> the end.
    /// </summary>
    internal string IntervalFormatFallback => _slots[IntervalFallbackSlot];

    /// <summary>
    /// CLDR's <c>dateTimeFormats</c>: how a date (<c>{1}</c>) and a time (<c>{0}</c>) are joined, by the width of the date.
    /// </summary>
    internal string GetDateTimeFormat(DateTimeStyleWidth width) => _slots[DateTimeFormatsStart + CheckStyleWidth(width)];

    /// <summary>
    /// CLDR's <c>dateTimeFormats-atTime</c>: the same join for a time that is a point on that date ("at", "um", "à").
    /// </summary>
    internal string GetAtTimeFormat(DateTimeStyleWidth width) => _slots[AtTimeFormatsStart + CheckStyleWidth(width)];

    internal string GetDateFormat(DateTimeStyleWidth width) => _slots[DateFormatsStart + CheckStyleWidth(width)];

    internal string GetTimeFormat(DateTimeStyleWidth width) => _slots[TimeFormatsStart + CheckStyleWidth(width)];

    /// <summary>
    /// CLDR's <c>appendItems</c> entry for a field the matched pattern lacks: <c>{0}</c> the pattern so far,
    /// <c>{1}</c> the field, <c>{2}</c> its <see cref="GetFieldDisplayName">display name</see>.
    /// </summary>
    internal string GetAppendItem(DateTimeAppendField field) => _slots[AppendItemsStart + CheckAppendField(field)];

    /// <summary>
    /// The field's wide display name from CLDR's <c>dateFields</c>, which is what ICU puts in an appendItems <c>{2}</c>.
    /// </summary>
    internal string GetFieldDisplayName(DateTimeAppendField field) => _slots[FieldDisplayNamesStart + CheckAppendField(field)];

    /// <summary>
    /// The twelve month names, January first. Months have no <see cref="DateTimeNameWidth.Short"/> width.
    /// </summary>
    internal ReadOnlySpan<string> GetMonthNames(DateTimeNameContext context, DateTimeNameWidth width)
    {
        return new ReadOnlySpan<string>(_slots, MonthsStart + (CheckContext(context) * MonthWidthCount + CheckWidth(width, MonthWidthCount)) * 12, 12);
    }

    /// <summary>
    /// The seven weekday names, Sunday first.
    /// </summary>
    internal ReadOnlySpan<string> GetWeekdayNames(DateTimeNameContext context, DateTimeNameWidth width)
    {
        return new ReadOnlySpan<string>(_slots, WeekdaysStart + (CheckContext(context) * WeekdayWidthCount + CheckWidth(width, WeekdayWidthCount)) * 7, 7);
    }

    /// <summary>
    /// The Gregorian eras, before the common era first: CLDR's <c>eraAbbr</c>, <c>eraNames</c> and <c>eraNarrow</c>.
    /// </summary>
    internal ReadOnlySpan<string> GetEraNames(DateTimeNameWidth width)
    {
        return new ReadOnlySpan<string>(_slots, ErasStart + CheckWidth(width, MonthWidthCount) * 2, 2);
    }

    /// <summary>
    /// The format-context <c>am</c> and <c>pm</c> day periods, in that order.
    /// </summary>
    internal ReadOnlySpan<string> GetDayPeriodNames(DateTimeNameWidth width)
    {
        return new ReadOnlySpan<string>(_slots, DayPeriodsStart + CheckWidth(width, MonthWidthCount) * 2, 2);
    }

    /// <summary>
    /// The locale's values with <paramref name="record"/> laid over them: what a derived locale is built from.
    /// </summary>
    internal DateTimePatternLocale Derive(string locale, in DateTimePatternRecord record)
    {
        var slots = (string[]) _slots.Clone();
        for (var i = 0; i < record.SlotIndexes.Length; i++)
        {
            slots[record.SlotIndexes[i]] = record.SlotValues[i];
        }

        return new DateTimePatternLocale(
            locale,
            slots,
            Merge(_availableFormats, record.SetFormats, record.RemovedSkeletons),
            this,
            record.SetFormats,
            MergeIntervals(_intervalFormats, record.SetIntervals, record.RemovedIntervals));
    }

    /// <summary>
    /// <see cref="Merge"/> for the interval patterns, keyed by skeleton and field.
    /// </summary>
    private static DateTimeIntervalPattern[] MergeIntervals(DateTimeIntervalPattern[] inherited, DateTimeIntervalPattern[] set, DateTimeIntervalPattern[] removed)
    {
        if (set.Length == 0 && removed.Length == 0)
        {
            return inherited;
        }

        var merged = new List<DateTimeIntervalPattern>(inherited.Length + set.Length);
        var i = 0;
        var j = 0;
        while (i < inherited.Length || j < set.Length)
        {
            int comparison;
            if (i == inherited.Length)
            {
                comparison = 1;
            }
            else if (j == set.Length)
            {
                comparison = -1;
            }
            else
            {
                comparison = DateTimeIntervalPattern.Compare(in inherited[i], in set[j]);
            }

            if (comparison < 0)
            {
                if (!IsRemoved(removed, in inherited[i]))
                {
                    merged.Add(inherited[i]);
                }

                i++;
            }
            else
            {
                merged.Add(set[j]);
                j++;
                if (comparison == 0)
                {
                    i++;
                }
            }
        }

        return merged.ToArray();

        static bool IsRemoved(DateTimeIntervalPattern[] removed, in DateTimeIntervalPattern entry)
        {
            foreach (var key in removed)
            {
                if (DateTimeIntervalPattern.Compare(in key, in entry) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Merges two skeleton-ordered lists: an entry of <paramref name="set"/> replaces the inherited one with the same
    /// skeleton, and an inherited skeleton in <paramref name="removed"/> is dropped.
    /// </summary>
    private static DateTimeSkeletonPattern[] Merge(DateTimeSkeletonPattern[] inherited, DateTimeSkeletonPattern[] set, string[] removed)
    {
        if (set.Length == 0 && removed.Length == 0)
        {
            return inherited;
        }

        var merged = new List<DateTimeSkeletonPattern>(inherited.Length + set.Length);
        var i = 0;
        var j = 0;
        while (i < inherited.Length || j < set.Length)
        {
            int comparison;
            if (i == inherited.Length)
            {
                comparison = 1;
            }
            else if (j == set.Length)
            {
                comparison = -1;
            }
            else
            {
                comparison = string.CompareOrdinal(inherited[i].Skeleton, set[j].Skeleton);
            }

            if (comparison < 0)
            {
                if (System.Array.IndexOf(removed, inherited[i].Skeleton) < 0)
                {
                    merged.Add(inherited[i]);
                }

                i++;
            }
            else
            {
                merged.Add(set[j]);
                j++;
                if (comparison == 0)
                {
                    i++;
                }
            }
        }

        return merged.ToArray();
    }

    private static int CheckStyleWidth(DateTimeStyleWidth width)
    {
        if ((uint) width > (uint) DateTimeStyleWidth.Short)
        {
            Throw.ArgumentOutOfRangeException(nameof(width), "Unknown style width.");
        }

        return (int) width;
    }

    private static int CheckAppendField(DateTimeAppendField field)
    {
        if ((uint) field >= AppendFieldCount)
        {
            Throw.ArgumentOutOfRangeException(nameof(field), "Unknown field.");
        }

        return (int) field;
    }

    private static int CheckContext(DateTimeNameContext context)
    {
        if ((uint) context > (uint) DateTimeNameContext.StandAlone)
        {
            Throw.ArgumentOutOfRangeException(nameof(context), "Unknown context.");
        }

        return (int) context;
    }

    private static int CheckWidth(DateTimeNameWidth width, int widthCount)
    {
        if ((uint) width >= (uint) widthCount)
        {
            Throw.ArgumentOutOfRangeException(nameof(width), "CLDR has no names of that width here.");
        }

        return (int) width;
    }
}

/// <summary>
/// An <c>availableFormats</c> entry: a skeleton (the fields, in canonical order) and the locale's pattern for it.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct DateTimeSkeletonPattern
{
    internal DateTimeSkeletonPattern(string skeleton, string pattern)
    {
        Skeleton = skeleton;
        Pattern = pattern;
    }

    internal string Skeleton { get; }

    internal string Pattern { get; }
}

/// <summary>
/// An <c>intervalFormats</c> entry: a skeleton, the field that differs between the two dates (<c>G</c> era, <c>y</c>
/// year, <c>M</c> month, <c>d</c> day, <c>a</c> am/pm, <c>h</c> hour, <c>m</c> minute) and the pattern writing both.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct DateTimeIntervalPattern
{
    internal DateTimeIntervalPattern(string skeleton, char field, string pattern)
    {
        Skeleton = skeleton;
        Field = field;
        Pattern = pattern;
    }

    internal string Skeleton { get; }

    internal char Field { get; }

    /// <summary>The pattern; empty for an entry a record removes.</summary>
    internal string Pattern { get; }

    /// <summary>The order the resource keeps them in: ordinal by skeleton, then by field.</summary>
    internal static int Compare(in DateTimeIntervalPattern x, in DateTimeIntervalPattern y)
    {
        var bySkeleton = string.CompareOrdinal(x.Skeleton, y.Skeleton);
        return bySkeleton != 0 ? bySkeleton : x.Field.CompareTo(y.Field);
    }
}

/// <summary>
/// What one record of <c>DateTimePatterns.bin</c> holds: the slots that differ from the locale's CLDR parent (all of
/// them, for the root), the <c>availableFormats</c> entries that are new or differ, and the skeletons it drops; and the
/// same two lists for the interval patterns.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct DateTimePatternRecord
{
    internal DateTimePatternRecord(
        byte[] slotIndexes,
        string[] slotValues,
        DateTimeSkeletonPattern[] setFormats,
        string[] removedSkeletons,
        DateTimeIntervalPattern[] setIntervals,
        DateTimeIntervalPattern[] removedIntervals)
    {
        SlotIndexes = slotIndexes;
        SlotValues = slotValues;
        SetFormats = setFormats;
        RemovedSkeletons = removedSkeletons;
        SetIntervals = setIntervals;
        RemovedIntervals = removedIntervals;
    }

    internal byte[] SlotIndexes { get; }

    internal string[] SlotValues { get; }

    internal DateTimeSkeletonPattern[] SetFormats { get; }

    internal string[] RemovedSkeletons { get; }

    internal DateTimeIntervalPattern[] SetIntervals { get; }

    /// <summary>The (skeleton, field) keys the locale drops; their <see cref="DateTimeIntervalPattern.Pattern"/> is empty.</summary>
    internal DateTimeIntervalPattern[] RemovedIntervals { get; }
}

/// <summary>
/// CLDR's four style widths, which <c>dateFormats</c>, <c>timeFormats</c> and <c>dateTimeFormats</c> are keyed by.
/// </summary>
internal enum DateTimeStyleWidth
{
    Full,
    Long,
    Medium,
    Short,
}

/// <summary>
/// CLDR's name widths. <see cref="Short"/> exists for weekdays only.
/// </summary>
internal enum DateTimeNameWidth
{
    Abbreviated,
    Wide,
    Narrow,
    Short,
}

/// <summary>
/// CLDR's name contexts: <see cref="Format"/> inside a pattern (<c>MMMM</c>, <c>EEEE</c>) and
/// <see cref="StandAlone"/> on its own (<c>LLLL</c>, <c>cccc</c>).
/// </summary>
internal enum DateTimeNameContext
{
    Format,
    StandAlone,
}

/// <summary>
/// The fields CLDR's <c>appendItems</c> are keyed by, in the order of ICU's <c>UDateTimePatternField</c>.
/// </summary>
internal enum DateTimeAppendField
{
    Era,
    Year,
    Quarter,
    Month,
    Week,
    Weekday,
    Day,
    Hour,
    Minute,
    Second,
    Zone,
}
