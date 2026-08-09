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
    [Theory]
    [InlineData("constrain")]
    [InlineData("reject")]
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
}
