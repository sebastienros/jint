using System.Threading;

namespace Jint.Native.Temporal;

/// <summary>
/// Which lunisolar reckoning a date is being read in — the two differ only in the meridian their
/// new moons and solar terms are timed against.
/// </summary>
internal enum LunisolarRegion
{
    /// <summary>The Chinese calendar (<c>chinese</c>), timed against Beijing.</summary>
    China,

    /// <summary>The Korean calendar (<c>dangi</c>), timed against Seoul.</summary>
    Korea,
}

/// <summary>
/// One lunisolar year: where it starts, how many months it has, and which of them is the leap month.
/// </summary>
internal sealed class LunisolarYear
{
    /// <summary>The related Gregorian year, which is the year <c>chinese</c> and <c>dangi</c> report.</summary>
    internal int Year;

    /// <summary>Days since 1970-01-01 of the first day of the first month.</summary>
    internal long Start;

    /// <summary>Twelve, or thirteen in a year carrying a leap month.</summary>
    internal int MonthCount;

    /// <summary>Zero-based index of the leap month, or −1 when the year has none.</summary>
    internal int LeapIndex;

    /// <summary>
    /// <see cref="MonthCount"/> + 1 entries: the first day of each month, and then the first day of the
    /// following year, so a month's length is always the difference between two adjacent entries.
    /// </summary>
    internal long[] MonthStarts = [];

    internal int DaysInYear => (int) (MonthStarts[MonthCount] - Start);

    internal int DaysInMonth(int ordinalMonth) => (int) (MonthStarts[ordinalMonth] - MonthStarts[ordinalMonth - 1]);
}

/// <summary>
/// The astronomical reckoning behind the Chinese and Korean lunisolar calendars, and the only thing that
/// answers for either of them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Globalization.ChineseLunisolarCalendar"/> and
/// <see cref="System.Globalization.KoreanLunisolarCalendar"/> are tables, and short ones: ISO 1901-02-19
/// to 2101-01-28 and 918-02-19 to 2051-02-10, while <c>Temporal.PlainDate</c> spans ±10<sup>8</sup> days.
/// Outside them the engine used to answer with the ISO year, <c>M{isoMonth}</c> and twelve months a year —
/// Gregorian fields wearing a lunisolar label (<see href="https://github.com/sebastienros/jint/issues/3451"/>).
/// It cannot refuse instead:
/// <see href="https://tc39.es/proposal-temporal/#sec-temporal-nonisocalendarisotodate">NonISOCalendarISOToDate</see>
/// is declared as returning <em>a Calendar Date Record</em>, with no throw completion, and
/// <c>get PlainDate.prototype.monthCode</c> reads it without <c>?</c>. So it has to answer, and the answer
/// has to be the calendar.
/// </para>
/// <para>
/// Nor are those two tables the same table on every target framework, which is why nothing reads them any
/// more: <c>ChineseLunisolarCalendar</c> read 2057-09-28 as 2057/9/1 on .NET 8 and as 2057/8/30 on .NET
/// Framework 4.8 and .NET 10, and .NET Framework's <c>KoreanLunisolarCalendar</c> put 8,225 of its 8,447
/// month starts before 1600 more than two days from a new moon
/// (<see href="https://github.com/sebastienros/jint/issues/3484"/>).
/// </para>
/// <para>
/// What is implemented is the reckoning both calendars are actually defined by: months begin on the day of
/// an astronomical new moon in the calendar's own time zone, the eleventh month is the one containing the
/// winter solstice, and a year that needs a thirteenth month takes it at the first month carrying no major
/// solar term. The solar-longitude, new-moon and ephemeris-correction series are Reingold and Dershowitz,
/// <em>Calendrical Calculations</em> (4th ed.), §14.4 and §19.4 — the same body of algorithms ICU reckons
/// these calendars with. Measured against the Hong Kong Observatory's published conversion table over
/// 1901–2100, it names one of 2,473 month boundaries differently, where the three BCL tables name none,
/// one and three, and ICU fifteen.
/// </para>
/// <para>
/// <b>Every search here is bounded.</b> The series are polynomials in centuries from J2000, and a quarter
/// of a million years out their higher-order terms grow until the new-moon sequence is no longer strictly
/// increasing. A correction loop that assumed monotonicity would not terminate there, and a CLR loop
/// inside one interpreter step crosses no statement boundary, so no execution constraint could interrupt
/// it (<see href="https://github.com/sebastienros/jint/issues/3428"/>). Each loop below therefore has an
/// iteration cap and a defined answer when it reaches it: further out the reckoning degrades, which
/// implementation-defined permits, but it always answers and always returns.
/// </para>
/// </remarks>
internal static class LunisolarAstronomy
{
    private const double MeanSynodicMonth = 29.530588861;
    private const double MeanTropicalYear = 365.242189;

