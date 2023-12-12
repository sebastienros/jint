namespace Jint.Runtime;

/// <summary>
/// Date related operations that can replaced with implementation that can handle also full IANA data as recommended
/// by the JS spec. Jint comes with <see cref="DefaultTimeSystem"/> which is based on built-in data which might be incomplete.
/// </summary>
/// <remarks>
/// This interface intentionally uses long instead of DateTime/DateTimeOffset as DateTime/DateTimeOffset cannot handle
/// neither negative years nor the date range that JS can.
/// </remarks>
public interface ITimeSystem
{
    /// <summary>
    /// Retrieves current UTC time.
    /// </summary>
    /// <returns>Current UTC time.</returns>
    DateTimeOffset GetUtcNow();
    
    /// <summary>
    /// Return the default time zone system is using. Usually <see cref="TimeZoneInfo.Local"/>, but can be altered via
    /// engine configuration, see <see cref="Options.TimeZone"/>.
    /// </summary>
    TimeZoneInfo DefaultTimeZone { get; }

    /// <summary>
    /// Tries to parse given time presentation string as JS date presentation based on epoch.
    /// </summary>
    /// <param name="date">Date/time to parse.</param>
    /// <param name="epochMilliseconds">The milliseconds since the UNIX epoch, can be negative for values before 1970.</param>
    /// <returns>true, if succeeded.</returns>
    bool TryParse(string date, out long epochMilliseconds);

    /// <summary>
    /// Retrieves UTC offset for given date presented as milliseconds since the Unix epoch.
    /// Defaults to using <see cref="TimeZoneInfo.GetUtcOffset(System.DateTimeOffset)"/> using the configured time zone.
    /// </summary>
    /// <param name="epochMilliseconds">Date as milliseconds since the Unix epoch, may be negative (for instants before the epoch).</param>
    /// <seealso cref="TimeZone"/>
    public TimeSpan GetUtcOffset(long epochMilliseconds);
}
