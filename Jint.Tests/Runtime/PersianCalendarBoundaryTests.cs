#nullable enable

using System.Globalization;
using System.Text;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// Where the <c>persian</c> calendar stops delegating to <see cref="PersianCalendar"/> and starts
/// reckoning on the 33-year cycle, and that each side of that seam is answered by one rule alone.
/// </summary>
/// <remarks>
/// <para>
/// The seam used to be wherever the runner's own calendar data put it — <c>MinSupportedDateTime</c>,
/// <c>MaxSupportedDateTime</c>, and the refusal <c>ToDateTime</c> raises outside them — so "outside the
/// table" was a different set of dates on a runtime whose data differed, and every proleptic answer moved
/// with it. It is now an ISO window Jint states for itself, 622-03-22 through 9999-12-31
/// (<see href="https://github.com/sebastienros/jint/issues/3604"/>).
/// </para>
/// <para>
/// So there are two kinds of assertion here, and neither is a date written down because a run once
/// produced it. <see cref="ThePlatformsPersianTableCoversExactlyTheWindowJintStates"/> asserts the
/// constants against the platform, so a runtime that ever moves its window fails there, naming the
/// reason, rather than moving a date a quarter of a million years away on one CI leg. Everything else
/// asserts Jint against the 33-year cycle, which this file writes out for itself — a test that asked the
/// engine what the engine's arithmetic ought to be would say nothing — and that cycle is integer
/// arithmetic on a fixed epoch, so its answers are the same on every runner by construction.
/// </para>
/// <para>
/// Both rules are needed, which is why the delegation is still there. Over the 9,377 whole years the two
/// share they disagree about 4,144 year-starts, and about which Persian month 1,514,413 of the window's
/// 3,425,164 days fall in: the table derives a year from the true vernal equinox at Tehran and is what
/// makes a date anybody keeps right, while the cycle is what every other Temporal implementation answers
/// a proleptic date with.
/// </para>
/// </remarks>
public class PersianCalendarBoundaryTests
{
    // The window Jint states, restated here so that moving it has to be a decision taken twice.
    private static readonly DateTime FirstIsoDay = new(622, 3, 22);
    private static readonly DateTime LastIsoDay = new(9999, 12, 31);
    private const int LastTableYear = 9378;
    private const int LastTableMonth = 10;
    private const int LastTableDay = 13;

    // The 33-year cycle, written out rather than reached for. The epoch is the Julian Day Number of ISO
    // 622-03-21, which is where the cycle starts Persian year 1 — a day before the table does.
    private const long PersianEpochJdn = 1948320L;
    private const long JdnOfEpochDay = 2440588L; // ISO 1970-01-01, the day count Temporal reckons in

    private static long FloorDiv(long a, long b)
    {
        var q = a / b;
        if ((a ^ b) < 0L && q * b != a)
        {
            q--;
        }

        return q;
    }

    private static long FloorMod(long a, long b)
    {
        var r = a % b;
        if (r != 0L && (r ^ b) < 0L)
        {
            r += b;
        }

        return r;
    }

    private static bool CycleIsLeapYear(long year) => FloorMod(25L * year + 11L, 33L) < 8L;

    private static long CycleYearStartJdn(long year)
        => PersianEpochJdn + 365L * (year - 1L) + FloorDiv(8L * year + 21L, 33L);

    private static int CycleDaysInMonth(long year, int month)
    {
        if (month <= 6)
        {
            return 31;
        }

        if (month <= 11)
        {
            return 30;
        }

        return CycleIsLeapYear(year) ? 30 : 29;
    }

    /// <summary>The Persian date the cycle puts on a Julian Day Number.</summary>
    private static (long Year, int Month, int Day) CyclePlace(long jdn)
    {
        var year = FloorDiv(jdn - PersianEpochJdn, 365L) + 1L;
        while (CycleYearStartJdn(year + 1L) <= jdn)
        {
            year++;
        }

        while (CycleYearStartJdn(year) > jdn)
        {
            year--;
        }

        var dayOfYear = jdn - CycleYearStartJdn(year) + 1L;

        if (dayOfYear <= 186L)
        {
            var early = (int) ((dayOfYear - 1L) / 31L) + 1;
            return (year, early, (int) (dayOfYear - (early - 1) * 31L));
        }

        var remaining = dayOfYear - 186L;
        var late = (int) ((remaining - 1L) / 30L) + 7;
        return (year, late, (int) (remaining - (late - 7) * 30L));
    }

