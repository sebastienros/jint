#nullable enable

using System.Globalization;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// Calendar arithmetic — <c>add</c>, <c>subtract</c>, <c>until</c>, <c>since</c> — used to be implemented
/// per calendar inside the engine and nowhere else, while <see cref="ICalendarProvider"/> was consulted only
/// for the two conversions. A host that corrected a calendar therefore corrected its field accessors and
/// left its arithmetic reading the BCL, and a host that added a calendar got a <c>RangeError</c> the moment
/// a date in it was moved. The arithmetic now walks the provider's own conversions for every calendar the
/// provider answers for.
/// <para>
/// Half of these check that. The other half check the thing that made the change worth measuring: an engine
/// that configures no provider still reaches the same per-calendar implementation it always did, and so does
/// a calendar under a provider that only meant to change something else. Every expected string here was
/// captured from the build before the change.
/// </para>
/// </summary>
public class TemporalCalendarArithmeticTests
{
    /// <summary>Every non-ISO calendar the engine implements itself, and one date well inside all eleven ranges.</summary>
    private const string Base = "2024-03-05";

    private const string Other = "2025-07-19";

    private static readonly (string Calendar, string Operation, string Expected)[] AdditionBaseline =
    [
        ("chinese", "add({ years: 1 })", "2025-02-22"),
        ("chinese", "add({ months: 1 })", "2024-04-03"),
        ("chinese", "add({ months: 13 })", "2025-03-24"),
        ("chinese", "subtract({ months: 1 })", "2024-02-04"),
        ("chinese", "subtract({ years: 2 })", "2022-02-25"),
        ("chinese", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-02"),
        ("dangi", "add({ years: 1 })", "2025-02-22"),
        ("dangi", "add({ months: 1 })", "2024-04-03"),
        ("dangi", "add({ months: 13 })", "2025-03-24"),
        ("dangi", "subtract({ months: 1 })", "2024-02-04"),
        ("dangi", "subtract({ years: 2 })", "2022-02-25"),
        ("dangi", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-02"),
        ("hebrew", "add({ years: 1 })", "2025-03-25"),
        ("hebrew", "add({ months: 1 })", "2024-04-04"),
        ("hebrew", "add({ months: 13 })", "2025-03-25"),
        ("hebrew", "subtract({ months: 1 })", "2024-02-04"),
        ("hebrew", "subtract({ years: 2 })", "2022-02-26"),
        ("hebrew", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-06-02"),
        ("persian", "add({ years: 1 })", "2025-03-05"),
        ("persian", "add({ months: 1 })", "2024-04-03"),
        ("persian", "add({ months: 13 })", "2025-04-04"),
        ("persian", "subtract({ months: 1 })", "2024-02-04"),
        ("persian", "subtract({ years: 2 })", "2022-03-06"),
        ("persian", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-15"),
        ("coptic", "add({ years: 1 })", "2025-03-05"),
        ("coptic", "add({ months: 1 })", "2024-04-04"),
        ("coptic", "add({ months: 13 })", "2025-03-05"),
        ("coptic", "subtract({ months: 1 })", "2024-02-04"),
        ("coptic", "subtract({ years: 2 })", "2022-03-05"),
        ("coptic", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-14"),
        ("ethiopic", "add({ years: 1 })", "2025-03-05"),
        ("ethiopic", "add({ months: 1 })", "2024-04-04"),
        ("ethiopic", "add({ months: 13 })", "2025-03-05"),
        ("ethiopic", "subtract({ months: 1 })", "2024-02-04"),
        ("ethiopic", "subtract({ years: 2 })", "2022-03-05"),
        ("ethiopic", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-14"),
        ("ethioaa", "add({ years: 1 })", "2025-03-05"),
        ("ethioaa", "add({ months: 1 })", "2024-04-04"),
        ("ethioaa", "add({ months: 13 })", "2025-03-05"),
        ("ethioaa", "subtract({ months: 1 })", "2024-02-04"),
        ("ethioaa", "subtract({ years: 2 })", "2022-03-05"),
        ("ethioaa", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-14"),
        ("indian", "add({ years: 1 })", "2025-03-06"),
        ("indian", "add({ months: 1 })", "2024-04-04"),
        ("indian", "add({ months: 13 })", "2025-04-05"),
        ("indian", "subtract({ months: 1 })", "2024-02-04"),
        ("indian", "subtract({ years: 2 })", "2022-03-06"),
        ("indian", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-15"),
        ("islamic-umalqura", "add({ years: 1 })", "2025-02-23"),
        ("islamic-umalqura", "add({ months: 1 })", "2024-04-03"),
        ("islamic-umalqura", "add({ months: 13 })", "2025-03-24"),
        ("islamic-umalqura", "subtract({ months: 1 })", "2024-02-05"),
        ("islamic-umalqura", "subtract({ years: 2 })", "2022-03-27"),
        ("islamic-umalqura", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-02"),
        ("islamic-civil", "add({ years: 1 })", "2025-02-23"),
        ("islamic-civil", "add({ months: 1 })", "2024-04-03"),
        ("islamic-civil", "add({ months: 13 })", "2025-03-24"),
        ("islamic-civil", "subtract({ months: 1 })", "2024-02-04"),
        ("islamic-civil", "subtract({ years: 2 })", "2022-03-28"),
        ("islamic-civil", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-03"),
        ("islamic-tbla", "add({ years: 1 })", "2025-02-23"),
        ("islamic-tbla", "add({ months: 1 })", "2024-04-03"),
        ("islamic-tbla", "add({ months: 13 })", "2025-03-24"),
        ("islamic-tbla", "subtract({ months: 1 })", "2024-02-04"),
        ("islamic-tbla", "subtract({ years: 2 })", "2022-03-28"),
        ("islamic-tbla", "add({ years: 1, months: 2, weeks: 1, days: 3 })", "2025-05-03"),
    ];

    private static readonly (string Calendar, string LargestUnit, string Until, string Since)[] DifferenceBaseline =
    [
        ("chinese", "year", "P1Y5M", "P1Y5M"),
        ("chinese", "month", "P17M", "P17M"),
        ("dangi", "year", "P1Y5M", "P1Y5M"),
        ("dangi", "month", "P17M", "P17M"),
        ("hebrew", "year", "P1Y3M28D", "P1Y4M28D"),
        ("hebrew", "month", "P16M28D", "P16M28D"),
        ("persian", "year", "P1Y4M13D", "P1Y4M13D"),
        ("persian", "month", "P16M13D", "P16M13D"),
        ("coptic", "year", "P1Y4M16D", "P1Y4M16D"),
        ("coptic", "month", "P17M16D", "P17M16D"),
        ("ethiopic", "year", "P1Y4M16D", "P1Y4M16D"),
        ("ethiopic", "month", "P17M16D", "P17M16D"),
        ("ethioaa", "year", "P1Y4M16D", "P1Y4M16D"),
        ("ethioaa", "month", "P17M16D", "P17M16D"),
        ("indian", "year", "P1Y4M13D", "P1Y4M13D"),
        ("indian", "month", "P16M13D", "P16M13D"),
        ("islamic-umalqura", "year", "P1Y5M", "P1Y5M"),
        ("islamic-umalqura", "month", "P17M", "P17M"),
        ("islamic-civil", "year", "P1Y4M28D", "P1Y4M28D"),
        ("islamic-civil", "month", "P16M28D", "P16M28D"),
        ("islamic-tbla", "year", "P1Y4M28D", "P1Y4M28D"),
        ("islamic-tbla", "month", "P16M28D", "P16M28D"),
    ];

    private static string AddScript(string calendar, string operation)
        => $"Temporal.PlainDate.from('{Base}').withCalendar('{calendar}').{operation}.toString()";

    private static string UntilScript(string calendar, string largestUnit)
        => $"Temporal.PlainDate.from('{Base}').withCalendar('{calendar}').until(Temporal.PlainDate.from('{Other}').withCalendar('{calendar}'), {{ largestUnit: '{largestUnit}' }}).toString()";

    private static string SinceScript(string calendar, string largestUnit)
        => $"Temporal.PlainDate.from('{Other}').withCalendar('{calendar}').since(Temporal.PlainDate.from('{Base}').withCalendar('{calendar}'), {{ largestUnit: '{largestUnit}' }}).toString()";

    /// <summary>
    /// The failing half of the issue: a calendar a host added reached every field accessor and no arithmetic
    /// at all, on any of the four types that have some. The provider's two conversions are still the whole
    /// subclass — nothing here teaches the engine how a Mayan month works.
    /// </summary>
    [Test]
    public void AHostAddedCalendarNowAddsSubtractsAndDiffers()
    {
        var engine = new Engine(options => options.Temporal.CalendarProvider = new WithMayan());

        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').add({ months: 1 }).toString()")
            .AsString().Should().Be("2024-04-05[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').add({ years: 1, months: 2 }).toString()")
            .AsString().Should().Be("2025-05-05[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').subtract({ months: 1 }).toString()")
            .AsString().Should().Be("2024-02-05[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').add({ days: 1 }).toString()")
            .AsString().Should().Be("2024-03-06[u-ca=mayan]");

        // a day that does not exist in the month landed on is constrained into it, from the length the
        // provider itself reported for that month
        engine.Evaluate("Temporal.PlainDate.from('2024-01-31').withCalendar('mayan').add({ months: 1 }).toString()")
            .AsString().Should().Be("2024-02-29[u-ca=mayan]");
        engine.Evaluate("(function () { try { Temporal.PlainDate.from('2024-01-31').withCalendar('mayan').add({ months: 1 }, { overflow: 'reject' }); return 'no error'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("RangeError");

        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('mayan').until(Temporal.PlainDate.from('{Other}').withCalendar('mayan'), {{ largestUnit: 'month' }}).toString()")
            .AsString().Should().Be("P16M14D");
        engine.Evaluate($"Temporal.PlainDate.from('{Other}').withCalendar('mayan').since(Temporal.PlainDate.from('{Base}').withCalendar('mayan')).toString()")
            .AsString().Should().Be("P501D");

        engine.Evaluate("Temporal.PlainDateTime.from('2024-03-05T12:30[u-ca=mayan]').add({ months: 2 }).toString()")
            .AsString().Should().Be("2024-05-05T12:30:00[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDateTime.from('2024-03-05T12:30[u-ca=mayan]').subtract({ years: 1 }).toString()")
            .AsString().Should().Be("2023-03-05T12:30:00[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDateTime.from('2024-03-05T12:30[u-ca=mayan]').until(Temporal.PlainDateTime.from('2025-07-19T04:00[u-ca=mayan]'), { largestUnit: 'year' }).toString()")
            .AsString().Should().Be("P1Y4M13DT15H30M");
        engine.Evaluate("Temporal.PlainDateTime.from('2025-07-19T04:00[u-ca=mayan]').since(Temporal.PlainDateTime.from('2024-03-05T12:30[u-ca=mayan]'), { largestUnit: 'year' }).toString()")
            .AsString().Should().Be("P1Y4M13DT15H30M");

        engine.Evaluate("Temporal.PlainYearMonth.from({ year: 5138, monthCode: 'M03', calendar: 'mayan' }).add({ months: 2 }).toString()")
            .AsString().Should().Be("2024-05-01[u-ca=mayan]");
        engine.Evaluate("Temporal.ZonedDateTime.from('2024-03-05T12:00[UTC][u-ca=mayan]').add({ months: 1 }).toString()")
            .AsString().Should().Be("2024-04-05T12:00:00+00:00[UTC][u-ca=mayan]");
    }

    /// <summary>
    /// The headline of the issue: a provider that <em>corrects</em> a calendar used to be obeyed by the field
    /// accessors and ignored by the arithmetic, so a date could be moved by a month and land where the
    /// corrected reckoning has no such month. Here the correction is a persian of twelve thirty-day months,
    /// which the BCL <c>PersianCalendar</c> agrees with nowhere: it puts <c>add({ months: 1 })</c> exactly
    /// thirty days out, one day past where the stock arithmetic lands, and the shift is only visible if the
    /// arithmetic went through the provider.
    /// </summary>
    [Test]
    public void ACorrectedCalendarMovesInTheFieldsItWasCorrectedTo()
    {
        var stock = new Engine();
        var engine = new Engine(options => options.Temporal.CalendarProvider = new ThirtyDayPersian());

        // the correction is visible in the fields, as it always was
        stock.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').year").AsNumber().Should().Be(1402);
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').year").AsNumber().Should().Be(25);
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').monthCode").AsString().Should().Be("M07");
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').day").AsNumber().Should().Be(11);

        // …and now in the arithmetic, which lands one day past where the BCL reckoning puts it
        stock.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ months: 1 }}).toString()")
            .AsString().Should().Be("2024-04-03[u-ca=persian]");
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ months: 1 }}).toString()")
            .AsString().Should().Be("2024-04-04[u-ca=persian]");

        // the property the issue is really about: reading the moved date's fields, and building a date from
        // those same fields, have to agree — one month on from (25, M07, 11) is (25, M08, 11) and nowhere else
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ months: 1 }}).year").AsNumber().Should().Be(25);
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ months: 1 }}).monthCode").AsString().Should().Be("M08");
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ months: 1 }}).day").AsNumber().Should().Be(11);
        engine.Evaluate("Temporal.PlainDate.from({ calendar: 'persian', year: 25, monthCode: 'M08', day: 11 }).toString()")
            .AsString().Should().Be("2024-04-04[u-ca=persian]");

        // a year is twelve of those months and nothing else, and thirteen months crosses into the next year
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ years: 1 }}).toString()")
            .AsString().Should().Be("2025-02-28[u-ca=persian]");
        engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('persian').add({{ months: 13 }}).toString()")
            .AsString().Should().Be("2025-03-30[u-ca=persian]");

        // and the difference is measured in the corrected fields too
        stock.Evaluate(UntilScript("persian", "month")).AsString().Should().Be("P16M13D");
        engine.Evaluate(UntilScript("persian", "month")).AsString().Should().Be("P16M21D");
    }

    /// <summary>
    /// The regression baseline: every expected string below was captured from the build before calendar
    /// arithmetic learned about the provider, and an engine that configures none must still produce it.
    /// </summary>
    [Test]
    public void ArithmeticOnTheElevenBuiltInCalendarsIsUnchangedOnAnUnconfiguredEngine()
    {
        var engine = new Engine();

        foreach (var (calendar, operation, expected) in AdditionBaseline)
        {
            engine.Evaluate(AddScript(calendar, operation))
                .AsString().Should().Be($"{expected}[u-ca={calendar}]", $"{calendar}.{operation}");
        }

        foreach (var (calendar, largestUnit, until, since) in DifferenceBaseline)
        {
            engine.Evaluate(UntilScript(calendar, largestUnit)).AsString().Should().Be(until, $"{calendar} until largestUnit={largestUnit}");
            engine.Evaluate(SinceScript(calendar, largestUnit)).AsString().Should().Be(since, $"{calendar} since largestUnit={largestUnit}");
        }
    }

    /// <summary>
    /// A provider is installed for one calendar and inherits the rest, so it claims all eleven and the
    /// arithmetic asks it about all eleven — which is the point, since the field accessors already did. What
    /// has to hold is that asking it changes no answer: the inherited conversions are the same BCL data the
    /// per-calendar implementations read, and the generic walk over them lands on the same dates.
    /// </summary>
    [Test]
    public void AProviderInstalledForOneCalendarLeavesTheOtherElevenWhereTheyWere()
    {
        var engine = new Engine(options => options.Temporal.CalendarProvider = new WithMayan());

        foreach (var (calendar, operation, expected) in AdditionBaseline)
        {
            engine.Evaluate(AddScript(calendar, operation))
                .AsString().Should().Be($"{expected}[u-ca={calendar}]", $"{calendar}.{operation}");
        }

        foreach (var (calendar, largestUnit, until, since) in DifferenceBaseline)
        {
            engine.Evaluate(UntilScript(calendar, largestUnit)).AsString().Should().Be(until, $"{calendar} until largestUnit={largestUnit}");
            engine.Evaluate(SinceScript(calendar, largestUnit)).AsString().Should().Be(since, $"{calendar} since largestUnit={largestUnit}");
        }
    }

    /// <summary>
    /// Weeks and days are ISO days on every calendar — the calendar reckoning covers years and months only,
    /// and the day count is added to the ISO date afterwards. True before the change, and unchanged by it,
    /// including for a calendar the host added.
    /// </summary>
    [Test]
    public void WeeksAndDaysAreAddedAsIsoDaysOnEveryCalendar()
    {
        var engine = new Engine();

        foreach (var calendar in new[]
        {
            "chinese", "dangi", "hebrew", "persian", "coptic", "ethiopic", "ethioaa",
            "indian", "islamic-umalqura", "islamic-civil", "islamic-tbla",
        })
        {
            engine.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('{calendar}').add({{ weeks: 2, days: 3 }}).toString()")
                .AsString().Should().Be($"2024-03-22[u-ca={calendar}]", calendar);
        }

        var withMayan = new Engine(options => options.Temporal.CalendarProvider = new WithMayan());
        withMayan.Evaluate($"Temporal.PlainDate.from('{Base}').withCalendar('mayan').add({{ weeks: 2, days: 3 }}).toString()")
            .AsString().Should().Be("2024-03-22[u-ca=mayan]");
    }

    /// <summary>
    /// The month walk that measures a difference steps one month at a time towards the target and stops when
    /// a step passes it, so a step that does not move the date never stops it. Inside the engine that can no
    /// longer happen — every conversion that used to saturate raises instead, which is what
    /// <see cref="NonIsoCalendarRangeTests"/> pins for the eleven built-in calendars — but the walk is written
    /// in whichever two conversions answer for the calendar, and a host provider is free to clamp where the
    /// engine no longer does. This is that crossing: a provider whose calendar cannot represent the target
    /// and answers with its own boundary date every time. The walk has to end, and it has to end as a
    /// <c>RangeError</c> a script can catch rather than as a CLR exception leaving <c>Engine.Evaluate</c>.
    /// </summary>
    /// <remarks>
    /// It runs on a dedicated thread because the failure it guards against is a non-terminating one: the
    /// loop is in the engine's own code and crosses no statement boundary, so no execution constraint
    /// interrupts it and a regression evaluated inline wedges the whole run instead of reporting it.
    /// </remarks>
    [Test]
    public void ADifferencePastAHostCalendarsRangeRaisesRangeErrorInsteadOfSpinning()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(options => options.Temporal.CalendarProvider = new ClampingMayan());

                // inside the window the provider can represent, the walk measures exactly what the same
                // calendar measures under a provider that clamps nowhere
                engine.Evaluate(MayanDifference("until", Base, Other, "month") + ".toString()")
                    .AsString().Should().Be("P16M14D");

                // and past either end of it the walk stands still, so it has to stop and say so. The walk
                // starts at the receiver for both operations -- `since` is `until` read backwards, not
                // walked backwards -- so aiming the receiver's walk at a date the provider cannot place is
                // what stands still, at each end of the window and for each largest unit that walks months.
                foreach (var largestUnit in new[] { "year", "month" })
                {
                    foreach (var operation in new[] { "until", "since" })
                    {
                        foreach (var two in new[] { PastTheCeiling, PastTheFloor })
                        {
                            engine.Evaluate(CaughtErrorOf(MayanDifference(operation, InsideTheWindow, two, largestUnit)))
                                .AsString().Should().Be("true|true", $"{operation} {two} by {largestUnit}");
                        }
                    }
                }
            },
            TestBudgets.WedgeCeiling,
            "the calendar difference walk did not terminate for a host-supplied calendar",
            DedicatedThread.DefaultStackSize);
    }

    /// <summary>A date <see cref="ClampingMayan"/> places exactly, and two it clamps at one end or the other.</summary>
    private const string InsideTheWindow = "2024-03-05";

    private const string PastTheCeiling = "2050-07-19";

    private const string PastTheFloor = "1900-01-03";

    private static string MayanDifference(string operation, string one, string two, string largestUnit)
        => $"Temporal.PlainDate.from('{one}').withCalendar('mayan').{operation}("
            + $"Temporal.PlainDate.from('{two}').withCalendar('mayan'), {{ largestUnit: '{largestUnit}' }})";

    /// <summary>
    /// Wraps <paramref name="expression"/> so it answers <c>"true|true"</c> for a <c>RangeError</c> carrying a
    /// message. A CLR exception leaving the engine is not caught here and fails the evaluation instead, which
    /// is the second half of what the test asserts.
    /// </summary>
    private static string CaughtErrorOf(string expression)
        => "(function () { try { " + expression
            + "; return 'no error'; } catch (e) { return (e instanceof RangeError) + '|' + (e.message.length > 0); } })()";
}