    /// <summary>Days since 1970-01-01 of 2000-01-01 12:00 UT, the epoch the series are written against.</summary>
    private const double J2000 = 10957.5;

    /// <summary>The solar longitude of the winter solstice, which defines the eleventh month.</summary>
    private const double WinterSolstice = 270.0;

    /// <summary>
    /// How far a bounded correction loop may walk. Every one of them normally settles in one or two
    /// steps; the cap only matters where the series stop being monotone, and there it fixes an answer
    /// rather than searching forever.
    /// </summary>
    private const int MaxCorrectionSteps = 8;

    /// <summary>
    /// How far the winter-solstice scan may walk from its estimate. The estimate is accurate to about a
    /// day for any date the series still models, and one year of slack covers the rest.
    /// </summary>
    private const int MaxSolsticeScan = 400;

    private static readonly long _koreaZoneChange1912 = TemporalHelpers.IsoDateToDays(1912, 1, 1);
    private static readonly long _koreaZoneChange1954 = TemporalHelpers.IsoDateToDays(1954, 3, 21);
    private static readonly long _koreaZoneChange1961 = TemporalHelpers.IsoDateToDays(1961, 8, 10);
    private static readonly long _daysTo1900 = TemporalHelpers.IsoDateToDays(1900, 1, 1);

    /// <summary>
    /// How many built years each calendar keeps. A power of two, so the slot is a mask rather than a
    /// division, and wide enough to hold the ±75-year window <c>FindReferenceYear</c> sweeps for a
    /// <c>PlainMonthDay</c> without a year evicting another: 151 consecutive years map to 151 slots.
    /// </summary>
    /// <remarks>
    /// A year costs a dozen new moons and a pair of solstice searches to build, and reading one date's
    /// fields asks for the same year once per accessor, so the first tier is what makes reading a date
    /// cost one build rather than six. The second is what makes a month-by-month difference walk
    /// affordable: measuring 2050-01-01 to 2300-01-03 in months steps through 250 lunisolar years some
    /// three thousand times.
    /// </remarks>
    private const int YearCacheSize = 256;

    // _lastChina / _lastKorea are the first tier and answer the common shape on their own: reading a run
    // of dates asks for the same year over and over. They are [ThreadStatic] because they need no
    // synchronisation at all that way.
    [ThreadStatic]
    private static LunisolarYear? _lastChina;

    [ThreadStatic]
    private static LunisolarYear? _lastKorea;

    // The second tier is shared, because a LunisolarYear is a pure function of its year and region --
    // two threads building one separately build the same thing, and nothing engine-affine goes in here.
    // Volatile.Write publishes it: every field of the year is written before the reference is, and the
    // Volatile.Read below is the matching acquire, so no thread can observe a half-built one. A slot lost
    // to a race costs a rebuild and nothing else.
    private static readonly LunisolarYear?[] _cacheChina = new LunisolarYear?[YearCacheSize];

    private static readonly LunisolarYear?[] _cacheKorea = new LunisolarYear?[YearCacheSize];

    private static LunisolarYear?[] CacheFor(LunisolarRegion region)
        => region == LunisolarRegion.China ? _cacheChina : _cacheKorea;

    /// <summary>The cached year, or null when it has not been built.</summary>
    private static LunisolarYear? Cached(int year, LunisolarRegion region)
    {
        var entry = Volatile.Read(ref CacheFor(region)[year & (YearCacheSize - 1)]);
        return entry is not null && entry.Year == year ? entry : null;
    }

