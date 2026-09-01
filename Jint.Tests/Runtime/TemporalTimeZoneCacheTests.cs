#nullable enable

using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>DefaultTimeZoneProvider.Instance</c> is the provider every unconfigured engine gets, so its resolution
/// cache is process-wide in practice. These tests pin that a script cannot grow it without bound.
/// </summary>
/// <remarks>
/// Non-parallelizable because the assertions are about a static shared by the whole test run: a fixture
/// running beside this one would add its own zones to the same dictionary.
/// </remarks>
[CollectionDefinition(nameof(TemporalTimeZoneCacheTests), DisableParallelization = true)]
[Collection(nameof(TemporalTimeZoneCacheTests))]
public class TemporalTimeZoneCacheTests
{
    /// <summary>
    /// <c>IsValidTimeZone</c> ends in the same resolution the working paths use, so validation used to
    /// populate the cache and every <i>rejected</i> identifier became a permanent entry.
    /// </summary>
    [Fact]
    public void RejectedTimeZoneIdentifiersDoNotGrowTheProcessWideCache()
    {
        var engine = new Engine();
        engine.Execute("""
            for (var i = 0; i < 500; i++) {
                try { Temporal.ZonedDateTime.from({ year: 2020, month: 1, day: 1, timeZone: 'A/' + i }); }
                catch (e) { }
            }
            """);

        DefaultTimeZoneProvider.Instance.TimeZoneCacheCount
            .Should().BeLessThanOrEqualTo(DefaultTimeZoneProvider.TimeZoneCacheBound);
    }

    /// <summary>
    /// The identifiers that <i>do</i> resolve are a large key space too, because an offset identifier
    /// resolves to a zone the provider manufactures from the string.
    /// </summary>
    [Fact]
    public void OffsetTimeZoneIdentifiersDoNotGrowTheProcessWideCache()
    {
        var engine = new Engine();
        engine.Execute("""
            function pad(n) { return (n < 10 ? '0' : '') + n; }
            for (var h = 0; h < 24; h++) {
                for (var m = 0; m < 60; m++) {
                    Temporal.ZonedDateTime
                        .from({ year: 2020, month: 1, day: 1, timeZone: '+' + pad(h) + ':' + pad(m) })
                        .getTimeZoneTransition('next');
                }
            }
            """);

        DefaultTimeZoneProvider.Instance.TimeZoneCacheCount
            .Should().BeLessThanOrEqualTo(DefaultTimeZoneProvider.TimeZoneCacheBound);
    }

    /// <summary>
    /// The bound is not allowed to become a correctness problem: a zone resolved after the cache overflowed
    /// answers exactly as it did before, because the value is a total function of the identifier.
    /// </summary>
    [Fact]
    public void AnEvictedZoneAnswersExactlyAsItDidBefore()
    {
        var engine = new Engine();
        const string Script = "Temporal.ZonedDateTime.from({ year: 2020, month: 6, day: 1, timeZone: 'Europe/Helsinki' }).offsetNanoseconds";

        var before = engine.Evaluate(Script).AsNumber();

        engine.Execute("""
            function pad(n) { return (n < 10 ? '0' : '') + n; }
            for (var h = 0; h < 24; h++) {
                for (var m = 0; m < 60; m++) {
                    Temporal.ZonedDateTime
                        .from({ year: 2020, month: 1, day: 1, timeZone: '+' + pad(h) + ':' + pad(m) })
                        .getTimeZoneTransition('next');
                }
            }
            """);

        engine.Evaluate(Script).AsNumber().Should().Be(before);
    }

    /// <summary>
    /// The syntactic screen that answers an impossible identifier without a system lookup must never decline
    /// an identifier the system can actually resolve, so every zone this machine has has to survive it.
    /// </summary>
    [Fact]
    public void EverySystemTimeZoneIdentifierSurvivesTheSyntacticScreen()
    {
        var provider = DefaultTimeZoneProvider.Instance;
        var screened = 0;

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (!zone.Id.Contains('/'))
            {
                // Windows identifiers ("Eastern Standard Time") are declined by the IANA naming rule that
                // predates this change, and the screen is not what decides them.
                continue;
            }

            provider.IsValidTimeZone(zone.Id).Should().BeTrue(because: $"'{zone.Id}' is a zone this machine has");
            screened++;
        }

