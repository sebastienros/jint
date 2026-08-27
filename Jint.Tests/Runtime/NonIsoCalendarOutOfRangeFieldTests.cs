#nullable enable

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions.Execution;

namespace Jint.Tests.Runtime;

/// <summary>
/// A date outside a non-ISO calendar's implementation range still reports that calendar's fields.
/// </summary>
/// <remarks>
/// <para>
/// <c>ChineseLunisolarCalendar</c> spans ISO 1901-02-19 to 2101-01-28 and <c>KoreanLunisolarCalendar</c>
/// 918-02-19 to 2051-02-10, while <c>Temporal.PlainDate</c> spans a quarter of a million years each way.
/// Outside those two tables the engine used to answer with the ISO year, <c>M{isoMonth}</c>, the ISO
/// days-in-month and twelve months a year — Gregorian fields wearing a lunisolar label, with nothing
/// downstream able to tell them from real ones
/// (<see href="https://github.com/sebastienros/jint/issues/3451"/>).
/// </para>
/// <para>
/// Refusing instead is not open to the engine.
/// <see href="https://tc39.es/proposal-temporal/#sec-temporal-nonisocalendarisotodate">NonISOCalendarISOToDate</see>
/// is declared as returning <em>a Calendar Date Record</em> and not "either a normal completion … or a
/// throw completion" — the phrasing its neighbour
/// <see href="https://tc39.es/proposal-temporal/#sec-temporal-nonisodateadd">NonISODateAdd</see> does
/// carry, which is what let the arithmetic refuse in
/// <see href="https://github.com/sebastienros/jint/pull/3452">#3452</see> — and
/// <c>get PlainDate.prototype.monthCode</c> reads it without <c>?</c>. So the accessors have to answer.
/// </para>
/// </remarks>
public class NonIsoCalendarOutOfRangeFieldTests
{
    /// <summary>
    /// A month code is <c>M</c> plus the month's zero-padded position in a common year of its calendar,
    /// with an <c>L</c> for a leap month — so <c>M13</c> is well formed for the thirteen-month Coptic and
    /// Ethiopic calendars and nothing above it ever is.
    /// </summary>
    private static readonly Regex WellFormedMonthCode = new(@"^M(0[1-9]|1[0-3])L?$", RegexOptions.Compiled);

    /// <summary>A lunisolar display month never reaches thirteen: the thirteenth is a leap month.</summary>
    private static readonly Regex WellFormedLunisolarMonthCode = new(@"^M(0[1-9]|1[0-2])L?$", RegexOptions.Compiled);

    private static string Evaluate(string expression)
    {
        var engine = new Engine();
        return engine.Evaluate(
            $"(function () {{ try {{ return String({expression}); }} catch (e) {{ return e.constructor.name; }} }})()").AsString();
    }

    private static string Fields(string isoDate, string calendar) => Evaluate(
        $"(function () {{ var d = Temporal.PlainDate.from('{isoDate}').withCalendar('{calendar}');" +
        " return [d.year, d.month, d.day, d.monthCode, d.monthsInYear, d.daysInMonth, d.daysInYear," +
        " d.dayOfYear, d.inLeapYear].join('|'); })()");

    /// <summary>
    /// The report from the issue. 1800-01-01 is not in the Chinese year 1800, it is not day 1 of a month
    /// coded <c>M01</c>, and the year it is in has twelve months because that year has twelve — not
    /// because twelve is what an ISO year has.
    /// </summary>
    [Test]
    public void TheReportedDatePastTheChineseRangeReportsChineseFields()
    {
        Fields("1800-01-01", "chinese").Should().Be("1799|12|7|M12|12|30|354|331|false");
    }

    /// <summary>
    /// 1800-01-01 is past the end of <c>ChineseLunisolarCalendar</c> and well inside
    /// <c>KoreanLunisolarCalendar</c>, and Korea reckoned the Chinese calendar until 1912. So the Korean
    /// table is an independent check on the reckoning that took over where the Chinese table stopped, and
    /// it says the same thing.
    /// </summary>
    /// <remarks>
    /// One date rather than a sweep, because .NET Framework's copy of the Korean table is not .NET Core's
    /// — it starts five days earlier and reads 1500-06-15 as 1500/5/19 where .NET Core reads 1500/5/9.
    /// The systematic form of this comparison, measured per runtime, is in
    /// <c>LunisolarAstronomyTests</c>.
    /// </remarks>
    [Test]
    public void ThePastDateTheKoreanTableAlsoCoversReadsTheSameInBoth()
    {
        Fields("1800-01-01", "chinese").Should().Be(Fields("1800-01-01", "dangi"));
    }