    /// <summary>
    /// The lunisolar year containing <paramref name="days"/> (days since 1970-01-01).
    /// </summary>
    internal static LunisolarYear ForFixed(long days, LunisolarRegion region)
    {
        var last = region == LunisolarRegion.China ? _lastChina : _lastKorea;
        if (last is not null && days >= last.Start && days < last.MonthStarts[last.MonthCount])
        {
            return last;
        }

        // A lunisolar year carries the number of the Gregorian year it mostly falls in, and begins in that
        // year's first two months, so the year holding a date is that Gregorian year or the one before.
        // The range check is what makes a hit a hit; the slot lookup only narrows the two candidates.
        var gregorian = GregorianYearOf(days);
        for (var candidate = gregorian; candidate >= gregorian - 1; candidate--)
        {
            var memo = Cached(candidate, region);
            if (memo is not null && days >= memo.Start && days < memo.MonthStarts[memo.MonthCount])
            {
                return Remember(memo, region);
            }
        }

        return Remember(Build(NewYearOnOrBefore(days, region), region), region);
    }

    /// <summary>
    /// The lunisolar year whose first month falls in the Gregorian year <paramref name="year"/>, or null
    /// when no lunisolar year starts in it — reachable only where the series have drifted far enough for
    /// a Gregorian year to contain no new year at all.
    /// </summary>
    internal static LunisolarYear? ForYear(int year, LunisolarRegion region)
    {
        var last = region == LunisolarRegion.China ? _lastChina : _lastKorea;
        if (last is not null && last.Year == year)
        {
            return last;
        }

        var memo = Cached(year, region);
        if (memo is not null)
        {
            return Remember(memo, region);
        }

        // A related Gregorian year outside Temporal's own ISO range cannot name a representable date
        // whatever this answers, and the series are polynomials: evaluating them at an arbitrary caller-
        // supplied year is how a field record ends up built from an overflowed double.
        if (year < -271821 || year > 275760)
        {
            return null;
        }

        // A lunisolar new year falls in the first two months of its related Gregorian year, so the new
        // year on or before that year's 1 July is normally the one wanted. The two bounded walks below
        // correct for a drift large enough to have moved it out of the first half of the year.
        var start = NewYearOnOrBefore(TemporalHelpers.IsoDateToDays(year, 7, 1), region);

        for (var i = 0; i < MaxCorrectionSteps && GregorianYearOf(start) > year; i++)
        {
            start = NewYearOnOrBefore(start - 1, region);
        }

        for (var i = 0; i < MaxCorrectionSteps; i++)
        {
            var next = NewYearOnOrBefore(start + 400, region);
            if (next <= start || GregorianYearOf(next) > year)
            {
                break;
            }

            start = next;
        }

        return GregorianYearOf(start) == year ? Remember(Build(start, region), region) : null;
    }

    private static LunisolarYear Remember(LunisolarYear year, LunisolarRegion region)
    {
        Volatile.Write(ref CacheFor(region)[year.Year & (YearCacheSize - 1)], year);

        if (region == LunisolarRegion.China)
        {
            _lastChina = year;
        }
        else
        {
            _lastKorea = year;
        }

        return year;
    }

    private static LunisolarYear Build(long start, LunisolarRegion region)
    {
        // 400 days is longer than any lunisolar year (thirteen months is at most 385 days) and shorter
        // than two of them, so the new year on or before it is always the following one.
        var next = NewYearOnOrBefore(start + 400, region);

        var starts = new long[14];
        starts[0] = start;

        var count = 13;
        for (var k = 1; k <= 13; k++)
        {
            starts[k] = NewMoonOnOrAfter(starts[k - 1] + 1, region);
            if (starts[k] >= next)
            {
                count = k;
                break;
            }
        }

        starts[count] = System.Math.Max(next, starts[count - 1] + 1);

        var leapIndex = -1;
        if (count == 13)
        {
            for (var k = 0; k < count; k++)
            {
                // NoMajorSolarTerm is the cheap half of the test and is true for at most a month or two
                // in a year, so IsLeapMonth -- which has to reckon the whole sui the month belongs to --
                // is asked only about those.
                if (NoMajorSolarTerm(starts[k], region) && IsLeapMonth(starts[k], region))
                {
                    leapIndex = k;
                    break;
                }
            }

            // A thirteen-month year has a leap month by construction. Where the series have degraded far
            // enough that no month reports itself leap, the last one takes the role, so that the display
            // months stay inside M01..M12 and the month codes stay well-formed.
            if (leapIndex < 0)
            {
                leapIndex = count - 1;
            }
        }

        return new LunisolarYear
        {
            Year = GregorianYearOf(start),
            Start = start,
            MonthCount = count,
            LeapIndex = leapIndex,
            MonthStarts = starts,
        };
    }