        foreach (var id in provider.GetAvailableTimeZones())
        {
            if (id.Contains('/'))
            {
                provider.IsValidTimeZone(id).Should().BeTrue(because: $"'{id}' is reported as available");
                screened++;
            }
        }

#if !NETFRAMEWORK
        // On Windows the first loop does nothing, because the system's zones are Windows identifiers. Ask
        // .NET for the IANA name of each instead: that is the key space the screen actually has to survive,
        // and it is a real check on the platform where the first loop is empty.
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaId))
            {
                DefaultTimeZoneProvider.IsPlausibleIanaName(ianaId)
                    .Should().BeTrue(because: $"'{ianaId}' is what .NET calls '{zone.Id}'");
                screened++;
            }
        }
#endif

        // never vacuous: Linux fills the first loop, Windows the second and third
        screened.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// A rejection is not merely bounded, it is not remembered at all -- which is what stops a script that
    /// invents plausible-looking identifiers from paying the cache in entries.
    /// </summary>
    [Fact]
    public void ARejectedIdentifierIsNotRemembered()
    {
        var provider = DefaultTimeZoneProvider.Instance;

        // survives the syntactic screen, so it reaches the system lookup and is rejected there
        var unknown = "Europe/Nowhere_" + Guid.NewGuid().ToString("N");
        var before = provider.TimeZoneCacheCount;

        provider.IsValidTimeZone(unknown).Should().BeFalse();

        provider.TimeZoneCacheCount.Should().Be(before);
    }

    /// <summary>
    /// Every shape the TZDB actually uses has to survive the screen, or it would decline an identifier the
    /// system could resolve.
    /// </summary>
    [Theory]
    [InlineData("America/New_York")]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("Antarctica/DumontDUrville")]
    [InlineData("Asia/Ho_Chi_Minh")]
    [InlineData("Etc/GMT+5")]
    [InlineData("Etc/GMT-14")]
    [InlineData("Pacific/Chatham")]
    [InlineData("US/Eastern")]
    [InlineData("posix/America/New_York")]
    [InlineData("right/UTC")]
    public void TheSyntacticScreenAcceptsEveryTzdbNameShape(string id)
    {
        DefaultTimeZoneProvider.IsPlausibleIanaName(id).Should().BeTrue();
    }

    /// <summary>
    /// And nothing that cannot be one, which is the half that costs an invented identifier nothing.
    /// </summary>
    [Theory]
    [InlineData("A/0")]
    [InlineData("Europe/0London")]
    [InlineData("Europe/")]
    [InlineData("/Europe")]
    [InlineData("Europe//London")]
    [InlineData("Europe/Lond on")]
    [InlineData("Europe/Lond*on")]
    [InlineData("Europe/Londön")]
    [InlineData("")]
    public void TheSyntacticScreenDeclinesWhatCannotNameAZone(string id)
    {
        DefaultTimeZoneProvider.IsPlausibleIanaName(id).Should().BeFalse();
    }

    [Fact]
    public void TheSyntacticScreenDeclinesAnAbsurdlyLongIdentifier()
    {
        DefaultTimeZoneProvider.IsPlausibleIanaName("Europe/" + new string('a', 1_000_000)).Should().BeFalse();
    }

    /// <summary>
    /// A rejected identifier stays rejected and a valid one stays valid, whether or not the answer was
    /// remembered.
    /// </summary>
    [Theory]
    [InlineData("A/0", false)]
    [InlineData("Not A Zone", false)]
    [InlineData("Europe/Nowhere", false)]
    [InlineData("ACT", false)]
    [InlineData("Europe/Helsinki", true)]
    [InlineData("europe/helsinki", true)]
    [InlineData("UTC", true)]
    [InlineData("Etc/UTC", true)]
    [InlineData("America/Argentina/Buenos_Aires", true)]
    [InlineData("+01:00", true)]
    [InlineData("-05:30", true)]
    [InlineData("+0130", true)]
    [InlineData("+01", true)]
    [InlineData("+01:00:00", false)]
    public void AnAnswerIsTheSameOnEveryAsking(string timeZoneId, bool expected)
    {
        var provider = DefaultTimeZoneProvider.Instance;

        provider.IsValidTimeZone(timeZoneId).Should().Be(expected);
        provider.IsValidTimeZone(timeZoneId).Should().Be(expected);
    }
}
