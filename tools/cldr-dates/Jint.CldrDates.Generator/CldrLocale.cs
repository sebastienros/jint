using System.Text;
using System.Text.Json;

namespace Jint.CldrDates.Generator;

/// <summary>
/// One locale's data as cldr-json resolves it: the <see cref="SlotLayout"/> values and the availableFormats kept.
/// </summary>
internal sealed class CldrLocale
{
    internal required string Id { get; init; }

    internal required string[] Slots { get; init; }

    internal required SortedDictionary<string, string> Formats { get; init; }
}

/// <summary>
/// Reads each locale out of cldr-dates-full and checks it, failing the run with every problem at once rather than
/// emitting something subtly wrong.
/// </summary>
internal static class CldrExtraction
{
    /// <summary>
    /// The pattern letters ECMA-402's fields map to: era, year, month (format and stand-alone), day, weekday (format
    /// and stand-alone), the three day-period kinds, the four hour cycles, minute, second, fractional second and the
    /// time-zone names.
    /// </summary>
    internal const string AllowedLetters = "GyMLdEcabBhHKkmsSzvO";

    /// <summary>
    /// CLDR 48.2 availableFormats entries outside <see cref="AllowedLetters"/>. They are CLDR's own data and are kept
    /// as they are; listing them is what makes a new one fail the run instead of slipping through, and an entry that no
    /// longer occurs fails it too.
    /// </summary>
    internal static readonly (string Locale, string Skeleton, string Letters)[] KnownLetterExceptions =
    [
        ("de-CH", "GyMEd", "Y"), // "E, MM.dd.Y G": the week-numbering year
        ("fa", "HHmmZ", "Z"), // "HH:mm (Z)": the RFC 822 offset, in the skeleton as well
        ("fa-AF", "HHmmZ", "Z"), // inherited from fa
        ("gd", "yMMM", "Y"), // "LLL Y"
        ("ksh", "yM", "Y"), // "Y-MM"
        ("sc", "yM", "Y"), // "MM/Y"
    ];

    /// <summary>
    /// CLDR 48.2 patterns carrying a numbering-system override (<c>_numbers</c>). The pattern is kept and the override
    /// is not: the resource has nowhere to put it, and no caller reads it yet.
    /// </summary>
    internal static readonly (string Locale, string Slot, string Numbers)[] KnownNumberingOverrides =
    [
        ("haw", "dateFormats/short", "M=romanlow"),
    ];

    /// <summary>
    /// Skeletons with a quarter (<c>Q</c>) or week (<c>w</c>, <c>W</c>) field, which ECMA-402 has no option for, and
    /// CLDR's plural (<c>-count-</c>) and alternative (<c>-alt-</c>) variants are not kept.
    /// </summary>
    internal static bool IsSkippedSkeleton(string key)
    {
        return key.Contains("-count-", StringComparison.Ordinal)
               || key.Contains("-alt-", StringComparison.Ordinal)
               || key.AsSpan().IndexOfAny('Q', 'w', 'W') >= 0;
    }

    /// <summary>
    /// The locales cldr-dates-full carries, in ordinal order.
    /// </summary>
    internal static List<string> LocaleIds(Inputs inputs)
    {
        return inputs.DatesFiles.Keys
            .Where(k => k.EndsWith("/ca-gregorian.json", StringComparison.Ordinal))
            .Select(k => k.Split('/')[2])
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    internal static List<CldrLocale> ExtractAll(Inputs inputs, List<string> ids)
    {
        var problems = new List<string>();

        using (var available = inputs.CoreJson("availableLocales.json"))
        {
            var full = available.RootElement.GetProperty("availableLocales").GetProperty("full").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
            if (!full.SetEquals(ids))
            {
                problems.Add($"cldr-dates-full carries {ids.Count} locales and cldr-core's availableLocales.json lists {full.Count}; they must be the same set");
            }
        }

        var seenLetterExceptions = new HashSet<(string, string)>();
        var seenNumberingOverrides = new HashSet<(string, string)>();
        var locales = new List<CldrLocale>(ids.Count);
        foreach (var id in ids)
        {
            locales.Add(Extract(inputs, id, problems, seenLetterExceptions, seenNumberingOverrides));
        }

        foreach (var (locale, skeleton, _) in KnownLetterExceptions)
        {
            if (!seenLetterExceptions.Contains((locale, skeleton)))
            {
                problems.Add($"KnownLetterExceptions lists {locale} {skeleton}, which no longer needs it: remove the entry");
            }
        }

        foreach (var (locale, slot, _) in KnownNumberingOverrides)
        {
            if (!seenNumberingOverrides.Contains((locale, slot)))
            {
                problems.Add($"KnownNumberingOverrides lists {locale} {slot}, which no longer has one: remove the entry");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException("The CLDR data failed validation:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  " + p)));
        }

        return locales;
    }

