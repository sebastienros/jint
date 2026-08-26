using System.Runtime.InteropServices;

namespace Jint.Native.Temporal;

/// <summary>
/// Interface for pluggable non-ISO calendar support in the Temporal API.
/// Override this on <see cref="Options.TemporalOptions.CalendarProvider"/> to plug in
/// richer calendar data (e.g. ICU4N for islamic-umalqura, NodaTime for Persian/Hebrew
/// at extreme dates, or icu-dotnet for Chinese/Dangi astronomical tables) without
/// modifying the Jint core.
/// </summary>
/// <remarks>
/// The provider is consulted only for calendars where <see cref="IsSupported"/> returns
/// true. ISO/Gregorian calendars are handled directly in TemporalHelpers and never
/// reach the provider.
/// <para>
/// The two conversions are also the whole of what calendar arithmetic — <c>add</c>, <c>subtract</c>,
/// <c>until</c>, <c>since</c> — asks for: a year is however many months
/// <see cref="CalendarFields.MonthsInYear"/> reports for it, a month however many days
/// <see cref="CalendarFields.DaysInMonth"/> reports, and a monthCode carried from one year into the next is
/// whatever <see cref="CalendarFieldsToIso"/> places there. A provider that answers those two consistently
/// therefore needs nothing else to make a date in its calendar movable.
/// </para>
/// </remarks>
public interface ICalendarProvider
{
    /// <summary>Returns true if this provider can handle the given calendar identifier.</summary>
    /// <remarks>
    /// Asked on every non-ISO calendar operation, so keep it cheap. <see cref="DefaultCalendarProvider"/>
    /// answers it from <see cref="GetSupportedCalendars"/>, which is what keeps the two consistent.
    /// </remarks>
    bool IsSupported(string calendar);

    /// <summary>Returns all calendar identifiers this provider supports.</summary>
    /// <remarks>
    /// This list is what makes an identifier the engine does not implement itself a valid one, everywhere a
    /// calendar identifier can appear: a calendar named here is accepted by <c>Temporal.PlainDate.from</c>,
    /// <c>withCalendar</c> and a <c>[u-ca=…]</c> annotation, is reported by
    /// <c>Intl.supportedValuesOf('calendar')</c>, and is a valid <c>calendar</c> option of
    /// <c>Intl.DateTimeFormat</c> — one list, because a calendar needs conversions before it needs names.
    /// It is read on a validation path — return a cached collection rather than building one per call.
    /// </remarks>
    IReadOnlyCollection<string> GetSupportedCalendars();

    /// <summary>
    /// Converts an ISO date (Gregorian year/month/day) to calendar-specific fields.
    /// </summary>
    /// <param name="calendar">The calendar identifier (e.g. "islamic-umalqura").</param>
    /// <param name="isoYear">Proleptic Gregorian year.</param>
    /// <param name="isoMonth">Gregorian month (1-12).</param>
    /// <param name="isoDay">Gregorian day-of-month.</param>
    CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay);

    /// <summary>
    /// Converts calendar-specific fields back to an ISO (Gregorian) date.
    /// Returns null when the input is invalid under <c>overflow == "reject"</c> or
    /// out of the calendar's supported range.
    /// </summary>
    /// <param name="calendar">The calendar identifier.</param>
    /// <param name="year">Calendar year.</param>
    /// <param name="monthCode">Optional month code (e.g. "M01", "M05L"). When non-null, takes precedence over <paramref name="month"/>.</param>
    /// <param name="month">Calendar ordinal month, or 0 if monthCode is provided.</param>
    /// <param name="day">Calendar day-of-month.</param>
    /// <param name="overflow">"constrain" or "reject" per Temporal overflow semantics.</param>
    IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow);
}

/// <summary>
/// Calendar-specific fields produced by <see cref="ICalendarProvider.IsoToCalendarFields"/>.
/// Mirrors the fields exposed by Temporal's PlainDate/PlainYearMonth/etc. accessors
/// for non-ISO calendars.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CalendarFields(
    int Year,
    int Month,
    string MonthCode,
    int Day,
    bool IsLeapMonth,
    int MonthsInYear,
    int DaysInMonth,
    int DaysInYear,
    bool InLeapYear);

/// <summary>
/// ISO (Gregorian) date components returned by <see cref="ICalendarProvider.CalendarFieldsToIso"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct IsoDateFields(int Year, int Month, int Day);
