using System.Text.Json;

namespace Jint.CldrDates.Generator;

/// <summary>
/// A calendar other than the Gregorian one whose CLDR data <c>DateTimeCalendarPatterns.bin</c> carries.
/// </summary>
/// <param name="CldrName">CLDR's name for it, the <c>ca-&lt;name&gt;.json</c> it is read from.</param>
/// <param name="Package">The <c>cldr-cal-*-full</c> package <c>pin.json</c> pins it in.</param>
/// <param name="Identifiers">The ECMA-402 calendar identifiers it serves.</param>
/// <param name="MonthCount">
/// How many month names it has, or 0 for a calendar that writes the Gregorian months (<c>buddhist</c>, <c>japanese</c>,
/// <c>roc</c>), whose names are the Gregorian data's. 14 is the Hebrew calendar's 13 and Adar II.
/// </param>
/// <param name="EraKeys">The CLDR era keys kept, in order: the ones an engine date can fall in.</param>
/// <param name="SameAs">
/// Other CLDR calendars of the package that must be this one's data in every locale and that no locale's XML defines
/// anything for, so that one copy serves them.
/// </param>
/// <param name="Chain">
/// The CLDR calendars ICU looks a style pattern or an availableFormats entry up in, in turn, before the generic calendar
/// its root aliases the last of them to: the calendar itself, and for <c>ethiopic-amete-alem</c> the Ethiopic calendar
/// its root aliases it to.
/// </param>
/// <param name="AtTimeCalendar">
/// The CLDR calendar whose <c>DateTimePatterns%atTime</c> the identifiers read, which the root aliases to nothing: for the
/// Islamic data, the <c>islamic-civil</c> Jint resolves every Islamic identifier to.
/// </param>
internal sealed record CalendarSpec(string CldrName, string Package, string[] Identifiers, int MonthCount, string[] EraKeys, string[] SameAs, string[] Chain, string AtTimeCalendar)
{
    /// <summary>
    /// Every calendar the resource carries, in the order its records are written. <c>chinese</c> and <c>dangi</c> are
    /// not among them: they write a related year and a year name, which <c>Intl.DateTimeFormat</c> writes in a lane
    /// of its own.
    /// </summary>
    /// <remarks>
    /// The Japanese calendar keeps the five eras from Meiji on (CLDR's 232 to 236): <c>Intl.DateTimeFormat</c> dates
    /// those, and writes an earlier date in its Gregorian year. CLDR has 237, most of them with a locale's own name.
    /// </remarks>
    internal static readonly CalendarSpec[] All =
    [
        new("buddhist", "cldr-cal-buddhist-full", ["buddhist"], 0, ["0"], [], ["buddhist"], "buddhist"),
        new("japanese", "cldr-cal-japanese-full", ["japanese"], 0, ["232", "233", "234", "235", "236"], [], ["japanese"], "japanese"),
        new("roc", "cldr-cal-roc-full", ["roc"], 0, ["0", "1"], [], ["roc"], "roc"),
        new("hebrew", "cldr-cal-hebrew-full", ["hebrew"], 14, ["0"], [], ["hebrew"], "hebrew"),
        new("persian", "cldr-cal-persian-full", ["persian"], 12, ["0"], [], ["persian"], "persian"),
        new("indian", "cldr-cal-indian-full", ["indian"], 12, ["0"], [], ["indian"], "indian"),
        new("coptic", "cldr-cal-coptic-full", ["coptic"], 13, ["1"], [], ["coptic"], "coptic"),
        new("ethiopic", "cldr-cal-ethiopic-full", ["ethiopic"], 13, ["0", "1"], [], ["ethiopic"], "ethiopic"),
        new("ethiopic-amete-alem", "cldr-cal-ethiopic-full", ["ethioaa"], 13, ["0"], [], ["ethiopic-amete-alem", "ethiopic"], "ethiopic-amete-alem"),
        new("islamic", "cldr-cal-islamic-full", ["islamic", "islamic-civil", "islamic-rgsa", "islamic-tbla", "islamic-umalqura"], 12, ["0", "1"],
            ["islamic-civil", "islamic-rgsa", "islamic-tbla", "islamic-umalqura"], ["islamic"], "islamic-civil"),
    ];

