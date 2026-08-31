using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.Native.Temporal;

/// <summary>
/// Provides calendar operations for non-ISO calendars using .NET's built-in Calendar classes.
/// Supports: Chinese, Dangi, Hebrew, Persian, Coptic, Ethiopic, EthioAA, Indian, Islamic-Umalqura.
/// Gregorian-based calendars (iso8601, gregory, japanese, roc, buddhist) are handled
/// directly in TemporalHelpers using ISO arithmetic.
/// </summary>
/// <remarks>
/// Constraining a field here means <c>System.Math.Clamp(value, 1, max)</c> against a computed
/// months-in-year or days-in-month. Math.Clamp throws ArgumentException when min is greater than max,
/// where the private clamp helpers it replaced returned min, so a max below 1 is not a badly
/// constrained date: it is a CLR exception escaping engine.Evaluate, where a script sees neither a
/// value nor a catchable JavaScript error. Every helper below that produces such a max therefore
/// states why it cannot answer below 1, and each maximum taken from System.Globalization instead
/// (Calendar.GetDaysInMonth, GetMonthsInYear, GetLeapMonth, IsLeapYear — all of which either answer 1
/// or more or throw for a year outside the calendar's range) sits inside a try whose catch answers
/// with a literal, with one of those helpers, or with the algorithmic reckoning the calendar's own
/// field accessors read past the end of its table — never below 1. What keeps all of it true is
/// Jint.Tests/Runtime/NonIsoCalendarTests.cs: 85,800 field combinations per overflow mode across all
/// eleven calendars, every one of which has to come back as a date or as null rather than throw.
/// </remarks>
internal static class NonIsoCalendars
{
    // Lazy calendar instances to avoid startup overhead. There is deliberately no
    // ChineseLunisolarCalendar or KoreanLunisolarCalendar here: those two tables are not the same table
    // on every target framework, so one script gave three answers
    // (https://github.com/sebastienros/jint/issues/3484). Both calendars are reckoned by
    // LunisolarAstronomy instead, which is the same code everywhere.
    private static HebrewCalendar? _hebrewCalendar;
    private static PersianCalendar? _persianCalendar;
    private static UmAlQuraCalendar? _umAlQuraCalendar;

    private static HebrewCalendar HebrewCal => _hebrewCalendar ??= new HebrewCalendar();
    private static PersianCalendar PersianCal => _persianCalendar ??= new PersianCalendar();
    private static UmAlQuraCalendar UmAlQuraCal => _umAlQuraCalendar ??= new UmAlQuraCalendar();

    // Epoch day constants (days since Unix epoch 1970-01-01)
    // Coptic epoch: Coptic year 1, month 1, day 1 = proleptic Gregorian August 29, 284 CE
    // Verified: Coptic (1687, 1, 1) = ISO September 11, 1970
    private const long CopticEpochDays = -615558;

    // Ethiopic epoch: Ethiopic year 1, month 1, day 1 = proleptic Gregorian August 27, 8 CE
    // Verified: Ethiopic (1963, 1, 1) = ISO September 11, 1970
    private const long EthiopicEpochDays = -716367;

    // EthioAA epoch: EthioAA year 1, month 1, day 1
    // Verified: EthioAA (7463, 1, 1) = ISO September 11, 1970
    private const long EthioAAEpochDays = -2725242;

