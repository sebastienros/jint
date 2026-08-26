#nullable enable

using System.Globalization;
using System.Numerics;
using Jint.Native.Intl;
using Jint.Native.Temporal;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The three shipped locale-data providers are extension points, not fixed implementations: a host that
/// disagrees with one datum derives from the default and overrides the one member that answers for it.
/// Every test here overrides exactly one member and delegates nothing — what the rest of the interface
/// answers has to come from the inherited implementation, or the extension point is not worth having.
/// </summary>
public class HostLocaleProviderTests
{
    [Test]
    public void OverridingOneCldrDatumLeavesTheOtherEighteenMembersInherited()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new OneCurrencyName());

        // the one overridden member
        engine.Evaluate("new Intl.DisplayNames('en', { type: 'currency' }).of('EUR')")
            .AsString().Should().Be("Space Credits");

        // …and every other member still answers, without the subclass forwarding anything
        engine.Evaluate("new Intl.DisplayNames('en', { type: 'currency' }).of('USD')")
            .AsString().Should().Be("US Dollar");
        engine.Evaluate("new Intl.ListFormat('en').format(['a', 'b'])")
            .AsString().Should().Be("a and b");
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'unit', unit: 'meter' }).format(5)")
            .AsString().Should().Be("5 m");
        engine.Evaluate("new Intl.RelativeTimeFormat('en').format(3, 'day')")
            .AsString().Should().Be("in 3 days");
        engine.Evaluate("Intl.supportedValuesOf('unit').length").AsNumber().Should().BeGreaterThan(0);
    }

    [Test]
    public void OverridingOneCurrencyReachesNumberFormat()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new OneCurrency());

        // the three displays the provider answers for
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'currency', currency: 'XCD' }).format(12.5)")
            .AsString().Should().Be("EC$12.50");
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'currency', currency: 'XCD', currencyDisplay: 'narrowSymbol' }).format(12.5)")
            .AsString().Should().Be("$12.50");
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'currency', currency: 'XCD', currencyDisplay: 'name' }).format(12.5)")
            .AsString().Should().StartWith("East Caribbean dollars");

        // the code display is the currency code by specification, whatever the provider says
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'currency', currency: 'XCD', currencyDisplay: 'code' }).format(12.5)")
            .AsString().Should().Be("XCD12.50");

        // …and every currency the subclass does not claim still reads the inherited data
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'currency', currency: 'USD' }).format(12.5)")
            .AsString().Should().Be("$12.50");
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'currency', currency: 'CAD', currencyDisplay: 'narrowSymbol' }).format(12.5)")
            .AsString().Should().Be("$12.50");
    }

    [Test]
    public void OverridingOneWeekInfoReachesIntlLocale()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new SundayIsTheWeekend());

        engine.Evaluate("new Intl.Locale('en-US').getWeekInfo().firstDay").AsNumber().Should().Be(3);
        engine.Evaluate("JSON.stringify(new Intl.Locale('en-US').getWeekInfo().weekend)").AsString().Should().Be("[7]");

        // an explicit -u-fw- still wins over the provider, per the specification
        engine.Evaluate("new Intl.Locale('en-US-u-fw-mon').getWeekInfo().firstDay").AsNumber().Should().Be(1);
    }

    [Test]
    public void OverridingOneListPatternLeavesTheRestOfTheCldrDataInherited()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new GermanLists());

        engine.Evaluate("new Intl.ListFormat('en').format(['a', 'b'])").AsString().Should().Be("a und b");
        engine.Evaluate("new Intl.ListFormat('en').format(['a', 'b', 'c'])").AsString().Should().Be("a, b und c");

        // the currency names, unit patterns and supported-value lists are the base class's
        engine.Evaluate("new Intl.DisplayNames('en', { type: 'currency' }).of('EUR')")
            .AsString().Should().Be("Euro");
        engine.Evaluate("new Intl.NumberFormat('en', { style: 'unit', unit: 'meter' }).format(5)")
            .AsString().Should().Be("5 m");
    }

    [Test]
    public void OverridingOneTimeZoneDatumLeavesTheOtherEightMembersInherited()
    {
        var engine = new Engine(options => options.Temporal.TimeZoneProvider = new FixedDefaultZone());

        // the one overridden member
        engine.Evaluate("Temporal.Now.timeZoneId()").AsString().Should().Be("Pacific/Auckland");

        // offsets, validity, canonicalization and the available list all come from the base class
        engine.Evaluate("Temporal.Instant.from('2024-01-15T12:00:00Z').toZonedDateTimeISO('America/New_York').offset")
            .AsString().Should().Be("-05:00");
        engine.Evaluate("Temporal.Instant.from('2024-07-15T12:00:00Z').toZonedDateTimeISO('America/New_York').offset")
            .AsString().Should().Be("-04:00");
        engine.Evaluate("Temporal.ZonedDateTime.from('2024-01-15T12:00:00[UTC]').timeZoneId")
            .AsString().Should().Be("UTC");
    }

    [Test]
    public void OverridingOneCalendarLeavesTheOtherTenInherited()
    {
        var engine = new Engine(options => options.Temporal.CalendarProvider = new ShiftedHebrewEra());

        var stock = new Engine();
        var stockHebrewYear = stock.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('hebrew').year").AsNumber();

        // the one overridden member, on the one calendar it claims
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('hebrew').year")
            .AsNumber().Should().Be(stockHebrewYear + 1000);

        // every other calendar still reads the inherited BCL-backed arithmetic
        foreach (var calendar in new[] { "islamic-civil", "coptic", "persian", "indian" })
        {
            var script = $"Temporal.PlainDate.from('2024-03-05').withCalendar('{calendar}').toString()";
            engine.Evaluate(script).AsString().Should().Be(stock.Evaluate(script).AsString());
        }

        // …and so does the other direction, which the subclass never mentions
        engine.Evaluate("Temporal.PlainDate.from({ year: 1445, monthCode: 'M08', day: 25, calendar: 'islamic-civil' }).toString()")
            .AsString().Should().Be(
                stock.Evaluate("Temporal.PlainDate.from({ year: 1445, monthCode: 'M08', day: 25, calendar: 'islamic-civil' }).toString()").AsString());
    }

    [Test]
    public void OverridingOneNumberingSystemsDigitsMakesItUsable()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new AlphabetDigits());

        // a numbering system the engine has never heard of: the provider is the only thing that can
        // say it exists, and the only thing that can transliterate it
        engine.Evaluate("new Intl.NumberFormat('en', { numberingSystem: 'lettera' }).format(1024)")
            .AsString().Should().Be("B,ACE");
        engine.Evaluate("new Intl.NumberFormat('en', { numberingSystem: 'lettera' }).resolvedOptions().numberingSystem")
            .AsString().Should().Be("lettera");
        engine.Evaluate("new Intl.RelativeTimeFormat('en', { numberingSystem: 'lettera' }).format(3, 'day')")
            .AsString().Should().Be("in D days");

        // …and every system the subclass does not claim still reads the inherited digit table
        engine.Evaluate("new Intl.NumberFormat('en', { numberingSystem: 'arab' }).format(123)")
            .AsString().Should().Be("١٢٣");
        engine.Evaluate("new Intl.NumberFormat('en').format(123)")
            .AsString().Should().Be("123");
    }

    [Test]
    public void OverridingOneMonthNameReachesDateTimeFormat()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new RevolutionaryMonths());

        engine.Evaluate("new Intl.DateTimeFormat('en', { month: 'long', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("Nivose");
        engine.Evaluate("new Intl.DateTimeFormat('en', { year: 'numeric', month: 'long', day: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2024, 6, 4))")
            .AsString().Should().Be("Messidor 4, 2024");
        engine.Evaluate("new Intl.DateTimeFormat('en', { month: 'long', timeZone: 'UTC' }).formatToParts(Date.UTC(2024, 0, 15))[0].value")
            .AsString().Should().Be("Nivose");

        // the style the subclass does not answer for, and every other member, are the inherited data
        engine.Evaluate("new Intl.DateTimeFormat('en', { month: 'short', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("Jan");
        engine.Evaluate("new Intl.DateTimeFormat('en', { weekday: 'long', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("Monday");
    }

    [Test]
    public void OverridingOneWeekdayNameReachesDateTimeFormat()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new PlanetaryWeekdays());

        engine.Evaluate("new Intl.DateTimeFormat('en', { weekday: 'long', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("Moonday");
        engine.Evaluate("new Intl.DateTimeFormat('en', { weekday: 'long', timeZone: 'UTC' }).formatToParts(Date.UTC(2024, 0, 14))[0].value")
            .AsString().Should().Be("Sunday");

        // the abbreviated names, and the months, are the inherited data
        engine.Evaluate("new Intl.DateTimeFormat('en', { weekday: 'short', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("Mon");
        engine.Evaluate("new Intl.DateTimeFormat('en', { month: 'long', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("January");
    }

    [Test]
    public void OverridingOneDayPeriodReachesDateTimeFormat()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new NauticalDayPeriods());

        engine.Evaluate("new Intl.DateTimeFormat('en', { hour: 'numeric', hour12: true, timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15, 15, 30))")
            .AsString().Should().Be("3 EVE");
        engine.Evaluate("new Intl.DateTimeFormat('en', { hour: 'numeric', hour12: true, timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15, 9, 30))")
            .AsString().Should().Be("9 MORN");
        engine.Evaluate("new Intl.DateTimeFormat('en', { hour: 'numeric', hour12: true, timeZone: 'UTC' }).formatToParts(Date.UTC(2024, 0, 15, 15, 30))[2].value")
            .AsString().Should().Be("EVE");

        // the months and weekdays are still the inherited data
        engine.Evaluate("new Intl.DateTimeFormat('en', { month: 'long', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15))")
            .AsString().Should().Be("January");
    }

    [Test]
    public void AddingACalendarTheEngineHasNeverSeenIsThreeOverrides()
    {
        var engine = new Engine(options => options.Temporal.CalendarProvider = new WithMayan());

        // the identifier is valid everywhere a calendar identifier can appear
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').year").AsNumber().Should().Be(5138);
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').toString()")
            .AsString().Should().Be("2024-03-05[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDate.from({ year: 5138, monthCode: 'M03', day: 5, calendar: 'mayan' }).toString()")
            .AsString().Should().Be("2024-03-05[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05[u-ca=mayan]').year").AsNumber().Should().Be(5138);
        engine.Evaluate("Temporal.PlainDateTime.from('2024-03-05T12:00[u-ca=mayan]').year").AsNumber().Should().Be(5138);
        engine.Evaluate("Temporal.ZonedDateTime.from('2024-03-05T12:00[UTC][u-ca=mayan]').year").AsNumber().Should().Be(5138);

        // …and every field accessor reads it through the two conversions
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').monthCode").AsString().Should().Be("M03");
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').daysInMonth").AsNumber().Should().Be(31);
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').inLeapYear").AsBoolean().Should().BeTrue();
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').with({ day: 10 }).toString()")
            .AsString().Should().Be("2024-03-10[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainYearMonth.from({ year: 5138, monthCode: 'M03', calendar: 'mayan' }).toString()")
            .AsString().Should().Be("2024-03-01[u-ca=mayan]");
        engine.Evaluate("Temporal.PlainMonthDay.from({ monthCode: 'M03', day: 5, calendar: 'mayan' }).toString()")
            .AsString().Should().Be("1972-03-05[u-ca=mayan]");

        // every calendar the subclass does not claim still reads the inherited BCL-backed arithmetic
        var stock = new Engine();
        foreach (var calendar in new[] { "islamic-civil", "coptic", "persian", "indian", "hebrew" })
        {
            var script = $"Temporal.PlainDate.from('2024-03-05').withCalendar('{calendar}').toString()";
            engine.Evaluate(script).AsString().Should().Be(stock.Evaluate(script).AsString());
        }
    }

    /// <summary>
    /// Calendar arithmetic is implemented per calendar inside the engine and is not routed through
    /// <see cref="ICalendarProvider"/>, so a calendar a host added has none. What this pins is that the
    /// refusal is a <c>RangeError</c> a script can catch, rather than a <see cref="NotSupportedException"/>
    /// escaping <c>Engine.Evaluate</c> as it did before.
    /// </summary>
    [Test]
    public void AHostCalendarHasNoArithmeticAndSaysSoInJavaScript()
    {
        var engine = new Engine(options => options.Temporal.CalendarProvider = new WithMayan());

        foreach (var script in new[]
        {
            "Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').add({ days: 1 })",
            "Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').subtract({ months: 1 })",
            "Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').until(Temporal.PlainDate.from('2024-05-05').withCalendar('mayan'))",
        })
        {
            engine.Evaluate($"(function () {{ try {{ {script}; return 'no error'; }} catch (e) {{ return e.constructor.name + ': ' + e.message; }} }})()")
                .AsString().Should().Be("RangeError: Calendar arithmetic is not implemented for 'mayan'");
        }

        // the calendars the engine implements itself are unaffected
        engine.Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('hebrew').add({ months: 1 }).toString()")
            .AsString().Should().Be(new Engine().Evaluate("Temporal.PlainDate.from('2024-03-05').withCalendar('hebrew').add({ months: 1 }).toString()").AsString());
    }

    [Test]
    public void AnEngineThatConfiguresNothingStillReadsTheSharedSingletons()
    {
        var options = new Options();

        options.Intl.CldrProvider.Should().BeSameAs(DefaultCldrProvider.Instance);
        options.Temporal.TimeZoneProvider.Should().BeSameAs(DefaultTimeZoneProvider.Instance);
        options.Temporal.CalendarProvider.Should().BeSameAs(DefaultCalendarProvider.Instance);
    }
}

