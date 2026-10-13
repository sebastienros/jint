using System.Globalization;
using System.Text;

namespace Jint.CldrDates.Generator;

/// <summary>
/// CLDR's day period rules (cldr-core <c>supplemental/dayPeriods.json</c>, <c>dayPeriodRuleSet</c>), which say which
/// flexible day period each hour falls in, resolved for each locale the way ICU's <c>DayPeriodRules::getInstance</c>
/// (icu4c <c>dayperiodrules.cpp</c>) finds them, and written as one <see cref="SlotLayout.DayPeriodRules"/> value.
/// </summary>
/// <remarks>
/// <para>
/// ICU looks a locale's rule set up by its own name and then by truncation — the locale with its last subtag removed,
/// until only the language is left — not along CLDR's parent chain. The two part where it matters: <c>zh-Hant</c>,
/// whose CLDR parent is the root, takes <c>zh</c>'s rules. A locale whose language has no rule set gets none, and ICU
/// writes am/pm for it; the root's rule set (<c>und</c>) has only <c>am</c> and <c>pm</c>, which writes the same, so such
/// a locale is given the root's.
/// </para>
/// <para>
/// The value has one character per hour, 0 to 23: the period's position in <see cref="SlotLayout.FlexibleDayPeriods"/>
/// (<c>1</c> morning1 to <c>8</c> night2), or <c>a</c> and <c>p</c> for am and pm; then <c>n</c> when the rule set has
/// <c>noon</c> at 12:00. <c>midnight</c> at 00:00 is checked and dropped, since ICU does not write it. English is
/// <c>111111111111222222333444n</c>.
/// </para>
/// </remarks>
internal static class DayPeriodRuleSets
{
    internal const string Root = "und";

    internal static Dictionary<string, string> Resolve(Inputs inputs, IReadOnlyList<string> ids, List<string> problems)
    {
        var encoded = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var json = inputs.CoreJson("supplemental/dayPeriods.json"))
        {
            foreach (var set in json.RootElement.GetProperty("supplemental").GetProperty("dayPeriodRuleSet").EnumerateObject())
            {
                if (Encode(set.Name, set.Value, problems) is { } value)
                {
                    encoded.Add(set.Name, value);
                }
            }
        }

        if (!encoded.ContainsKey(Root))
        {
            problems.Add("dayPeriods.json has no rule set for the root (und)");
        }

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            resolved.Add(id, Lookup(encoded, id) ?? encoded.GetValueOrDefault(Root, ""));
        }

        return resolved;
    }

    /// <summary>
    /// ICU's lookup: the locale, then each truncation down to the language; null when none of them has a rule set.
    /// </summary>
    private static string? Lookup(Dictionary<string, string> encoded, string id)
    {
        for (var name = id; ; name = name[..name.LastIndexOf('-')])
        {
            if (encoded.TryGetValue(name, out var value))
            {
                return value;
            }

            if (!name.Contains('-', StringComparison.Ordinal))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// One rule set, checked the way ICU's loader checks it: whole hours, <c>at</c> only for midnight at 00:00 and noon
    /// at 12:00, and every hour of the day in exactly one period.
    /// </summary>
    private static string? Encode(string locale, System.Text.Json.JsonElement rules, List<string> problems)
    {
        var hours = new char[24];
        var noon = false;
        var valid = true;
        foreach (var rule in rules.EnumerateObject())
        {
            var period = rule.Name;
            if (rule.Value.TryGetProperty("_at", out var at))
            {
                var time = at.GetString();
                if (string.Equals(period, "noon", StringComparison.Ordinal) && string.Equals(time, "12:00", StringComparison.Ordinal))
                {
                    noon = true;
                }
                else if (!string.Equals(period, "midnight", StringComparison.Ordinal) || !string.Equals(time, "00:00", StringComparison.Ordinal))
                {
                    problems.Add($"dayPeriods.json {locale}: {period} is at {time}; ICU allows only midnight at 00:00 and noon at 12:00");
                    valid = false;
                }

                continue;
            }

            char code;
            var position = Array.IndexOf(SlotLayout.FlexibleDayPeriods, period);
            if (position > 0)
            {
                code = (char) ('0' + position);
            }
            else if (string.Equals(period, "am", StringComparison.Ordinal))
            {
                code = 'a';
            }
            else if (string.Equals(period, "pm", StringComparison.Ordinal))
            {
                code = 'p';
            }
            else
            {
                problems.Add($"dayPeriods.json {locale}: {period} is not a period ICU knows");
                valid = false;
                continue;
            }

            if (!rule.Value.TryGetProperty("_from", out var fromValue) || !rule.Value.TryGetProperty("_before", out var beforeValue)
                || Hour(fromValue.GetString()) is not { } from || Hour(beforeValue.GetString()) is not { } before
                || from > 23 || before is < 1 or > 24 || from == before)
            {
                problems.Add($"dayPeriods.json {locale}: {period} is not a range of whole hours");
                valid = false;
                continue;
            }

            for (var hour = from; hour != before; hour = (hour + 1) % 24)
            {
                if (hours[hour] != '\0')
                {
                    problems.Add($"dayPeriods.json {locale}: {hour}:00 is in two periods");
                    valid = false;
                }

                hours[hour] = code;
                if (before == 24 && hour == 23)
                {
                    break;
                }
            }
        }

        if (Array.IndexOf(hours, '\0') >= 0)
        {
            problems.Add($"dayPeriods.json {locale}: not every hour of the day is in a period");
            valid = false;
        }

        if (!valid)
        {
            return null;
        }

        var value = new StringBuilder(25).Append(hours);
        if (noon)
        {
            value.Append('n');
        }

        return value.ToString();
    }

    private static int? Hour(string? time)
    {
        if (time is not { Length: 5 } || time[2] != ':' || !string.Equals(time[3..], "00", StringComparison.Ordinal)
            || !int.TryParse(time.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hour))
        {
            return null;
        }

        return hour;
    }
}
