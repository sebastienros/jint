#nullable enable

using System.Globalization;
using System.Text;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// The astronomical lunisolar reckoning, checked against the two BCL tables it takes over from.
/// </summary>
/// <remarks>
/// <para>
/// Outside <c>ChineseLunisolarCalendar</c>'s ISO 1901-02-19 to 2101-01-28 and
/// <c>KoreanLunisolarCalendar</c>'s 918-02-19 to 2051-02-10 there is nothing to check an answer against.
/// What these do instead is run the reckoning across the whole of each table and require it to reproduce
/// it: an algorithm that reproduces two independently compiled tables day for day is the same algorithm
/// outside them, which is the only claim that can be made about the years past their ends.
/// </para>
/// <para>
/// The tables reach back further than the calendar they tabulate does. China's present rules — true
/// solar terms rather than mean ones — date from the Shixian reform of 1645, and Korea reckoned the
/// Chinese calendar, in Chinese time, until 1912. Before those dates the tables record a different
/// calendar, which no modern algorithm reproduces and none should.
/// </para>
/// <para>
/// The tables also do not agree with each other across target frameworks, which is why only one window
/// below is asserted exactly. See the remarks on
/// <see cref="TheReckoningTracksTheRestOfBothTables"/> for what differs where.
/// </para>
/// </remarks>
public class LunisolarAstronomyTests
{
    private static readonly string[] TheTwoTables = ["chinese", "dangi"];

    private static EastAsianLunisolarCalendar TableFor(string calendar) => calendar switch
    {
        "chinese" => new ChineseLunisolarCalendar(),
        _ => new KoreanLunisolarCalendar(),
    };

    private static LunisolarRegion RegionFor(string calendar) => calendar switch
    {
        "chinese" => LunisolarRegion.China,
        _ => LunisolarRegion.Korea,
    };

    /// <summary>
    /// Every day of 1912 to 2051, read twice — once by <c>KoreanLunisolarCalendar</c> and once by the
    /// reckoning — with no allowance at all, and on every target framework.
    /// </summary>
    /// <remarks>
    /// 1912 is when Korea began reckoning its own calendar rather than China's, so those 50,811 days are
    /// the whole of what that table has to say about the calendar this implements. Reproducing them
    /// exactly is the strongest statement available about the years past the table's end, and it is the
    /// end of the table that matters: 2051-02-10 is where the reckoning takes over.
    /// </remarks>
    [Test]
    public void TheReckoningReproducesTheKoreanTableExactlyFrom1912()
    {
        var (compared, disagreements, report) = Compare("dangi", 1912);

        compared.Should().BeGreaterThan(50_000);
        report.Should().BeEmpty($"{disagreements} of {compared} days disagree");
    }

    /// <summary>
    /// The rest of both tables, bounded rather than exact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things put days on this side of the line, and only the first is about the reckoning. A new
    /// moon falling within minutes of local midnight lands on either side of it, which moves one month
    /// boundary by a day and so 30 days at a time. The centuries before China's Shixian reform of 1645
    /// are a different calendar — mean solar terms rather than true ones — which no modern algorithm
    /// reproduces and none should.
    /// </para>
    /// <para>
    /// And the tables do not agree with each other. <c>ChineseLunisolarCalendar</c> reads 2057-09-28 as
    /// 2057/9/1 on .NET 8 and as 2057/8/30 on .NET Framework 4.8 and .NET 10;
    /// <c>KoreanLunisolarCalendar</c> on .NET Framework starts five days earlier than its .NET Core
    /// counterpart and reads 1500-06-15 as 1500/5/19 where .NET Core reads 1500/5/9. So the measurement
    /// is per runtime: <c>chinese</c> from 1901 is 30, 60 and 120 days of 73,027 on .NET 8, .NET 10 and
    /// .NET Framework; <c>dangi</c> from 1654 is 2,218, 2,218 and 2,750 of 145,042. The allowances are
    /// the largest of those with room.
    /// </para>
    /// </remarks>
    [TestCase("chinese", 1901, 0.005)]
    [TestCase("dangi", 1654, 0.030)]
    public void TheReckoningTracksTheRestOfBothTables(string name, int fromIsoYear, double allowed)
    {
        var (compared, disagreements, _) = Compare(name, fromIsoYear);

        compared.Should().BeGreaterThan(70_000);
        ((double) disagreements / compared).Should().BeLessThan(
            allowed,
            "{0} of {1} days disagree",
            disagreements,
            compared);
    }