/// <summary>One currency's display name; the other eighteen members are inherited.</summary>
file sealed class OneCurrencyName : DefaultCldrProvider
{
    public override string? GetCurrencyDisplayName(string locale, string code)
        => string.Equals(code, "EUR", StringComparison.Ordinal) ? "Space Credits" : base.GetCurrencyDisplayName(locale, code);
}

/// <summary>One currency's symbols and name; every other currency is the inherited data.</summary>
file sealed class OneCurrency : DefaultCldrProvider
{
    public override CurrencyData? GetCurrencyData(string locale, string currencyCode)
        => string.Equals(currencyCode, "XCD", StringComparison.Ordinal)
            ? new CurrencyData { Symbol = "EC$", NarrowSymbol = "$", DisplayName = "East Caribbean dollars" }
            : base.GetCurrencyData(locale, currencyCode);
}

/// <summary>One locale's week layout; every other member is inherited.</summary>
file sealed class SundayIsTheWeekend : DefaultCldrProvider
{
    public override WeekInfo? GetWeekInfo(string locale)
        => new WeekInfo { FirstDay = DayOfWeek.Wednesday, Weekend = [DayOfWeek.Sunday] };
}

/// <summary>One numbering system the engine has never heard of; every other one is inherited.</summary>
file sealed class AlphabetDigits : DefaultCldrProvider
{
    public override string? GetNumberingSystemDigits(string numberingSystem)
        => string.Equals(numberingSystem, "lettera", StringComparison.OrdinalIgnoreCase)
            ? "ABCDEFGHIJ"
            : base.GetNumberingSystemDigits(numberingSystem);
}

