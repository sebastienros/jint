using System.Numerics;

namespace Jint.Native.Temporal;

/// <summary>
/// Interface for pluggable time zone support in Temporal API.
/// This allows users to provide custom time zone implementations (e.g., using TimeZoneConverter or NodaTime).
/// </summary>
/// <remarks>
/// https://tc39.es/proposal-temporal/#sec-temporal-timezone-objects
/// </remarks>
public interface ITimeZoneProvider
{
    /// <summary>
    /// Gets the UTC offset in nanoseconds for a specific instant in a time zone.
    /// </summary>
    /// <param name="timeZoneId">The IANA time zone identifier (e.g., "America/New_York").</param>
    /// <param name="epochNanoseconds">The instant as nanoseconds since Unix epoch.</param>
    /// <returns>The UTC offset in nanoseconds.</returns>
    long GetOffsetNanosecondsFor(string timeZoneId, BigInteger epochNanoseconds);

    /// <summary>
    /// Gets possible instant(s) for an ambiguous local date/time in a time zone.
    /// Returns multiple values during DST "fall back" (ambiguous time),
    /// or empty during DST "spring forward" (skipped time).
    /// </summary>
    /// <param name="timeZoneId">The IANA time zone identifier.</param>
    /// <param name="year">Local year.</param>
    /// <param name="month">Local month (1-12).</param>
    /// <param name="day">Local day.</param>
    /// <param name="hour">Local hour (0-23).</param>
    /// <param name="minute">Local minute (0-59).</param>
    /// <param name="second">Local second (0-59).</param>
    /// <param name="millisecond">Local millisecond.</param>
    /// <param name="microsecond">Local microsecond.</param>
    /// <param name="nanosecond">Local nanosecond.</param>
    /// <returns>Array of possible epoch nanosecond values.</returns>
    BigInteger[] GetPossibleInstantsFor(
        string timeZoneId,
        int year, int month, int day,
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond);

    /// <summary>
    /// Gets the epoch nanoseconds of the next DST transition after the given instant.
    /// </summary>
    /// <param name="timeZoneId">The IANA time zone identifier.</param>
    /// <param name="epochNanoseconds">The starting instant.</param>
    /// <returns>The epoch nanoseconds of the next transition, or null if none.</returns>
    BigInteger? GetNextTransition(string timeZoneId, BigInteger epochNanoseconds);

    /// <summary>
    /// Gets the epoch nanoseconds of the previous DST transition before the given instant.
    /// </summary>
    /// <param name="timeZoneId">The IANA time zone identifier.</param>
    /// <param name="epochNanoseconds">The starting instant.</param>
    /// <returns>The epoch nanoseconds of the previous transition, or null if none.</returns>
    BigInteger? GetPreviousTransition(string timeZoneId, BigInteger epochNanoseconds);

    /// <summary>
    /// Validates whether a time zone identifier is recognized.
    /// </summary>
    /// <param name="timeZoneId">The time zone identifier to validate.</param>
    /// <returns>True if the identifier is valid.</returns>
    bool IsValidTimeZone(string timeZoneId);

    /// <summary>
    /// Canonicalizes a time zone identifier to its IANA canonical form.
    /// </summary>
    /// <param name="timeZoneId">The time zone identifier to canonicalize.</param>
    /// <returns>The canonical IANA identifier, or null if invalid.</returns>
    string? CanonicalizeTimeZone(string timeZoneId);

    /// <summary>
    /// Gets all available time zone identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetAvailableTimeZones();

    /// <summary>
    /// Gets the system's default time zone identifier.
    /// </summary>
    string GetDefaultTimeZone();
}