    /// <summary>
    /// The day before a table's first day and the day after its last: the reckoning that takes over has
    /// to leave the calendar where the table left it, or the boundary is a visible seam.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stated structurally rather than as dates, because the tables themselves differ between target
    /// frameworks. Both end on a year's last day, so what has to hold there is that the two sides make
    /// one continuous calendar: the day after the table's last is day 1 of the following year.
    /// </para>
    /// <para>
    /// The start of the table is asserted for <c>chinese</c> only. .NET Framework's
    /// <c>KoreanLunisolarCalendar</c> begins on 918-02-14 and calls it day 1 of the Korean year 918,
    /// while .NET Core's begins on 918-02-19 and calls that day 1 — so on .NET Framework there is no year
    /// boundary at the table's start for the reckoning to join to, and the seam there is that table's,
    /// not this one's.
    /// </para>
    /// </remarks>
    [TestCase("chinese", true)]
    [TestCase("dangi", false)]
    public void TheReckoningJoinsTheTableWithoutASeam(string calendar, bool tableStartsOnANewYear)
    {
        var table = calendar switch
        {
            "chinese" => (EastAsianLunisolarCalendar) new ChineseLunisolarCalendar(),
            _ => new KoreanLunisolarCalendar(),
        };

        var firstDay = table.MinSupportedDateTime.Date;
        var lastDay = table.MaxSupportedDateTime.Date;

        using var _ = new AssertionScope();

        if (tableStartsOnANewYear)
        {
            var beforeFirst = Read(firstDay.AddDays(-1), calendar);
            var first = Read(firstDay, calendar);

            first.Month.Should().Be(1, "the table starts on a new year's day");
            first.Day.Should().Be(1);
            first.DayOfYear.Should().Be(1);

            beforeFirst.Year.Should().Be(first.Year - 1, "the day before is in the preceding year");
            beforeFirst.Month.Should().Be(beforeFirst.MonthsInYear, "and is in that year's last month");
            beforeFirst.Day.Should().Be(beforeFirst.DaysInMonth, "on that month's last day");
            beforeFirst.DayOfYear.Should().Be(beforeFirst.DaysInYear, "which is that year's last day");
        }

        var last = Read(lastDay, calendar);
        var afterLast = Read(lastDay.AddDays(1), calendar);

        last.Day.Should().Be(last.DaysInMonth, "the table ends on a year's last day");
        last.DayOfYear.Should().Be(last.DaysInYear);

        afterLast.Year.Should().Be(last.Year + 1, "the day after starts the following year");
        afterLast.Month.Should().Be(1);
        afterLast.Day.Should().Be(1);
        afterLast.DayOfYear.Should().Be(1);
    }

    private readonly record struct Read4(
        int Year, int Month, int Day, string MonthCode,
        int MonthsInYear, int DaysInMonth, int DaysInYear, int DayOfYear);