    private static CldrLocale Extract(Inputs inputs, string id, List<string> problems, HashSet<(string, string)> seenLetterExceptions, HashSet<(string, string)> seenNumberingOverrides)
    {
        using var gregorianDocument = JsonDocument.Parse(inputs.DatesFiles[$"package/main/{id}/ca-gregorian.json"]);
        using var fieldsDocument = JsonDocument.Parse(inputs.DatesFiles[$"package/main/{id}/dateFields.json"]);
        var gregorian = LocaleNode(gregorianDocument, id, problems).GetProperty("dates").GetProperty("calendars").GetProperty("gregorian");
        var fields = LocaleNode(fieldsDocument, id, problems).GetProperty("dates").GetProperty("fields");

        var slots = new string[SlotLayout.Names.Length];
        for (var slot = 0; slot < slots.Length; slot++)
        {
            var name = SlotLayout.Names[slot];
            var node = name.StartsWith("fields/", StringComparison.Ordinal) ? Navigate(fields, name["fields/".Length..]) : Navigate(gregorian, name);
            if (node is not { } value)
            {
                problems.Add($"{id}: {name} is missing");
                slots[slot] = "";
                continue;
            }

            slots[slot] = ReadString(id, name, value, problems, seenNumberingOverrides);
            CheckSlot(id, slot, slots[slot], problems);
        }

        var formats = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in gregorian.GetProperty("dateTimeFormats").GetProperty("availableFormats").EnumerateObject())
        {
            if (IsSkippedSkeleton(entry.Name))
            {
                continue;
            }

            var pattern = ReadString(id, "availableFormats/" + entry.Name, entry.Value, problems, seenNumberingOverrides);
            CheckAvailableFormat(id, entry.Name, pattern, problems, seenLetterExceptions);
            formats.Add(entry.Name, pattern);
        }

        if (formats.Count == 0)
        {
            problems.Add($"{id}: no availableFormats");
        }