/// <summary>
/// A calendar Jint has never heard of, defined as ISO shifted by the Mayan Long Count epoch so its
/// arithmetic is checkable by eye. The two conversions are the whole subclass: nobody but the host can
/// convert a calendar the engine does not know, and everything else about it — including, now, how a date in
/// it moves — is inherited.
/// </summary>
file sealed class WithMayan : DefaultCalendarProvider
{
    private const int EpochOffset = 3114;

    public override IReadOnlyCollection<string> GetSupportedCalendars()
        => [.. base.GetSupportedCalendars(), "mayan"];

    public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay)
    {
        if (!string.Equals(calendar, "mayan", StringComparison.Ordinal))
        {
            return base.IsoToCalendarFields(calendar, isoYear, isoMonth, isoDay);
        }

        var leap = DateTime.IsLeapYear(isoYear);
        return new CalendarFields(
            isoYear + EpochOffset, isoMonth, $"M{isoMonth:D2}", isoDay,
            IsLeapMonth: false, MonthsInYear: 12, DaysInMonth: DateTime.DaysInMonth(isoYear, isoMonth),
            DaysInYear: leap ? 366 : 365, InLeapYear: leap);
    }

    public override IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        if (!string.Equals(calendar, "mayan", StringComparison.Ordinal))
        {
            return base.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
        }

        var resolved = monthCode is not null ? int.Parse(monthCode.Substring(1), CultureInfo.InvariantCulture) : month;
        return new IsoDateFields(year - EpochOffset, resolved, day);
    }
}