    private static Read4 Read(DateTime date, string calendar)
    {
        var isoDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var f = Fields(isoDate, calendar).Split('|');

        return new Read4(
            int.Parse(f[0], CultureInfo.InvariantCulture),
            int.Parse(f[1], CultureInfo.InvariantCulture),
            int.Parse(f[2], CultureInfo.InvariantCulture),
            f[3],
            int.Parse(f[4], CultureInfo.InvariantCulture),
            int.Parse(f[5], CultureInfo.InvariantCulture),
            int.Parse(f[6], CultureInfo.InvariantCulture),
            int.Parse(f[7], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// What the fields report has to be a lunisolar calendar, whatever the date: months of 29 or 30 days,
    /// years of twelve or thirteen of them, and a well-formed month code.
    /// </summary>
    /// <remarks>
    /// This is the general form of the defect. The ISO-like answer it replaces reported months of 28 or
    /// 31 days and years of 365, which is what made it indistinguishable from a real one downstream.
    /// </remarks>
    [TestCase("chinese")]
    [TestCase("dangi")]
    public void EveryDatePastTheTableStillReportsALunisolarCalendar(string calendar)
    {
        var failures = new StringBuilder();
        var sampled = 0;

        for (var isoYear = -20_000; isoYear <= 20_000; isoYear += 401)
        {
            foreach (var isoMonth in new[] { 1, 4, 7, 10 })
            {
                var isoDate = (isoYear < 0 ? "-" : "+")
                    + System.Math.Abs(isoYear).ToString("D6", CultureInfo.InvariantCulture)
                    + "-" + isoMonth.ToString("D2", CultureInfo.InvariantCulture) + "-15";

                sampled++;
                var reported = Fields(isoDate, calendar).Split('|');
                if (reported.Length != 9)
                {
                    failures.Append(isoDate).Append(": ").AppendLine(string.Join("|", reported));
                    continue;
                }

                var day = int.Parse(reported[2], CultureInfo.InvariantCulture);
                var monthCode = reported[3];
                var monthsInYear = int.Parse(reported[4], CultureInfo.InvariantCulture);
                var daysInMonth = int.Parse(reported[5], CultureInfo.InvariantCulture);
                var daysInYear = int.Parse(reported[6], CultureInfo.InvariantCulture);
                var dayOfYear = int.Parse(reported[7], CultureInfo.InvariantCulture);

                if (monthsInYear is not (12 or 13)
                    || daysInMonth is not (29 or 30)
                    || daysInYear < 353 || daysInYear > 385
                    || day < 1 || day > daysInMonth
                    || dayOfYear < 1 || dayOfYear > daysInYear
                    || !WellFormedLunisolarMonthCode.IsMatch(monthCode))
                {
                    failures.Append(isoDate).Append(": ").AppendLine(string.Join("|", reported));
                }
            }
        }

        sampled.Should().BeGreaterThan(350);
        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// The general form of the defect, over all eleven non-ISO calendars: whatever the date, the fields
    /// have to describe the calendar that was asked, not the ISO one wearing its name.
    /// </summary>
    /// <remarks>
    /// A lunisolar year has twelve or thirteen months of 29 or 30 days; a Coptic or Ethiopic one has
    /// thirteen, twelve of thirty days and a thirteenth of five or six; an Islamic one twelve of 29 or 30;
    /// a Persian or Indian one twelve of 29 to 31. None of them has a 365-day year of 28-to-31-day months,
    /// which is what an ISO answer under a calendar's name looks like. Only <c>chinese</c> and
    /// <c>dangi</c> ever gave one — the other nine already had a reckoning of their own to fall back on —
    /// which is what makes this the regression guard for all eleven rather than a test of two.
    /// </remarks>
    [TestCase("chinese", 12, 13, 29, 30, 353, 385)]
    [TestCase("dangi", 12, 13, 29, 30, 353, 385)]
    [TestCase("hebrew", 12, 13, 29, 30, 353, 385)]
    [TestCase("persian", 12, 12, 29, 31, 365, 366)]
    [TestCase("indian", 12, 12, 29, 31, 365, 366)]
    [TestCase("coptic", 13, 13, 5, 30, 365, 366)]
    [TestCase("ethiopic", 13, 13, 5, 30, 365, 366)]
    [TestCase("ethioaa", 13, 13, 5, 30, 365, 366)]
    [TestCase("islamic-umalqura", 12, 12, 29, 30, 354, 355)]
    [TestCase("islamic-civil", 12, 12, 29, 30, 354, 355)]
    [TestCase("islamic-tbla", 12, 12, 29, 30, 354, 355)]
    public void NoCalendarReportsIsoFieldsUnderItsOwnName(
        string calendar,
        int minMonthsInYear,
        int maxMonthsInYear,
        int minDaysInMonth,
        int maxDaysInMonth,
        int minDaysInYear,
        int maxDaysInYear)
    {
        var failures = new StringBuilder();
        var sampled = 0;

        for (var isoYear = -20_000; isoYear <= 20_000; isoYear += 991)
        {
            foreach (var isoMonth in new[] { 1, 7 })
            {
                var isoDate = (isoYear < 0 ? "-" : "+")
                    + System.Math.Abs(isoYear).ToString("D6", CultureInfo.InvariantCulture)
                    + "-" + isoMonth.ToString("D2", CultureInfo.InvariantCulture) + "-15";

                sampled++;
                var reported = Fields(isoDate, calendar).Split('|');
                if (reported.Length != 9)
                {
                    failures.Append(isoDate).Append(": ").AppendLine(string.Join("|", reported));
                    continue;
                }

                var monthsInYear = int.Parse(reported[4], CultureInfo.InvariantCulture);
                var daysInMonth = int.Parse(reported[5], CultureInfo.InvariantCulture);
                var daysInYear = int.Parse(reported[6], CultureInfo.InvariantCulture);

                if (monthsInYear < minMonthsInYear || monthsInYear > maxMonthsInYear
                    || daysInMonth < minDaysInMonth || daysInMonth > maxDaysInMonth
                    || daysInYear < minDaysInYear || daysInYear > maxDaysInYear
                    || !WellFormedMonthCode.IsMatch(reported[3]))
                {
                    failures.Append(isoDate).Append(": ").AppendLine(string.Join("|", reported));
                }
            }
        }

        sampled.Should().BeGreaterThan(70);
        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// A date read out of a calendar has to go back into it: the fields the accessors report, handed to
    /// <c>from</c>, are the date they were read off. That is what <c>with</c> and every
    /// <c>PlainYearMonth</c> conversion depend on.
    /// </summary>
    [TestCase("chinese")]
    [TestCase("dangi")]
    public void FieldsReadPastTheTableGoBackIntoTheCalendar(string calendar)
    {
        var failures = new StringBuilder();

        foreach (var isoDate in new[]
                 {
                     "-020000-05-05", "-000500-03-03", "0500-01-01", "1500-06-15",
                     "1800-01-01", "1900-12-31", "2200-08-08", "2300-01-01", "9999-12-31",
                 })
        {
            var roundTripped = Evaluate(
                $"(function () {{ var d = Temporal.PlainDate.from('{isoDate}').withCalendar('{calendar}');" +
                $" return Temporal.PlainDate.from({{ calendar: '{calendar}', year: d.year," +
                " monthCode: d.monthCode, day: d.day }).equals(d); })()");

            if (!string.Equals(roundTripped, "true", StringComparison.Ordinal))
            {
                failures.Append(calendar).Append(' ').Append(isoDate).Append(": ").AppendLine(roundTripped);
            }
        }

        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// Nothing inside a table moves. The two calendars are read across their whole tabulated span and
    /// have to answer exactly what they answered before, which is what makes this a fallback rather than
    /// a replacement.
    /// </summary>
    /// <remarks>
    /// Except where the table contradicts itself, which <c>KoreanLunisolarCalendar</c> does:
    /// <c>GetMonth</c> names month 13 for 1189-01-09 while <c>GetLeapMonth</c> reports that year as
    /// having none, so <c>GetDaysInMonth</c> refuses the month <c>GetMonth</c> just named. Those dates
    /// used to come back as ISO fields under the calendar's name — the same defect from a second cause —
    /// and are now reckoned like the ones past the end of the table. They are skipped here because there
    /// is no table answer to compare against.
    /// </remarks>
    [TestCase("chinese", "1901-02-19", "2101-01-28")]
    [TestCase("dangi", "0918-02-19", "2051-02-10")]
    public void ADateInsideTheTableIsStillAnsweredByTheTable(string calendar, string first, string last)
    {
        var table = calendar switch
        {
            "chinese" => (EastAsianLunisolarCalendar) new ChineseLunisolarCalendar(),
            _ => new KoreanLunisolarCalendar(),
        };

        var failures = new StringBuilder();
        var sampled = 0;

        var from = DateTime.ParseExact(first, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = DateTime.ParseExact(last, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        for (var date = from; date <= to; date = date.AddDays(97))
        {
            try
            {
                _ = table.GetDaysInMonth(table.GetYear(date), table.GetMonth(date));
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            sampled++;
            var isoDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var reported = Fields(isoDate, calendar).Split('|');

            var expected = string.Join(
                "|",
                table.GetYear(date).ToString(CultureInfo.InvariantCulture),
                table.GetMonth(date).ToString(CultureInfo.InvariantCulture),
                table.GetDayOfMonth(date).ToString(CultureInfo.InvariantCulture));

            var actual = string.Join("|", reported[0], reported[1], reported[2]);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                failures.Append(isoDate).Append(": table said ").Append(expected)
                    .Append(", engine said ").AppendLine(actual);
            }
        }

        sampled.Should().BeGreaterThan(700);
        failures.ToString().Should().BeEmpty();
    }
}