    private static (int Compared, int Disagreements, string Report) Compare(string name, int fromIsoYear)
    {
        var calendar = TableFor(name);
        var region = RegionFor(name);

        var first = new DateTime(fromIsoYear, 1, 1);
        if (first < calendar.MinSupportedDateTime)
        {
            first = calendar.MinSupportedDateTime.Date.AddDays(1);
        }

        var last = calendar.MaxSupportedDateTime.Date;

        var report = new StringBuilder();
        var compared = 0;
        var disagreements = 0;

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            compared++;

            var days = (long) (date - new DateTime(1970, 1, 1)).TotalDays;
            var year = LunisolarAstronomy.ForFixed(days, region);

            var ordinal = 1;
            while (ordinal < year.MonthCount && year.MonthStarts[ordinal] <= days)
            {
                ordinal++;
            }

            var day = (int) (days - year.MonthStarts[ordinal - 1]) + 1;
            var leapOrdinal = year.LeapIndex + 1;

            var tableYear = calendar.GetYear(date);
            var tableMonth = calendar.GetMonth(date);
            var tableDay = calendar.GetDayOfMonth(date);
            var tableLeap = calendar.GetLeapMonth(tableYear);

            if (year.Year == tableYear && ordinal == tableMonth && day == tableDay && leapOrdinal == tableLeap)
            {
                continue;
            }

            disagreements++;
            if (disagreements <= 20)
            {
                report
                    .Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    .Append(": table said ")
                    .Append(tableYear).Append('/').Append(tableMonth).Append('/').Append(tableDay)
                    .Append(" leap=").Append(tableLeap)
                    .Append(", reckoning said ")
                    .Append(year.Year).Append('/').Append(ordinal).Append('/').Append(day)
                    .Append(" leap=").Append(leapOrdinal)
                    .AppendLine();
            }
        }

        return (compared, disagreements, report.ToString());
    }

    /// <summary>
    /// The two directions have to be each other's inverse, or a date read out of a calendar cannot be put
    /// back into it — which is what <c>with</c>, <c>from</c> and every <c>PlainYearMonth</c> operation do.
    /// </summary>
    /// <remarks>
    /// The sweep runs a hundred thousand years each way, far outside either table, so it is also what
    /// exercises the bounded searches: the series stop being monotone out there, and a correction loop
    /// that assumed otherwise would not return from this at all.
    /// </remarks>
    [TestCaseSource(nameof(TheTwoTables))]
    public void ReadingAYearAndAskingForItByNumberFindTheSameYear(string name)
    {
        var region = RegionFor(name);

        var failures = new StringBuilder();
        var checkedDates = 0;

        for (var isoYear = -100_000; isoYear <= 100_000; isoYear += 997)
        {
            for (var isoMonth = 1; isoMonth <= 12; isoMonth += 5)
            {
                checkedDates++;

                var days = TemporalHelpers.IsoDateToDays(isoYear, isoMonth, 15);
                var year = LunisolarAstronomy.ForFixed(days, region);

                if (days < year.Start || days >= year.MonthStarts[year.MonthCount])
                {
                    failures.Append(isoYear).Append('-').Append(isoMonth)
                        .AppendLine(": the year found does not contain the date");
                    continue;
                }

                if (year.MonthCount is not (12 or 13))
                {
                    failures.Append(isoYear).Append('-').Append(isoMonth)
                        .Append(": month count is ").Append(year.MonthCount).AppendLine();
                    continue;
                }

                var byNumber = LunisolarAstronomy.ForYear(year.Year, region);
                if (byNumber is null || byNumber.Start != year.Start)
                {
                    failures.Append(isoYear).Append('-').Append(isoMonth)
                        .Append(": year ").Append(year.Year).AppendLine(" does not resolve back to the same start");
                }
            }
        }

        checkedDates.Should().BeGreaterThan(500);
        failures.ToString().Should().BeEmpty();
    }
}