/// <summary>
/// A persian the host disagrees with the BCL about: twelve months of thirty days counted from 2000-01-01,
/// which shares no month boundary with <see cref="System.Globalization.PersianCalendar"/>. Correcting a
/// calendar the engine already implements is what the second override is for, and the two together are the
/// whole reckoning — the arithmetic asks for nothing else.
/// </summary>
file sealed class ThirtyDayPersian : DefaultCalendarProvider
{
    private const int MonthsPerYear = 12;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = MonthsPerYear * DaysPerMonth;

    private static readonly long Epoch = new DateTime(2000, 1, 1).Ticks / TimeSpan.TicksPerDay;

    public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay)
    {
        if (!string.Equals(calendar, "persian", StringComparison.Ordinal))
        {
            return base.IsoToCalendarFields(calendar, isoYear, isoMonth, isoDay);
        }

        var index = new DateTime(isoYear, isoMonth, isoDay).Ticks / TimeSpan.TicksPerDay - Epoch;
        var year = (int) Math.Floor(index / (double) DaysPerYear) + 1;
        var rest = (int) (index - (long) (year - 1) * DaysPerYear);
        var month = rest / DaysPerMonth + 1;
        var day = rest % DaysPerMonth + 1;
        return new CalendarFields(
            year, month, $"M{month:D2}", day,
            IsLeapMonth: false, MonthsInYear: MonthsPerYear, DaysInMonth: DaysPerMonth,
            DaysInYear: DaysPerYear, InLeapYear: false);
    }

    public override IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        if (!string.Equals(calendar, "persian", StringComparison.Ordinal))
        {
            return base.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
        }

        var resolved = monthCode is not null ? int.Parse(monthCode.Substring(1, 2), CultureInfo.InvariantCulture) : month;
        var leapMonthAsked = monthCode is not null && monthCode.Length != 3;
        if (leapMonthAsked || resolved < 1 || resolved > MonthsPerYear || day < 1 || day > DaysPerMonth)
        {
            if (leapMonthAsked || string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                return null;
            }

            resolved = resolved < 1 ? 1 : (resolved > MonthsPerYear ? MonthsPerYear : resolved);
            day = day < 1 ? 1 : (day > DaysPerMonth ? DaysPerMonth : day);
        }

        var index = Epoch + (long) (year - 1) * DaysPerYear + (resolved - 1) * DaysPerMonth + (day - 1);
        var iso = new DateTime(index * TimeSpan.TicksPerDay);
        return new IsoDateFields(iso.Year, iso.Month, iso.Day);
    }
}

