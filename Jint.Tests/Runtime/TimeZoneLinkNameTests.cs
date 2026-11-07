#nullable enable

using System.Numerics;

using Jint.Native.Intl.Data;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// An IANA Link name (<c>Europe/Kiev</c>, <c>US/Eastern</c>, ...) is a valid time zone identifier, and behaves as the
/// zone it points to even where the system's tz database does not have the Link itself - Ubuntu 26.04 without
/// <c>tzdata-legacy</c> has no file for any of them (issue #4474).
/// </summary>
public class TimeZoneLinkNameTests
{
    /// <summary>
    /// Supported non-canonical IDs skipped by the Link-target check: UTC aliases, legacy POSIX Zones, and Vostok.
    /// The four POSIX Zones are in tzdb's backward file but may be absent without the tzdata-legacy package.
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

    [TestCase("Indian/Reunion", "Réunion Time")]
    [TestCase("Indian/Mahe", "Seychelles Time")]
    [TestCase("Indian/Christmas", "Christmas Island Time")]
    [TestCase("Indian/Cocos", "Cocos Islands Time")]
    [TestCase("Antarctica/Syowa", "Syowa Time")]
    public void CrossCountryLinkUsesItsOwnMetazoneName(string link, string expectedName)
    {
        var engine = new Engine();
        engine.SetValue("tz", link);

        var name = engine.Evaluate("""
            new Intl.DateTimeFormat('en', { timeZone: tz, timeZoneName: 'long' })
                .formatToParts(0)
                .find(part => part.type === 'timeZoneName')
                .value
            """).AsString();

        name.Should().Be(expectedName);
    }

    [TestCase("CST6CDT", "America/Chicago")]
    [TestCase("EST5EDT", "America/New_York")]
    [TestCase("MST7MDT", "America/Denver")]
    [TestCase("PST8PDT", "America/Los_Angeles")]
    public void LegacyPosixZoneUsesItsApproximateUsZone(string legacyZone, string targetZone)
    {
        var provider = new DefaultTimeZoneProvider();
        var dates = new[]
        {
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero),
        };

        foreach (var date in dates)
        {
            var epochNanoseconds = new BigInteger(date.ToUnixTimeMilliseconds()) * 1_000_000;
            provider.GetOffsetNanosecondsFor(legacyZone, epochNanoseconds)
                .Should().Be(provider.GetOffsetNanosecondsFor(targetZone, epochNanoseconds));
        }
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