    private static long JdnOfIsoDay(DateTime day)
        => TemporalHelpers.IsoDateToDays(day.Year, day.Month, day.Day) + JdnOfEpochDay;

    private static (long Year, int Month, int Day) PlacedByJint(long jdn)
    {
        var iso = TemporalHelpers.DaysToIsoDate(jdn - JdnOfEpochDay);
        var placed = NonIsoCalendars.IsoToCalendarDate("persian", in iso);
        return (placed.Year, placed.Month, placed.Day);
    }

    /// <summary>
    /// The platform's table still covers exactly the window Jint says it does, in ISO days and in Persian
    /// fields both. This is the one assertion here that can fail for a reason which is not Jint's, and it
    /// exists so that reason gets named: every proleptic Persian date in the engine is decided by these
    /// numbers agreeing with the runtime underneath.
    /// </summary>
    [Test]
    public void ThePlatformsPersianTableCoversExactlyTheWindowJintStates()
    {
        var cal = new PersianCalendar();

        cal.MinSupportedDateTime.Date.Should().Be(FirstIsoDay);
        cal.MaxSupportedDateTime.Date.Should().Be(LastIsoDay);

        cal.GetYear(FirstIsoDay).Should().Be(1);
        cal.GetMonth(FirstIsoDay).Should().Be(1);
        cal.GetDayOfMonth(FirstIsoDay).Should().Be(1);

        cal.GetYear(LastIsoDay).Should().Be(LastTableYear);
        cal.GetMonth(LastIsoDay).Should().Be(LastTableMonth);
        cal.GetDayOfMonth(LastIsoDay).Should().Be(LastTableDay);

        cal.ToDateTime(1, 1, 1, 0, 0, 0, 0).Should().Be(FirstIsoDay);
        cal.ToDateTime(LastTableYear, LastTableMonth, LastTableDay, 0, 0, 0, 0).Should().Be(LastIsoDay);

        // And nothing past either end, which is the question PersianTableHolds now answers by comparison
        // rather than by catching this.
        Action pastTheLastDay = () => cal.ToDateTime(LastTableYear, LastTableMonth, LastTableDay + 1, 0, 0, 0, 0);
        Action pastTheLastMonth = () => cal.ToDateTime(LastTableYear, LastTableMonth + 1, 1, 0, 0, 0, 0);
        Action pastTheLastYear = () => cal.ToDateTime(LastTableYear + 1, 1, 1, 0, 0, 0, 0);
        Action beforeTheFirstYear = () => cal.ToDateTime(0, 12, 29, 0, 0, 0, 0);

        pastTheLastDay.Should().Throw<ArgumentOutOfRangeException>();
        pastTheLastMonth.Should().Throw<ArgumentOutOfRangeException>();
        pastTheLastYear.Should().Throw<ArgumentOutOfRangeException>();
        beforeTheFirstYear.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The year the window stops inside is still reported whole. <c>DateTime</c> ends part-way through
    /// Persian 9378, so the table answers for that year with ten months, 289 days and a tenth month
    /// thirteen days long; its lengths come from the reckoning instead, while the day the table placed
    /// keeps its place (<see href="https://github.com/sebastienros/jint/issues/3523"/>).
    /// </summary>
    [Test]
    public void TheYearTheWindowStopsInsideIsStillAWholeYear()
    {
        var lastDay = new IsoDate(LastIsoDay.Year, LastIsoDay.Month, LastIsoDay.Day);
        var placed = NonIsoCalendars.IsoToCalendarDate("persian", in lastDay);

        placed.Year.Should().Be(LastTableYear);
        placed.Month.Should().Be(LastTableMonth);
        placed.Day.Should().Be(LastTableDay);

        placed.MonthsInYear.Should().Be(12);
        placed.DaysInMonth.Should().Be(CycleDaysInMonth(LastTableYear, LastTableMonth));
        placed.DaysInYear.Should().Be(CycleIsLeapYear(LastTableYear) ? 366 : 365);
        placed.InLeapYear.Should().Be(CycleIsLeapYear(LastTableYear));
    }

    /// <summary>
    /// Every day for four hundred either side of each edge of the window: inside it the answer is the
    /// table's, outside it the cycle's, and the change happens on the day the constants name and on no
    /// other. The sweep is dense rather than sampled because the seam is one day wide.
    /// </summary>
    [Test]
    public void TheHandOffHappensOnTheDayTheWindowNamesAndOnNoOther()
    {
        var cal = new PersianCalendar();
        var firstJdn = JdnOfIsoDay(FirstIsoDay);
        var lastJdn = JdnOfIsoDay(LastIsoDay);

        var failures = new StringBuilder();
        var days = 0;

        foreach (var edge in new[] { firstJdn, lastJdn })
        {
            for (var jdn = edge - 400L; jdn <= edge + 400L; jdn++)
            {
                days++;
                var iso = TemporalHelpers.DaysToIsoDate(jdn - JdnOfEpochDay);

                (long Year, int Month, int Day) expected;
                string by;

                if (jdn >= firstJdn && jdn <= lastJdn)
                {
                    var dt = new DateTime(iso.Year, iso.Month, iso.Day);
                    expected = (cal.GetYear(dt), cal.GetMonth(dt), cal.GetDayOfMonth(dt));
                    by = "the table";
                }
                else
                {
                    expected = CyclePlace(jdn);
                    by = "the cycle";
                }

                var actual = PlacedByJint(jdn);
                if (actual != expected)
                {
                    failures.AppendLine(
                        $"ISO {iso.Year:D4}-{iso.Month:D2}-{iso.Day:D2}: {actual.Year}-{actual.Month}-{actual.Day}, "
                        + $"but {by} says {expected.Year}-{expected.Month}-{expected.Day}");
                }
            }
        }

        days.Should().Be(2 * 801);
        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// The seam is a seam: the two rules answer differently on the very day the window ends, so the sweep
    /// above is asserting a choice rather than a coincidence.
    /// </summary>
    /// <remarks>
    /// At the bottom the cycle begins Persian year 1 a day before the table does, so ISO 622-03-21 and
    /// 622-03-22 both read as 1-01-01. At the top the table is thirteen days into Persian 9378's tenth
    /// month where the cycle is ten, so the answer steps two days backwards at ISO 10000-01-01. Each is
    /// one day of the year the two rules change over in, and neither is reachable from a date anything
    /// keeps.
    /// </remarks>
    [Test]
    public void TheTwoRulesDisagreeAtBothEdgesOfTheWindow()
    {
        var firstJdn = JdnOfIsoDay(FirstIsoDay);
        var lastJdn = JdnOfIsoDay(LastIsoDay);

        CyclePlace(firstJdn).Should().NotBe(PlacedByJint(firstJdn));
        CyclePlace(lastJdn).Should().NotBe(PlacedByJint(lastJdn));

        PlacedByJint(firstJdn - 1L).Should().Be((1L, 1, 1));
        PlacedByJint(firstJdn).Should().Be((1L, 1, 1));
        PlacedByJint(lastJdn).Should().Be((9378L, 10, 13));
        PlacedByJint(lastJdn + 1L).Should().Be((9378L, 10, 11));
    }

    /// <summary>
    /// A proleptic Persian year is as long as the cycle's leap rule says it is, and the next one starts
    /// exactly that many days later — which is the whole of what a calendar past the end of its table is.
    /// </summary>
    /// <remarks>
    /// Read through the engine in both directions, so the year-start formula, the leap predicate and the
    /// month lengths have to agree with each other as well as with the cycle. Nothing here touches the
    /// platform: every year sampled lies outside the window.
    /// </remarks>
    [TestCase(-272442, 200)]
    [TestCase(-100000, 100)]
    [TestCase(-200, 190)]
    [TestCase(9379, 400)]
    [TestCase(43666, 100)]
    [TestCase(275000, 100)]
    public void AProlepticYearIsAsLongAsTheCyclesLeapRuleSaysAndTheNextStartsThere(int firstYear, int years)
    {
        var failures = new StringBuilder();

        for (var year = firstYear; year < firstYear + years; year++)
        {
            var start = NonIsoCalendars.CalendarDateToIso("persian", year, "M01", 1, 1, "reject");
            var next = NonIsoCalendars.CalendarDateToIso("persian", year + 1, "M01", 1, 1, "reject");

            if (start is null || next is null)
            {
                failures.AppendLine($"{year}: the calendar declined to place its own first day");
                continue;
            }

            var startIso = start.Value;
            var startDay = TemporalHelpers.IsoDateToDays(startIso.Year, startIso.Month, startIso.Day);
            var nextDay = TemporalHelpers.IsoDateToDays(next.Value.Year, next.Value.Month, next.Value.Day);
            var expectedLength = CycleIsLeapYear(year) ? 366 : 365;

            var offBy = startDay + JdnOfEpochDay - CycleYearStartJdn(year);
            if (offBy != 0L)
            {
                failures.AppendLine(
                    $"{year}-01-01 is ISO {startIso.Year:D4}-{startIso.Month:D2}-{startIso.Day:D2}, "
                    + $"{offBy} days off the cycle's");
            }

            if (nextDay - startDay != expectedLength)
            {
                failures.AppendLine($"{year} is {nextDay - startDay} days long, not {expectedLength}");
            }

            var placed = NonIsoCalendars.IsoToCalendarDate("persian", in startIso);
            if (placed.Year != year || placed.Month != 1 || placed.Day != 1)
            {
                failures.AppendLine($"{year}-01-01 read back as {placed.Year}-{placed.Month}-{placed.Day}");
            }

            if (placed.DaysInYear != expectedLength || placed.InLeapYear != CycleIsLeapYear(year))
            {
                failures.AppendLine(
                    $"{year} reports {placed.DaysInYear} days and inLeapYear={placed.InLeapYear}, "
                    + $"not {expectedLength} and {CycleIsLeapYear(year)}");
            }
        }

        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// Every day of every month of a proleptic year, placed and read back: six months of 31 days, five of
    /// 30, and a twelfth of 30 or 29 as the leap rule says, laid end to end with no day left over.
    /// </summary>
    [TestCase(-272442)]
    [TestCase(9379)]
    [TestCase(9380)]
    [TestCase(43666)]
    [TestCase(275139)]
    public void EveryMonthOfAProlepticYearIsWhereTheCyclePutsIt(int year)
    {
        var failures = new StringBuilder();
        var jdn = CycleYearStartJdn(year);

        for (var month = 1; month <= 12; month++)
        {
            var length = CycleDaysInMonth(year, month);

            for (var day = 1; day <= length; day++, jdn++)
            {
                var iso = NonIsoCalendars.CalendarDateToIso("persian", year, $"M{month:D2}", month, day, "reject");
                if (iso is null)
                {
                    failures.AppendLine($"{year}-{month:D2}-{day:D2} was declined");
                    continue;
                }

                var isoDate = iso.Value;
                var placedJdn = TemporalHelpers.IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day) + JdnOfEpochDay;
                if (placedJdn != jdn)
                {
                    failures.AppendLine($"{year}-{month:D2}-{day:D2} is {placedJdn - jdn} days off the cycle's day");
                }

                var placed = NonIsoCalendars.IsoToCalendarDate("persian", in isoDate);
                if (placed.Year != year || placed.Month != month || placed.Day != day || placed.DaysInMonth != length)
                {
                    failures.AppendLine(
                        $"{year}-{month:D2}-{day:D2} read back as {placed.Year}-{placed.Month}-{placed.Day} "
                        + $"in a month of {placed.DaysInMonth} days, not {length}");
                }
            }

            var pastTheMonth = NonIsoCalendars.CalendarDateToIso("persian", year, $"M{month:D2}", month, length + 1, "reject");
            if (pastTheMonth is not null)
            {
                failures.AppendLine($"{year}-{month:D2} accepted a day {length + 1}");
            }
        }

        (jdn - CycleYearStartJdn(year)).Should().Be(CycleIsLeapYear(year) ? 366 : 365);
        failures.ToString().Should().BeEmpty();
    }
}
