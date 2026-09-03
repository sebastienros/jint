#nullable enable

using System.Text;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

public class NonIsoCalendarTests
{
    private static readonly string[] Calendars =
    [
        "chinese", "dangi", "hebrew", "persian", "coptic", "ethiopic", "ethioaa",
        "indian", "islamic-umalqura", "islamic-civil", "islamic-tbla"
    ];

    // Well-formed codes only, valid and invalid. A malformed short code such as "M1" never reaches this
    // helper -- Temporal's month-code grammar rejects it with a RangeError first -- and the helper reads
    // the two display digits by slicing, so it assumes the length its callers guarantee.
    private static readonly string?[] MonthCodes =
    [
        null, "M01", "M02", "M05", "M06", "M12", "M13", "M01L", "M02L", "M05L", "M12L", "M99", "X01"
    ];

    private static readonly int[] Years = [-1, 1, 2, 1000, 5779, 5780, 9999, 10000];

    private static readonly int[] Days = [0, 1, 29, 30, 31];

    /// <summary>
    /// Every field combination has to come back as a date or as null. A CLR exception here escapes
    /// engine.Evaluate, where a script sees neither a value nor a catchable JavaScript error.
    /// </summary>
    /// <remarks>
    /// The hazard this covers is Math.Clamp, which throws ArgumentException for inverted bounds: these
    /// conversions clamp a day against a computed days-in-month, and a Hebrew year where .NET's
    /// HebrewCalendar and the algorithmic leap-year predicate disagreed would drive that maximum to
    /// zero. The combination is not reachable today, which is why this sweep passes either way; it is
    /// here so that a future widening of a year range or a change to a leap-year rule cannot make it
    /// reachable unnoticed.
    /// </remarks>
    [TestCase("constrain")]
    [TestCase("reject")]
    public void CalendarDateToIsoNeverThrowsForAnyFieldCombination(string overflow)
    {
        var failures = new StringBuilder();
        var combinations = 0;

        foreach (var calendar in Calendars)
        {
            foreach (var year in Years)
            {
                foreach (var monthCode in MonthCodes)
                {
                    for (var month = 0; month <= 14; month++)
                    {
                        foreach (var day in Days)
                        {
                            combinations++;
                            try
                            {
                                _ = NonIsoCalendars.CalendarDateToIso(calendar, year, monthCode, month, day, overflow);
                            }
                            catch (Exception e)
                            {
                                failures.AppendLine($"{calendar} year={year} monthCode={monthCode ?? "<null>"} month={month} day={day}: {e.GetType().Name}: {e.Message}");
                            }
                        }
                    }
                }
            }
        }

        combinations.Should().Be(Calendars.Length * Years.Length * MonthCodes.Length * 15 * Days.Length);
        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// The Persian calendar at both ends of Temporal's own range, where the arithmetic rule is the only
    /// thing that can answer: <c>PersianCalendar</c>'s table stops at ISO 622-03-22 and 9999-12-31, and
    /// these two dates are a quarter of a million years outside it in either direction.
    /// </summary>
    /// <remarks>
    /// The expected values are test262's own — the three
    /// <c>intl402/Temporal/*/prototype/withCalendar/extreme-dates.js</c> files assert exactly these, and
    /// <c>ZonedDateTime/from/extreme-dates.js</c> asserts the way back — which is to say they are ICU's
    /// 33-year cycle. The 2820-year cycle Jint used to extend the calendar with put both ends about two
    /// months out, and therefore in the wrong Persian year
    /// (<see href="https://github.com/sebastienros/jint/issues/3604"/>). <c>eraYear</c> is the same number
    /// as <c>year</c> because <c>ap</c> is the calendar's only era, which is why the off-by-one was first
    /// read as an era defect.
    /// </remarks>
    [TestCase("-271821-04-20", -272442, 1, "M01", 10)]
    [TestCase("+275760-09-13", 275139, 7, "M07", 12)]
    public void ThePersianCalendarPlacesTheEndsOfTemporalsRangeWhereTheStandardLibrariesDo(
        string iso, int year, int month, string monthCode, int day)
    {
        var engine = new Engine();
        var date = $"Temporal.PlainDate.from('{iso}').withCalendar('persian')";

        engine.Evaluate($"{date}.year").AsNumber().Should().Be(year);
        engine.Evaluate($"{date}.month").AsNumber().Should().Be(month);
        engine.Evaluate($"{date}.monthCode").AsString().Should().Be(monthCode);
        engine.Evaluate($"{date}.day").AsNumber().Should().Be(day);
        engine.Evaluate($"{date}.era").AsString().Should().Be("ap");
        engine.Evaluate($"{date}.eraYear").AsNumber().Should().Be(year);

        // And the way back, which is what ZonedDateTime/from/extreme-dates.js failed on: the fields are
        // an answer only if they name the day they came from.
        engine.Evaluate($"Temporal.PlainDate.from({{ calendar: 'persian', year: {year}, month: {month}, day: {day} }}).toString()")
            .AsString().Should().Be($"{iso}[u-ca=persian]");
    }

    /// <summary>
    /// The years <c>PersianCalendar</c> does cover are still its own to place, which the arithmetic rule
    /// never reaches: Nowruz 1403 fell on ISO 2024-03-20, and the revolution of 22 Bahman 1357 on
    /// 1979-02-11.
    /// </summary>
    [TestCase("2024-03-20", 1403, 1, 1)]
    [TestCase("1979-02-11", 1357, 11, 22)]
    public void ThePersianCalendarStillPlacesTheYearsItsTableCovers(string iso, int year, int month, int day)
    {
        var engine = new Engine();
        var date = $"Temporal.PlainDate.from('{iso}').withCalendar('persian')";

        engine.Evaluate($"[{date}.year, {date}.month, {date}.day].join('-')")
            .AsString().Should().Be($"{year}-{month}-{day}");
    }
}