    /// <summary>
    /// Result of converting an ISO date to a non-ISO calendar date.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct CalendarDate(
        int Year,
        int Month, // ordinal month (1-based, includes leap months)
        string MonthCode, // M01-M12 or M##L for leap months
        int Day,
        bool IsLeapMonth,
        int MonthsInYear,
        int DaysInMonth,
        int DaysInYear,
        bool InLeapYear);

    /// <summary>
    /// The failure of a provider that names a calendar in <see cref="ICalendarProvider.GetSupportedCalendars"/>
    /// and then hands its conversion back to the base class, which has never heard of it. Reported as a
    /// <c>RangeError</c> naming the member the host still owes, so it arrives as a JavaScript error rather
    /// than as a <see cref="NotSupportedException"/> escaping the engine.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowUnclaimedByProvider(Engine engine, string calendar, string member)
    {
        Throw.RangeError(
            engine.Realm,
            $"Calendar '{calendar}' is claimed by the configured {nameof(ICalendarProvider)} but its {member} does not answer for it");
    }

    /// <summary>
    /// Returns true if the calendar is a non-ISO calendar this adapter can convert — the eleven it
    /// implements itself, plus any a host <see cref="ICalendarProvider"/> claims when one is installed.
    /// </summary>
    internal static bool IsNonIsoCalendar(string calendar, Engine? engine = null)
    {
        if (calendar is "chinese" or "dangi" or "hebrew" or "persian"
            or "coptic" or "ethiopic" or "ethioaa" or "indian"
            or "islamic-umalqura" or "islamic-civil" or "islamic-tbla")
        {
            return true;
        }

        var provider = engine?.Options.Temporal.CalendarProvider;
        return provider is not null
            && !ReferenceEquals(provider, DefaultCalendarProvider.Instance)
            && provider.IsSupported(calendar);
    }

    /// <summary>
    /// Converts an ISO date to calendar-specific fields.
    /// When <paramref name="engine"/> is supplied and its options expose a non-default
    /// <see cref="ICalendarProvider"/>, the conversion is delegated to that provider.
    /// </summary>
    internal static CalendarDate IsoToCalendarDate(string calendar, in IsoDate isoDate, Engine? engine = null)
    {
        var provider = engine?.Options.Temporal.CalendarProvider;
        if (provider is not null && provider != DefaultCalendarProvider.Instance && provider.IsSupported(calendar))
        {
            CalendarFields fields;
            try
            {
                fields = provider.IsoToCalendarFields(calendar, isoDate.Year, isoDate.Month, isoDate.Day);
            }
            catch (NotSupportedException)
            {
                // The provider claims the calendar and then delegates it to an inherited conversion that
                // has never heard of it. That is the host's contract broken, not the script's mistake, but
                // a CLR exception crossing out of a Temporal call is nobody's idea of a diagnosis.
                ThrowUnclaimedByProvider(engine!, calendar, nameof(ICalendarProvider.IsoToCalendarFields));
                return default;
            }

            return new CalendarDate(
                fields.Year, fields.Month, fields.MonthCode, fields.Day,
                fields.IsLeapMonth, fields.MonthsInYear, fields.DaysInMonth,
                fields.DaysInYear, fields.InLeapYear);
        }

        try
        {
            return calendar switch
            {
                "chinese" => LunisolarAlgorithmicFromIso(LunisolarRegion.China, in isoDate),
                "dangi" => LunisolarAlgorithmicFromIso(LunisolarRegion.Korea, in isoDate),
                "hebrew" => HebrewToCalendarDate(in isoDate),
                "persian" => PersianToCalendarDate(in isoDate),
                "coptic" => FixedEpochToCalendarDate(CopticEpochDays, in isoDate),
                "ethiopic" => FixedEpochToCalendarDate(EthiopicEpochDays, in isoDate),
                "ethioaa" => FixedEpochToCalendarDate(EthioAAEpochDays, in isoDate),
                "indian" => IndianToCalendarDate(in isoDate),
                "islamic-umalqura" => IslamicUmalquraToCalendarDate(in isoDate),
                "islamic-civil" => IslamicCivilToCalendarDate(in isoDate, 1948439L),
                "islamic-tbla" => IslamicCivilToCalendarDate(in isoDate, 1948438L),
                _ => throw new NotSupportedException($"Calendar '{calendar}' not supported by NonIsoCalendars")
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            // Unreachable for the eleven calendars above, and it is the point of
            // https://github.com/sebastienros/jint/issues/3451 that it is: answering with ISO fields under
            // another calendar's name is indistinguishable downstream from answering correctly. Each of
            // the eleven now has a reckoning of its own to fall back on past the end of whatever backs it
            // -- the lunisolar pair since #3451, the others before it -- and each falls back to that where
            // it applies rather than to this. What remains here is the last resort of a static analysis
            // that cannot see that, and Jint.Tests/Runtime/NonIsoCalendarOutOfRangeFieldTests.cs is what
            // says no calendar reaches it.
            var monthCode = $"M{isoDate.Month:D2}";
            var daysInMonth = IsoDate.IsoDateInMonth(isoDate.Year, isoDate.Month);
            var daysInYear = IsoDate.IsLeapYear(isoDate.Year) ? 366 : 365;
            return new CalendarDate(isoDate.Year, isoDate.Month, monthCode, isoDate.Day, false, 12, daysInMonth, daysInYear, IsoDate.IsLeapYear(isoDate.Year));
        }
    }

    /// <summary>
    /// Converts a calendar date to an ISO date. Returns null if the date is invalid with overflow "reject".
    /// When <paramref name="engine"/> is supplied and its options expose a non-default
    /// <see cref="ICalendarProvider"/>, the conversion is delegated to that provider.
    /// </summary>
    internal static IsoDate? CalendarDateToIso(string calendar, int year, string? monthCode, int month, int day, string overflow, Engine? engine = null)
    {
        var provider = engine?.Options.Temporal.CalendarProvider;
        if (provider is not null && provider != DefaultCalendarProvider.Instance && provider.IsSupported(calendar))
        {
            IsoDateFields? iso;
            try
            {
                iso = provider.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
            }
            catch (NotSupportedException)
            {
                ThrowUnclaimedByProvider(engine!, calendar, nameof(ICalendarProvider.CalendarFieldsToIso));
                return null;
            }

            return iso is null ? null : new IsoDate(iso.Value.Year, iso.Value.Month, iso.Value.Day);
        }

        var result = BuiltinCalendarDateToIso(calendar, year, monthCode, month, day, overflow);

        // If calendar conversion failed (e.g., year out of .NET calendar range),
        // fall back to treating fields as ISO (best effort)
        if (result is null && !string.Equals(overflow, "reject", StringComparison.Ordinal))
        {
            // Don't fall back for fundamentally-invalid monthCodes (out-of-range display number,
            // or leap variant on a calendar that doesn't support it / a different leap month).
            // The Hebrew M02L → ISO 5779-02 fallback was masking spec-mandated RangeErrors.
            if (monthCode is not null && !TryValidateMonthCode(calendar, monthCode, out var validatedDisplayMonth))
            {
                return null;
            }

            var isoMonth = month > 0
                ? month
                : (monthCode is not null
                    ? int.Parse(monthCode.AsSpan(1, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture)
                    : 1);
            return TemporalHelpers.RegulateIsoDate(year, System.Math.Clamp(isoMonth, 1, 12), day, overflow);
        }

        return result;
    }

    /// <summary>
    /// The engine's own reckoning of a calendar date, with no host provider consulted and no last-resort
    /// ISO fallback: null means this calendar cannot place these fields, and says so.
    /// </summary>
    /// <remarks>
    /// Each arm answers across the whole of Temporal's range, falling back past the end of whatever
    /// <see cref="Calendar"/> backs it to a reckoning of its own — which is why
    /// <see cref="CalendarDateAdd"/> can place a result the backing calendar declines by asking this
    /// rather than by refusing.
    /// </remarks>
    private static IsoDate? BuiltinCalendarDateToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        return calendar switch
        {
            "chinese" => LunisolarDateToIso(LunisolarRegion.China, year, monthCode, month, day, overflow),
            "dangi" => LunisolarDateToIso(LunisolarRegion.Korea, year, monthCode, month, day, overflow),
            "hebrew" => HebrewDateToIso(year, monthCode, month, day, overflow),
            "persian" => PersianDateToIso(year, monthCode, month, day, overflow),
            "coptic" => FixedEpochDateToIso(CopticEpochDays, 13, year, monthCode, month, day, overflow),
            "ethiopic" => FixedEpochDateToIso(EthiopicEpochDays, 13, year, monthCode, month, day, overflow),
            "ethioaa" => FixedEpochDateToIso(EthioAAEpochDays, 13, year, monthCode, month, day, overflow),
            "indian" => IndianDateToIso(year, monthCode, month, day, overflow),
            "islamic-umalqura" => IslamicUmalquraDateToIso(year, monthCode, month, day, overflow),
            "islamic-civil" => IslamicCivilTabularDateToIso(year, monthCode, month, day, overflow, 1948439L),
            "islamic-tbla" => IslamicCivilTabularDateToIso(year, monthCode, month, day, overflow, 1948438L),
            _ => throw new NotSupportedException($"Calendar '{calendar}' not supported by NonIsoCalendars")
        };
    }

    /// <summary>
    /// Steps <paramref name="months"/> ordinal months out from (<paramref name="year"/>,
    /// <paramref name="ordinalMonth"/>) in a lunisolar calendar, without walking the years between.
    /// </summary>
    /// <remarks>
    /// The walk below costs one whole <see cref="LunisolarYear"/> per calendar year crossed — a dozen new
    /// moons and two solstice searches — so a hundred thousand months was eight thousand of them, and the
    /// largest addition the calendar's range permits ran for three quarters of a minute
    /// (<see href="https://github.com/sebastienros/jint/issues/3511"/>). The months of <c>chinese</c> and
    /// <c>dangi</c> are consecutive new moons, so which month lies a given number of them away is
    /// arithmetic on the lunation index.
    /// <para>
    /// <see langword="false"/> means "ask the walk", and every reason for it is a place a closed form must
    /// not answer: the reckoning cannot place the year to start from, the ordinal does not occur in it, the
    /// step is larger than any representable date could need, or the month the step landed on does not
    /// begin on the new moon it counted to. None of them is reachable for a date this engine can represent,
    /// which is why the walk stays as the fallback rather than being deleted.
    /// </para>
    /// </remarks>
    private static bool TryStepLunisolarMonths(
        string calendar,
        int year,
        int ordinalMonth,
        int months,
        out int steppedYear,
        out int steppedOrdinalMonth)
    {
        steppedYear = year;
        steppedOrdinalMonth = ordinalMonth;

        var region = RegionOf(calendar);
        var from = LunisolarAstronomy.ForYear(year, region);
        if (from is null || ordinalMonth < 1 || ordinalMonth > from.MonthCount)
        {
            return false;
        }

        var landedOn = LunisolarAstronomy.StepMonths(from.MonthStarts[ordinalMonth - 1], months, region);
        if (landedOn is null)
        {
            return false;
        }

        var target = landedOn.Value;
        var landed = LunisolarAstronomy.ForFixed(target, region);

        var ordinal = 1;
        while (ordinal < landed.MonthCount && landed.MonthStarts[ordinal] <= target)
        {
            ordinal++;
        }

        if (landed.MonthStarts[ordinal - 1] != target)
        {
            return false;
        }

        steppedYear = landed.Year;
        steppedOrdinalMonth = ordinal;
        return true;
    }

    /// <summary>
    /// How many steps a calendar walk takes between two constraint checks.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Engine.ConstraintCheckInterval"/>'s ten thousand, which is calibrated
    /// for an array element — tens of nanoseconds apiece. One step here is a calendar <em>year</em>, and
    /// for <c>chinese</c> and <c>dangi</c> building one costs a dozen new moons and two solstice searches,
    /// something like a hundred microseconds, so ten thousand of them would be a second of latency on a
    /// budget a host may have set to a hundred milliseconds. 256 steps is a hundredth of that, and a power
    /// of two so the cadence costs a mask rather than a division. Nothing an ordinary date does reaches it:
    /// a year's worth of months is one step.
    /// </remarks>
    private const int CalendarWalkCheckInterval = 256;

    /// <summary>
    /// Counts one step of a calendar walk, and every <see cref="CalendarWalkCheckInterval"/>th one gives
    /// the engine's execution constraints the chance to stop it.
    /// </summary>
    /// <remarks>
    /// A walk of ordinal months is a CLR loop inside one interpreter step, so it crosses no statement
    /// boundary and nothing in <see cref="Engine.RunPerStatementChecks"/> is ever reached from it: a
    /// <c>LimitExecutionTime</c> never got a check, a <c>LimitStatements</c> never counted and a
    /// <c>CancellationToken</c> was never observed for as long as one <c>add</c> ran
    /// (<see href="https://github.com/sebastienros/jint/issues/3511"/>). This is the same self-checking the
    /// bulk array built-ins do with <see cref="Engine.ConstraintCheckInterval"/>, at the cadence one step
    /// of <em>this</em> loop costs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountWalkStep(ref int steps, Engine? engine)
    {
        if ((++steps & (CalendarWalkCheckInterval - 1)) == 0)
        {
            engine?.Constraints.Check();
        }
    }

    /// <summary>
    /// Whether the engine's <see cref="ICalendarProvider"/> is the one that answers for this calendar.
    /// It is the same identity-then-membership test <see cref="IsoToCalendarDate"/> and
    /// <see cref="CalendarDateToIso"/> already make, asked once here so that a date's arithmetic and its
    /// field accessors are never answered by two different reckonings.
    /// </summary>
    private static bool AnsweredByProvider(string calendar, [NotNullWhen(true)] Engine? engine)
    {
        var provider = engine?.Options.Temporal.CalendarProvider;
        return provider is not null
            && !ReferenceEquals(provider, DefaultCalendarProvider.Instance)
            && provider.IsSupported(calendar);
    }

    /// <summary>
    /// The calendar fields of the first of (<paramref name="year"/>, <paramref name="ordinalMonth"/>), or
    /// null when the conversion cannot place that month. Everything the walk below needs to know about a
    /// month it has not landed on yet — how long it is, how many months its year holds — is read back off
    /// a trip out to ISO and straight back in, because those two conversions are all an
    /// <see cref="ICalendarProvider"/> supplies.
    /// </summary>
    private static CalendarDate? ProviderProbeMonth(string calendar, int year, int ordinalMonth, Engine engine)
    {
        var iso = CalendarDateToIso(calendar, year, null, ordinalMonth, 1, "constrain", engine);
        return iso is null ? null : IsoToCalendarDate(calendar, iso.Value, engine);
    }

    /// <summary>
    /// The number of months in a calendar year, read off the same probe. A year the provider cannot place
    /// at all has no month count, and answering zero would leave the month-stepping loop below turning
    /// forever, so it raises the "reject" signal the caller already reports as a <c>RangeError</c>.
    /// </summary>
    private static int ProviderMonthsInYear(string calendar, int year, Engine engine)
    {
        var probe = ProviderProbeMonth(calendar, year, 1, engine);
        if (probe is null || probe.Value.MonthsInYear < 1)
        {
            throw new InvalidOperationException("reject");
        }

        return probe.Value.MonthsInYear;
    }

    /// <summary>
    /// The same year-then-month-then-day walk the per-calendar implementations below make, expressed only
    /// in terms of the two conversions an <see cref="ICalendarProvider"/> supplies, for a calendar whose
    /// reckoning nobody but the provider knows. Every conversion here can decline to answer; each one that
    /// does ends in the "reject" signal an out-of-range date already raises and the caller already turns
    /// into a <c>RangeError</c>, so no null is dereferenced and no CLR exception leaves the engine.
    /// </summary>
    private static IsoDate ProviderCalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow, Engine engine)
    {
        var reject = string.Equals(overflow, "reject", StringComparison.Ordinal);
        var calDate = IsoToCalendarDate(calendar, in isoDate, engine);
        var newYear = calDate.Year + years;
        int newOrdinalMonth;

        if (years != 0)
        {
            // Years move by monthCode, never by ordinal: a lunisolar year need not hold as many months as
            // the one beside it, so the fourth month of one year is not the fourth month of the next.
            // Asking the provider to place the same monthCode in the target year is what carries the
            // meaning across, and a rejected placement is how it says that code does not occur there.
            var placed = CalendarDateToIso(calendar, newYear, calDate.MonthCode, 0, 1, "reject", engine);
            if (placed is not null)
            {
                newOrdinalMonth = IsoToCalendarDate(calendar, placed.Value, engine).Month;
            }
            else
            {
                // A leap month landing in a year that has no such leap month. Constraining moves to the
                // calendar's own non-leap equivalent, which is the rule the per-calendar paths apply too;
                // if even that will not place, the ordinal it started from is the last thing left to try.
                if (reject)
                {
                    throw new InvalidOperationException("reject");
                }

                var fallbackCode = NonLeapEquivalentMonthCode(calendar, calDate.MonthCode);
                var fallback = CalendarDateToIso(calendar, newYear, fallbackCode, 0, 1, "constrain", engine);
                newOrdinalMonth = fallback is null
                    ? calDate.Month
                    : IsoToCalendarDate(calendar, fallback.Value, engine).Month;
            }
        }
        else
        {
            newOrdinalMonth = calDate.Month;
        }

        if (months != 0)
        {
            newOrdinalMonth += months;
            var steps = 0;
            var monthsInYear = ProviderMonthsInYear(calendar, newYear, engine);
            while (newOrdinalMonth > monthsInYear)
            {
                CountWalkStep(ref steps, engine);
                newOrdinalMonth -= monthsInYear;
                newYear++;
                monthsInYear = ProviderMonthsInYear(calendar, newYear, engine);
            }

            while (newOrdinalMonth < 1)
            {
                CountWalkStep(ref steps, engine);
                newYear--;
                newOrdinalMonth += ProviderMonthsInYear(calendar, newYear, engine);
            }
        }

        // The length of the month landed on stays the provider's to state, so the day is clamped against a
        // probe of that month rather than against anything computed here.
        var target = ProviderProbeMonth(calendar, newYear, newOrdinalMonth, engine);
        if (target is null)
        {
            throw new InvalidOperationException("reject");
        }

        var newDay = calDate.Day;
        if (newDay > target.Value.DaysInMonth)
        {
            if (reject)
            {
                throw new InvalidOperationException("reject");
            }

            newDay = target.Value.DaysInMonth;
        }

        var result = CalendarDateToIso(calendar, newYear, null, newOrdinalMonth, newDay, "constrain", engine);
        if (result is null)
        {
            throw new InvalidOperationException("reject");
        }

        return result.Value;
    }

    /// <summary>
    /// Adds years and months to an ISO date using calendar-specific reckoning.
    /// Years are added preserving monthCode semantics (not ordinal month).
    /// Months are added as ordinal month steps.
    /// </summary>
    /// <remarks>
    /// A calendar a host <see cref="ICalendarProvider"/> answers for is walked by
    /// <see cref="ProviderCalendarDateAdd"/>, through the provider's own two conversions — which is what
    /// gives a calendar the host added arithmetic at all, and what keeps a calendar the host corrected
    /// from being corrected in its field accessors and not in its arithmetic. The per-calendar
    /// implementations below answer for every calendar the provider leaves to the engine, so an engine
    /// that configures nothing reaches exactly the same one it always did.
    /// <para>
    /// The BCL arm asks its backing <see cref="Calendar"/> five things — a year's month count, its leap
    /// month's ordinal, a monthCode's ordinal in that year, a month's length, and where a resolved
    /// (year, ordinal, day) lands in ISO — and every one of them falls back to the same reckoning
    /// <see cref="IsoToCalendarDate"/> and <see cref="BuiltinCalendarDateToIso"/> read past the end of
    /// that calendar's table, so a date whose fields the engine reports is a date whose arithmetic the
    /// engine performs (https://github.com/sebastienros/jint/issues/3483).
    /// </para>
    /// </remarks>
    internal static IsoDate CalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow, Engine? engine = null)
    {
        if (AnsweredByProvider(calendar, engine))
        {
            return ProviderCalendarDateAdd(calendar, in isoDate, years, months, overflow, engine);
        }

        // For calendars that use epoch-day arithmetic (coptic/ethiopic/ethioaa)
        // or Indian calendar, handle them directly
        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            return FixedEpochCalendarDateAdd(calendar, in isoDate, years, months, overflow);
        }

        if (calendar is "indian")
        {
            return IndianCalendarDateAdd(in isoDate, years, months, overflow);
        }

        if (calendar is "islamic-civil" or "islamic-tbla" or "islamic-umalqura")
        {
            return IslamicTabularCalendarDateAdd(calendar, in isoDate, years, months, overflow);
        }

        var cal = GetCalendar(calendar);
        var calDate = IsoToCalendarDate(calendar, in isoDate, engine);
        var newYear = calDate.Year + years;
        int newOrdinalMonth;

        // When adding years, preserve monthCode (not ordinal month)
        if (years != 0 && calDate.IsLeapMonth)
        {
            // Was on a leap month - check if it exists in the new year
            var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, newYear);
            if (leapOrdinal > 0 && GetLeapDisplayMonth(calendar, leapOrdinal) == GetLeapDisplayMonth(calendar, GetLeapMonthOrdinal(calendar, cal, calDate.Year)))
            {
                // Same leap month exists in new year
                newOrdinalMonth = leapOrdinal;
            }
            else
            {
                // Leap month doesn't exist in new year
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain to the calendar-specific non-leap equivalent. For chinese/dangi the
                // leap month follows the same display number (M04L → M04). For hebrew the leap
                // M05L (Adar I) is followed by M06 (Adar) in non-leap years, so it advances by 1.
                var baseMonthCode = NonLeapEquivalentMonthCode(calendar, calDate.MonthCode);
                newOrdinalMonth = MonthCodeToOrdinal(calendar, cal, newYear, baseMonthCode, overflow);
            }
        }
        else if (years != 0)
        {
            // Not a leap month - resolve the same monthCode in the new year
            newOrdinalMonth = MonthCodeToOrdinal(calendar, cal, newYear, calDate.MonthCode, overflow);
        }
        else
        {
            newOrdinalMonth = calDate.Month;
        }

        // Add months (ordinal stepping)
        if (months != 0
            && calendar is "chinese" or "dangi"
            && TryStepLunisolarMonths(calendar, newYear, newOrdinalMonth, months, out var steppedYear, out var steppedMonth))
        {
            newYear = steppedYear;
            newOrdinalMonth = steppedMonth;
        }
        else if (months != 0)
        {
            newOrdinalMonth += months;
            var steps = 0;
            var monthsInYear = GetMonthsInYear(calendar, cal, newYear);
            while (newOrdinalMonth > monthsInYear)
            {
                CountWalkStep(ref steps, engine);
                newOrdinalMonth -= monthsInYear;
                newYear++;
                monthsInYear = GetMonthsInYear(calendar, cal, newYear);
            }

            while (newOrdinalMonth < 1)
            {
                CountWalkStep(ref steps, engine);
                newYear--;
                newOrdinalMonth += GetMonthsInYear(calendar, cal, newYear);
            }
        }

        // Constrain day
        var maxDay = GetDaysInMonthCal(calendar, cal, newYear, newOrdinalMonth);
        var newDay = calDate.Day;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        // Convert back to ISO
        if (cal is not null)
        {
            try
            {
                var dt = cal.ToDateTime(newYear, newOrdinalMonth, newDay, 0, 0, 0, 0);
                return new IsoDate(dt.Year, dt.Month, dt.Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Past the end of the table, which is not the end of the calendar. Fall through to the
                // conversion, which reckons those years.
            }
        }

        {
            // The conversion this calls has a reckoning of its own past the end of every backing
            // calendar, and it is the reckoning the field accessors already read the same date with, so
            // placing the result there is what keeps one date from getting two verdicts
            // (https://github.com/sebastienros/jint/issues/3483).
            //
            // BuiltinCalendarDateToIso rather than CalendarDateToIso: the latter's last-resort arm
            // answers an unplaceable date with its fields read as ISO, which is a different calendar's
            // answer wearing this one's name -- and, worse for the walk below, an answer whose progress
            // says nothing about this calendar's.
            var placed = BuiltinCalendarDateToIso(calendar, newYear, null, newOrdinalMonth, newDay, "constrain");
            if (placed is not null)
            {
                return placed.Value;
            }

            // Nothing left that can place it: the reckoning declines for a year outside Temporal's own
            // range, and there is no nearby valid date to clamp to, so both overflow modes report it.
            // NonISODateAdd is implementation-defined and may throw
            // (https://tc39.es/proposal-temporal/#sec-temporal-nonisodateadd), and CalendarDateAdd
            // raises a RangeError for a result it cannot represent
            // (https://tc39.es/proposal-temporal/#sec-temporal-calendardateadd).
            //
            // The clamp both of these replaced answered with the calendar's MaxSupportedDateTime for an
            // underflow as well as an overflow, so subtracting a century from a chinese date moved it
            // forward to 2101-01-28 -- and, since every further step landed on that same date,
            // CalendarDateUntil's month walk below never passed its target and never returned
            // (https://github.com/sebastienros/jint/issues/3428). What makes this arm safe where that
            // clamp was not is that the reckoning it defers to is strictly monotone in (year, ordinal
            // month), so a step still moves; CalendarDateUntil's no-progress guard stays as the
            // structural backstop for a reckoning that saturates rather than progressing.
            throw new CalendarRangeException(calendar);
        }
    }

    /// <summary>
    /// Whether stepping <paramref name="months"/> months out from <paramref name="one"/> lands past
    /// <paramref name="calTwo"/>, and where that step landed. The comparison is made in calendar-field
    /// space with the day left un-constrained: a day the step had to force down to fit a shorter target
    /// month still counts from where it started, which is the "would the original day exist there?"
    /// criterion the specification's ISODateSurpasses states and the polyfill implements.
    /// </summary>
    private static bool MonthStepSurpasses(
        string calendar,
        in IsoDate one,
        in CalendarDate calOne,
        in CalendarDate calTwo,
        int years,
        int months,
        int sign,
        Engine? engine,
        out IsoDate stepped)
    {
        stepped = CalendarDateAdd(calendar, in one, years, months, "constrain", engine);
        var steppedCal = IsoToCalendarDate(calendar, in stepped, engine);
        var notionalDay = steppedCal.Day != calOne.Day ? calOne.Day : steppedCal.Day;
        var ccd = CompareCalendarFields(calTwo.Year, calTwo.MonthCode, calTwo.Day, steppedCal.Year, steppedCal.MonthCode, notionalDay);
        return ccd * sign < 0;
    }

    /// <summary>
    /// Computes the difference between two ISO dates using calendar-specific reckoning.
    /// Mirrors the temporal-polyfill HelperBase.untilCalendar algorithm:
    /// (1) compute years from a "diffInYearSign" formula based on monthCode/day position,
    /// (2) optionally pre-skip whole-cycle months for largestUnit=month,
    /// (3) iterate ±1 month at a time, comparing in calendar-field space with the day
    ///     un-constrained (so a day forced down by AddCalendar still counts as "past target"),
    /// (4) measure remaining days as ISO epoch-day diff.
    /// </summary>
    /// <remarks>
    /// Every calendar-specific step here is one of <see cref="IsoToCalendarDate"/> or
    /// <see cref="CalendarDateAdd"/>, so threading <paramref name="engine"/> through is the whole of what a
    /// calendar a host <see cref="ICalendarProvider"/> answers for needs: the difference is then measured
    /// in the provider's own fields, the way its <c>add</c> already moves in them.
    /// </remarks>
    internal static DurationRecord CalendarDateUntil(string calendar, in IsoDate one, in IsoDate two, string largestUnit, Engine? engine = null)
    {
        var rawSign = TemporalHelpers.CompareIsoDates(one, two);
        if (rawSign == 0)
        {
            return new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var epochOne = TemporalHelpers.IsoDateToDays(one.Year, one.Month, one.Day);
        var epochTwo = TemporalHelpers.IsoDateToDays(two.Year, two.Month, two.Day);

        if (string.Equals(largestUnit, "week", StringComparison.Ordinal))
        {
            var totalDays = (int) (epochTwo - epochOne);
            var w = totalDays / 7;
            return new DurationRecord(0, 0, w, totalDays - w * 7, 0, 0, 0, 0, 0, 0);
        }

        if (!string.Equals(largestUnit, "year", StringComparison.Ordinal) &&
            !string.Equals(largestUnit, "month", StringComparison.Ordinal))
        {
            // "day" or smaller: the date portion is just the ISO-day diff.
            return new DurationRecord(0, 0, 0, (int) (epochTwo - epochOne), 0, 0, 0, 0, 0, 0);
        }

        // Spec convention: sign = +1 if two > one (forward), -1 if backward.
        var sign = -rawSign;

        var calOne = IsoToCalendarDate(calendar, in one, engine);
        var calTwo = IsoToCalendarDate(calendar, in two, engine);

        var years = 0;
        var diffYears = calTwo.Year - calOne.Year;
        if (diffYears != 0)
        {
            int diffInYearSign;
            var mcCmp = string.CompareOrdinal(calTwo.MonthCode, calOne.MonthCode);
            if (mcCmp > 0) diffInYearSign = 1;
            else if (mcCmp < 0) diffInYearSign = -1;
            else diffInYearSign = System.Math.Sign(calTwo.Day - calOne.Day);

            var isOneFurtherInYear = diffInYearSign * sign < 0;
            years = isOneFurtherInYear ? diffYears - sign : diffYears;

            // Strict overshoot follow-up using un-constrained-day comparison: this catches the
            // case where AddCalendar's constrain mode forced the day down to fit a shorter target
            // month. The spec treats "would-be day exists in target's month?" as the overshoot
            // criterion, so an intermediate that landed at the target only because the day was
            // truncated should NOT count as a full year. Chinese/Hebrew leap-month wrapping plus
            // day-overflow within the same year boundary all flow through this check.
            if (years != 0)
            {
                var check = CalendarDateAdd(calendar, in one, years, 0, "constrain", engine);
                var checkCal = IsoToCalendarDate(calendar, in check, engine);
                var notional = checkCal.Day != calOne.Day ? calOne.Day : checkCal.Day;
                var ccd = CompareCalendarFields(calTwo.Year, calTwo.MonthCode, calTwo.Day, checkCal.Year, checkCal.MonthCode, notional);
                if (ccd * sign < 0)
                {
                    years -= sign;
                }
            }
        }

        var months = 0;
        if (string.Equals(largestUnit, "month", StringComparison.Ordinal))
        {
            if (years != 0)
            {
                // Whole-year skip estimate, from the average month length the conversion already reported
                // for the starting date's year. Multiplying the year span by that year's month count
                // instead -- which is what this was -- drifts wherever a calendar's years do not all hold
                // the same number of months: a lunisolar year holds twelve or thirteen, averaging 12.37, so
                // starting from a thirteen-month year and walking a millennium overestimates by some 625
                // months, every one of which the back-off loop below then steps off one at a time. The
                // estimate only decides how long that walk is; the two loops decide the answer.
                var monthsInYear = System.Math.Max(calOne.MonthsInYear, 1);
                var averageMonthDays = System.Math.Max(calOne.DaysInYear, 1) / (double) monthsInYear;
                months = (int) System.Math.Round((epochTwo - epochOne) / averageMonthDays);

                // A provider free enough with its fields to report an average that puts the estimate on the
                // wrong side of zero gets the old one, which at least has the sign of the walk.
                if (System.Math.Sign(months) != sign)
                {
                    months = years * monthsInYear;
                }
            }

            years = 0;
        }

        // Both walks below count their steps against one budget: each step runs a whole CalendarDateAdd,
        // which counts its own year steps, but a walk of many short adds would otherwise reach no check at
        // all — and a month difference is measured by walking, so the walk is where the cost is.
        var walkSteps = 0;

        // The iteration below only ever steps in the direction of sign, so an estimate that has already
        // reached past two would be returned as it stands and the overshoot would come back as a negative
        // day count. Walk it back a month at a time until it is short of two again; an estimate that was
        // never past it leaves this loop untouched.
        while (months != 0 && MonthStepSurpasses(calendar, in one, in calOne, in calTwo, years, months, sign, engine, out _))
        {
            CountWalkStep(ref walkSteps, engine);
            months -= sign;
        }

        var currentIso = years != 0 || months != 0
            ? CalendarDateAdd(calendar, in one, years, months, "constrain", engine)
            : one;

        while (true)
        {
            CountWalkStep(ref walkSteps, engine);

            var prevMonths = months;
            months += sign;

            // Continue while the step has not yet passed two.
            if (MonthStepSurpasses(calendar, in one, in calOne, in calTwo, years, months, sign, engine, out var nextIso))
            {
                months = prevMonths;
                break;
            }

            // The loop's only exit above is "this step passed two", so a step that does not move the
            // date never takes it: one more month of a walk that stands still is still standing still,
            // however many are taken. Without this the loop turns forever — a script asking a Chinese
            // date for its distance to a date before 1901 never returned, and no execution constraint
            // interrupts a CLR loop that crosses no statement boundary
            // (https://github.com/sebastienros/jint/issues/3428).
            //
            // For the eleven calendars the engine reckons itself this is now a structural guarantee
            // rather than a live path: every conversion that used to saturate raises
            // CalendarRangeException instead of answering with a boundary date. It stays because the
            // walk above is written in whichever two conversions answer for the calendar, and a host
            // ICalendarProvider is free to clamp where the engine no longer does. Either way a walk
            // that cannot reach its target is a difference the calendar cannot represent, and that is
            // the RangeError CalendarDateAdd raises — not a degraded answer that never reached two.
            if (nextIso == currentIso)
            {
                throw new CalendarRangeException(calendar);
            }

            currentIso = nextIso;
        }

        var epochCurrent = TemporalHelpers.IsoDateToDays(currentIso.Year, currentIso.Month, currentIso.Day);
        var days = (int) (epochTwo - epochCurrent);

        return new DurationRecord(years, months, 0, days, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Returns the maximum number of days the given Chinese/Dangi LEAP monthCode has
    /// across any year in the search window around the canonical 1972 anchor. Used by
    /// PlainMonthDay to decide whether to fall back to the regular (non-leap) monthCode.
    /// Returns 0 if the monthCode is invalid or not a leap monthCode.
    /// </summary>
    internal static int MaxDaysForChineseLeapMonth(string calendar, string monthCode)
    {
        if (calendar is not ("chinese" or "dangi")) return 30;
        if (monthCode.Length != 4 || monthCode[3] != 'L') return 0;

        var cal = GetCalendar(calendar);
        var approxYear = IsoYearToCalendarYear(calendar, cal, 1972);
        var maxSeen = 0;
        for (var y = approxYear - 75; y <= approxYear + 75; y++)
        {
            var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, y);
            if (leapOrdinal <= 0) continue;
            try
            {
                var ord = MonthCodeToOrdinal(calendar, cal, y, monthCode, "reject");
                if (ord <= 0) continue;
                var dim = GetDaysInMonthCal(calendar, cal, y, ord);
                if (dim > maxSeen) maxSeen = dim;
            }
            catch
            {
                // monthCode doesn't exist this year
            }
        }

        return maxSeen;
    }

    /// <summary>Same as <see cref="MaxDaysForChineseLeapMonth"/> but for a REGULAR monthCode.</summary>
    internal static int MaxDaysForChineseRegularMonth(string calendar, string monthCode)
    {
        if (calendar is not ("chinese" or "dangi")) return 30;
        if (monthCode.Length != 3) return 0;

        var cal = GetCalendar(calendar);
        var approxYear = IsoYearToCalendarYear(calendar, cal, 1972);
        var maxSeen = 0;
        for (var y = approxYear - 75; y <= approxYear + 75; y++)
        {
            try
            {
                var ord = MonthCodeToOrdinal(calendar, cal, y, monthCode, "reject");
                if (ord <= 0) continue;
                var dim = GetDaysInMonthCal(calendar, cal, y, ord);
                if (dim > maxSeen) maxSeen = dim;
            }
            catch
            {
                // monthCode doesn't exist this year
            }
        }

        return maxSeen;
    }

    /// <summary>
    /// Compares two calendar-field tuples (year, monthCode, day) lexicographically.
    /// MonthCode comparison is ordinal-string (so "M05" &lt; "M05L" &lt; "M06"), which matches the
    /// chronological order of months within a year for all supported calendars.
    /// Returns +1 if A > B, -1 if A &lt; B, 0 if equal.
    /// </summary>
    private static int CompareCalendarFields(int aYear, string aMonthCode, int aDay, int bYear, string bMonthCode, int bDay)
    {
        if (aYear != bYear)
        {
            return aYear > bYear ? 1 : -1;
        }

        var mcCmp = string.CompareOrdinal(aMonthCode, bMonthCode);
        if (mcCmp != 0)
        {
            return mcCmp > 0 ? 1 : -1;
        }

        if (aDay != bDay)
        {
            return aDay > bDay ? 1 : -1;
        }

        return 0;
    }

    /// <summary>
    /// Returns the era string for a non-ISO calendar.
    /// </summary>
    internal static string? CalendarEra(string calendar, in CalendarDate calDate)
    {
        return calendar switch
        {
            // Single-era calendars: always use the primary era
            "hebrew" => "am",
            "persian" => "ap",
            "coptic" => "am",
            "ethioaa" => "aa",
            "indian" => "shaka",
            "chinese" or "dangi" => null, // no eras
            // Multi-era calendars: era depends on year
            "ethiopic" => calDate.Year >= 1 ? "am" : "aa",
            "islamic-umalqura" or "islamic-civil" or "islamic-tbla" => calDate.Year >= 1 ? "ah" : "bh",
            _ => null
        };
    }

    /// <summary>
    /// Returns the eraYear for a non-ISO calendar.
    /// </summary>
    internal static int? CalendarEraYear(string calendar, in CalendarDate calDate)
    {
        return calendar switch
        {
            // Single-era calendars: eraYear = year directly
            "hebrew" => calDate.Year,
            "persian" => calDate.Year,
            "coptic" => calDate.Year,
            "ethioaa" => calDate.Year,
            "indian" => calDate.Year,
            "chinese" or "dangi" => null, // no eras
            // Multi-era calendars: eraYear depends on year
            "ethiopic" => calDate.Year >= 1 ? calDate.Year : calDate.Year + 5500,
            "islamic-umalqura" or "islamic-civil" or "islamic-tbla" => calDate.Year >= 1 ? calDate.Year : 1 - calDate.Year,
            _ => null
        };
    }

    /// <summary>
    /// Returns the non-leap monthCode equivalent for a leap monthCode in a given calendar.
    /// Used when constraining a leap-month date into a year that doesn't have that leap month.
    /// chinese/dangi: M0NL → M0N (same display number, drop the 'L' suffix).
    /// hebrew: M05L (Adar I) → M06 (Adar). M05L is Hebrew's only valid leap monthCode
    /// (validated upstream by <see cref="TryValidateMonthCode"/>); in non-leap years its sole
    /// non-leap equivalent is M06 (the regular Adar).
    /// Non-leap monthCodes are returned unchanged.
    /// </summary>
    internal static string NonLeapEquivalentMonthCode(string calendar, string monthCode)
    {
        if (monthCode.Length != 4 || monthCode[3] != 'L')
        {
            return monthCode;
        }

        if (string.Equals(calendar, "hebrew", StringComparison.Ordinal))
        {
            // Hebrew has exactly one leap monthCode: M05L (Adar I) → M06 (Adar).
            return "M06";
        }

        // chinese, dangi: drop the trailing 'L'
        return monthCode.Substring(0, 3);
    }

    /// <summary>
    /// Resolves a monthCode string to an ordinal month number for a given calendar year.
    /// Returns the ordinal month, or throws/constrains based on overflow.
    /// </summary>
    internal static int MonthCodeToOrdinal(string calendar, Calendar? cal, int year, string monthCode, string overflow)
    {
        var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
        var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);

        if (calendar is "persian" or "indian" or "islamic-umalqura" or "islamic-civil" or "islamic-tbla")
        {
            // These calendars have 12 months and no leap months
            // Invalid monthCode (M13+, M00, or any leap month) is always rejected
            if (isLeap || displayMonth < 1 || displayMonth > 12)
            {
                throw new InvalidOperationException("reject");
            }

            return displayMonth;
        }

        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            // 13-month calendars with no leap months
            // Invalid monthCode (M14+, M00, or any leap month) is always rejected
            if (isLeap || displayMonth < 1 || displayMonth > 13)
            {
                throw new InvalidOperationException("reject");
            }

            return displayMonth;
        }

        var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, year);

        if (isLeap)
        {
            if (leapOrdinal <= 0)
            {
                // No leap month this year
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain: use the base month
                return displayMonth < leapOrdinal || leapOrdinal <= 0 ? displayMonth : displayMonth + 1;
            }

            var leapDisplay = GetLeapDisplayMonth(calendar, leapOrdinal);
            if (displayMonth != leapDisplay)
            {
                // Wrong leap month
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain: use the base month
                return displayMonth < leapOrdinal ? displayMonth : displayMonth + 1;
            }

            return leapOrdinal;
        }

        // Non-leap month
        if (leapOrdinal > 0 && displayMonth >= leapOrdinal)
        {
            return displayMonth + 1;
        }

        return displayMonth;
    }

    /// <summary>
    /// Returns the maximum valid display-month for a calendar. monthCode "M{n}" with n outside
    /// [1, MaxDisplayMonth(calendar)] is fundamentally invalid and must throw RangeError
    /// regardless of overflow option. Returns null for ISO/Gregorian calendars (which use the
    /// standard 12-month rule via different validation paths).
    /// </summary>
    internal static int? MaxDisplayMonth(string calendar)
    {
        return calendar switch
        {
            "coptic" or "ethiopic" or "ethioaa" => 13,
            "chinese" or "dangi" or "hebrew" => 12, // leap variants share display number 1-12
            "persian" or "indian" => 12,
            "islamic-umalqura" or "islamic-civil" or "islamic-tbla" => 12,
            _ => null
        };
    }

    /// <summary>
    /// Validates a monthCode's display-month part against a calendar's range and leap-month
    /// support. Returns true and emits the parsed display month if the monthCode is fundamentally
    /// valid for the calendar (display number in [1, MaxDisplayMonth] and, for leap variants
    /// ("M##L"), the calendar supports leap months). Returns false otherwise. The display month
    /// is also emitted on the false path when the structure parsed successfully (so callers can
    /// build precise error messages) — only structurally malformed monthCodes leave it at 0.
    /// This validation is overflow-independent per spec.
    /// </summary>
    internal static bool TryValidateMonthCode(string calendar, string monthCode, out int displayMonth)
    {
        displayMonth = 0;

        if (monthCode.Length < 3 || monthCode[0] != 'M')
        {
            return false;
        }

        if (!int.TryParse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out displayMonth))
        {
            return false;
        }

        var max = MaxDisplayMonth(calendar);
        if (max is null)
        {
            return true; // Unknown calendar — defer to caller
        }

        if (displayMonth < 1 || displayMonth > max.Value)
        {
            return false;
        }

        var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
        if (isLeap)
        {
            // chinese / dangi support leap variants of any month (M01L–M12L can occur)
            // hebrew has exactly one leap monthCode: M05L (Adar I). M01L, M02L, …, M04L,
            //   M06L, …, M12L are fundamentally invalid.
            // Other non-ISO calendars don't support leap months at all.
            return calendar switch
            {
                "chinese" or "dangi" => true,
                "hebrew" => displayMonth == 5,
                _ => false,
            };
        }

        return true;
    }

    /// <summary>
    /// Returns true if (month, monthCode) is internally consistent for a given non-ISO calendar
    /// year — i.e. both refer to the same calendar month. Used for spec-mandated mismatch
    /// validation that must happen regardless of overflow option.
    /// </summary>
    internal static bool MonthAndMonthCodeAgree(string calendar, int year, int month, string monthCode)
    {
        if (month <= 0)
        {
            return true;
        }

        Calendar? cal;
        try
        {
            cal = calendar switch
            {
                "coptic" or "ethiopic" or "ethioaa" or "indian" => null,
                "islamic-civil" or "islamic-tbla" => null,
                _ => GetCalendar(calendar)
            };
        }
        catch (NotSupportedException)
        {
            // Calendar not handled here — defer to caller's downstream validation.
            return true;
        }

        try
        {
            var resolved = MonthCodeToOrdinal(calendar, cal, year, monthCode, "reject");
            return resolved == month;
        }
        catch (InvalidOperationException)
        {
            // monthCode out of range or otherwise unresolvable for this year — let the
            // downstream CalendarDateToIso path produce the appropriate error.
            return true;
        }
    }

    /// <summary>
    /// Validates that month and monthCode are consistent for a non-ISO calendar.
    /// If only monthCode is provided, resolves it to an ordinal month.
    /// If only month is provided, uses the ordinal directly.
    /// If both are provided, validates consistency.
    /// </summary>
    internal static int ResolveMonthAndMonthCode(string calendar, int year, int month, string? monthCode, string overflow)
    {
        Calendar? cal = calendar is "coptic" or "ethiopic" or "ethioaa" or "indian" ? null : GetCalendar(calendar);

        if (monthCode is not null && month > 0)
        {
            // Both provided - validate consistency
            var resolvedOrdinal = MonthCodeToOrdinal(calendar, cal, year, monthCode, overflow);
            if (resolvedOrdinal != month)
            {
                throw new InvalidOperationException("reject"); // month and monthCode do not match
            }

            return resolvedOrdinal;
        }

        if (monthCode is not null)
        {
            return MonthCodeToOrdinal(calendar, cal, year, monthCode, overflow);
        }

        if (month > 0)
        {
            // Validate ordinal month is in range
            var maxMonths = GetMonthsInYear(calendar, cal, year);
            if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
            {
                return System.Math.Clamp(month, 1, maxMonths);
            }

            if (month < 1 || month > maxMonths)
            {
                throw new InvalidOperationException("reject");
            }

            return month;
        }

        throw new InvalidOperationException("reject"); // neither month nor monthCode provided
    }

    /// <summary>
    /// The same "latest calendar year whose (monthCode, day) still lands on or before the ISO reference
    /// year" search as below, expressed only in terms of the two conversions an
    /// <see cref="ICalendarProvider"/> supplies, for a calendar the engine itself does not implement.
    /// </summary>
    private static int FindProviderReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day, Engine engine)
    {
        var anchor = IsoToCalendarDate(calendar, new IsoDate(isoReferenceYear, 12, 31), engine).Year;
        var bestYear = anchor;
        var bestDay = long.MinValue;

        for (var year = anchor - 12; year <= anchor + 12; year++)
        {
            var iso = CalendarDateToIso(calendar, year, monthCode, 0, day, "reject", engine);
            if (iso is null || iso.Value.Year > isoReferenceYear)
            {
                continue;
            }

            var epochDay = IsoToJulianDay(iso.Value.Year, iso.Value.Month, iso.Value.Day);
            if (epochDay > bestDay)
            {
                bestDay = epochDay;
                bestYear = year;
            }
        }

        return bestYear;
    }

    /// <summary>
    /// Finds a calendar year where (year, monthCode, day) converts to an ISO date
    /// with the given ISO reference year. Used for PlainMonthDay without explicit year.
    /// </summary>
    internal static int FindCalendarReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day, Engine? engine = null)
    {
        // A calendar only a host provider knows has no BCL calendar behind it, so the search below cannot
        // run. The provider's own two conversions are enough to do it generically.
        if (engine is not null && !IsNonIsoCalendar(calendar))
        {
            return FindProviderReferenceYear(calendar, isoReferenceYear, monthCode, day, engine);
        }

        // For calendars that don't use .NET Calendar class
        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            return FindFixedEpochReferenceYear(calendar, isoReferenceYear, monthCode, day);
        }

        if (calendar is "indian")
        {
            return FindIndianReferenceYear(isoReferenceYear, monthCode, day);
        }

        if (calendar is "islamic-civil" or "islamic-tbla")
        {
            return FindIslamicTabularReferenceYear(calendar, isoReferenceYear, monthCode, day);
        }

        var cal = GetCalendar(calendar);

        // Widen the search window for lunisolar calendars (chinese/dangi/hebrew) where rare
        // leap months (e.g. chinese M02L only ~every 25-30 years) won't appear within ±12 years
        // of the canonical anchor. Bounded by typical .NET BCL ranges (chinese 1901-2100,
        // dangi 918-2050, hebrew 5343-5999 ≈ ISO 1583-2239).
        var window = calendar is "chinese" or "dangi" or "hebrew" ? 75 : 12;

        // For lunisolar calendars, a calendar year spans parts of two ISO years.
        // Spec algorithm: find the LATEST calendar year y such that ToIso(y, monthCode, day) has
        // ISO year ≤ isoReferenceYear AND the day is valid (not constrained) in that year's
        // monthCode. This produces the "latest representable" reference year, which is what tests
        // like reference-year-1972 (M05L hebrew → 1970, M02 D30 hebrew → 1971, M12 D30 islamic
        // → 1971) require. We fall back to a constrained-day match only if no exact-day year
        // exists within the search window.
        var approxYear = IsoYearToCalendarYear(calendar, cal, isoReferenceYear);
        var isLeapMonthCode = monthCode.Length == 4 && monthCode[3] == 'L';

        var bestYear = int.MinValue;
        var bestIsoDays = long.MinValue;
        var upperBound = TemporalHelpers.IsoDateToDays(isoReferenceYear, 12, 31);
        for (var y = approxYear - window; y <= approxYear + window; y++)
        {
            if (isLeapMonthCode)
            {
                var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, y);
                if (leapOrdinal <= 0)
                {
                    continue;
                }
            }

            try
            {
                var ordinal = MonthCodeToOrdinal(calendar, cal, y, monthCode, "reject");
                var maxDay = GetDaysInMonthCal(calendar, cal, y, ordinal);
                if (day > maxDay)
                {
                    continue; // day not valid in this year — skip in pass 1
                }

                var placed = PlaceOrdinalMonth(calendar, cal, y, ordinal, day);
                if (placed is null)
                {
                    continue;
                }

                // Pick the LATEST valid ISO date that's ≤ end-of-refYear. When two calendar
                // years both produce ISO dates in refYear (e.g. Hebrew M04 D26 in 5732 → 1972-01
                // and 5733 → 1972-12), the spec says use the later one.
                if (placed.Value <= upperBound && placed.Value > bestIsoDays)
                {
                    bestIsoDays = placed.Value;
                    bestYear = y;
                }
            }
            catch
            {
                // This year doesn't have the requested monthCode
            }
        }

        if (bestYear != int.MinValue)
        {
            return bestYear;
        }

        // Pass 1.5: rare leap months (chinese M09L/M10L/M11L, etc.) may not occur in any year
        // ≤ isoReferenceYear. Look forward for the SOONEST future year that admits the
        // un-constrained day; per the spec note in chinese-leap-month-codes-common, "uncommon"
        // leap months use a reference year in the near future when no past year qualifies.
        if (isLeapMonthCode)
        {
            var futureYear = int.MinValue;
            var futureIsoDays = long.MaxValue;
            for (var y = approxYear - window; y <= approxYear + window; y++)
            {
                var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, y);
                if (leapOrdinal <= 0) continue;

                try
                {
                    var ordinal = MonthCodeToOrdinal(calendar, cal, y, monthCode, "reject");
                    var maxDay = GetDaysInMonthCal(calendar, cal, y, ordinal);
                    if (day > maxDay) continue;
                    var placed = PlaceOrdinalMonth(calendar, cal, y, ordinal, day);
                    if (placed is null) continue;
                    if (placed.Value > upperBound && placed.Value < futureIsoDays)
                    {
                        futureIsoDays = placed.Value;
                        futureYear = y;
                    }
                }
                catch
                {
                    // skip
                }
            }

            if (futureYear != int.MinValue)
            {
                return futureYear;
            }
        }

        // Fallback: when no year has the day un-constrained, pick the year where this monthCode
        // holds the MOST days (so day constrains as little as possible). Tiebreak by latest ISO
        // date ≤ refYear ceiling. This matches the spec for cases like Hebrew M02 D31 constrain
        // → 30 (find the year where Cheshvan has 30 days, not 29) with refYear=1971.
        var fallbackYear = int.MinValue;
        var fallbackMaxDay = 0;
        var fallbackKey = long.MinValue;
        for (var y = approxYear - window; y <= approxYear + window; y++)
        {
            if (isLeapMonthCode)
            {
                var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, y);
                if (leapOrdinal <= 0)
                {
                    continue;
                }
            }

            try
            {
                var ordinal = MonthCodeToOrdinal(calendar, cal, y, monthCode, "reject");
                var maxDay = GetDaysInMonthCal(calendar, cal, y, ordinal);
                var clampedDay = System.Math.Min(day, maxDay);
                var placed = PlaceOrdinalMonth(calendar, cal, y, ordinal, clampedDay);
                if (placed is null || placed.Value > upperBound)
                {
                    continue;
                }

                if (maxDay > fallbackMaxDay || (maxDay == fallbackMaxDay && placed.Value > fallbackKey))
                {
                    fallbackMaxDay = maxDay;
                    fallbackKey = placed.Value;
                    fallbackYear = y;
                }
            }
            catch
            {
                // This year doesn't have the requested monthCode
            }
        }

        return fallbackYear != int.MinValue ? fallbackYear : approxYear;
    }

    /// <summary>
    /// Where a resolved (year, ordinal month, day) lands, as days since 1970-01-01, or null when this
    /// calendar cannot place it. The backing <see cref="Calendar"/> answers where there is one, and the
    /// engine's own conversion answers where there is not.
    /// </summary>
    private static long? PlaceOrdinalMonth(string calendar, Calendar? cal, int year, int ordinalMonth, int day)
    {
        if (cal is not null)
        {
            try
            {
                var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
                return TemporalHelpers.IsoDateToDays(dt.Year, dt.Month, dt.Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        var iso = BuiltinCalendarDateToIso(calendar, year, null, ordinalMonth, day, "reject");
        return iso is null ? null : TemporalHelpers.IsoDateToDays(iso.Value.Year, iso.Value.Month, iso.Value.Day);
    }

    #region Private Helpers

    /// <summary>
    /// Determines if a Hebrew year is a leap year using the 19-year Metonic cycle.
    /// Works for any Hebrew year, including those outside .NET HebrewCalendar's range.
    /// In each 19-year cycle, years 3, 6, 8, 11, 14, 17, 19 are leap years.
    /// </summary>
    private static bool IsHebrewLeapYearAlgorithmic(int year)
    {
        // Use mathematical modulo (always non-negative) for correct behavior on year ≤ 0.
        long v = ((long) 7 * year + 1L) % 19L;
        if (v < 0) v += 19L;
        return v < 7L;
    }

    // Hebrew arithmetic calendar (Reingold–Dershowitz). Used as a proleptic fallback for years
    // outside the .NET HebrewCalendar range (which only supports Hebrew years 5343–5999,
    // ≈ ISO 1583–2239). Year 1 (Tishrei 1) = JDN 347998 (= ISO −3760-09-07 proleptic Gregorian).
    private const long HebrewEpochJdn = 347998L;

    /// <summary>
    /// Mathematical floor division, returning floor(a/b). Differs from C#'s truncating "/"
    /// for negative dividends — required for the Reingold–Dershowitz Hebrew formulas to
    /// extend correctly into proleptic (year ≤ 0) territory.
    /// </summary>
    private static long FloorDiv(long a, long b)
    {
        long q = a / b;
        if ((a ^ b) < 0L && q * b != a) q--;
        return q;
    }

    /// <summary>Mathematical floor modulo, returning a non-negative residue (when b is positive).</summary>
    private static long FloorMod(long a, long b)
    {
        long r = a % b;
        if (r != 0L && (r ^ b) < 0L) r += b;
        return r;
    }

    private static long HebrewElapsedDays(int year)
    {
        long monthsElapsed = FloorDiv(235L * year - 234L, 19L);
        long partsElapsed = 12084L + 13753L * monthsElapsed;
        long days = 29L * monthsElapsed + FloorDiv(partsElapsed, 25920L);
        // Lo Adu Rosh dehiyya: postpone Tishrei 1 to avoid Sun/Wed/Fri.
        if (FloorMod(3L * (days + 1L), 7L) < 3L) days++;
        return days;
    }

    private static int HebrewYearLengthCorrection(int year)
    {
        long ny0 = HebrewElapsedDays(year - 1);
        long ny1 = HebrewElapsedDays(year);
        long ny2 = HebrewElapsedDays(year + 1);
        if (ny2 - ny1 == 356L) return 2;
        if (ny1 - ny0 == 382L) return 1;
        return 0;
    }

    private static long HebrewYearStartJdn(int year)
    {
        return HebrewEpochJdn + HebrewElapsedDays(year) + HebrewYearLengthCorrection(year);
    }

    private static int HebrewYearLength(int year)
    {
        return (int) (HebrewYearStartJdn(year + 1) - HebrewYearStartJdn(year));
    }

    private static int HebrewDaysInMonthOrdinal(int year, int ordinalMonth)
    {
        bool leap = IsHebrewLeapYearAlgorithmic(year);
        switch (ordinalMonth)
        {
            case 1: return 30; // Tishrei
            case 2: // Cheshvan: 30 if "complete" year (length 355/385), else 29
                {
                    var len = HebrewYearLength(year);
                    return len == 355 || len == 385 ? 30 : 29;
                }
            case 3: // Kislev: 29 if "deficient" year (length 353/383), else 30
                {
                    var len = HebrewYearLength(year);
                    return len == 353 || len == 383 ? 29 : 30;
                }
            case 4: return 29; // Tevet
            case 5: return 30; // Shevat
        }
        if (leap)
        {
            switch (ordinalMonth)
            {
                case 6: return 30; // Adar I (M05L)
                case 7: return 29; // Adar II (M06)
                case 8: return 30; // Nisan
                case 9: return 29; // Iyar
                case 10: return 30; // Sivan
                case 11: return 29; // Tammuz
                case 12: return 30; // Av
                case 13: return 29; // Elul
            }
        }
        else
        {
            switch (ordinalMonth)
            {
                case 6: return 29; // Adar (M06)
                case 7: return 30; // Nisan
                case 8: return 29; // Iyar
                case 9: return 30; // Sivan
                case 10: return 29; // Tammuz
                case 11: return 30; // Av
                case 12: return 29; // Elul
            }
        }

        // Unreachable while .NET's HebrewCalendar and IsHebrewLeapYearAlgorithmic agree about a year:
        // every caller has already clamped or rejected the ordinal against 13-if-leap-else-12, and
        // ordinal 13 exists only in a leap year. The fallback is nevertheless the shortest Hebrew month
        // rather than 0, because this value becomes the max of Math.Clamp(day, 1, maxDay) -- and a max
        // of 0 is inverted bounds, which is an ArgumentException escaping engine.Evaluate rather than a
        // constrained date. 29 never lengthens a month, so a day is still constrained, never admitted.
        return 29;
    }

    private static int HebrewMonthCodeToOrdinal(int year, string monthCode)
    {
        bool leap = IsHebrewLeapYearAlgorithmic(year);
        if (string.Equals(monthCode, "M05L", StringComparison.Ordinal))
        {
            return leap ? 6 : -1;
        }

        if (monthCode.Length != 3 || monthCode[0] != 'M')
        {
            return -1;
        }

        if (!int.TryParse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var displayMonth)
            || displayMonth < 1 || displayMonth > 12)
        {
            return -1;
        }

        if (displayMonth <= 5) return displayMonth;
        return leap ? displayMonth + 1 : displayMonth;
    }

    private static string HebrewOrdinalToMonthCode(int year, int ordinalMonth, out bool isLeapMonth)
    {
        bool leap = IsHebrewLeapYearAlgorithmic(year);
        if (leap)
        {
            if (ordinalMonth <= 5) { isLeapMonth = false; return $"M{ordinalMonth:D2}"; }
            if (ordinalMonth == 6) { isLeapMonth = true; return "M05L"; }
            isLeapMonth = false;
            return $"M{ordinalMonth - 1:D2}";
        }
        isLeapMonth = false;
        return $"M{ordinalMonth:D2}";
    }

    private static CalendarDate HebrewAlgorithmicFromIso(in IsoDate isoDate)
    {
        long jdn = IsoToJulianDay(isoDate.Year, isoDate.Month, isoDate.Day);
        long days = jdn - HebrewEpochJdn;
        int yearEst = (int) (days / 365L) + 1;
        while (HebrewYearStartJdn(yearEst + 1) <= jdn) yearEst++;
        while (HebrewYearStartJdn(yearEst) > jdn) yearEst--;

        int year = yearEst;
        long dayOfYear = jdn - HebrewYearStartJdn(year); // 0-based
        int ordinalMonth = 1;
        while (true)
        {
            var dim = HebrewDaysInMonthOrdinal(year, ordinalMonth);
            if (dayOfYear < dim) break;
            dayOfYear -= dim;
            ordinalMonth++;
        }
        int day = (int) dayOfYear + 1;

        var monthCode = HebrewOrdinalToMonthCode(year, ordinalMonth, out var isLeapMonth);
        int monthsInYear = IsHebrewLeapYearAlgorithmic(year) ? 13 : 12;
        int daysInMonth = HebrewDaysInMonthOrdinal(year, ordinalMonth);
        int daysInYear = HebrewYearLength(year);
        bool isLeapYear = IsHebrewLeapYearAlgorithmic(year);

        return new CalendarDate(year, ordinalMonth, monthCode, day, isLeapMonth, monthsInYear, daysInMonth, daysInYear, isLeapYear);
    }

    private static IsoDate? HebrewAlgorithmicToIso(int year, int ordinalMonth, int day)
    {
        long jdn = HebrewYearStartJdn(year);
        for (int m = 1; m < ordinalMonth; m++)
        {
            jdn += HebrewDaysInMonthOrdinal(year, m);
        }
        jdn += day - 1;
        return JdnToIso(jdn);
    }

    /// <summary>
    /// The <see cref="Calendar"/> backing a calendar, or null for one the engine reckons for itself.
    /// </summary>
    /// <remarks>
    /// <c>chinese</c> and <c>dangi</c> answer null: their BCL tables are not the same table on every
    /// target framework, so nothing reads them (https://github.com/sebastienros/jint/issues/3484).
    /// </remarks>
    private static Calendar? GetCalendar(string calendar)
    {
        return calendar switch
        {
            "chinese" or "dangi" => null,
            "hebrew" => HebrewCal,
            "persian" => PersianCal,
            "islamic-umalqura" => UmAlQuraCal,
            _ => throw new NotSupportedException($"Calendar '{calendar}' not supported")
        };
    }

    /// <summary>
    /// Converts an ISO year to an approximate calendar year.
    /// </summary>
    /// <remarks>
    /// A lunisolar calendar year carries the number of the Gregorian year it mostly falls in, so the ISO
    /// year is already the answer for <c>chinese</c> and <c>dangi</c>, which is what the null arm returns
    /// and what their BCL calendars returned for them before they were retired.
    /// </remarks>
    private static int IsoYearToCalendarYear(string calendar, Calendar? cal, int isoYear)
    {
        if (cal is null)
        {
            return isoYear;
        }

        try
        {
            // Use July 1 of the ISO year as a reference point
            var dt = new DateTime(System.Math.Max(1, System.Math.Min(isoYear, 9999)), 7, 1);
            if (dt >= cal.MinSupportedDateTime && dt <= cal.MaxSupportedDateTime)
            {
                return cal.GetYear(dt);
            }
        }
        catch
        {
            // Fall through
        }

        return isoYear;
    }

    /// <summary>
    /// Gets the ordinal position of the leap month in a given year.
    /// Returns 0 if the year has no leap month.
    /// </summary>
    private static int GetLeapMonthOrdinal(string calendar, Calendar? cal, int year)
    {
        if (calendar is "persian" or "coptic" or "ethiopic" or "ethioaa" or "indian" or "islamic-umalqura")
        {
            return 0; // These calendars never have leap months
        }

        if (calendar is "hebrew")
        {
            try
            {
                return HebrewCal.IsLeapYear(year) ? 6 : 0;
            }
            catch
            {
                // Out of .NET range: use algorithmic 19-year cycle
                // Years 3, 6, 8, 11, 14, 17, 19 of each 19-year cycle are leap years
                return IsHebrewLeapYearAlgorithmic(year) ? 6 : 0;
            }
        }

        if (calendar is "chinese" or "dangi")
        {
            // Which month of a lunisolar year is the leap one is the reckoning to say, for every year.
            // The BCL tables answered it differently on different target frameworks, and only for the
            // centuries they cover (https://github.com/sebastienros/jint/issues/3484).
            var resolved = LunisolarAstronomy.ForYear(year, RegionOf(calendar));
            return resolved is null ? 0 : resolved.LeapIndex + 1;
        }

        return 0;
    }

    /// <summary>
    /// The meridian a lunisolar calendar is reckoned at, for the two the engine implements.
    /// </summary>
    private static LunisolarRegion RegionOf(string calendar)
        => string.Equals(calendar, "dangi", StringComparison.Ordinal) ? LunisolarRegion.Korea : LunisolarRegion.China;

    /// <summary>
    /// Gets the display month number of the leap month at the given ordinal.
    /// For Chinese/Dangi: leapOrdinal 5 → display 4 (M04L).
    /// For Hebrew: leapOrdinal 6 → display 5 (M05L).
    /// </summary>
    private static int GetLeapDisplayMonth(string calendar, int leapOrdinal)
    {
        return leapOrdinal - 1;
    }

    /// <summary>
    /// Gets the number of months in a calendar year.
    /// </summary>
    /// <remarks>
    /// Never below 12: every arm answers 12 or 13 outright, and Calendar.GetMonthsInYear answers 12 or
    /// 13 for the lunisolar calendars that reach it — or throws for a year outside its range, which the
    /// catch answers with 12 or 13 too. The value becomes the max of Math.Clamp(month, 1, ...); see the
    /// type's remarks for what an answer below 1 would cost.
    /// </remarks>
    private static int GetMonthsInYear(string calendar, Calendar? cal, int year)
    {
        if (calendar is "persian" or "indian" or "islamic-umalqura" or "islamic-civil" or "islamic-tbla")
        {
            return 12;
        }

        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            return 13;
        }

        if (calendar is "chinese" or "dangi")
        {
            var reckoned = LunisolarAstronomy.ForYear(year, RegionOf(calendar));
            return reckoned?.MonthCount ?? 12;
        }

        if (cal is null)
        {
            return 12;
        }

        try
        {
            return cal.GetMonthsInYear(year);
        }
        catch
        {
            // For Hebrew out-of-range, use algorithmic leap year detection
            if (calendar is "hebrew")
            {
                return IsHebrewLeapYearAlgorithmic(year) ? 13 : 12;
            }

            return 12;
        }
    }

    /// <summary>
    /// Gets the number of days in a calendar month.
    /// </summary>
    /// <remarks>
    /// A year past the end of the backing <see cref="Calendar"/> is answered by the same reckoning the
    /// field accessors read that year with, rather than with a flat 30 — a month length is what decides
    /// where <see cref="CalendarDateAdd"/> clamps the day, so a guessed one places the wrong date.
    /// </remarks>
    private static int GetDaysInMonthCal(string calendar, Calendar? cal, int year, int month)
    {
        if (cal is not null)
        {
            try
            {
                return cal.GetDaysInMonth(year, month);
            }
            catch
            {
                // Past the end of the table; fall through to the reckoning below.
            }
        }

        return AlgorithmicDaysInMonth(calendar, year, month);
    }

    /// <summary>
    /// The length of an ordinal month in a year no <see cref="Calendar"/> covers, from the same
    /// algorithmic reckoning <see cref="IsoToCalendarDate"/> and <see cref="CalendarDateToIso"/> use
    /// there. 30 is the last resort, for a calendar with no reckoning of its own and for an ordinal that
    /// does not occur in the year at all.
    /// </summary>
    private static int AlgorithmicDaysInMonth(string calendar, int year, int ordinalMonth)
    {
        switch (calendar)
        {
            case "chinese":
            case "dangi":
                var resolved = LunisolarAstronomy.ForYear(year, RegionOf(calendar));
                if (resolved is not null && ordinalMonth >= 1 && ordinalMonth <= resolved.MonthCount)
                {
                    return resolved.DaysInMonth(ordinalMonth);
                }

                break;

            case "hebrew":
                if (ordinalMonth >= 1 && ordinalMonth <= 13)
                {
                    return HebrewDaysInMonthOrdinal(year, ordinalMonth);
                }

                break;

            case "persian":
                if (ordinalMonth >= 1 && ordinalMonth <= 12)
                {
                    return PersianAlgorithmicDaysInMonth(year, ordinalMonth);
                }

                break;
        }

        return 30;
    }

    #endregion

    #region Lunisolar (Chinese/Dangi) Calendar

    /// <summary>
    /// Resolves a lunisolar calendar date to ISO. Null when this calendar cannot place these fields.
    /// </summary>
    /// <remarks>
    /// The whole year -- its month count, which month is the leap one, how long each month is, and where
    /// day 1 of each falls -- comes from one <see cref="LunisolarYear"/>, so a date cannot be resolved
    /// half one way and half another.
    /// </remarks>
    private static IsoDate? LunisolarDateToIso(LunisolarRegion region, int year, string? monthCode, int month, int day, string overflow)
    {
        var resolved = LunisolarAstronomy.ForYear(year, region);
        if (resolved is null)
        {
            return null;
        }

        var leapMonth = resolved.LeapIndex + 1;
        int ordinalMonth;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            // Display month must be 1-12 (no calendar has display month > 12)
            if (displayMonth < 1 || displayMonth > 12)
            {
                return null;
            }

            if (isLeap)
            {
                if (leapMonth <= 0 || leapMonth - 1 != displayMonth)
                {
                    if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    // Constrain: use the non-leap version of the month
                    ordinalMonth = leapMonth > 0 && displayMonth >= leapMonth ? displayMonth + 1 : displayMonth;
                }
                else
                {
                    ordinalMonth = leapMonth;
                }
            }
            else
            {
                if (leapMonth > 0 && displayMonth >= leapMonth)
                {
                    ordinalMonth = displayMonth + 1;
                }
                else
                {
                    ordinalMonth = displayMonth;
                }
            }

            // Validate month matches ordinal if both provided
            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                // monthCode takes precedence
            }
        }
        else
        {
            ordinalMonth = month;
        }

        var maxMonths = resolved.MonthCount;

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, maxMonths);
        }
        else if (ordinalMonth < 1 || ordinalMonth > maxMonths)
        {
            return null;
        }

        var maxDay = resolved.DaysInMonth(ordinalMonth);

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        return IsoFromDays(resolved.MonthStarts[ordinalMonth - 1] + day - 1);
    }

    /// <summary>
    /// Reads an ISO date as a lunisolar date, for every date in Temporal's range.
    /// </summary>
    private static CalendarDate LunisolarAlgorithmicFromIso(LunisolarRegion region, in IsoDate isoDate)
    {
        var days = TemporalHelpers.IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
        var year = LunisolarAstronomy.ForFixed(days, region);

        var ordinalMonth = 1;
        while (ordinalMonth < year.MonthCount && year.MonthStarts[ordinalMonth] <= days)
        {
            ordinalMonth++;
        }

        var day = (int) (days - year.MonthStarts[ordinalMonth - 1]) + 1;
        var isLeapMonth = ordinalMonth - 1 == year.LeapIndex;

        // A leap month carries the display number of the month it follows, and every month after it in
        // the year is displaced by one -- which is exactly how the BCL arm above reads GetLeapMonth.
        var displayMonth = year.LeapIndex >= 0 && ordinalMonth - 1 >= year.LeapIndex ? ordinalMonth - 1 : ordinalMonth;
        var monthCode = isLeapMonth ? $"M{displayMonth:D2}L" : $"M{displayMonth:D2}";

        return new CalendarDate(
            year.Year,
            ordinalMonth,
            monthCode,
            day,
            isLeapMonth,
            year.MonthCount,
            year.DaysInMonth(ordinalMonth),
            year.DaysInYear,
            year.MonthCount == 13);
    }

    #endregion

    #region Hebrew Calendar

    private static CalendarDate HebrewToCalendarDate(in IsoDate isoDate)
    {
        var cal = HebrewCal;

        // ISO year may fall outside System.DateTime's range (1–9999 in BCL); for those years
        // we cannot construct a DateTime at all, so route directly to the algorithmic path.
        if (isoDate.Year < 1 || isoDate.Year > 9999)
        {
            return HebrewAlgorithmicFromIso(in isoDate);
        }

        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        if (dt < cal.MinSupportedDateTime || dt > cal.MaxSupportedDateTime)
        {
            return HebrewAlgorithmicFromIso(in isoDate);
        }

        var year = cal.GetYear(dt);
        var ordinalMonth = cal.GetMonth(dt);
        var day = cal.GetDayOfMonth(dt);
        var isLeapYear = cal.IsLeapYear(year);

        bool isLeapMonth;
        string monthCode;

        if (isLeapYear)
        {
            // Leap year: 13 months
            // ordinals 1-5 → M01-M05
            // ordinal 6 → M05L (Adar I, the intercalary month)
            // ordinals 7-13 → M06-M12
            if (ordinalMonth <= 5)
            {
                monthCode = $"M{ordinalMonth:D2}";
                isLeapMonth = false;
            }
            else if (ordinalMonth == 6)
            {
                monthCode = "M05L";
                isLeapMonth = true;
            }
            else
            {
                monthCode = $"M{ordinalMonth - 1:D2}";
                isLeapMonth = false;
            }
        }
        else
        {
            // Non-leap year: 12 months, ordinals 1-12 → M01-M12
            monthCode = $"M{ordinalMonth:D2}";
            isLeapMonth = false;
        }

        var monthsInYear = isLeapYear ? 13 : 12;
        var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
        var daysInYear = cal.GetDaysInYear(year);

        return new CalendarDate(year, ordinalMonth, monthCode, day, isLeapMonth, monthsInYear, daysInMonth, daysInYear, isLeapYear);
    }

    private static IsoDate? HebrewDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var cal = HebrewCal;
        int ordinalMonth;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            // Display month must be 1-12
            if (displayMonth < 1 || displayMonth > 12)
            {
                return null;
            }

            bool yearIsLeap;
            try
            {
                yearIsLeap = cal.IsLeapYear(year);
            }
            catch
            {
                yearIsLeap = IsHebrewLeapYearAlgorithmic(year);
            }

            if (isLeap)
            {
                // Hebrew has only one leap monthCode: M05L (Adar I). M01L, M02L, …, M04L,
                // M06L, …, M12L are fundamentally invalid for the Hebrew calendar — reject
                // unconditionally regardless of overflow option.
                if (displayMonth != 5)
                {
                    return null;
                }

                if (!yearIsLeap)
                {
                    // M05L exists only in leap years; constrain to M06 (Adar) in non-leap years
                    // unless overflow is "reject".
                    if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    // In Hebrew, M05L (Adar I) constrains to M06 (Adar) in non-leap years.
                    // M06 occupies ordinal 6 in non-leap years (after Shevat=M05).
                    ordinalMonth = 6;
                }
                else
                {
                    ordinalMonth = 6; // M05L → ordinal 6 in leap year (Adar I)
                }
            }
            else
            {
                if (yearIsLeap && displayMonth >= 6)
                {
                    ordinalMonth = displayMonth + 1; // M06 → ordinal 7, etc.
                }
                else
                {
                    ordinalMonth = displayMonth;
                }
            }

            // Validate month matches ordinal if both provided
            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }
        else
        {
            ordinalMonth = month;
        }

        // Constrain ordinal month
        int maxMonths;
        bool yearInDotNetRange = true;
        try
        {
            maxMonths = cal.IsLeapYear(year) ? 13 : 12;
        }
        catch
        {
            yearInDotNetRange = false;
            maxMonths = IsHebrewLeapYearAlgorithmic(year) ? 13 : 12;
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, maxMonths);
        }
        else if (ordinalMonth < 1 || ordinalMonth > maxMonths)
        {
            return null;
        }

        // Constrain day
        int maxDay;
        try
        {
            maxDay = yearInDotNetRange
                ? cal.GetDaysInMonth(year, ordinalMonth)
                : HebrewDaysInMonthOrdinal(year, ordinalMonth);
        }
        catch
        {
            maxDay = HebrewDaysInMonthOrdinal(year, ordinalMonth);
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        if (!yearInDotNetRange)
        {
            return HebrewAlgorithmicToIso(year, ordinalMonth, day);
        }

        try
        {
            var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
            return new IsoDate(dt.Year, dt.Month, dt.Day);
        }
        catch
        {
            return HebrewAlgorithmicToIso(year, ordinalMonth, day);
        }
    }

    #endregion

    #region Persian Calendar

    // Persian arithmetic calendar (2820-year cycle, Reingold–Dershowitz / Birashk).
    // Used as a proleptic fallback for years outside the .NET PersianCalendar range
    // (which only supports years 1–9999, ≈ ISO 622–10620). The 2820-year cycle has
    // 683 leap years; year 1 starts at JDN 1948321 = ISO 622-03-22 (proleptic Gregorian).
    private const long PersianEpochJdn = 1948321L;

    private static bool IsPersianLeapYearAlgorithmic(int year)
    {
        long yp = (long) year - 474L;
        long mod = ((yp % 2820L) + 2820L) % 2820L;
        long yc = mod + 474L;
        return ((yc + 38L) * 682L) % 2816L < 682L;
    }

    private static long PersianYearStartJdn(int year)
    {
        long yp = (long) year - 474L;
        long mod = ((yp % 2820L) + 2820L) % 2820L;
        long cycle = (yp - mod) / 2820L;
        long yc = mod + 474L;
        return PersianEpochJdn + cycle * 1029983L + 365L * (yc - 1L) + (yc * 682L - 110L) / 2816L;
    }

    /// <summary>
    /// Days in a Persian month, computed algorithmically for the years PersianCalendar cannot answer for.
    /// </summary>
    /// <remarks>
    /// Never below 29: 31 for months 1-6, 30 for 7-11, and 30 or 29 for month 12. A month argument
    /// outside 1-12 answers from one of those arms as well — zero and negatives take the first, 13 and
    /// above the last. The value becomes the max of Math.Clamp(day, 1, ...); see the type's remarks.
    /// </remarks>
    private static int PersianAlgorithmicDaysInMonth(int year, int month)
    {
        if (month <= 6) return 31;
        if (month <= 11) return 30;
        return IsPersianLeapYearAlgorithmic(year) ? 30 : 29;
    }

    private static CalendarDate PersianAlgorithmicFromIso(in IsoDate isoDate)
    {
        long jdn = IsoToJulianDay(isoDate.Year, isoDate.Month, isoDate.Day);

        // Estimate Persian year then walk to exact.
        long days = jdn - PersianEpochJdn;
        int yearEst = (int) (days / 365L) + 1;
        while (PersianYearStartJdn(yearEst + 1) <= jdn) yearEst++;
        while (PersianYearStartJdn(yearEst) > jdn) yearEst--;

        int year = yearEst;
        long dayOfYear = jdn - PersianYearStartJdn(year) + 1; // 1-based

        int month, day;
        if (dayOfYear <= 186)
        {
            month = (int) ((dayOfYear - 1) / 31) + 1;
            day = (int) (dayOfYear - (month - 1) * 31);
        }
        else
        {
            long remaining = dayOfYear - 186;
            month = (int) ((remaining - 1) / 30) + 7;
            day = (int) (remaining - (month - 7) * 30);
        }

        bool isLeap = IsPersianLeapYearAlgorithmic(year);
        var monthCode = $"M{month:D2}";
        return new CalendarDate(year, month, monthCode, day, false, 12, PersianAlgorithmicDaysInMonth(year, month), isLeap ? 366 : 365, isLeap);
    }

    private static IsoDate? PersianAlgorithmicToIso(int year, int month, int day)
    {
        long jdn = PersianYearStartJdn(year);
        for (int m = 1; m < month; m++)
        {
            jdn += PersianAlgorithmicDaysInMonth(year, m);
        }
        jdn += day - 1;
        return JdnToIso(jdn);
    }

    private static CalendarDate PersianToCalendarDate(in IsoDate isoDate)
    {
        var cal = PersianCal;

        if (isoDate.Year < 1 || isoDate.Year > 9999)
        {
            return PersianAlgorithmicFromIso(in isoDate);
        }

        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        if (dt < cal.MinSupportedDateTime || dt > cal.MaxSupportedDateTime)
        {
            return PersianAlgorithmicFromIso(in isoDate);
        }

        var year = cal.GetYear(dt);
        var ordinalMonth = cal.GetMonth(dt);
        var day = cal.GetDayOfMonth(dt);
        var isLeapYear = cal.IsLeapYear(year);

        var monthCode = $"M{ordinalMonth:D2}";
        var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
        var daysInYear = cal.GetDaysInYear(year);

        return new CalendarDate(year, ordinalMonth, monthCode, day, false, 12, daysInMonth, daysInYear, isLeapYear);
    }

    private static IsoDate? PersianDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var cal = PersianCal;
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                // Persian has no leap months
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                ordinalMonth = displayMonth;
            }

            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        // Constrain month
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, 12);
        }
        else if (ordinalMonth < 1 || ordinalMonth > 12)
        {
            return null;
        }

        // Constrain day
        int maxDay;
        try
        {
            maxDay = cal.GetDaysInMonth(year, ordinalMonth);
        }
        catch
        {
            // Fallback: use the algorithmic computation, which works for all years.
            maxDay = PersianAlgorithmicDaysInMonth(year, ordinalMonth);
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        try
        {
            var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
            return new IsoDate(dt.Year, dt.Month, dt.Day);
        }
        catch
        {
            // Year is outside .NET PersianCalendar range — use algorithmic conversion.
            return PersianAlgorithmicToIso(year, ordinalMonth, day);
        }
    }

    #endregion

    #region Fixed-Epoch Calendars (Coptic/Ethiopic/EthioAA)

    /// <summary>
    /// Returns the number of leap years from year 1 through year n (inclusive)
    /// for calendars where year % 4 == 3 is a leap year.
    /// </summary>
    private static long FixedEpochLeapCount(long n)
    {
        if (n < 3) return 0;
        return (n - 3) / 4 + 1;
    }

    /// <summary>
    /// Returns true if a fixed-epoch calendar year is a leap year.
    /// Leap years are those where year % 4 == 3 (years 3, 7, 11, ...).
    /// </summary>
    private static bool IsFixedEpochLeapYear(int year)
    {
        // Handle negative years: need mathematical modulo (always non-negative)
        var mod = ((year % 4) + 4) % 4;
        return mod == 3;
    }

    /// <summary>
    /// Returns the number of days from the calendar epoch to the start of the given year.
    /// </summary>
    private static long FixedEpochDaysToYear(int year)
    {
        if (year >= 1)
        {
            return (long) (year - 1) * 365 + FixedEpochLeapCount(year - 1);
        }

        // For years <= 0 (proleptic): count backwards
        // Year 0 is 1 year before year 1, year -1 is 2 years before, etc.
        // Each year has 365 or 366 days. In the proleptic direction,
        // we need to handle the leap pattern correctly.
        // Year Y (Y <= 0): daysToYear(Y) = -(daysFromYear(Y) to year 1)
        // daysFromYear(Y) to year 1 = sum of days in years Y, Y+1, ..., 0
        // For simplicity, use the formula: (Y-1)*365 + leapCount
        // where leapCount handles negative years
        var y = (long) year - 1; // years elapsed (negative)
        // For negative y, floor(y/4) gives the correct negative leap count
        // But our leap rule is year%4==3, so for year Y:
        // Leap years below 1: -1 (mod4=3), -5 (mod4=3), -9 (mod4=3), ...
        // Count of leap years from Y to 0 inclusive where ((k%4)+4)%4 == 3:
        // k = -1, -5, -9, ..., Y (if Y matches pattern)
        // Total days = y * 365 + (negative leap count)

        // Use a different approach: convert to positive counting
        // Let p = 1 - year (p >= 1 for year <= 0)
        // The leap years in range [year, 0] are at positions where mod4==3
        // Equivalently, leap count = FixedEpochLeapCount(p) considering the shift
        // Actually, for proleptic years, the leap pattern mirrors:
        // Year -1: (-1+4)%4=3, leap. Year 0: 0%4=0, not leap.
        // Year -2: (-2+4)%4=2, not. Year -3: (-3+4)%4=1, not. Year -4: (-4+4)%4=0, not.
        // Year -5: (-5+8)%4=3, leap.
        // So leap years: -1, -5, -9, ... i.e., every 4 years starting from -1
        // Count of leap years from year to -1 (inclusive, for years year..0 that are leap):
        // = count of k in [year, 0] where ((k%4)+4)%4 == 3
        // = count of k in [year, -1] where ((k%4)+4)%4 == 3 (since year 0 is not leap)
        // The leap years are -1, -5, -9, ..., down to year
        // If year <= -1: count = floor((-year - 1) / 4) + 1 when ((-year-1)%4+4)%4... actually
        // count = floor((- year) / 4) ... let me just count directly:
        // Years from (year) to (-1) that are leap:
        // These are: -1, -5, -9, ..., last >= year
        // First (largest): -1. Step: -4.
        // Count = floor((-1 - year) / 4) + 1 when year <= -1

        // Days from epoch to year Y (Y <= 0):
        // = -(days from Y to 1) = -( sum_{k=Y}^{0} daysInYear(k) )
        // daysInYear(k) = 366 if leap(k), else 365
        // = -( (1-Y)*365 + leapsBetween(Y, 0) )
        long p = 1 - year; // number of years from Y to 0 inclusive
        long leapsBetween;
        if (year <= -1)
        {
            leapsBetween = (-1 - year) / 4 + 1;
        }
        else
        {
            // year == 0
            leapsBetween = 0;
        }

        return -(p * 365 + leapsBetween);
    }

    /// <summary>
    /// Returns the number of days in the given month of a fixed-epoch calendar year.
    /// Months 1-12 have 30 days each. Month 13 has 5 days (6 in leap year).
    /// </summary>
    /// <remarks>
    /// Never below 5, the length of a common-year epagomenal month, and a month argument outside 1-13
    /// takes the 30 fallback. The value becomes the max of Math.Clamp(day, 1, ...); see the type's
    /// remarks.
    /// </remarks>
    private static int FixedEpochDaysInMonth(int year, int month)
    {
        if (month >= 1 && month <= 12) return 30;
        if (month == 13) return IsFixedEpochLeapYear(year) ? 6 : 5;
        return 30; // fallback
    }

    /// <summary>
    /// Converts a fixed-epoch calendar date to epoch days (days since Unix epoch).
    /// </summary>
    private static long FixedEpochToEpochDays(long calendarEpochDays, int year, int month, int day)
    {
        return calendarEpochDays + FixedEpochDaysToYear(year) + (month - 1) * 30 + day - 1;
    }

    /// <summary>
    /// Converts epoch days to a fixed-epoch calendar date.
    /// </summary>
    private static void EpochDaysToFixedEpoch(long calendarEpochDays, long epochDays, out int year, out int month, out int day)
    {
        var dfc = epochDays - calendarEpochDays; // days from calendar epoch

        // Approximate year
        if (dfc >= 0)
        {
            year = (int) (dfc / 365) + 1;
            // Adjust: we may have overshot
            while (FixedEpochDaysToYear(year) > dfc)
            {
                year--;
            }

            while (FixedEpochDaysToYear(year + 1) <= dfc)
            {
                year++;
            }
        }
        else
        {
            year = (int) (dfc / 366); // conservative (may undershoot)
            while (FixedEpochDaysToYear(year) > dfc)
            {
                year--;
            }

            while (FixedEpochDaysToYear(year + 1) <= dfc)
            {
                year++;
            }
        }

        var dayOfYear = (int) (dfc - FixedEpochDaysToYear(year)); // 0-based
        month = dayOfYear / 30 + 1;
        if (month > 13) month = 13;
        day = dayOfYear - (month - 1) * 30 + 1;
    }

    /// <summary>
    /// Converts an ISO date to a fixed-epoch calendar date (Coptic/Ethiopic/EthioAA).
    /// </summary>
    private static CalendarDate FixedEpochToCalendarDate(long calendarEpochDays, in IsoDate isoDate)
    {
        var epochDays = TemporalHelpers.IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
        EpochDaysToFixedEpoch(calendarEpochDays, epochDays, out var year, out var month, out var day);

        var monthCode = $"M{month:D2}";
        var isLeapYear = IsFixedEpochLeapYear(year);
        var daysInMonth = FixedEpochDaysInMonth(year, month);
        var daysInYear = isLeapYear ? 366 : 365;

        return new CalendarDate(year, month, monthCode, day, false, 13, daysInMonth, daysInYear, isLeapYear);
    }

    /// <summary>
    /// Converts a fixed-epoch calendar date to an ISO date.
    /// </summary>
    private static IsoDate? FixedEpochDateToIso(long calendarEpochDays, int maxMonth, int year, string? monthCode, int month, int day, string overflow)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                // These calendars have no leap months
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                // Validate display month range (1-13 for coptic/ethiopic/ethioaa)
                if (displayMonth < 1 || displayMonth > maxMonth)
                {
                    if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    ordinalMonth = System.Math.Clamp(displayMonth, 1, maxMonth);
                }
                else
                {
                    ordinalMonth = displayMonth;
                }
            }

            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        // Constrain month
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, maxMonth);
        }
        else if (ordinalMonth < 1 || ordinalMonth > maxMonth)
        {
            return null;
        }

        // Constrain day
        var maxDay = FixedEpochDaysInMonth(year, ordinalMonth);

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        var epochDays = FixedEpochToEpochDays(calendarEpochDays, year, ordinalMonth, day);
        return TemporalHelpers.DaysToIsoDate(epochDays);
    }

    /// <summary>
    /// CalendarDateAdd for fixed-epoch calendars (Coptic/Ethiopic/EthioAA).
    /// </summary>
    private static IsoDate FixedEpochCalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow)
    {
        var epochDays = GetCalendarEpochDays(calendar);
        var isoEpochDays = TemporalHelpers.IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
        EpochDaysToFixedEpoch(epochDays, isoEpochDays, out var calYear, out var calMonth, out var calDay);

        var newYear = calYear + years;
        var newMonth = calMonth;

        // Add months (ordinal stepping through 13-month years)
        if (months != 0)
        {
            newMonth += months;
            while (newMonth > 13)
            {
                newMonth -= 13;
                newYear++;
            }

            while (newMonth < 1)
            {
                newYear--;
                newMonth += 13;
            }
        }

        // Constrain day
        var maxDay = FixedEpochDaysInMonth(newYear, newMonth);
        var newDay = calDay;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        var resultEpochDays = FixedEpochToEpochDays(epochDays, newYear, newMonth, newDay);
        return TemporalHelpers.DaysToIsoDate(resultEpochDays);
    }

    /// <summary>
    /// Finds a calendar reference year for a fixed-epoch calendar such that
    /// (calendarYear, monthCode, day) maps to an ISO date in isoReferenceYear.
    /// </summary>
    private static int FindFixedEpochReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day)
    {
        var epochDays = GetCalendarEpochDays(calendar);
        var monthNum = MonthCodeToMonthNumber(monthCode);

        // For M13 and days that require a leap year, search for a leap calendar year
        // that maps to an ISO year near the reference year.
        // Strategy: try ISO reference years starting from the target and going back,
        // preferring years where the month has the maximum number of days.
        var bestYear = 0;
        var bestMaxDay = 0;
        var bestIsoYear = 0;

        for (var isoOffset = 0; isoOffset <= 4; isoOffset++)
        {
            var targetIsoYear = isoReferenceYear - isoOffset;
            var midYearDays = TemporalHelpers.IsoDateToDays(targetIsoYear, 7, 1);
            EpochDaysToFixedEpoch(epochDays, midYearDays, out var approxYear, out _, out _);

            for (var y = approxYear - 2; y <= approxYear + 2; y++)
            {
                var maxDay = FixedEpochDaysInMonth(y, monthNum);
                var clampedDay = System.Math.Min(day, maxDay);
                var isoDate = FixedEpochDateToIso(epochDays, 13, y, monthCode, 0, clampedDay, "constrain");
                if (isoDate is null)
                {
                    continue;
                }

                // Prefer years where the month has more days (to maximize valid range)
                if (maxDay > bestMaxDay || (maxDay == bestMaxDay && System.Math.Abs(isoDate.Value.Year - isoReferenceYear) < System.Math.Abs(bestIsoYear - isoReferenceYear)))
                {
                    bestYear = y;
                    bestMaxDay = maxDay;
                    bestIsoYear = isoDate.Value.Year;
                }

                // If we found a perfect match (day fits and ISO year matches), use it
                if (day <= maxDay && isoDate.Value.Year == targetIsoYear)
                {
                    return y;
                }
            }
        }

        if (bestYear != 0)
        {
            return bestYear;
        }

        // Fallback: approximate
        var fallbackDays = TemporalHelpers.IsoDateToDays(isoReferenceYear, 7, 1);
        EpochDaysToFixedEpoch(epochDays, fallbackDays, out var fallbackYear, out _, out _);
        return fallbackYear;
    }

    private static long GetCalendarEpochDays(string calendar)
    {
        return calendar switch
        {
            "coptic" => CopticEpochDays,
            "ethiopic" => EthiopicEpochDays,
            "ethioaa" => EthioAAEpochDays,
            _ => throw new NotSupportedException($"Calendar '{calendar}' does not use fixed epoch")
        };
    }

    /// <summary>
    /// Extracts the numeric month from a monthCode string (e.g., "M05" -> 5, "M13" -> 13).
    /// </summary>
    private static int MonthCodeToMonthNumber(string monthCode)
    {
        return int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    #endregion

    #region Indian (Saka) Calendar

    /// <summary>
    /// Returns true if an Indian (Saka) calendar year is a leap year.
    /// An Indian year is a leap year if the corresponding Gregorian year (sakaYear + 78) is a leap year.
    /// </summary>
    private static bool IsIndianLeapYear(int sakaYear)
    {
        return IsoDate.IsLeapYear(sakaYear + 78);
    }

    /// <summary>
    /// Returns the number of days in the given month of an Indian calendar year.
    /// Month 1 (Chaitra): 30 days (31 in leap year).
    /// Months 2-6: 31 days each.
    /// Months 7-12: 30 days each.
    /// </summary>
    /// <remarks>
    /// Never below 30, and the final arm covers a month argument outside 1-12 as well. The value
    /// becomes the max of Math.Clamp(day, 1, ...); see the type's remarks.
    /// </remarks>
    private static int IndianDaysInMonth(int sakaYear, int month)
    {
        if (month == 1) return IsIndianLeapYear(sakaYear) ? 31 : 30;
        if (month >= 2 && month <= 6) return 31;
        return 30; // months 7-12
    }

    /// <summary>
    /// Converts an ISO date to an Indian (Saka) calendar date.
    /// </summary>
    private static CalendarDate IndianToCalendarDate(in IsoDate isoDate)
    {
        var isoYear = isoDate.Year;

        // Day of year in ISO calendar (1-based)
        var isoDayOfYear = DayOfYear(isoYear, isoDate.Month, isoDate.Day);

        int sakaYear;
        int sakaDayOfYear; // 0-based within Saka year

        // Chaitra 1 is always ISO day 81:
        // Non-leap: Jan(31) + Feb(28) + Mar 22 = 81
        // Leap: Jan(31) + Feb(29) + Mar 21 = 81
        const int chaitraStartDoy = 81;

        if (isoDayOfYear >= chaitraStartDoy)
        {
            // We're in the current Saka year
            sakaYear = isoYear - 78;
            sakaDayOfYear = isoDayOfYear - chaitraStartDoy; // 0-based
        }
        else
        {
            // We're still in the previous Saka year (before Chaitra of current ISO year)
            sakaYear = isoYear - 79;
            // Day of year within previous Saka year
            var prevIsoYear = isoYear - 1;
            var prevYearDays = IsoDate.IsLeapYear(prevIsoYear) ? 366 : 365;
            sakaDayOfYear = (prevYearDays - chaitraStartDoy) + isoDayOfYear;
        }

        // Convert sakaDayOfYear (0-based) to month/day
        var isLeapYear = IsIndianLeapYear(sakaYear);
        var remaining = sakaDayOfYear;
        var month = 1;

        for (var m = 1; m <= 12; m++)
        {
            var dim = IndianDaysInMonth(sakaYear, m);
            if (remaining < dim)
            {
                month = m;
                break;
            }

            remaining -= dim;
            month = m + 1;
        }

        if (month > 12) month = 12; // safety clamp
        var day = remaining + 1;

        var monthCode = $"M{month:D2}";
        var daysInMonth = IndianDaysInMonth(sakaYear, month);
        var daysInYear = isLeapYear ? 366 : 365;

        return new CalendarDate(sakaYear, month, monthCode, day, false, 12, daysInMonth, daysInYear, isLeapYear);
    }

    /// <summary>
    /// Converts an Indian (Saka) calendar date to an ISO date.
    /// </summary>
    private static IsoDate? IndianDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                // Indian has no leap months
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                ordinalMonth = displayMonth;
            }

            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        // Constrain month
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, 12);
        }
        else if (ordinalMonth < 1 || ordinalMonth > 12)
        {
            return null;
        }

        // Constrain day
        var maxDay = IndianDaysInMonth(year, ordinalMonth);

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        // Convert to ISO: compute the day-of-year offset
        // Chaitra 1 of Saka year Y = March 22 (or 21) of ISO year Y + 78
        var isoYear = year + 78;
        var isLeapYear = IsoDate.IsLeapYear(isoYear);

        // Compute Saka day of year (0-based)
        var sakaDoy = 0;
        for (var m = 1; m < ordinalMonth; m++)
        {
            sakaDoy += IndianDaysInMonth(year, m);
        }

        sakaDoy += day - 1;

        // Chaitra 1 = March 22 (non-leap) or March 21 (leap) of isoYear
        // Day 81 of ISO year (1-based), in both cases
        var isoDoy = 81 + sakaDoy; // 1-based ISO day of year
        var totalDaysInIsoYear = isLeapYear ? 366 : 365;

        int resultIsoYear;
        int resultIsoDoy;

        if (isoDoy > totalDaysInIsoYear)
        {
            // Wraps to next ISO year
            resultIsoYear = isoYear + 1;
            resultIsoDoy = isoDoy - totalDaysInIsoYear;
        }
        else
        {
            resultIsoYear = isoYear;
            resultIsoDoy = isoDoy;
        }

        // Convert ISO day-of-year to month/day
        return DayOfYearToIsoDate(resultIsoYear, resultIsoDoy);
    }

    /// <summary>
    /// CalendarDateAdd for the Indian (Saka) calendar.
    /// </summary>
    private static IsoDate IndianCalendarDateAdd(in IsoDate isoDate, int years, int months, string overflow)
    {
        var calDate = IndianToCalendarDate(in isoDate);
        var newYear = calDate.Year + years;
        var newMonth = calDate.Month;

        // Add months
        if (months != 0)
        {
            newMonth += months;
            while (newMonth > 12)
            {
                newMonth -= 12;
                newYear++;
            }

            while (newMonth < 1)
            {
                newYear--;
                newMonth += 12;
            }
        }

        // Constrain day
        var maxDay = IndianDaysInMonth(newYear, newMonth);
        var newDay = calDate.Day;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        var result = IndianDateToIso(newYear, null, newMonth, newDay, overflow);

        // Answering with the input date when the conversion cannot be made -- which is what this did --
        // is a no-progress answer: `add` reports that adding a year changed nothing, and the month walk
        // in CalendarDateUntil takes the same step forever. See CalendarRangeException.
        return result ?? throw new CalendarRangeException("indian");
    }

    /// <summary>
    /// Finds a reference year for the Indian calendar.
    /// </summary>
    private static int FindIndianReferenceYear(int isoReferenceYear, string monthCode, int day)
    {
        // Indian year ≈ ISO year - 78
        var approxYear = isoReferenceYear - 78;

        for (var y = approxYear - 1; y <= approxYear + 1; y++)
        {
            var ordinal = MonthCodeToMonthNumber(monthCode);
            var maxDay = IndianDaysInMonth(y, ordinal);
            var clampedDay = System.Math.Min(day, maxDay);
            var isoDate = IndianDateToIso(y, null, ordinal, clampedDay, "constrain");
            if (isoDate?.Year == isoReferenceYear)
            {
                return y;
            }
        }

        return approxYear;
    }

    /// <summary>
    /// Returns the day of year (1-based) for an ISO date.
    /// </summary>
    private static int DayOfYear(int year, int month, int day)
    {
        int[] monthDays = IsoDate.IsLeapYear(year)
            ? new[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335 }
            : new[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };

        return monthDays[month - 1] + day;
    }

    /// <summary>
    /// Converts an ISO day of year (1-based) to an ISO date.
    /// </summary>
    private static IsoDate DayOfYearToIsoDate(int year, int dayOfYear)
    {
        int[] monthDays = IsoDate.IsLeapYear(year)
            ? new[] { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 }
            : new[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        var remaining = dayOfYear;
        for (var m = 0; m < 12; m++)
        {
            if (remaining <= monthDays[m])
            {
                return new IsoDate(year, m + 1, remaining);
            }

            remaining -= monthDays[m];
        }

        return new IsoDate(year, 12, 31); // safety fallback
    }

    #endregion

    #region Islamic Umm al-Qura Calendar

    /// <summary>
    /// Converts an ISO date to an Islamic Umm al-Qura calendar date.
    /// Uses .NET's UmAlQuraCalendar when within range, falls back to islamic-civil algorithm otherwise.
    /// </summary>
    private static CalendarDate IslamicUmalquraToCalendarDate(in IsoDate isoDate)
    {
        var cal = UmAlQuraCal;

        // ISO year may fall outside System.DateTime's 1–9999 range; route directly to the
        // tabular fallback for those years rather than letting `new DateTime` throw and the
        // outer catch return ISO-like fields.
        if (isoDate.Year < 1 || isoDate.Year > 9999)
        {
            return IslamicCivilToCalendarDate(in isoDate);
        }

        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        if (dt >= cal.MinSupportedDateTime && dt <= cal.MaxSupportedDateTime)
        {
            var year = cal.GetYear(dt);
            var ordinalMonth = cal.GetMonth(dt);
            var day = cal.GetDayOfMonth(dt);
            var isLeapYear = cal.IsLeapYear(year);

            var monthCode = $"M{ordinalMonth:D2}";
            var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
            var daysInYear = cal.GetDaysInYear(year);

            return new CalendarDate(year, ordinalMonth, monthCode, day, false, 12, daysInMonth, daysInYear, isLeapYear);
        }

        // Fall back to islamic-civil algorithm for dates outside UmAlQura range
        return IslamicCivilToCalendarDate(in isoDate);
    }

    /// <summary>
    /// Islamic civil calendar conversion (fallback for out-of-range UmAlQura dates).
    /// </summary>
    private static CalendarDate IslamicCivilToCalendarDate(in IsoDate isoDate, long epochJdn = 1948439L)
    {
        // Convert ISO to Julian Day Number
        var jdn = IsoToJulianDay(isoDate.Year, isoDate.Month, isoDate.Day);

        // Forward formula: JDN = yearDays + monthDays + day + epoch (1-based day),
        // so JDN(1,1,1) = epoch + 1. To make inverse 0-based, subtract epoch + 1.
        var daysFromEpoch = jdn - epochJdn - 1;

        // 30-year cycle with 11 leap years
        var cycle30 = daysFromEpoch / 10631;
        var remaining = daysFromEpoch % 10631;
        if (remaining < 0)
        {
            cycle30--;
            remaining += 10631;
        }

        // Year within cycle
        var yearInCycle = 0;
        var daysSoFar = 0L;
        for (var y = 0; y < 30; y++)
        {
            var daysInYear = IsIslamicCivilLeapYear(y + 1) ? 355 : 354;
            if (daysSoFar + daysInYear > remaining)
            {
                yearInCycle = y;
                break;
            }

            daysSoFar += daysInYear;
        }

        var year = (int) (cycle30 * 30 + yearInCycle + 1);
        remaining -= daysSoFar;

        // Month within year - use cumulative month days matching IslamicToJdn
        var month = 1;
        for (var m = 1; m <= 12; m++)
        {
            var nextMonthStart = IslamicCumulativeMonthDays(m + 1);
            if (remaining < nextMonthStart)
            {
                month = m;
                break;
            }

            if (m == 12) month = 12;
        }

        var monthOffset = IslamicCumulativeMonthDays(month);
        var day = (int) (remaining - monthOffset) + 1;

        var isLeapYear = IsIslamicCivilLeapYear(year);
        var monthCode = $"M{month:D2}";
        var dim = IslamicCivilDaysInMonth(year, month);
        var diy = isLeapYear ? 355 : 354;

        return new CalendarDate(year, month, monthCode, day, false, 12, dim, diy, isLeapYear);
    }

    private static bool IsIslamicCivilLeapYear(int year)
    {
        // Positive mod: ((year % 30) + 30) % 30 to handle negative years
        var mod = ((year % 30) + 30) % 30;
        // Leap years in 30-year cycle: 2, 5, 7, 10, 13, 16, 18, 21, 24, 26, 29
        return mod is 2 or 5 or 7 or 10 or 13 or 16 or 18 or 21 or 24 or 26 or 29;
    }

    /// <summary>
    /// Returns the number of days in the given month of the Islamic Civil (tabular) calendar.
    /// </summary>
    /// <remarks>
    /// Never below 29, and a month argument outside 1-12 answers 30 or 29 from the same two arms. The
    /// value becomes the max of Math.Clamp(day, 1, ...); see the type's remarks.
    /// </remarks>
    private static int IslamicCivilDaysInMonth(int year, int month)
    {
        // Odd months have 30 days, even months have 29 days
        // except month 12 in leap years has 30 days
        if (month % 2 == 1) return 30;
        if (month == 12 && IsIslamicCivilLeapYear(year)) return 30;
        return 29;
    }

    /// <summary>
    /// Converts an ISO date (proleptic Gregorian) to a Julian Day Number.
    /// Uses Howard Hinnant's epoch-day formula (via TemporalHelpers.IsoDateToDays) plus the
    /// constant offset 2440588 (= JDN of 1970-01-01) so that the result is correct for any
    /// proleptic ISO year — the older Fliegel–Van Flandern formula written here previously
    /// used truncating C# integer division and broke for years &lt; −4800.
    /// </summary>
    private static long IsoToJulianDay(int year, int month, int day)
    {
        return TemporalHelpers.IsoDateToDays(year, month, day) + 2440588L;
    }

    /// <summary>
    /// The proleptic ISO date for a count of days since 1970-01-01, which is the scale
    /// <see cref="LunisolarAstronomy"/> reckons in. <see cref="JdnToIso"/> answers for every Julian Day
    /// Number, so the nullable it returns never is one here.
    /// </summary>
    internal static IsoDate IsoFromDays(long days) => JdnToIso(days + 2440588L)!.Value;

    /// <summary>
    /// Converts an Islamic Umm al-Qura calendar date to an ISO date.
    /// Uses .NET's UmAlQuraCalendar when within range, falls back to islamic-civil algorithm otherwise.
    /// </summary>
    private static IsoDate? IslamicUmalquraDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                ordinalMonth = displayMonth;
            }

            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        // Constrain month
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, 12);
        }
        else if (ordinalMonth < 1 || ordinalMonth > 12)
        {
            return null;
        }

        // Try UmAlQura calendar first
        var cal = UmAlQuraCal;
        try
        {
            // Check if year is in UmAlQura's supported range
            var minYear = cal.GetYear(cal.MinSupportedDateTime);
            var maxYear = cal.GetYear(cal.MaxSupportedDateTime);

            if (year >= minYear && year <= maxYear)
            {
                var maxDay = cal.GetDaysInMonth(year, ordinalMonth);
                if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
                {
                    day = System.Math.Clamp(day, 1, maxDay);
                }
                else if (day < 1 || day > maxDay)
                {
                    return null;
                }

                var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
                return new IsoDate(dt.Year, dt.Month, dt.Day);
            }
        }
        catch
        {
            // Fall through to islamic-civil
        }

        // Fall back to islamic-civil algorithm
        var maxDayCivil = IslamicCivilDaysInMonth(year, ordinalMonth);
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDayCivil);
        }
        else if (day < 1 || day > maxDayCivil)
        {
            return null;
        }

        return IslamicCivilDateToIso(year, ordinalMonth, day);
    }

    /// <summary>
    /// Converts an Islamic civil date to an ISO date using the tabular algorithm.
    /// </summary>
    private static IsoDate? IslamicCivilDateToIso(int year, int month, int day)
    {
        // Islamic civil uses Friday epoch
        var jdn = IslamicToJdn(year, month, day, 1948439L);
        return JdnToIso(jdn);
    }

    /// <summary>
    /// Cumulative days at the start of each Islamic month (0-indexed).
    /// M01=0, M02=30, M03=59, M04=89, M05=118, M06=148, M07=177, M08=207, M09=236, M10=266, M11=295, M12=325
    /// </summary>
    private static int IslamicCumulativeMonthDays(int month)
    {
        // Odd months have 30 days, even months have 29 days
        // Cumulative: sum of alternating 30, 29, 30, 29, ...
        var m = month - 1;
        return m * 30 - m / 2; // = 30*m - floor(m/2) for m months before
    }

    private static long IslamicToJdn(int year, int month, int day, long epoch)
    {
        var monthDays = (long) IslamicCumulativeMonthDays(month);
        var yearDays = (long) (year - 1) * 354L + (long) System.Math.Floor((3.0 + 11.0 * year) / 30.0);
        return monthDays + yearDays + day + epoch;
    }

    private static IsoDate? JdnToIso(long jdn)
    {
        // The standard Fliegel–Van Flandern formula assumes positive intermediate values, so
        // (a + 32044) and (4a + 3) need to stay non-negative. For very negative JDNs (deep
        // proleptic Hebrew/Persian/Islamic dates), shift forward by an integer number of
        // 400-year Gregorian cycles, run the formula, then subtract those cycles' years.
        const long Cycle = 146097L; // days per 400-year Gregorian cycle
        long shift = 0L;
        if (jdn + 32044L < 0L)
        {
            shift = (-(jdn + 32044L) + Cycle - 1L) / Cycle;
        }
        long jdnAdj = jdn + shift * Cycle;

        var a = jdnAdj + 32044L;
        var b = (4 * a + 3) / 146097;
        var c = a - 146097 * b / 4;
        var d = (4 * c + 3) / 1461;
        var e = c - 1461 * d / 4;
        var m = (5 * e + 2) / 153;

        var isoDay = (int) (e - (153 * m + 2) / 5 + 1);
        var isoMonth = (int) (m + 3 - 12 * (m / 10));
        var isoYear = (int) (100 * b + d - 4800 + m / 10) - (int) (shift * 400L);

        return new IsoDate(isoYear, isoMonth, isoDay);
    }

    #endregion

    #region Islamic Civil/Tbla Calendar

    /// <summary>
    /// Converts an Islamic tabular calendar date to an ISO date.
    /// islamic-civil uses Friday epoch (JDN 1948440), islamic-tbla uses Thursday epoch (JDN 1948439).
    /// </summary>
    private static IsoDate? IslamicCivilTabularDateToIso(int year, string? monthCode, int month, int day, string overflow, long epoch)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                ordinalMonth = displayMonth;
            }

            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        // Constrain month
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = System.Math.Clamp(ordinalMonth, 1, 12);
        }
        else if (ordinalMonth < 1 || ordinalMonth > 12)
        {
            return null;
        }

        // Constrain day
        var maxDay = IslamicCivilDaysInMonth(year, ordinalMonth);
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = System.Math.Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        var jdn = IslamicToJdn(year, ordinalMonth, day, epoch);
        return JdnToIso(jdn);
    }

    /// <summary>
    /// CalendarDateAdd for Islamic calendars (islamic-civil/islamic-tbla/islamic-umalqura).
    /// </summary>
    private static IsoDate IslamicTabularCalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow)
    {
        var calDate = IsoToCalendarDate(calendar, in isoDate);
        var newYear = calDate.Year + years;
        var newMonth = calDate.Month;

        // Add months
        if (months != 0)
        {
            newMonth += months;
            while (newMonth > 12)
            {
                newMonth -= 12;
                newYear++;
            }

            while (newMonth < 1)
            {
                newYear--;
                newMonth += 12;
            }
        }

        // Constrain day - use calendar-specific days in month
        int maxDay;
        if (calendar is "islamic-umalqura")
        {
            var cal = UmAlQuraCal;
            try
            {
                var minYear = cal.GetYear(cal.MinSupportedDateTime);
                var maxYear = cal.GetYear(cal.MaxSupportedDateTime);
                if (newYear >= minYear && newYear <= maxYear)
                {
                    maxDay = cal.GetDaysInMonth(newYear, newMonth);
                }
                else
                {
                    maxDay = IslamicCivilDaysInMonth(newYear, newMonth);
                }
            }
            catch
            {
                maxDay = IslamicCivilDaysInMonth(newYear, newMonth);
            }
        }
        else
        {
            maxDay = IslamicCivilDaysInMonth(newYear, newMonth);
        }

        var newDay = calDate.Day;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        // Convert back to ISO using the appropriate calendar
        var result = CalendarDateToIso(calendar, newYear, null, newMonth, newDay, overflow);

        // Same no-progress hazard as the Indian arm above: the input date is not an answer to
        // "add a year to this", and a step that stands still is one CalendarDateUntil never gets past.
        return result ?? throw new CalendarRangeException(calendar);
    }

    /// <summary>
    /// Finds a reference year for Islamic tabular calendars.
    /// </summary>
    private static int FindIslamicTabularReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day)
    {
        long epoch = calendar is "islamic-tbla" ? 1948438L : 1948439L;
        var monthNum = MonthCodeToMonthNumber(monthCode);

        // Approximate Islamic year from ISO year: Islamic year ≈ (ISO - 622) * 33/32
        var approxYear = (int) ((isoReferenceYear - 622.0) * 33.0 / 32.0);

        // Pass 1: find the LATEST calendar year where the day is valid (un-constrained) AND
        // ISO date ≤ end-of-refYear. Required for cases like Islamic M12 D30 where M12 only has
        // 30 days in leap years (refYear=1971 expected). Compares full ISO date so that ties on
        // year are broken by month/day.
        var bestYear = int.MinValue;
        var bestKey = long.MinValue;
        var upperBound = (long) isoReferenceYear * 10000 + 1231;
        for (var y = approxYear - 5; y <= approxYear + 5; y++)
        {
            var maxDay = IslamicCivilDaysInMonth(y, monthNum);
            if (day > maxDay)
            {
                continue;
            }

            var jdn = IslamicToJdn(y, monthNum, day, epoch);
            var isoDate = JdnToIso(jdn);
            if (!isoDate.HasValue) continue;
            var key = (long) isoDate.Value.Year * 10000 + isoDate.Value.Month * 100 + isoDate.Value.Day;
            if (key <= upperBound && key > bestKey)
            {
                bestKey = key;
                bestYear = y;
            }
        }

        if (bestYear != int.MinValue)
        {
            return bestYear;
        }

        // Fallback: pick the year with the most days in this monthCode (so day constrains as
        // little as possible), tiebreak by latest ISO date ≤ refYear.
        var fallbackYear = int.MinValue;
        var fallbackMaxDay = 0;
        var fallbackKey = long.MinValue;
        for (var y = approxYear - 5; y <= approxYear + 5; y++)
        {
            var maxDay = IslamicCivilDaysInMonth(y, monthNum);
            var clampedDay = System.Math.Min(day, maxDay);
            var jdn = IslamicToJdn(y, monthNum, clampedDay, epoch);
            var isoDate = JdnToIso(jdn);
            if (!isoDate.HasValue) continue;
            var key = (long) isoDate.Value.Year * 10000 + isoDate.Value.Month * 100 + isoDate.Value.Day;
            if (key > upperBound) continue;
            if (maxDay > fallbackMaxDay || (maxDay == fallbackMaxDay && key > fallbackKey))
            {
                fallbackMaxDay = maxDay;
                fallbackKey = key;
                fallbackYear = y;
            }
        }

        return fallbackYear != int.MinValue ? fallbackYear : approxYear;
    }

    #endregion
}
