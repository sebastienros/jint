#nullable enable

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
    [Fact]
    public void OverridingOneCldrDatumLeavesTheOtherTwentyTwoMembersInherited()
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void AnEngineThatConfiguresNothingStillReadsTheSharedSingletons()
    {
        var options = new Options();

        options.Intl.CldrProvider.Should().BeSameAs(DefaultCldrProvider.Instance);
        options.Temporal.TimeZoneProvider.Should().BeSameAs(DefaultTimeZoneProvider.Instance);
        options.Temporal.CalendarProvider.Should().BeSameAs(DefaultCalendarProvider.Instance);
    }
}

/// <summary>One currency's display name; the other twenty-two members are inherited.</summary>
file sealed class OneCurrencyName : DefaultCldrProvider
{
    public override string? GetCurrencyDisplayName(string locale, string code)
        => string.Equals(code, "EUR", StringComparison.Ordinal) ? "Space Credits" : base.GetCurrencyDisplayName(locale, code);
}

/// <summary>One list-pattern set; the other twenty-two members are inherited.</summary>
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
/// Present only to be compiled: a host reaching a member the engine never consults still has to be able
/// to override it, and the compiler is the only thing that checks that all twenty-three are virtual.
/// </summary>
file sealed class EveryCldrMemberOverridden : DefaultCldrProvider
{
    public override ListPatterns? GetListPatterns(string locale, string type, string style) => base.GetListPatterns(locale, type, style);
    public override RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style) => base.GetRelativeTimePatterns(locale, unit, style);
    public override string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style) => base.GetRelativeTimeSpecialPhrase(locale, unit, value, past, style);
    public override string? GetNumberingSystemDigits(string numberingSystem) => base.GetNumberingSystemDigits(numberingSystem);
    public override string? GetDefaultNumberingSystem(string locale) => base.GetDefaultNumberingSystem(locale);
    public override CompactPatterns? GetCompactPatterns(string locale, string style) => base.GetCompactPatterns(locale, style);
    public override CurrencyData? GetCurrencyData(string locale, string currencyCode) => base.GetCurrencyData(locale, currencyCode);
    public override UnitPatterns? GetUnitPatterns(string locale, string unit, string style) => base.GetUnitPatterns(locale, unit, style);
    public override DateTimePatterns? GetDateTimePatterns(string locale, string? dateStyle, string? timeStyle) => base.GetDateTimePatterns(locale, dateStyle, timeStyle);
    public override string[]? GetMonthNames(string locale, string style, string? calendar) => base.GetMonthNames(locale, style, calendar);
    public override string[]? GetWeekdayNames(string locale, string style) => base.GetWeekdayNames(locale, style);
    public override string[]? GetDayPeriods(string locale, string style, string? calendar) => base.GetDayPeriods(locale, style, calendar);
    public override string[]? GetEraNames(string locale, string style, string? calendar) => base.GetEraNames(locale, style, calendar);
    public override string? GetCurrencyDisplayName(string locale, string code) => base.GetCurrencyDisplayName(locale, code);
    public override string? GetLikelySubtags(string locale) => base.GetLikelySubtags(locale);
    public override WeekInfo? GetWeekInfo(string locale) => base.GetWeekInfo(locale);
    public override string SelectPluralCategory(string locale, double value, string type) => base.SelectPluralCategory(locale, value, type);
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