/// <summary>One month-name style; every other name the formatter needs is inherited.</summary>
file sealed class RevolutionaryMonths : DefaultCldrProvider
{
    public override string[]? GetMonthNames(string locale, string style, string? calendar)
        => string.Equals(style, "long", StringComparison.Ordinal)
            ? ["Nivose", "Pluviose", "Ventose", "Germinal", "Floreal", "Prairial", "Messidor", "Thermidor", "Fructidor", "Vendemiaire", "Brumaire", "Frimaire"]
            : base.GetMonthNames(locale, style, calendar);
}

/// <summary>One weekday-name style; every other name the formatter needs is inherited.</summary>
file sealed class PlanetaryWeekdays : DefaultCldrProvider
{
    public override string[]? GetWeekdayNames(string locale, string style)
        => string.Equals(style, "long", StringComparison.Ordinal)
            ? ["Sunday", "Moonday", "Marsday", "Mercuryday", "Jupiterday", "Venusday", "Saturnday"]
            : base.GetWeekdayNames(locale, style);
}

/// <summary>One pair of day periods; every other name the formatter needs is inherited.</summary>
file sealed class NauticalDayPeriods : DefaultCldrProvider
{
    public override string[]? GetDayPeriods(string locale, string style, string? calendar) => ["MORN", "EVE"];
}

