using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Temporal</c> now puts a calendar identifier its own table does not name to
/// <see cref="ICalendarProvider.IsSupported"/>, and <see cref="DefaultCalendarProvider.IsSupported"/> answers
/// from <see cref="DefaultCalendarProvider.GetSupportedCalendars"/> rather than from a second hardcoded list.
/// Both are only reachable once a host installs a provider of its own, and both have to leave an
/// unconfigured engine exactly where it was — which is what these check, identifier by identifier.
/// </summary>
public class TemporalCalendarProviderTests
{
    private static readonly string[] TheElevenNonIsoCalendars =
    [
        "chinese", "dangi", "hebrew", "persian",
        "coptic", "ethiopic", "ethioaa", "indian",
        "islamic-umalqura", "islamic-civil", "islamic-tbla",
    ];

    /// <summary>
    /// The list-backed <c>IsSupported</c> replaced a switch over these same eleven identifiers. Equal sets
    /// is the whole reason the replacement is invisible, so the two are compared entry by entry rather than
    /// spot-checked.
    /// </summary>
    [Test]
    public void TheDefaultProviderClaimsExactlyTheElevenCalendarsTheEngineImplements()
    {
        var provider = DefaultCalendarProvider.Instance;

        provider.GetSupportedCalendars().Should().BeEquivalentTo(TheElevenNonIsoCalendars);

        foreach (var calendar in TheElevenNonIsoCalendars)
        {
            provider.IsSupported(calendar).Should().BeTrue(calendar);
        }

        foreach (var other in new[]
        {
            "iso8601", "gregory", "buddhist", "japanese", "roc", "islamic", "islamic-rgsa",
            "ethiopic-amete-alem", "islamicc", "gregorian", "mayan", "", "CHINESE",
        })
        {
            provider.IsSupported(other).Should().BeFalse(other);
        }
    }

    /// <summary>
    /// The provider is consulted for an identifier only when the host installed one of its own; an engine
    /// that configures nothing keeps the fixed table, so a calendar nobody implements is still a RangeError.
    /// </summary>
    [Test]
    public void AnUnconfiguredEngineStillRefusesACalendarNobodyImplements()
    {
        var engine = new Engine();

        foreach (var script in new[]
        {
            "Temporal.PlainDate.from('2024-03-05').withCalendar('mayan')",
            "Temporal.PlainDate.from({ year: 2024, month: 3, day: 5, calendar: 'mayan' })",
            "Temporal.PlainDate.from('2024-03-05[u-ca=mayan]')",
        })
        {
            engine.Evaluate($"(function () {{ try {{ {script}; return 'no error'; }} catch (e) {{ return e.constructor.name; }} }})()")
                .AsString().Should().Be("RangeError", script);
        }

        // and the two the specification lists but Temporal refuses are still refused
        foreach (var calendar in new[] { "islamic", "islamic-rgsa" })
        {
            engine.Evaluate($"(function () {{ try {{ Temporal.PlainDate.from({{ year: 2024, month: 3, day: 5, calendar: '{calendar}' }}); return 'no error'; }} catch (e) {{ return e.constructor.name; }} }})()")
                .AsString().Should().Be("RangeError", calendar);
        }
    }
}
