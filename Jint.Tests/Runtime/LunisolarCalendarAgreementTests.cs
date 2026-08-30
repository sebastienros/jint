#nullable enable

using System.Globalization;
using System.Text;

namespace Jint.Tests.Runtime;

/// <summary>
/// The <c>chinese</c> and <c>dangi</c> calendars answer the same on every target framework, and what they
/// answer is the calendar those two names denote.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here compares the engine against a <c>System.Globalization</c> calendar, which is the practice
/// that let <see href="https://github.com/sebastienros/jint/issues/3484"/> hide: every test that read
/// <c>chinese</c> or <c>dangi</c> compared it to whichever of those tables the runtime it was executing on
/// happened to carry, so three different answers all looked right.
/// <c>ChineseLunisolarCalendar</c> reads 2057-09-28 as 2057/9/1 on .NET 8 and as 2057/8/30 on .NET
/// Framework 4.8 and .NET 10; .NET Framework's <c>KoreanLunisolarCalendar</c> begins five days earlier
/// than .NET Core's and disagrees with it about 8,251 of the 9,106 month starts before 1600.
/// </para>
/// <para>
/// The expectations below come from outside the engine instead. The <c>chinese</c> ones are the Hong Kong
/// Observatory's published Gregorian–Lunar Calendar Conversion Table
/// (<see href="https://www.hko.gov.hk/en/gts/time/conversion.htm"/>), which covers 1901–2100 and is the
/// reference the calendar is maintained against. The <c>dangi</c> ones before 1912 are the Chinese
/// calendar of the same years, because Korea reckoned China's calendar until then, and are corroborated
/// by ICU and by .NET Core's table, both of which agree with them. <see cref="EveryMonthBeginsAtANewMoon"/>
/// needs no authority at all.
/// </para>
/// </remarks>
public class LunisolarCalendarAgreementTests
{
    private static string Fields(string isoDate, string calendar)
    {
        var engine = new Engine();
        return engine.Evaluate(
            $@"(function () {{
                 try {{
                   var d = Temporal.PlainDate.from('{isoDate}').withCalendar('{calendar}');
                   return d.year + '|' + d.monthCode + '|' + d.day;
                 }} catch (e) {{ return e.constructor.name + ': ' + e.message; }}
               }})()").AsString();
    }

    /// <summary>
    /// The two dates the issue leads with, and the ones on either side of them that separate the three
    /// runtimes. Each is the first day of a lunar month, so a table that is a day out reports it as day
    /// 29 or 30 of the month before.
    /// </summary>
    /// <remarks>
    /// Reading down the "before" column says which runtime each case used to fail on: 2057-09-28 was
    /// 2057/M08/30 on .NET Framework and .NET 10, 2089-09-04 and 2097-08-07 were the month before on
    /// .NET Framework, and the four <c>dangi</c> dates were five to nine days out on .NET Framework.
    /// </remarks>
    [TestCase("chinese", "1901-02-19", "1901|M01|1", TestName = "chinese, the first day the old table covered")]
    [TestCase("chinese", "1917-03-23", "1917|M02L|1", TestName = "chinese, the leap second month of 1917")]
    [TestCase("chinese", "1922-06-25", "1922|M05L|1", TestName = "chinese, the leap fifth month of 1922")]
    [TestCase("chinese", "1987-07-26", "1987|M06L|1", TestName = "chinese, the leap sixth month of 1987")]
    [TestCase("chinese", "2020-05-23", "2020|M04L|1", TestName = "chinese, the leap fourth month of 2020")]
    [TestCase("chinese", "2024-02-10", "2024|M01|1", TestName = "chinese, new year 2024")]
    [TestCase("chinese", "2033-12-22", "2033|M11L|1", TestName = "chinese, the leap eleventh month of 2033")]
    [TestCase("chinese", "2057-09-28", "2057|M09|1", TestName = "chinese, the ninth month of 2057")]
    [TestCase("chinese", "2089-09-04", "2089|M08|1", TestName = "chinese, the eighth month of 2089")]
    [TestCase("chinese", "2097-08-07", "2097|M07|1", TestName = "chinese, the seventh month of 2097")]
    [TestCase("chinese", "1800-01-01", "1799|M12|7", TestName = "chinese, a century before the old table")]
    [TestCase("dangi", "1000-08-08", "1000|M07|1", TestName = "dangi, the seventh month of 1000")]
    [TestCase("dangi", "1200-03-01", "1200|M02|8", TestName = "dangi, the second month of 1200")]
    [TestCase("dangi", "1400-06-15", "1400|M05|14", TestName = "dangi, the fifth month of 1400")]
    [TestCase("dangi", "1500-06-15", "1500|M05|9", TestName = "dangi, the fifth month of 1500")]
    [TestCase("dangi", "1912-02-18", "1912|M01|1", TestName = "dangi, new year 1912")]
    [TestCase("dangi", "2020-05-23", "2020|M04L|1", TestName = "dangi, the leap fourth month of 2020")]
    [TestCase("dangi", "2050-01-01", "2049|M12|8", TestName = "dangi, near the end of the old Korean table")]
    public void ADateReadsTheSameOnEveryTargetFramework(string calendar, string isoDate, string expected)
    {
        Fields(isoDate, calendar).Should().Be(expected, $"{calendar} {isoDate}");
    }

    /// <summary>
    /// Korea reckoned China's calendar until 1912, so over the years both of the retired tables covered —
    /// 1901 through 1911 — <c>dangi</c> and <c>chinese</c> have to be the same calendar day for day.
    /// </summary>
    [Test]
    public void DangiIsTheChineseCalendarBefore1912()
    {
        var failures = new StringBuilder();

        for (var date = new DateTime(1901, 2, 19); date < new DateTime(1912, 1, 1); date = date.AddDays(13))
        {
            var iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var chinese = Fields(iso, "chinese");
            var dangi = Fields(iso, "dangi");
            if (!string.Equals(chinese, dangi, StringComparison.Ordinal))
            {
                failures.Append(iso).Append(": chinese ").Append(chinese).Append(", dangi ").AppendLine(dangi);
            }
        }

        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// The one property of a lunisolar calendar that needs no table and no authority: a month begins on
    /// the day of a new moon. Every month start the engine reports has to sit within two days of the mean
    /// new moon, which is loose enough for the ±0.6 day the true conjunction wanders from the mean and for
    /// any meridian, and tight enough that a table which is not a lunisolar calendar cannot pass.
    /// </summary>
    /// <remarks>
    /// This is what convicts .NET Framework's <c>KoreanLunisolarCalendar</c> without appeal to any
    /// reference: over 918–1600 it puts 8,225 of its 8,447 month starts more than two days from a new
    /// moon, a median of 7.2 days out — around first quarter. ICU, .NET Core's two tables and the
    /// reckoning all sit inside ±2 days for every one of theirs.
    /// </remarks>
    [TestCase("chinese")]
    [TestCase("dangi")]
    public void EveryMonthBeginsAtANewMoon(string calendar)
    {
        // The mean synodic month and the mean new moon of 2000-01-06 18:14 UT, as a Julian day number.
        const double MeanSynodicMonth = 29.530588861;
        const double MeanNewMoonJ2000 = 2451550.09766;

        var engine = new Engine();
        var failures = new StringBuilder();
        var monthStarts = 0;

        foreach (var year in new[] { 950, 1100, 1250, 1400, 1500, 1600, 1750, 1850, 1920, 1990, 2030, 2080 })
        {
            for (var date = new DateTime(year, 1, 1); date < new DateTime(year + 1, 1, 1); date = date.AddDays(1))
            {
                var iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var day = engine.Evaluate(
                    $"Temporal.PlainDate.from('{iso}').withCalendar('{calendar}').day").AsNumber();

                if (day != 1)
                {
                    continue;
                }

                monthStarts++;

                // Julian day number of this date at midnight UT.
                var julianDay = 2440587.5 + (date - new DateTime(1970, 1, 1)).TotalDays;
                var cycles = System.Math.Round((julianDay + 0.5 - MeanNewMoonJ2000) / MeanSynodicMonth);
                var offset = MeanNewMoonJ2000 + (MeanSynodicMonth * cycles) - julianDay;

                if (System.Math.Abs(offset) > 2.0)
                {
                    failures.Append(iso).Append(": day 1 of a month, ")
                        .Append(offset.ToString("F2", CultureInfo.InvariantCulture))
                        .AppendLine(" days from the nearest mean new moon");
                }
            }
        }

        monthStarts.Should().BeGreaterThan(140);
        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// <c>Intl.DateTimeFormat</c> and <c>Temporal</c> read the same calendar, so they have to name the
    /// same month and day — including outside the span the retired tables covered, where
    /// <c>Intl</c> used to clamp to the table's own first or last date and report that instead.
    /// </summary>
    [TestCase("chinese", "1800-01-01")]
    [TestCase("chinese", "2057-09-28")]
    [TestCase("chinese", "2200-06-15")]
    [TestCase("dangi", "1500-06-15")]
    [TestCase("dangi", "2057-09-28")]
    [TestCase("dangi", "0800-01-01")]
    public void IntlNamesTheSameDayAsTemporal(string calendar, string isoDate)
    {
        var year = int.Parse(isoDate.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
        var month = int.Parse(isoDate.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
        var day = int.Parse(isoDate.Substring(8, 2), NumberStyles.None, CultureInfo.InvariantCulture);

        var engine = new Engine();
        var both = engine.Evaluate(
            $@"(function () {{
                 var d = Temporal.PlainDate.from('{isoDate}').withCalendar('{calendar}');
                 var parts = new Intl.DateTimeFormat('en-u-ca-{calendar}', {{
                     year: 'numeric', month: 'numeric', day: 'numeric', timeZone: 'UTC' }})
                   .formatToParts(new Date(Date.UTC({year}, {month - 1}, {day})));
                 var read = {{}};
                 for (var i = 0; i < parts.length; i++) {{ read[parts[i].type] = parts[i].value; }}
                 return d.year + '/' + Number(d.monthCode.substring(1, 3)) + '/' + d.day
                      + '  ' + read.relatedYear + '/' + read.month + '/' + read.day;
               }})()").AsString();

        var halves = both.Split(new[] { "  " }, StringSplitOptions.None);
        halves[1].Should().Be(halves[0], $"{calendar} {isoDate}: Intl and Temporal name the same day");
    }
}