    #region Lunisolar structure

    /// <summary>
    /// The first day of the lunisolar year containing <paramref name="days"/>.
    /// </summary>
    private static long NewYearOnOrBefore(long days, LunisolarRegion region)
    {
        var newYear = NewYearInSui(days, region);
        return days >= newYear ? newYear : NewYearInSui(days - 180, region);
    }

    /// <summary>
    /// The first day of the lunisolar year in the <em>sui</em> — the solstice-to-solstice span —
    /// containing <paramref name="days"/>. A sui of thirteen new moons carries the leap month, and the
    /// new year is then one month later than it would otherwise be.
    /// </summary>
    private static long NewYearInSui(long days, LunisolarRegion region)
    {
        var s1 = WinterSolsticeOnOrBefore(days, region);
        var s2 = WinterSolsticeOnOrBefore(s1 + 370, region);
        var m12 = NewMoonOnOrAfter(s1 + 1, region);
        var m13 = NewMoonOnOrAfter(m12 + 1, region);
        var nextM11 = NewMoonBefore(s2 + 1, region);

        var leapSui = (long) System.Math.Round((nextM11 - m12) / MeanSynodicMonth) == 12L;
        if (leapSui && (NoMajorSolarTerm(m12, region) || NoMajorSolarTerm(m13, region)))
        {
            return NewMoonOnOrAfter(m13 + 1, region);
        }

        return m13;
    }

    /// <summary>
    /// Whether the month beginning on <paramref name="monthStart"/> is the leap month of the
    /// <em>sui</em> it falls in — the solstice-to-solstice span, which is the window the rule is stated
    /// over rather than the calendar year.
    /// </summary>
    /// <remarks>
    /// Reading it off the calendar year instead is wrong, and 2033 is where it shows: that year holds two
    /// months with no major solar term, one in a sui of twelve months, which can carry no leap month at
    /// all, and one in the following sui of thirteen, which is the leap eleventh month the calendar
    /// actually has.
    /// </remarks>
    private static bool IsLeapMonth(long monthStart, LunisolarRegion region)
    {
        var s1 = WinterSolsticeOnOrBefore(monthStart, region);
        var s2 = WinterSolsticeOnOrBefore(s1 + 370, region);
        var m12 = NewMoonOnOrAfter(s1 + 1, region);
        var nextM11 = NewMoonBefore(s2 + 1, region);

        if ((long) System.Math.Round((nextM11 - m12) / MeanSynodicMonth) != 12L)
        {
            // A sui of twelve months has no month to spare.
            return false;
        }

        // The leap month is the first month of the sui carrying no major solar term, so any earlier one
        // that carries none takes the role instead.
        var month = m12;
        for (var i = 0; i < 14 && month < monthStart; i++)
        {
            if (NoMajorSolarTerm(month, region))
            {
                return false;
            }

            var following = NewMoonOnOrAfter(month + 1, region);
            if (following <= month)
            {
                break;
            }

            month = following;
        }

        return true;
    }

    /// <summary>
    /// Whether the month beginning on <paramref name="monthStart"/> contains no major solar term, which
    /// is the first half of what makes a month a leap month.
    /// </summary>
    private static bool NoMajorSolarTerm(long monthStart, LunisolarRegion region)
    {
        return CurrentMajorSolarTerm(monthStart, region)
            == CurrentMajorSolarTerm(NewMoonOnOrAfter(monthStart + 1, region), region);
    }

    /// <summary>The index, 1 to 12, of the major solar term in force on <paramref name="days"/>.</summary>
    private static int CurrentMajorSolarTerm(long days, LunisolarRegion region)
    {
        var longitude = SolarLongitude(UniversalFromStandard(days, region));
        return Amod(2 + (int) System.Math.Floor(longitude / 30.0), 12);
    }