    /// <summary>
    /// CLDR's keys for the months, in the order the slots hold them: <c>1</c> to <c>13</c>, and for the Hebrew calendar
    /// <c>7-yeartype-leap</c>, Adar II, last.
    /// </summary>
    internal string[] MonthKeys => MonthCount switch
    {
        0 => [],
        14 => [.. Enumerable.Range(1, 13).Select(m => m.ToString(System.Globalization.CultureInfo.InvariantCulture)), "7-yeartype-leap"],
        _ => [.. Enumerable.Range(1, MonthCount).Select(m => m.ToString(System.Globalization.CultureInfo.InvariantCulture))],
    };

    /// <summary>
    /// The fixed-position values each locale's record of this calendar carries, by their JSON path under
    /// <c>dates/calendars/&lt;name&gt;</c>; the loader reads them by position and checks the names.
    /// </summary>
    /// <remarks>
    /// The time formats and the atTime patterns are not cldr-json's resolution of the element but what ICU reads
    /// (<see cref="CalendarExtraction"/>). The <c>dateTimeFormats</c> joining patterns are not here at all: ICU reads the
    /// Gregorian ones for an interval, and nothing else reads them.
    /// </remarks>
    internal string[] SlotNames
    {
        get
        {
            var names = new List<string>();
            foreach (var width in SlotLayout.StyleWidths)
            {
                names.Add("dateFormats/" + width);
            }

            foreach (var width in SlotLayout.StyleWidths)
            {
                names.Add("timeFormats/" + width);
            }

            foreach (var width in SlotLayout.StyleWidths)
            {
                names.Add("dateTimeFormats-atTime/standard/" + width);
            }

            foreach (var key in SlotLayout.AppendItemKeys)
            {
                names.Add("dateTimeFormats/appendItems/" + key);
            }

            names.Add(SlotLayout.IntervalFallback);
            foreach (var context in SlotLayout.Contexts)
            {
                foreach (var width in SlotLayout.MonthWidths)
                {
                    foreach (var key in MonthKeys)
                    {
                        names.Add($"months/{context}/{width}/{key}");
                    }
                }
            }

            foreach (var width in SlotLayout.EraWidths)
            {
                foreach (var key in EraKeys)
                {
                    names.Add($"eras/{width}/{key}");
                }
            }

            return [.. names];
        }
    }

    /// <summary>The first slot of the atTime patterns, the appendItems, the fallback, the months and the eras.</summary>
    internal const int AtTimeStart = 8;
    internal const int AppendItemsStart = 12;
    internal const int IntervalFallbackSlot = 23;
    internal const int MonthsStart = 24;
}

/// <summary>
/// One calendar's data, every locale of it: what <see cref="CalendarPatternFile"/> writes.
/// </summary>
/// <param name="Spec">The calendar.</param>
/// <param name="Locales">Each locale's record, in the order of <see cref="CldrExtraction.LocaleIds"/>; the slots are <see cref="CalendarSpec.SlotNames"/>.</param>
/// <param name="OwnKeys">
/// Per locale, the availableFormats skeletons its CLDR XML defines for the calendar itself (or the calendar of
/// <see cref="CalendarSpec.Chain"/> that has any), in ordinal order.
/// </param>
internal sealed record CalendarData(CalendarSpec Spec, List<CldrLocale> Locales, Dictionary<string, string[]> OwnKeys);

