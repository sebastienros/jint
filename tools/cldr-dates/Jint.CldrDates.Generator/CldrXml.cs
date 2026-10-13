using System.Xml.Linq;

namespace Jint.CldrDates.Generator;

/// <summary>
/// What one locale's own CLDR XML defines for one calendar, as ICU's data build keeps it.
/// </summary>
internal sealed class ExplicitCalendar
{
    /// <summary>
    /// Whether it defines any of the calendar's <c>dateFormats</c>, <c>timeFormats</c> or standard
    /// <c>dateTimeFormats</c>, which ICU keeps in one array, <c>calendar/&lt;name&gt;/DateTimePatterns</c>.
    /// </summary>
    internal bool StylePatterns { get; set; }

    /// <summary>Whether it defines atTime <c>dateTimeFormats</c>, ICU's <c>calendar/&lt;name&gt;/DateTimePatterns%atTime</c>.</summary>
    internal bool AtTime { get; set; }

    /// <summary>The availableFormats skeletons it defines, without CLDR's <c>-alt-</c> and <c>-count-</c> variants.</summary>
    internal SortedSet<string> AvailableFormats { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Reads which calendar values each locale defines itself out of CLDR's unresolved XML, <c>common/main/*.xml</c>.
/// </summary>
/// <remarks>
/// <para>
/// cldr-json resolves every element through CLDR's inheritance, which loses what ICU's own data is built from: which
/// locale defines which values. ICU keeps a calendar's date, time and joining patterns in one array that a locale has
/// only where it defines one of them, so its time styles are those of the locale that defines its date patterns; and it
/// has an atTime joining pattern only where a locale defines one, even one identical to what it would inherit
/// (<c>zh_Hant</c>'s Gregorian <c>{1} {0}</c>). <see cref="CalendarExtraction"/> reads both from here.
/// </para>
/// <para>
/// A value counts as ICU's data build counts it: an element with a pattern, not an alias and not CLDR's inheritance
/// marker <c>↑↑↑</c>, and not <c>draft="unconfirmed"</c> or <c>"provisional"</c>. So read, the 48.2 files say which
/// locales have each calendar's <c>DateTimePatterns</c> and <c>DateTimePatterns%atTime</c> exactly as ICU 78.3's data
/// does, for every locale and calendar.
/// </para>
/// </remarks>
internal static class CldrXml
{
    private const string InheritanceMarker = "↑↑↑";

    /// <summary>
    /// For each cldr-json locale, what its XML defines, by CLDR's calendar name; the root is <c>und</c>.
    /// </summary>
    internal static Dictionary<string, Dictionary<string, ExplicitCalendar>> Read(Inputs inputs, IEnumerable<string> ids, List<string> problems)
    {
        var result = new Dictionary<string, Dictionary<string, ExplicitCalendar>>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var file = string.Equals(id, ParentLocales.Root, StringComparison.Ordinal) ? "root" : id.Replace('-', '_');
            if (!inputs.MainXmlFiles.TryGetValue(file, out var bytes))
            {
                problems.Add($"{id}: cldr-common has no common/main/{file}.xml");
                result.Add(id, new Dictionary<string, ExplicitCalendar>(StringComparer.Ordinal));
                continue;
            }

            using var stream = new MemoryStream(bytes);
            var document = XDocument.Load(stream);
            var calendars = new Dictionary<string, ExplicitCalendar>(StringComparer.Ordinal);
            foreach (var calendar in document.Root?.Element("dates")?.Element("calendars")?.Elements("calendar") ?? [])
            {
                var data = new ExplicitCalendar
                {
                    StylePatterns = Defines(calendar.Element("dateFormats")) || Defines(calendar.Element("timeFormats")),
                };

                var dateTimeFormats = calendar.Element("dateTimeFormats");
                foreach (var format in dateTimeFormats?.Elements("dateTimeFormatLength").Elements("dateTimeFormat") ?? [])
                {
                    var type = (string?) format.Attribute("type");
                    if (!Defines(format))
                    {
                        continue;
                    }

                    if (string.Equals(type, "atTime", StringComparison.Ordinal))
                    {
                        data.AtTime = true;
                    }
                    else if (type is null || string.Equals(type, "standard", StringComparison.Ordinal))
                    {
                        data.StylePatterns = true;
                    }
                }

                foreach (var item in dateTimeFormats?.Element("availableFormats")?.Elements("dateFormatItem") ?? [])
                {
                    if (item.Attribute("alt") is null && item.Attribute("count") is null && IsConfirmed(item)
                        && !string.Equals(item.Value, InheritanceMarker, StringComparison.Ordinal)
                        && (string?) item.Attribute("id") is { } skeleton)
                    {
                        data.AvailableFormats.Add(skeleton);
                    }
                }

                calendars[(string) calendar.Attribute("type")!] = data;
            }

            result.Add(id, calendars);
        }

        return result;
    }

    /// <summary>Whether an element carries a pattern of its own: one ICU's data build keeps.</summary>
    private static bool Defines(XElement? element)
    {
        return element is not null
               && !element.Descendants("alias").Any()
               && element.Descendants("pattern").Any(p => IsConfirmed(p) && !string.Equals(p.Value, InheritanceMarker, StringComparison.Ordinal));
    }

    private static bool IsConfirmed(XElement element)
    {
        var draft = (string?) element.Attribute("draft");
        return !string.Equals(draft, "unconfirmed", StringComparison.Ordinal) && !string.Equals(draft, "provisional", StringComparison.Ordinal);
    }
}