/// <summary>
/// <see cref="WithMayan"/>'s calendar, given the one property the engine's own conversions no longer have: a
/// range, and a clamp at each end of it instead of a refusal. That is what the eleven built-in calendars
/// used to do — answer with a boundary date for anything past their end — and it is the shape that made the
/// month walk stand still forever, every step past the boundary landing on the boundary so that no step ever
/// passed the target. A host provider can still be written this way, which is why the walk keeps a
/// no-progress guard of its own.
/// </summary>
file sealed class ClampingMayan : DefaultCalendarProvider
{
    private const int EpochOffset = 3114;

    private const int FloorIsoYear = 2000;
    private const int CeilingIsoYear = 2030;

    public override IReadOnlyCollection<string> GetSupportedCalendars()
        => [.. base.GetSupportedCalendars(), "mayan"];

    public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay)
    {
        if (!string.Equals(calendar, "mayan", StringComparison.Ordinal))
        {
            return base.IsoToCalendarFields(calendar, isoYear, isoMonth, isoDay);
        }

        var leap = DateTime.IsLeapYear(isoYear);
        return new CalendarFields(
            isoYear + EpochOffset, isoMonth, $"M{isoMonth:D2}", isoDay,
            IsLeapMonth: false, MonthsInYear: 12, DaysInMonth: DateTime.DaysInMonth(isoYear, isoMonth),
            DaysInYear: leap ? 366 : 365, InLeapYear: leap);
    }

    public override IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        if (!string.Equals(calendar, "mayan", StringComparison.Ordinal))
        {
            return base.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
        }

        var isoYear = year - EpochOffset;

        // the clamp: a date this calendar cannot place answers with the nearer end of what it can, and that
        // answer does not move however many further months are added to it
        if (isoYear < FloorIsoYear)
        {
            return new IsoDateFields(FloorIsoYear, 1, 1);
        }

        if (isoYear > CeilingIsoYear)
        {
            return new IsoDateFields(CeilingIsoYear, 12, 31);
        }

        var resolved = monthCode is not null ? int.Parse(monthCode.Substring(1), CultureInfo.InvariantCulture) : month;
        return new IsoDateFields(isoYear, resolved, day);
    }
}