/// <summary>
/// Reads each locale of each <see cref="CalendarSpec"/> out of its <c>cldr-cal-*-full</c> package and checks it, failing
/// the run with every problem at once, as <see cref="CldrExtraction"/> does for the Gregorian calendar.
/// </summary>
/// <remarks>
/// <para>
/// cldr-json resolves each element of a calendar through CLDR's inheritance, which is what ICU reads for the
/// availableFormats, the appendItems, the interval formats and the names. Three things ICU decides by which locale
/// defines a value (icu4c <c>dtptngen.cpp</c> and <c>smpdtfmt.cpp</c>), and the generator reads those from CLDR's XML
/// (<see cref="CldrXml"/>):
/// </para>
/// <list type="bullet">
/// <item>The style patterns are one array per calendar, <c>calendar/&lt;name&gt;/DateTimePatterns</c>, which the root
/// aliases to the generic calendar's. A locale has the array only where it defines the calendar's (or the generic
/// calendar's) date, time or joining patterns itself, and the array carries that locale's time patterns: <c>en-GB</c>'s
/// Buddhist time styles are <c>en-001</c>'s <c>h:mm a</c>, not the <c>HH:mm</c> cldr-json resolves the element to. The
/// date and time formats kept are those of that locale (<see cref="StyleOwner"/>).</item>
/// <item>A date and a time are joined with <c>calendar/&lt;name&gt;/DateTimePatterns%atTime</c>, which the root aliases
/// to nothing; failing that with the Gregorian <c>%atTime</c>, which the root does not have either; and failing both with
/// the calendar's own joining patterns (<see cref="JoiningPatterns"/>).</item>
/// <item>ICU adds a calendar's availableFormats entries walking every locale of the chain for the calendar's own, and
/// only then, through the root's alias, the generic calendar's; the order decides the format matcher's ties. The
/// skeletons each locale defines for the calendar itself are recorded (<see cref="CalendarData.OwnKeys"/>).</item>
/// </list>
/// </remarks>
internal static class CalendarExtraction
{
    /// <summary>
    /// CLDR 48.2 calendar patterns carrying a numbering-system override (<c>_numbers</c>), by locale, calendar (null for
    /// every calendar) and path. As in the Gregorian data the pattern is kept and the override is not, so these are written
    /// in the formatter's own digits where ICU writes Hebrew numerals, Japanese's 元年 or Hawaiian's Roman months. A locale
    /// inheriting a listed pattern (<c>he-IL</c>) is covered by its parent's entry; a new one fails the run, and so does an
    /// entry that no longer occurs.
    /// </summary>
    internal static readonly (string Locale, string? Calendar, string Path, string Numbers)[] KnownNumberingOverrides =
    [
        ("haw", null, "dateFormats/short", "M=romanlow"), // the generic calendar's, as in the Gregorian data
        ("he", "hebrew", "dateFormats/full", "hebr"),
        ("he", "hebrew", "dateFormats/long", "hebr"),
        ("he", "hebrew", "dateFormats/medium", "hebr"),
        ("he", "hebrew", "dateFormats/short", "hebr"),
        ("ja", "japanese", "dateFormats/full", "y=jpanyear"), // 元年 for a first year
        ("ja", "japanese", "dateFormats/long", "y=jpanyear"),
        ("ja", "japanese", "dateFormats/medium", "y=jpanyear"),
        ("yi", "hebrew", "dateFormats/full", "hebr"),
        ("yi", "hebrew", "dateFormats/long", "hebr"),
        ("yi", "hebrew", "dateFormats/medium", "hebr"),
        ("yi", "hebrew", "dateFormats/short", "hebr"),
    ];