    private static long WinterSolsticeOnOrBefore(long days, LunisolarRegion region)
    {
        var approx = EstimatePriorSolarLongitude(WinterSolstice, UniversalFromStandard(days + 1, region));
        var candidate = (long) System.Math.Floor(approx) - 1L;

        for (var i = 0; i < MaxSolsticeScan; i++)
        {
            if (WinterSolstice < SolarLongitude(UniversalFromStandard(candidate + 1, region)))
            {
                return candidate;
            }

            candidate++;
        }

        return (long) System.Math.Floor(approx);
    }

    private static long NewMoonOnOrAfter(long days, LunisolarRegion region)
    {
        var moment = UniversalFromStandard(days, region);
        var moon = NthNewMoon(NewMoonIndexBefore(moment) + 1);
        return (long) System.Math.Floor(StandardFromUniversal(moon, region));
    }

    private static long NewMoonBefore(long days, LunisolarRegion region)
    {
        var moment = UniversalFromStandard(days, region);
        var moon = NthNewMoon(NewMoonIndexBefore(moment));
        return (long) System.Math.Floor(StandardFromUniversal(moon, region));
    }

    /// <summary>
    /// The largest <c>n</c> with <c>NthNewMoon(n)</c> strictly before <paramref name="moment"/>. The mean
    /// synodic month gives a first index; the three bounded walks below correct it.
    /// </summary>
    private static long NewMoonIndexBefore(double moment)
    {
        var n = (long) System.Math.Round((moment - _newMoonZero) / MeanSynodicMonth);

        // The mean synodic month alone is not enough of an estimate: the difference between dynamical and
        // universal time is a quadratic in the year, and a hundred thousand years out it is more than a
        // year on its own, which is a dozen months of error in a linear guess. Stepping by the residual
        // converges from any distance, in one step for every date the series are accurate over.
        for (var i = 0; i < MaxCorrectionSteps; i++)
        {
            var step = (long) System.Math.Round((moment - NthNewMoon(n)) / MeanSynodicMonth);
            if (step == 0L)
            {
                break;
            }

            n += step;
        }

        for (var i = 0; i < MaxCorrectionSteps && NthNewMoon(n) >= moment; i++)
        {
            n--;
        }

        for (var i = 0; i < MaxCorrectionSteps && NthNewMoon(n + 1) < moment; i++)
        {
            n++;
        }

        return n;
    }

    #endregion

    #region Time zones

    /// <summary>
    /// The calendar's own zone offset, in days. Both calendars are reckoned in civil time at a meridian,
    /// and both changed which one: China from Beijing local mean time to UTC+8 in 1929, Korea from
    /// China's meridian to its own in 1912 and then twice more.
    /// </summary>
    private static double Zone(double moment, LunisolarRegion region)
    {
        var days = (long) System.Math.Floor(moment);

        if (region == LunisolarRegion.China)
        {
            // 1397/180 hours is the mean solar time of 116°25′E, the meridian of the Beijing observatory.
            return GregorianYearOf(days) < 1929 ? 1397.0 / 180.0 / 24.0 : 8.0 / 24.0;
        }

        // Korea reckoned the Chinese calendar, in Chinese time, until 1912; KoreanLunisolarCalendar's own
        // table says so, and reading those centuries against Seoul's meridian instead disagrees with it
        // more than twice as often. From 1912 the reckoning is Korea's own civil time, which moved twice.
        if (days < _koreaZoneChange1912) return 1397.0 / 180.0 / 24.0;
        if (days < _koreaZoneChange1954) return 9.0 / 24.0;
        if (days < _koreaZoneChange1961) return 8.5 / 24.0;
        return 9.0 / 24.0;
    }

    private static double UniversalFromStandard(double moment, LunisolarRegion region) => moment - Zone(moment, region);

    private static double StandardFromUniversal(double moment, LunisolarRegion region) => moment + Zone(moment, region);

    #endregion

    #region Solar longitude

    private static readonly int[] _solarCoefficients =
    [
        403406, 195207, 119433, 112392, 3891, 2819, 1721, 660, 350, 334,
        314, 268, 242, 234, 158, 132, 129, 114, 99, 93,
        86, 78, 72, 68, 64, 46, 38, 37, 32, 29,
        28, 27, 27, 25, 24, 21, 21, 20, 18, 17,
        14, 13, 13, 13, 12, 10, 10, 10, 10,
    ];

