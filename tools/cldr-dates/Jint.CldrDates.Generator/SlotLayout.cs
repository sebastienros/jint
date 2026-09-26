namespace Jint.CldrDates.Generator;

/// <summary>
/// The fixed-position values every locale record carries, in the order <c>DateTimePatternData</c> in
/// <c>Jint/Native/Intl/Data</c> reads them.
/// </summary>
/// <remarks>
/// Each name is the value's JSON path: under <c>dates/fields</c> of <c>dateFields.json</c> when it starts with
/// <c>fields/</c>, under <c>dates/calendars/gregorian</c> of <c>ca-gregorian.json</c> otherwise. The names are
/// written into the resource's index, and <c>Jint.Tests</c> compares them with the loader's own layout, so the
/// two cannot drift apart silently: change this table and the loader's in the same pull request.
/// </remarks>
internal static class SlotLayout
{
    internal static readonly string[] StyleWidths = ["full", "long", "medium", "short"];

    /// <summary>
    /// CLDR's appendItems keys, in the order of ICU's <c>UDateTimePatternField</c> they apply to.
    /// </summary>
    internal static readonly string[] AppendItemKeys = ["Era", "Year", "Quarter", "Month", "Week", "Day-Of-Week", "Day", "Hour", "Minute", "Second", "Timezone"];

    /// <summary>
    /// The <c>dateFields.json</c> field whose display name is <c>{2}</c> in the appendItems entry at the same position.
    /// </summary>
    internal static readonly string[] FieldKeys = ["era", "year", "quarter", "month", "week", "weekday", "day", "hour", "minute", "second", "zone"];

    internal static readonly string[] Contexts = ["format", "stand-alone"];
    internal static readonly string[] MonthWidths = ["abbreviated", "wide", "narrow"];
    internal static readonly string[] DayWidths = ["abbreviated", "wide", "narrow", "short"];
    internal static readonly string[] Days = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
    internal static readonly string[] EraWidths = ["eraAbbr", "eraNames", "eraNarrow"];
    internal static readonly string[] DayPeriodWidths = ["abbreviated", "wide", "narrow"];

    internal static readonly string[] Names = BuildNames();

    /// <summary>
    /// How a slot's value is checked: a glue pattern joins a date and a time, an append pattern adds a missing field.
    /// </summary>
    internal static SlotKind KindOf(int slot)
    {
        var name = Names[slot];
        if (name.StartsWith("dateTimeFormats/appendItems/", StringComparison.Ordinal))
        {
            return SlotKind.AppendPattern;
        }

        if (name.StartsWith("dateTimeFormats", StringComparison.Ordinal))
        {
            return SlotKind.GluePattern;
        }

        if (name.StartsWith("dateFormats/", StringComparison.Ordinal) || name.StartsWith("timeFormats/", StringComparison.Ordinal))
        {
            return SlotKind.Pattern;
        }

        return SlotKind.Name;
    }

    private static string[] BuildNames()
    {
        var names = new List<string>();
        foreach (var width in StyleWidths)
        {
            names.Add("dateTimeFormats/" + width);
        }

        foreach (var width in StyleWidths)
        {
            names.Add("dateTimeFormats-atTime/standard/" + width);
        }

        foreach (var width in StyleWidths)
        {
            names.Add("dateFormats/" + width);
        }

        foreach (var width in StyleWidths)
        {
            names.Add("timeFormats/" + width);
        }

        foreach (var key in AppendItemKeys)
        {
            names.Add("dateTimeFormats/appendItems/" + key);
        }

        foreach (var key in FieldKeys)
        {
            names.Add("fields/" + key + "/displayName");
        }

        foreach (var context in Contexts)
        {
            foreach (var width in MonthWidths)
            {
                for (var month = 1; month <= 12; month++)
                {
                    names.Add($"months/{context}/{width}/{month}");
                }
            }
        }

        foreach (var context in Contexts)
        {
            foreach (var width in DayWidths)
            {
                foreach (var day in Days)
                {
                    names.Add($"days/{context}/{width}/{day}");
                }
            }
        }

        foreach (var width in EraWidths)
        {
            names.Add($"eras/{width}/0");
            names.Add($"eras/{width}/1");
        }

        foreach (var width in DayPeriodWidths)
        {
            names.Add($"dayPeriods/format/{width}/am");
            names.Add($"dayPeriods/format/{width}/pm");
        }

        return [.. names];
    }
}

internal enum SlotKind
{
    Name,
    Pattern,
    GluePattern,
    AppendPattern,
}