    /// <summary>
    /// CLDR 48.2 calendar patterns with a letter outside <see cref="CldrExtraction.AllowedLetters"/>, by locale, calendar
    /// (null for every calendar) and path; an availableFormats entry's skeleton is checked under the entry's path. They are
    /// the generic calendar's, which every calendar here inherits, and are kept as CLDR writes them: the formatter writes
    /// <c>Y</c> as the year, <c>Z</c> as a zone name and <c>e</c> as a weekday. A new one fails the run, and so does an
    /// entry that no longer occurs.
    /// </summary>
    internal static readonly (string Locale, string? Calendar, string Path, string Letters)[] KnownLetterExceptions =
    [
        ("ak", null, "intervalFormats/yMMM/M", "Y"), // "MMM – MMM Y G": the week-numbering year
        ("ak", null, "intervalFormats/yMMM/y", "Y"),
        ("fa", null, "availableFormats/HHmmZ", "Z"), // "HH:mm (Z)", as in the Gregorian data
        ("fa", "islamic", "availableFormats/HmZ", "Z"),
        ("fa-AF", null, "availableFormats/HHmmZ", "Z"), // inherited from fa
        ("fa-AF", "islamic", "availableFormats/HmZ", "Z"),
        ("gl", null, "dateFormats/full", "Y"), // "EEEE, d 'de' MMMM 'de' Y G"
        ("ko", null, "intervalFormats/GyMMMEd/d", "e"), // "G y년 M월 d일 e요일 – M월 d일 e요일"
        ("ko-CN", null, "intervalFormats/GyMMMEd/d", "e"), // inherited from ko
        ("ko-KP", null, "intervalFormats/GyMMMEd/d", "e"),
        ("kok", null, "intervalFormats/GyMEd/d", "Y"), // "E, d-M-Y GGGGG – E-d-M-y"
        ("kok", null, "intervalFormats/GyMd/G", "Y"),
        ("kok-Deva", null, "intervalFormats/GyMEd/d", "Y"), // inherited from kok
        ("kok-Deva", null, "intervalFormats/GyMd/G", "Y"),
        ("ksh", null, "availableFormats/yM", "Y"), // "Y-MM", as in the Gregorian data
        ("my", null, "availableFormats/yyyyMd", "Y"), // "GGGGG dd/MM/Y"
        ("te", null, "availableFormats/yyyyMMMM", "Y"), // "MMMM YYYY"
    ];

    /// <summary>
    /// CLDR 48.2 calendar interval patterns in which no field letter repeats, as <see cref="CldrExtraction.KnownUnsplittableIntervals"/>
    /// lists the Gregorian ones: <c>fa</c>'s stand-alone month before the dash and format month after it.
    /// </summary>
    internal static readonly (string Locale, string? Calendar, string Path)[] KnownUnsplittableIntervals =
    [
        ("fa", null, "intervalFormats/GyMMM/M"), // as in the Gregorian data
        ("fa", null, "intervalFormats/yMMM/M"),
        ("fa", null, "intervalFormats/yMMMM/M"),
        ("fa-AF", null, "intervalFormats/GyMMM/M"), // inherited from fa
        ("fa-AF", null, "intervalFormats/yMMM/M"),
        ("fa-AF", null, "intervalFormats/yMMMM/M"),
    ];