        return new CldrLocale { Id = id, Slots = slots, Formats = formats };
    }

    private static JsonElement LocaleNode(JsonDocument document, string id, List<string> problems)
    {
        var main = document.RootElement.GetProperty("main");
        if (!main.TryGetProperty(id, out var node))
        {
            problems.Add($"{id}: the file is keyed by another locale");
            node = main.EnumerateObject().First().Value;
        }

        return node;
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

    private static string ReadString(string id, string name, JsonElement value, List<string> problems, HashSet<(string, string)> seenNumberingOverrides)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("_value", out var inner) && inner.ValueKind == JsonValueKind.String)
        {
            if (value.TryGetProperty("_numbers", out var numbers))
            {
                var known = Array.Exists(KnownNumberingOverrides, o => o.Locale == id && o.Slot == name && o.Numbers == numbers.GetString());
                if (known)
                {
                    seenNumberingOverrides.Add((id, name));
                }
                else
                {
                    problems.Add($"{id}: {name} carries the numbering override {numbers.GetString()}, which KnownNumberingOverrides does not list");
                }
            }

            return inner.GetString()!;
        }

        problems.Add($"{id}: {name} is neither a string nor a {{ _value }} object");
        return "";
    }

    private static void CheckSlot(string id, int slot, string value, List<string> problems)
    {
        var name = SlotLayout.Names[slot];
        if (value.Length == 0)
        {
            problems.Add($"{id}: {name} is empty");
            return;
        }

        switch (SlotLayout.KindOf(slot))
        {
            case SlotKind.Pattern:
                CheckPattern(id, name, value, "", problems);
                break;
            case SlotKind.GluePattern:
                CheckPlaceholders(id, name, value, allowsDisplayName: false, problems);
                break;
            case SlotKind.AppendPattern:
                CheckPlaceholders(id, name, value, allowsDisplayName: true, problems);
                break;
        }
    }

    private static void CheckAvailableFormat(string id, string skeleton, string pattern, List<string> problems, HashSet<(string, string)> seenLetterExceptions)
    {
        var extra = "";
        var exception = Array.FindIndex(KnownLetterExceptions, e => e.Locale == id && e.Skeleton == skeleton);
        if (exception >= 0)
        {
            extra = KnownLetterExceptions[exception].Letters;
        }

        var usedExtra = false;
        foreach (var letter in skeleton)
        {
            if (AllowedLetters.Contains(letter, StringComparison.Ordinal))
            {
                continue;
            }

            if (extra.Contains(letter, StringComparison.Ordinal))
            {
                usedExtra = true;
                continue;
            }

            problems.Add($"{id}: the skeleton {skeleton} has the letter '{letter}'");
        }

        usedExtra |= CheckPattern(id, "availableFormats/" + skeleton, pattern, extra, problems);
        if (usedExtra)
        {
            seenLetterExceptions.Add((id, skeleton));
        }
    }

    /// <summary>
    /// A date or time pattern must parse (every quote closed) and use only <see cref="AllowedLetters"/>, plus
    /// <paramref name="extra"/>; answers whether it used one of the extra letters.
    /// </summary>
    private static bool CheckPattern(string id, string name, string pattern, string extra, List<string> problems)
    {
        if (!TryGetPatternLetters(pattern, out var letters))
        {
            problems.Add($"{id}: {name} \"{pattern}\" has an unclosed quote");
            return false;
        }

        if (letters.Length == 0)
        {
            problems.Add($"{id}: {name} \"{pattern}\" has no field");
        }

        var usedExtra = false;
        foreach (var letter in letters)
        {
            if (AllowedLetters.Contains(letter, StringComparison.Ordinal))
            {
                continue;
            }

            if (extra.Contains(letter, StringComparison.Ordinal))
            {
                usedExtra = true;
                continue;
            }

            problems.Add($"{id}: {name} \"{pattern}\" has the letter '{letter}'");
        }

        return usedExtra;
    }

    /// <summary>
    /// A glue pattern (<c>{1}</c> the date, <c>{0}</c> the time) or an appendItems pattern (<c>{0}</c> the pattern so
    /// far, <c>{1}</c> the field, <c>{2}</c> its display name) must parse, place the fields it needs and have no
    /// letter outside quotes.
    /// </summary>
    private static void CheckPlaceholders(string id, string name, string pattern, bool allowsDisplayName, List<string> problems)
    {
        if (!TryGetPatternLetters(pattern, out var letters))
        {
            problems.Add($"{id}: {name} \"{pattern}\" has an unclosed quote");
            return;
        }

        if (letters.Length > 0)
        {
            problems.Add($"{id}: {name} \"{pattern}\" has letters outside quotes");
        }

        if (!pattern.Contains("{0}", StringComparison.Ordinal) || !pattern.Contains("{1}", StringComparison.Ordinal))
        {
            problems.Add($"{id}: {name} \"{pattern}\" does not place both {{0}} and {{1}}");
        }

        var allowed = allowsDisplayName ? "012" : "01";
        for (var i = pattern.IndexOf('{', StringComparison.Ordinal); i >= 0; i = pattern.IndexOf('{', i + 1))
        {
            if (i + 2 >= pattern.Length || pattern[i + 2] != '}' || !allowed.Contains(pattern[i + 1], StringComparison.Ordinal))
            {
                problems.Add($"{id}: {name} \"{pattern}\" has a placeholder other than {string.Join(", ", allowed.Select(c => "{" + c + "}"))}");
            }
        }
    }

    /// <summary>
    /// The letters of a CLDR pattern that are fields rather than literal text: https://www.unicode.org/reports/tr35/tr35-dates.html#Date_Format_Patterns.
    /// Text between single quotes is literal, and two single quotes are a literal quote, inside or outside a quoted run.
    /// </summary>
    internal static bool TryGetPatternLetters(string pattern, out string letters)
    {
        var builder = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (!quoted && char.IsAsciiLetter(c))
            {
                builder.Append(c);
            }
        }

        letters = builder.ToString();
        return !quoted;
    }
}