/// <summary>One list-pattern set; the other eighteen members are inherited.</summary>
file sealed class GermanLists : DefaultCldrProvider
{
    public override ListPatterns? GetListPatterns(string locale, string type, string style)
        => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} und {1}", Two = "{0} und {1}" };
}

/// <summary>One time zone datum — the system default; the other eight members are inherited.</summary>
file sealed class FixedDefaultZone : DefaultTimeZoneProvider
{
    public override string GetDefaultTimeZone() => "Pacific/Auckland";
}

/// <summary>One calendar's ISO-to-fields conversion; the other three members are inherited.</summary>
file sealed class ShiftedHebrewEra : DefaultCalendarProvider
{
    public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay)
    {
        var fields = base.IsoToCalendarFields(calendar, isoYear, isoMonth, isoDay);
        return string.Equals(calendar, "hebrew", StringComparison.Ordinal)
            ? fields with { Year = fields.Year + 1000 }
            : fields;
    }
}

/// <summary>
/// Present only to be compiled: the compiler is the only thing that checks that all nineteen are virtual.
/// A calendar Jint has never heard of, defined as ISO shifted by the Mayan Long Count epoch so the
/// arithmetic is checkable by eye. The two conversions are the whole subclass: nobody but the host can
/// convert a calendar the engine does not know, and everything else about it is inherited.
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
/// Present only to be compiled: a host reaching a member the engine never consults still has to be able
/// to override it, and the compiler is the only thing that checks that all twenty-one are virtual.
/// </summary>
file sealed class EveryCldrMemberOverridden : DefaultCldrProvider
{
    public override ListPatterns? GetListPatterns(string locale, string type, string style) => base.GetListPatterns(locale, type, style);
    public override RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style) => base.GetRelativeTimePatterns(locale, unit, style);
    public override string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style) => base.GetRelativeTimeSpecialPhrase(locale, unit, value, past, style);
    public override string? GetNumberingSystemDigits(string numberingSystem) => base.GetNumberingSystemDigits(numberingSystem);
    public override string? GetDefaultNumberingSystem(string locale) => base.GetDefaultNumberingSystem(locale);
    public override CurrencyData? GetCurrencyData(string locale, string currencyCode) => base.GetCurrencyData(locale, currencyCode);
    public override UnitPatterns? GetUnitPatterns(string locale, string unit, string style) => base.GetUnitPatterns(locale, unit, style);
    public override string[]? GetMonthNames(string locale, string style, string? calendar) => base.GetMonthNames(locale, style, calendar);
    public override string[]? GetWeekdayNames(string locale, string style) => base.GetWeekdayNames(locale, style);
    public override string[]? GetDayPeriods(string locale, string style, string? calendar) => base.GetDayPeriods(locale, style, calendar);
    public override string[]? GetEraNames(string locale, string style, string? calendar) => base.GetEraNames(locale, style, calendar);
    public override string? GetCurrencyDisplayName(string locale, string code) => base.GetCurrencyDisplayName(locale, code);
    public override WeekInfo? GetWeekInfo(string locale) => base.GetWeekInfo(locale);
    public override IReadOnlyCollection<string> GetSupportedCalendars() => base.GetSupportedCalendars();
    public override IReadOnlyCollection<string> GetSupportedCollations() => base.GetSupportedCollations();
    public override IReadOnlyCollection<string> GetSupportedCurrencies() => base.GetSupportedCurrencies();
    public override IReadOnlyCollection<string> GetSupportedNumberingSystems() => base.GetSupportedNumberingSystems();
    public override IReadOnlyCollection<string> GetSupportedTimeZones() => base.GetSupportedTimeZones();
    public override IReadOnlyCollection<string> GetSupportedUnits() => base.GetSupportedUnits();
}