    internal static List<CalendarData> ExtractAll(Inputs inputs, List<string> ids, ParentLocales parents)
    {
        var problems = new List<string>();
        var seen = new Seen();
        var gregorian = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var documents = new List<JsonDocument>();
        try
        {
            foreach (var id in ids)
            {
                var document = JsonDocument.Parse(inputs.DatesFiles[$"package/main/{id}/ca-gregorian.json"]);
                documents.Add(document);
                gregorian.Add(id, document.RootElement.GetProperty("main").GetProperty(id).GetProperty("dates").GetProperty("calendars").GetProperty("gregorian"));
            }

            var defined = CldrXml.Read(inputs, ids, problems);
            var result = new List<CalendarData>(CalendarSpec.All.Length);
            foreach (var spec in CalendarSpec.All)
            {
                CheckDefinitions(spec, ids, defined, problems);
                var nodes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var id in ids)
                {
                    var key = $"{spec.Package}/package/main/{id}/ca-{spec.CldrName}.json";
                    if (!inputs.CalendarFiles.TryGetValue(key, out var bytes))
                    {
                        problems.Add($"{id}: {spec.Package} has no ca-{spec.CldrName}.json");
                        continue;
                    }

                    var document = JsonDocument.Parse(bytes);
                    documents.Add(document);
                    nodes.Add(id, document.RootElement.GetProperty("main").GetProperty(id).GetProperty("dates").GetProperty("calendars").GetProperty(spec.CldrName));
                    foreach (var same in spec.SameAs)
                    {
                        var sameKey = $"{spec.Package}/package/main/{id}/ca-{same}.json";
                        using var other = JsonDocument.Parse(inputs.CalendarFiles[sameKey]);
                        var otherNode = other.RootElement.GetProperty("main").GetProperty(id).GetProperty("dates").GetProperty("calendars").GetProperty(same);
                        if (!string.Equals(otherNode.GetRawText(), nodes[id].GetRawText(), StringComparison.Ordinal))
                        {
                            problems.Add($"{id}: ca-{same}.json is not ca-{spec.CldrName}.json; the resource stores one {spec.CldrName} calendar for {string.Join(", ", spec.Identifiers)}");
                        }
                    }
                }

                if (nodes.Count != ids.Count)
                {
                    continue;
                }

                result.Add(Extract(spec, ids, nodes, gregorian, defined, parents, problems, seen));
            }

            for (var i = 0; i < KnownNumberingOverrides.Length; i++)
            {
                if (!seen.Overrides.Contains(i))
                {
                    problems.Add($"CalendarExtraction.KnownNumberingOverrides lists {KnownNumberingOverrides[i]}, which no longer occurs: remove the entry");
                }
            }

            for (var i = 0; i < KnownLetterExceptions.Length; i++)
            {
                if (!seen.Letters.Contains(i))
                {
                    problems.Add($"CalendarExtraction.KnownLetterExceptions lists {KnownLetterExceptions[i]}, which no longer occurs: remove the entry");
                }
            }

            for (var i = 0; i < KnownUnsplittableIntervals.Length; i++)
            {
                if (!seen.Unsplittable.Contains(i))
                {
                    problems.Add($"CalendarExtraction.KnownUnsplittableIntervals lists {KnownUnsplittableIntervals[i]}, which now splits: remove the entry");
                }
            }

            if (problems.Count > 0)
            {
                throw new InvalidDataException("The CLDR calendar data failed validation:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  " + p)));
            }

            return result;
        }
        finally
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }
        }
    }

    private static CalendarData Extract(
        CalendarSpec spec,
        List<string> ids,
        Dictionary<string, JsonElement> nodes,
        Dictionary<string, JsonElement> gregorian,
        Dictionary<string, Dictionary<string, ExplicitCalendar>> defined,
        ParentLocales parents,
        List<string> problems,
        Seen seen)
    {
        var slotNames = spec.SlotNames;
        var locales = new List<CldrLocale>(ids.Count);
        var formatsById = new Dictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var node = nodes[id];
            var owner = nodes[StyleOwner(id, spec, defined, parents)];
            var joining = JoiningPatterns(id, spec, node, owner, gregorian[id], defined, parents);
            var slots = new string[slotNames.Length];
            for (var slot = 0; slot < slots.Length; slot++)
            {
                var name = slotNames[slot];
                JsonElement? value;
                if (name.StartsWith("timeFormats/", StringComparison.Ordinal) || name.StartsWith("dateFormats/", StringComparison.Ordinal))
                {
                    value = Navigate(owner, name);
                }
                else if (name.StartsWith("dateTimeFormats-atTime/", StringComparison.Ordinal))
                {
                    var width = name["dateTimeFormats-atTime/standard/".Length..];
                    value = Navigate(joining.Node, joining.AtTime ? name : "dateTimeFormats/" + width) ?? Navigate(joining.Node, "dateTimeFormats/" + width);
                }
                else
                {
                    value = Navigate(node, name);
                }

                if (value is not { } present)
                {
                    problems.Add($"{id} {spec.CldrName}: {name} is missing");
                    slots[slot] = "";
                    continue;
                }

                slots[slot] = ReadString(id, spec, name, present, problems, seen);
                CheckSlot(id, spec, name, slots[slot], problems, seen);
            }

            var formats = ReadAvailableFormats(id, spec, node, problems, seen);
            formatsById.Add(id, formats);
            locales.Add(new CldrLocale { Id = id, Slots = slots, Formats = formats, RawIntervals = ReadIntervals(id, spec, node, problems, seen) });
        }

        IntervalResolution.Resolve(locales, parents);

        // ICU adds a calendar's availableFormats walking its own entries up the whole chain, and only then (through the
        // root's alias) the generic calendar's; the skeletons each locale defines for the calendar itself are recorded,
        // so the loader can add them in that order.
        var ownKeys = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            ownKeys.Add(id, [.. spec.Chain
                .SelectMany(calendar => defined[id].TryGetValue(calendar, out var data) ? data.AvailableFormats : [])
                .Where(skeleton => formatsById[id].ContainsKey(skeleton))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)]);
        }

        return new CalendarData(spec, locales, ownKeys);
    }

    /// <summary>
    /// The locale whose <c>calendar/&lt;name&gt;/DateTimePatterns</c> ICU reads for <paramref name="id"/>'s style
    /// patterns: the nearest of it and its ancestors that defines the calendar's date, time or joining patterns, each
    /// calendar of <see cref="CalendarSpec.Chain"/> in turn, and failing those the nearest that defines the generic
    /// calendar's, the root last.
    /// </summary>
    private static string StyleOwner(string id, CalendarSpec spec, Dictionary<string, Dictionary<string, ExplicitCalendar>> defined, ParentLocales parents)
    {
        foreach (var calendar in spec.Chain.Append("generic"))
        {
            for (var locale = id; locale is not null; locale = parents.ParentOf(locale))
            {
                if (defined[locale].TryGetValue(calendar, out var data) && data.StylePatterns)
                {
                    return locale;
                }
            }
        }

        return ParentLocales.Root;
    }

    /// <summary>
    /// Where ICU's joining patterns for the calendar come from (icu4c <c>dtptngen.cpp</c>'s <c>setDateTimeFromCalendar</c>
    /// and <c>smpdtfmt.cpp</c>'s <c>construct</c>): the <c>DateTimePatterns%atTime</c> of
    /// <see cref="CalendarSpec.AtTimeCalendar"/> where a locale of the chain defines it, which the root aliases to nothing;
    /// failing that the Gregorian <c>%atTime</c>, which the root does not have either; and failing both, the joining
    /// patterns of the calendar's own <c>DateTimePatterns</c>, <paramref name="owner"/>'s.
    /// </summary>
    private static (JsonElement Node, bool AtTime) JoiningPatterns(
        string id,
        CalendarSpec spec,
        JsonElement node,
        JsonElement owner,
        JsonElement gregorian,
        Dictionary<string, Dictionary<string, ExplicitCalendar>> defined,
        ParentLocales parents)
    {
        if (DefinesAtTime(spec.AtTimeCalendar))
        {
            return (node, true);
        }

        return DefinesAtTime("gregorian") ? (gregorian, true) : (owner, false);

        bool DefinesAtTime(string calendar)
        {
            for (var locale = id; locale is not null && !string.Equals(locale, ParentLocales.Root, StringComparison.Ordinal); locale = parents.ParentOf(locale))
            {
                if (defined[locale].TryGetValue(calendar, out var data) && data.AtTime)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A calendar of <see cref="CalendarSpec.SameAs"/> must define nothing anywhere, so that one copy of the data serves
    /// it; and at most one calendar of <see cref="CalendarSpec.Chain"/> may define availableFormats, so that the loader's
    /// order of the calendar's own entries, level by level, is ICU's.
    /// </summary>
    private static void CheckDefinitions(CalendarSpec spec, List<string> ids, Dictionary<string, Dictionary<string, ExplicitCalendar>> defined, List<string> problems)
    {
        foreach (var id in ids)
        {
            foreach (var same in spec.SameAs)
            {
                if (defined[id].TryGetValue(same, out var data) && (data.StylePatterns || data.AtTime || data.AvailableFormats.Count > 0))
                {
                    problems.Add($"{id}: its XML defines {same} patterns of its own, which the resource stores one {spec.CldrName} calendar for");
                }
            }
        }

        var withFormats = spec.Chain.Where(calendar => ids.Any(id => defined[id].TryGetValue(calendar, out var data) && data.AvailableFormats.Count > 0)).ToList();
        if (withFormats.Count > 1)
        {
            problems.Add($"{spec.CldrName}: both {string.Join(" and ", withFormats)} define availableFormats, which the loader cannot order as ICU does");
        }
    }

    private static SortedDictionary<string, string> ReadAvailableFormats(string id, CalendarSpec spec, JsonElement node, List<string> problems, Seen seen)
    {
        var formats = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in node.GetProperty("dateTimeFormats").GetProperty("availableFormats").EnumerateObject())
        {
            if (CldrExtraction.IsSkippedSkeleton(entry.Name))
            {
                continue;
            }

            var path = "availableFormats/" + entry.Name;
            var pattern = ReadString(id, spec, path, entry.Value, problems, seen);
            CheckLetters(id, spec, path, entry.Name, problems, seen);
            CheckLetters(id, spec, path, pattern, problems, seen);

            formats.Add(entry.Name, pattern);
        }

        if (formats.Count == 0)
        {
            problems.Add($"{id} {spec.CldrName}: no availableFormats");
        }

        return formats;
    }

    private static SortedDictionary<string, SortedDictionary<char, string>> ReadIntervals(string id, CalendarSpec spec, JsonElement node, List<string> problems, Seen seen)
    {
        var intervals = new SortedDictionary<string, SortedDictionary<char, string>>(StringComparer.Ordinal);
        foreach (var entry in node.GetProperty("dateTimeFormats").GetProperty("intervalFormats").EnumerateObject())
        {
            if (string.Equals(entry.Name, "intervalFormatFallback", StringComparison.Ordinal))
            {
                continue;
            }

            var letters = new SortedDictionary<char, string>();
            foreach (var letter in entry.Value.EnumerateObject())
            {
                if (letter.Name.Contains("-alt-", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = "intervalFormats/" + entry.Name + "/" + letter.Name;
                if (letter.Name.Length != 1 || !IntervalResolution.FieldOfLetter.ContainsKey(letter.Name[0]))
                {
                    problems.Add($"{id} {spec.CldrName}: {name} is keyed by a letter ICU does not read");
                    continue;
                }

                var pattern = ReadString(id, spec, name, letter.Value, problems, seen);
                CheckLetters(id, spec, name, pattern, problems, seen);
                var split = CldrExtraction.SplitPoint(pattern);
                if (split <= 0 || split >= pattern.Length)
                {
                    var known = Find(KnownUnsplittableIntervals.Select(e => (e.Locale, e.Calendar, e.Path)).ToArray(), id, spec, name);
                    if (known >= 0)
                    {
                        seen.Unsplittable.Add(known);
                    }
                    else
                    {
                        problems.Add($"{id} {spec.CldrName}: {name} \"{pattern}\" writes no field twice, so it cannot be split into its two dates");
                    }
                }

                letters.Add(letter.Name[0], pattern);
            }

            intervals.Add(entry.Name, letters);
        }

        if (intervals.Count == 0)
        {
            problems.Add($"{id} {spec.CldrName}: no intervalFormats");
        }

        return intervals;
    }

    private static JsonElement? Navigate(JsonElement node, string path)
    {
        foreach (var part in path.Split('/'))
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out node))
            {
                return null;
            }
        }

        return node;
    }

    private static string ReadString(string id, CalendarSpec spec, string name, JsonElement value, List<string> problems, Seen seen)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("_value", out var inner) && inner.ValueKind == JsonValueKind.String)
        {
            if (value.TryGetProperty("_numbers", out var numbers))
            {
                var known = Find(KnownNumberingOverrides.Select(o => (o.Locale, o.Calendar, o.Path)).ToArray(), id, spec, name);
                if (known >= 0 && string.Equals(KnownNumberingOverrides[known].Numbers, numbers.GetString(), StringComparison.Ordinal))
                {
                    seen.Overrides.Add(known);
                }
                else
                {
                    problems.Add($"{id} {spec.CldrName}: {name} carries the numbering override {numbers.GetString()}, which CalendarExtraction.KnownNumberingOverrides does not list");
                }
            }

            return inner.GetString()!;
        }

        problems.Add($"{id} {spec.CldrName}: {name} is neither a string nor a {{ _value }} object");
        return "";
    }

    /// <summary>
    /// The entry of a known list for <paramref name="id"/>'s <paramref name="path"/> in <paramref name="spec"/>, or -1:
    /// one naming the locale or, for a locale that inherits it, the nearest ancestor by truncation (<c>he-IL</c>, <c>he</c>).
    /// </summary>
    private static int Find((string Locale, string? Calendar, string Path)[] known, string id, CalendarSpec spec, string path)
    {
        for (var locale = id; ; locale = ParentLocales.Truncate(locale))
        {
            var index = Array.FindIndex(known, e => e.Locale == locale && (e.Calendar is null || e.Calendar == spec.CldrName) && e.Path == path);
            if (index >= 0 || string.Equals(locale, ParentLocales.Root, StringComparison.Ordinal))
            {
                return index;
            }
        }
    }

    private static void CheckSlot(string id, CalendarSpec spec, string name, string value, List<string> problems, Seen seen)
    {
        if (value.Length == 0)
        {
            problems.Add($"{id} {spec.CldrName}: {name} is empty");
            return;
        }

        if (name.StartsWith("dateFormats/", StringComparison.Ordinal) || name.StartsWith("timeFormats/", StringComparison.Ordinal))
        {
            CheckLetters(id, spec, name, value, problems, seen);
        }
        else if (name.StartsWith("dateTimeFormats", StringComparison.Ordinal) && !string.Equals(name, SlotLayout.IntervalFallback, StringComparison.Ordinal))
        {
            if (!CldrExtraction.TryGetPatternLetters(value, out var letters) || letters.Length > 0 || !value.Contains("{0}", StringComparison.Ordinal) || !value.Contains("{1}", StringComparison.Ordinal))
            {
                problems.Add($"{id} {spec.CldrName}: {name} \"{value}\" does not place {{0}} and {{1}} around quoted text");
            }
        }
    }

    /// <summary>
    /// A pattern must parse and use only <see cref="CldrExtraction.AllowedLetters"/>, or the letters
    /// <see cref="KnownLetterExceptions"/> lists for its path.
    /// </summary>
    private static void CheckLetters(string id, CalendarSpec spec, string name, string pattern, List<string> problems, Seen seen)
    {
        if (!CldrExtraction.TryGetPatternLetters(pattern, out var letters))
        {
            problems.Add($"{id} {spec.CldrName}: {name} \"{pattern}\" has an unclosed quote");
            return;
        }

        foreach (var letter in letters)
        {
            if (CldrExtraction.AllowedLetters.Contains(letter, StringComparison.Ordinal))
            {
                continue;
            }

            var known = Find(KnownLetterExceptions.Select(e => (e.Locale, e.Calendar, e.Path)).ToArray(), id, spec, name);
            if (known >= 0 && KnownLetterExceptions[known].Letters.Contains(letter, StringComparison.Ordinal))
            {
                seen.Letters.Add(known);
                continue;
            }

            problems.Add($"{id} {spec.CldrName}: {name} \"{pattern}\" has the letter '{letter}'");
        }
    }

    /// <summary>Which entries of the known lists the data still needs.</summary>
    private sealed class Seen
    {
        internal HashSet<int> Overrides { get; } = [];

        internal HashSet<int> Letters { get; } = [];

        internal HashSet<int> Unsplittable { get; } = [];
    }
}