    private static readonly double[] _solarMultipliers =
    [
        270.54861, 340.19128, 63.91854, 331.26220, 317.843, 86.631, 240.052, 310.26, 247.23, 260.87,
        297.82, 343.14, 166.79, 81.53, 3.50, 132.75, 182.95, 162.03, 29.8, 266.4,
        249.2, 157.6, 257.8, 185.1, 69.9, 8.0, 197.1, 250.4, 65.3, 162.7,
        341.5, 291.6, 98.5, 146.7, 110.0, 5.2, 342.6, 230.9, 256.1, 45.3,
        242.9, 115.2, 151.8, 285.3, 53.3, 126.6, 205.7, 85.9, 146.1,
    ];

    private static readonly double[] _solarAddends =
    [
        0.9287892, 35999.1376958, 35999.4089666, 35998.7287385, 71998.20261, 71998.4403, 36000.35726,
        71997.4812, 32964.4678, -19.4410, 445267.1117, 45036.8840, 3.1008, 22518.4434, -19.9739,
        65928.9345, 9038.0293, 3034.7684, 33718.148, 3034.448, -2280.773, 29929.992, 31556.493,
        149.588, 9037.750, 107997.405, -4444.176, 151.771, 67555.316, 31556.080, -4561.540,
        107996.706, 1221.655, 62894.167, 31437.369, 14578.298, -31931.757, 34777.243, 1221.999,
        62894.511, -4442.039, 107997.909, 119.066, 16859.071, -4.578, 26895.292, -39.127,
        12297.536, 90073.778,
    ];

    /// <summary>The apparent longitude of the sun, in degrees, at a universal <paramref name="moment"/>.</summary>
    private static double SolarLongitude(double moment)
    {
        var c = JulianCenturies(moment);

        var sum = 0.0;
        for (var i = 0; i < _solarCoefficients.Length; i++)
        {
            sum += _solarCoefficients[i] * SinDegrees(_solarMultipliers[i] + _solarAddends[i] * c);
        }

        var longitude = 282.7771834 + 36000.76953744 * c + 0.000005729577951308232 * sum;
        return Mod(longitude + Aberration(c) + Nutation(c), 360.0);
    }

    private static double Nutation(double c)
    {
        var a = 124.90 - 1934.134 * c + 0.002063 * c * c;
        var b = 201.11 + 72001.5377 * c + 0.00057 * c * c;
        return -0.004778 * SinDegrees(a) - 0.0003667 * SinDegrees(b);
    }

    private static double Aberration(double c)
    {
        return 0.0000974 * CosDegrees(177.63 + 35999.01848 * c) - 0.005575;
    }

    /// <summary>
    /// An estimate of the last moment at or before <paramref name="moment"/> at which the sun was at
    /// <paramref name="longitude"/>, run back at the sun's mean rate and then corrected once.
    /// </summary>
    private static double EstimatePriorSolarLongitude(double longitude, double moment)
    {
        var rate = MeanTropicalYear / 360.0;
        var tau = moment - rate * Mod(SolarLongitude(moment) - longitude, 360.0);
        var delta = Mod(SolarLongitude(tau) - longitude + 180.0, 360.0) - 180.0;
        return System.Math.Min(moment, tau - rate * delta);
    }

    #endregion

    #region New moons

    private static readonly double[] _newMoonSineCoefficients =
    [
        -0.40720, 0.17241, 0.01608, 0.01039, 0.00739, -0.00514, 0.00208, -0.00111, -0.00057, 0.00056,
        -0.00042, 0.00042, 0.00038, -0.00024, -0.00007, 0.00004, 0.00004, 0.00003, 0.00003, -0.00003,
        0.00003, -0.00002, -0.00002, 0.00002,
    ];

    private static readonly int[] _newMoonEccentricityFactors =
    [
        0, 1, 0, 0, 1, 1, 2, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] _newMoonSolarCoefficients =
    [
        0, 1, 0, 0, -1, 1, 2, 0, 0, 1, 0, 1, -1, 2, 0, 3, 1, 0, 1, -1, -1, 1, 0, 0,
    ];

    private static readonly int[] _newMoonLunarCoefficients =
    [
        1, 0, 2, 0, 1, 1, 0, 1, 1, 2, 3, 0, 0, 1, 0, 0, 1, 2, 2, 0, 1, 1, 3, 4,
    ];

