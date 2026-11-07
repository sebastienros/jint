#nullable enable

using Jint.Native.Intl.Data;

namespace Jint.Tests.Runtime;

/// <summary>
/// An IANA Link name (<c>Europe/Kiev</c>, <c>US/Eastern</c>, ...) is a valid time zone identifier, and behaves as the
/// zone it points to even where the system's tz database does not have the Link itself - Ubuntu 26.04 without
/// <c>tzdata-legacy</c> has no file for any of them (issue #4474).
/// </summary>
public class TimeZoneLinkNameTests
{
    /// <summary>
    /// Names that are not canonical in <see cref="TimeZoneData"/> but are Zones, not Links, in the tz database, so
    /// the system has them without the backward links.
    /// </summary>
    private static readonly HashSet<string> NonCanonicalZones =
        ["Etc/GMT", "Etc/UTC", "CST6CDT", "EST5EDT", "MST7MDT", "PST8PDT", "Antarctica/Vostok"];

    [Test]
    public void EveryLinkNameHasAZone()
    {
        var canonical = new HashSet<string>(TimeZoneData.GetCanonicalTimeZones(), StringComparer.Ordinal);
        var unresolved = new List<string>();

        foreach (var name in TimeZoneData.GetAllTimeZones())
        {
            if (canonical.Contains(name) || NonCanonicalZones.Contains(name))
            {
                continue;
            }

            if (!TimeZoneData.TryGetLinkTarget(name, out var zone)
                || TimeZoneData.TryGetLinkTarget(zone, out _)
                || !(canonical.Contains(zone) || NonCanonicalZones.Contains(zone)))
            {
                unresolved.Add(name);
            }
        }

        unresolved.Should().BeEmpty();
    }

    [TestCase("Europe/Kiev", "Europe/Kyiv")]
    [TestCase("US/Eastern", "America/New_York")]
    [TestCase("Asia/Calcutta", "Asia/Kolkata")]
    [TestCase("Asia/Saigon", "Asia/Ho_Chi_Minh")]
    [TestCase("America/Buenos_Aires", "America/Argentina/Buenos_Aires")]
    [TestCase("GB", "Europe/London")]
    public void ALinkNameFormatsAsItsZone(string link, string zone)
    {
        var engine = new Engine();
        const string Format = "new Intl.DateTimeFormat('en', { timeZone: tz, timeZoneName: 'short', hour: 'numeric' }).format(0)";

        engine.SetValue("tz", link);
        var formattedForLink = engine.Evaluate(Format).AsString();
        engine.SetValue("tz", zone);
        var formattedForZone = engine.Evaluate(Format).AsString();

        formattedForLink.Should().Be(formattedForZone);
    }

    [TestCase("Europe/Kiev", "Europe/Kyiv")]
    [TestCase("US/Eastern", "America/New_York")]
    [TestCase("Asia/Calcutta", "Asia/Kolkata")]
    [TestCase("GB", "Europe/London")]
    public void ALinkNameIsATemporalTimeZoneWithTheOffsetOfItsZone(string link, string zone)
    {
        var engine = new Engine();
        engine.SetValue("link", link);
        engine.SetValue("zone", zone);

        var result = engine.Evaluate("""
            const forLink = new Temporal.ZonedDateTime(0n, link);
            const forZone = new Temporal.ZonedDateTime(0n, zone);
            [forLink.timeZoneId, forLink.offset === forZone.offset]
            """).AsArray();

        result[0].AsString().Should().Be(link);
        result[1].AsBoolean().Should().BeTrue();
    }
}
