#nullable enable

using Jint.Tests.Test262;

namespace Jint.Tests.Test262Infrastructure;

[TestFixture]
public sealed class NodaTimeZoneProviderTests
{
    // tzdb 2026d restores these POSIX-compatible Zones, whose pre-standard-time
    // histories differ from the cities they were linked to in tzdb 2026b/c.
    // https://data.iana.org/time-zones/tzdb-2026d/NEWS
    // TimeZoneEquals compares primary identifiers, not the offset at one instant:
    // https://tc39.es/proposal-temporal/#sec-temporal-timezoneequals
    [TestCase("CST6CDT", "America/Chicago", "-06:00", "-05:50:36")]
    [TestCase("EST5EDT", "America/New_York", "-05:00", "-04:56:02")]
    [TestCase("MST7MDT", "America/Denver", "-07:00", "-06:59:56")]
    [TestCase("PST8PDT", "America/Los_Angeles", "-08:00", "-07:52:58")]
    public void PosixZonesHaveDistinctIdentitiesAndHistories(
        string posixZone, string cityZone, string posixOffset, string cityOffset)
    {
        var provider = NodaTimeZoneProvider.Instance;
        Assert.That(provider.GetPrimaryTimeZoneIdentifier(posixZone), Is.EqualTo(posixZone));

        using var engine = CreateEngine();
        engine.SetValue("posixZone", posixZone);
        engine.SetValue("cityZone", cityZone);
        engine.Execute("""
            const posix = new Temporal.ZonedDateTime(0n, posixZone);
            const city = new Temporal.ZonedDateTime(0n, cityZone);
            const historicalInstant = Temporal.Instant.from('1880-01-01T00:00:00Z');
            """);

        Assert.That(engine.Evaluate("posix.timeZoneId").AsString(), Is.EqualTo(posixZone));
        Assert.That(engine.Evaluate("posix.offset === city.offset").AsBoolean(), Is.True);
        Assert.That(engine.Evaluate("posix.equals(city)").AsBoolean(), Is.False);
        Assert.That(engine.Evaluate("city.equals(posix)").AsBoolean(), Is.False);
        Assert.That(engine.Evaluate("historicalInstant.toZonedDateTimeISO(posixZone).offset").AsString(), Is.EqualTo(posixOffset));
        Assert.That(engine.Evaluate("historicalInstant.toZonedDateTimeISO(cityZone).offset").AsString(), Is.EqualTo(cityOffset));
    }

    [TestCase("US/Central", "America/Chicago")]
    [TestCase("US/Eastern", "America/New_York")]
    [TestCase("US/Mountain", "America/Denver")]
    [TestCase("US/Pacific", "America/Los_Angeles")]
    public void LinksStillCompareEqualToTheirPrimaryZones(string link, string zone)
    {
        Assert.That(NodaTimeZoneProvider.Instance.GetPrimaryTimeZoneIdentifier(link), Is.EqualTo(zone));

        using var engine = CreateEngine();
        engine.SetValue("link", link);
        engine.SetValue("zone", zone);
        Assert.That(engine.Evaluate("""
            const instant = Temporal.Instant.from('1880-01-01T00:00:00Z');
            const linked = instant.toZonedDateTimeISO(link);
            const primary = instant.toZonedDateTimeISO(zone);
            linked.timeZoneId === link && linked.equals(primary) &&
                primary.equals(linked) && linked.offset === primary.offset;
            """).AsBoolean(), Is.True);
    }

    private static Engine CreateEngine() => new(options =>
    {
        options.ExperimentalFeatures = ExperimentalFeature.All;
        options.Temporal.TimeZoneProvider = NodaTimeZoneProvider.Instance;
        options.LimitExecutionTime(TimeSpan.FromSeconds(30));
    });
}