    private static readonly int[] _newMoonMoonCoefficients =
    [
        0, 0, 0, 2, 0, 0, 0, -2, 2, 0, 0, 2, 2, 0, 4, 0, -2, 2, 0, -2, -2, 2, 0, 0,
    ];

    private static readonly double[] _newMoonAdditionalConstants =
    [
        251.88, 251.83, 349.42, 84.66, 141.74, 207.14, 154.84, 34.52, 207.19, 291.34, 161.72, 239.56, 331.55,
    ];

    private static readonly double[] _newMoonAdditionalCoefficients =
    [
        0.016321, 26.651886, 36.412478, 18.206239, 53.303771, 2.453732, 7.306860, 27.261239, 0.121824,
        1.844379, 24.198154, 25.513099, 3.592518,
    ];

    private static readonly double[] _newMoonAdditionalFactors =
    [
        0.000165, 0.000164, 0.000126, 0.000110, 0.000062, 0.000060, 0.000056, 0.000047, 0.000042, 0.000040,
        0.000037, 0.000035, 0.000023,
    ];

    /// <summary>
    /// The universal moment of the <paramref name="n"/>th new moon after the one of January 2000,
    /// counting the new moon of −11 CE as index 0.
    /// </summary>
    private static double NthNewMoon(long n)
    {
        var k = n - 24724L;
        var c = k / 1236.85;
        var c2 = c * c;
        var c3 = c2 * c;
        var c4 = c3 * c;

        var approx = J2000
            + 5.09766
            + MeanSynodicMonth * 1236.85 * c
            + 0.00015437 * c2
            - 0.000000150 * c3
            + 0.00000000073 * c4;

        var eccentricity = 1.0 - 0.002516 * c - 0.0000074 * c2;
        var solarAnomaly = 2.5534 + 29.10535670 * 1236.85 * c - 0.0000014 * c2 - 0.00000011 * c3;
        var lunarAnomaly = 201.5643 + 385.81693528 * 1236.85 * c + 0.0107582 * c2 + 0.00001238 * c3 - 0.000000058 * c4;
        var moonArgument = 160.7108 + 390.67050284 * 1236.85 * c - 0.0016118 * c2 - 0.00000227 * c3 + 0.000000011 * c4;
        var omega = 124.7746 - 1.56375588 * 1236.85 * c + 0.0020672 * c2 + 0.00000215 * c3;

        var eccentricitySquared = eccentricity * eccentricity;

        var correction = -0.00017 * SinDegrees(omega);
        for (var i = 0; i < _newMoonSineCoefficients.Length; i++)
        {
            var factor = _newMoonEccentricityFactors[i] switch
            {
                0 => 1.0,
                1 => eccentricity,
                _ => eccentricitySquared,
            };

            correction += _newMoonSineCoefficients[i]
                * factor
                * SinDegrees(
                    _newMoonSolarCoefficients[i] * solarAnomaly
                    + _newMoonLunarCoefficients[i] * lunarAnomaly
                    + _newMoonMoonCoefficients[i] * moonArgument);
        }

        var extra = 0.000325 * SinDegrees(299.77 + 132.8475848 * c - 0.009173 * c2);

        var additional = 0.0;
        for (var i = 0; i < _newMoonAdditionalConstants.Length; i++)
        {
            additional += _newMoonAdditionalFactors[i]
                * SinDegrees(_newMoonAdditionalConstants[i] + _newMoonAdditionalCoefficients[i] * k);
        }

        var moment = approx + correction + extra + additional;
        return moment - EphemerisCorrection(moment);
    }

    #endregion

    #region Ephemeris correction