/// <summary>The same compile-time census for the nine time zone members.</summary>
file sealed class EveryTimeZoneMemberOverridden : DefaultTimeZoneProvider
{
    public override long GetOffsetNanosecondsFor(string timeZoneId, BigInteger epochNanoseconds) => base.GetOffsetNanosecondsFor(timeZoneId, epochNanoseconds);
    public override BigInteger[] GetPossibleInstantsFor(string timeZoneId, int year, int month, int day, int hour, int minute, int second, int millisecond, int microsecond, int nanosecond)
        => base.GetPossibleInstantsFor(timeZoneId, year, month, day, hour, minute, second, millisecond, microsecond, nanosecond);
    public override BigInteger? GetNextTransition(string timeZoneId, BigInteger epochNanoseconds) => base.GetNextTransition(timeZoneId, epochNanoseconds);
    public override BigInteger? GetPreviousTransition(string timeZoneId, BigInteger epochNanoseconds) => base.GetPreviousTransition(timeZoneId, epochNanoseconds);
    public override bool IsValidTimeZone(string timeZoneId) => base.IsValidTimeZone(timeZoneId);
    public override string? CanonicalizeTimeZone(string timeZoneId) => base.CanonicalizeTimeZone(timeZoneId);
    public override IReadOnlyCollection<string> GetAvailableTimeZones() => base.GetAvailableTimeZones();
    public override string GetDefaultTimeZone() => base.GetDefaultTimeZone();
    public override string? GetPrimaryTimeZoneIdentifier(string timeZoneId) => base.GetPrimaryTimeZoneIdentifier(timeZoneId);
}

/// <summary>And for the four calendar members.</summary>
file sealed class EveryCalendarMemberOverridden : DefaultCalendarProvider
{
    public override bool IsSupported(string calendar) => base.IsSupported(calendar);
    public override IReadOnlyCollection<string> GetSupportedCalendars() => base.GetSupportedCalendars();
    public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay) => base.IsoToCalendarFields(calendar, isoYear, isoMonth, isoDay);
    public override IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow) => base.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
}
