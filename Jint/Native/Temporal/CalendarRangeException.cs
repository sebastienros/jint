namespace Jint.Native.Temporal;

/// <summary>
/// Signals that non-ISO calendar arithmetic left the range the calendar's implementation can represent.
/// </summary>
/// <remarks>
/// <para>
/// Jint reckons the eleven non-ISO calendars with <see cref="System.Globalization.Calendar"/> classes and
/// with fixed-epoch arithmetic, and several of those classes cover far less than Temporal's own date
/// range: <c>ChineseLunisolarCalendar</c> spans ISO 1901-02-19 to 2101-01-28, <c>HebrewCalendar</c>
/// 1583-01-01 to 2239-09-29. A step past either end has no date to answer with.
/// </para>
/// <para>
/// What used to be answered there was the calendar's <em>maximum</em> supported date, whichever end had
/// been overrun, which made <c>subtract</c> move forward and made <c>until</c>'s one-month-at-a-time walk
/// stand still forever. <see cref="TemporalHelpers"/> catches this at the two calendar entry points and
/// raises the <c>RangeError</c> that <c>CalendarDateAdd</c> raises for a result it cannot represent.
/// </para>
/// </remarks>
internal sealed class CalendarRangeException : Exception
{
    internal CalendarRangeException(string calendar)
        : base($"Date is outside the range supported by the '{calendar}' calendar")
    {
    }
}