    // Hoisted rather than written as `params` arrays at the call sites: EphemerisCorrection runs on
    // every solar-longitude and new-moon evaluation, and a params array there is an allocation per call.
    private static readonly double[] _correction2006 = [62.92, 0.32217, 0.005589];
    private static readonly double[] _correction1987 = [63.86, 0.3345, -0.060374, 0.0017275, 0.000651814, 0.00002373599];
    private static readonly double[] _correction1900 = [-0.00002, 0.000297, 0.025184, -0.181133, 0.553040, -0.861938, 0.677066, -0.212591];
    private static readonly double[] _correction1800 =
    [
        -0.000009, 0.003844, 0.083563, 0.865736, 4.867575, 15.845535, 31.332267, 38.291999,
        28.316289, 11.636204, 2.043794,
    ];
    private static readonly double[] _correction1700 = [8.118780842, -0.005092142, 0.003336121, -0.0000266484];
    private static readonly double[] _correction1600 = [120.0, -0.9808, -0.01532, 0.000140272128];
    private static readonly double[] _correction0500 = [1574.2, -556.01, 71.23472, 0.319781, -0.8503463, -0.005050998, 0.0083572073];
    private static readonly double[] _correctionAncient = [10583.6, -1014.41, 33.78311, -5.952053, -0.1798452, 0.022174192, 0.0090316521];

    private static double JulianCenturies(double moment)
    {
        return (moment + EphemerisCorrection(moment) - J2000) / 36525.0;
    }

    /// <summary>
    /// The difference between dynamical and universal time, in days, as a function of the year — the
    /// piecewise fit of Reingold and Dershowitz, §14.2, itself following Morrison and Stephenson.
    /// </summary>
    private static double EphemerisCorrection(double moment)
    {
        var year = GregorianYearOf((long) System.Math.Floor(moment));
        var c = (TemporalHelpers.IsoDateToDays(year, 7, 1) - _daysTo1900) / 36525.0;
        var y2000 = year - 2000;

        if (year >= 2051 && year <= 2150)
        {
            var t = (year - 1820) / 100.0;
            return (-20.0 + 32.0 * t * t + 0.5628 * (2150 - year)) / 86400.0;
        }

        if (year >= 2006 && year <= 2050)
        {
            return Poly(y2000, _correction2006) / 86400.0;
        }

        if (year >= 1987 && year <= 2005)
        {
            return Poly(y2000, _correction1987) / 86400.0;
        }

        if (year >= 1900 && year <= 1986)
        {
            return Poly(c, _correction1900);
        }

        if (year >= 1800 && year <= 1899)
        {
            return Poly(c, _correction1800);
        }

        if (year >= 1700 && year <= 1799)
        {
            return Poly(year - 1700, _correction1700) / 86400.0;
        }

        if (year >= 1600 && year <= 1699)
        {
            return Poly(year - 1600, _correction1600) / 86400.0;
        }

        if (year >= 500 && year <= 1599)
        {
            return Poly((year - 1000) / 100.0, _correction0500) / 86400.0;
        }

        if (year > -500 && year < 500)
        {
            return Poly(year / 100.0, _correctionAncient) / 86400.0;
        }

        var far = (year - 1820) / 100.0;
        return (-20.0 + 32.0 * far * far) / 86400.0;
    }

    #endregion

    #region Arithmetic helpers

    private static double Poly(double x, double[] coefficients)
    {
        var result = 0.0;
        for (var i = coefficients.Length - 1; i >= 0; i--)
        {
            result = result * x + coefficients[i];
        }

        return result;
    }

    /// <summary>Non-negative modulo, which the trigonometric reductions all assume.</summary>
    private static double Mod(double x, double y)
    {
        var r = x - y * System.Math.Floor(x / y);
        return r < 0.0 ? r + y : r;
    }

    /// <summary>Modulo mapped into 1..<paramref name="y"/> rather than 0..<paramref name="y"/>−1.</summary>
    private static int Amod(int x, int y)
    {
        var r = x % y;
        if (r <= 0)
        {
            r += y;
        }

        return r;
    }

    private static double SinDegrees(double degrees) => System.Math.Sin(degrees * System.Math.PI / 180.0);

    private static double CosDegrees(double degrees) => System.Math.Cos(degrees * System.Math.PI / 180.0);

    /// <summary>The proleptic Gregorian year containing the given count of days since 1970-01-01.</summary>
    private static int GregorianYearOf(long days) => NonIsoCalendars.IsoFromDays(days).Year;

    #endregion

    /// <summary>
    /// The anchor <see cref="NewMoonIndexBefore"/> counts new moons from. Declared last on purpose:
    /// static field initializers run in textual order, and computing it calls <see cref="NthNewMoon"/>,
    /// which reads every series table above.
    /// </summary>
    private static readonly double _newMoonZero = NthNewMoon(0);
}
