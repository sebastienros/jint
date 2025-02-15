using System.Globalization;
using Jint.Native;
using Jint.Native.Date;

namespace Jint.Runtime;

public class DefaultTimeSystem : ITimeSystem
{
    private static readonly string[] _defaultFormats = ["yyyy-MM-dd", "yyyy-MM", "yyyy"];

    private static readonly string[] _secondaryFormats =
    [
        "yyyy-MM-ddTHH:mm:ss.FFF",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm",

        // Formats used in DatePrototype toString methods
        "ddd MMM dd yyyy HH:mm:ss 'GMT'K",
        "ddd MMM dd yyyy",
        "HH:mm:ss 'GMT'K",

        // standard formats
        "yyyy-M-dTH:m:s.FFFK",
        "yyyy/M/dTH:m:s.FFFK",
        "yyyy-M-dTH:m:sK",
        "yyyy/M/dTH:m:sK",
        "yyyy-M-dTH:mK",
        "yyyy/M/dTH:mK",
        "yyyy-M-d H:m:s.FFFK",
        "yyyy/M/d H:m:s.FFFK",
        "yyyy-M-d H:m:sK",
        "yyyy/M/d H:m:sK",
        "yyyy-M-d H:mK",
        "yyyy/M/d H:mK",
        "yyyy-M-dK",
        "yyyy/M/dK",
        "yyyy-MK",
        "yyyy/MK",
        "yyyyK",
        "THH:mm:ss.FFFK",
        "THH:mm:ssK",
        "THH:mmK",
        "THHK"
    ];

    private readonly CultureInfo _parsingCulture;

    public DefaultTimeSystem(TimeZoneInfo timeZoneInfo, CultureInfo parsingCulture)
    {
        _parsingCulture = parsingCulture;
        DefaultTimeZone = timeZoneInfo;
    }

    public virtual DateTimeOffset GetUtcNow()
    {
        return DateTimeOffset.UtcNow;
    }

    public TimeZoneInfo DefaultTimeZone { get; }

    public virtual bool TryParse(string date, out long epochMilliseconds)
    {
        epochMilliseconds = long.MinValue;

        if (string.IsNullOrEmpty(date))
        {
            return false;
        }

        // special check for large years that always require + or - in front and have 6 digit year
        if ((date[0] == '+'|| date[0] == '-') && date.IndexOf('-', 1) == 7)
        {
            return TryParseLargeYear(date, out epochMilliseconds);
        }

        var startParen = date.IndexOf('(');
        if (startParen != -1)
        {
            // informative text
            date = date.Substring(0, startParen);
        }

        date = date.Trim();

        if (!DateTime.TryParseExact(date, _defaultFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
        {
            if (!DateTime.TryParseExact(date, _secondaryFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
            {
                if (!DateTime.TryParse(date, _parsingCulture, DateTimeStyles.AdjustToUniversal, out result))
                {
                    if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                    {
                        // fall back to trying with MimeKit
                        if (DateUtils.TryParse(date, out var mimeKitResult))
                        {
                            var dateAsUtc = mimeKitResult.ToUniversalTime();
#pragma warning disable MA0132
                            epochMilliseconds = (long) Math.Floor((dateAsUtc - DateConstructor.Epoch).TotalMilliseconds);
#pragma warning restore MA0132
                            return true;
                        }

                        return false;
                    }
                }
            }
        }

        var convertToUtcAfter = result.Kind == DateTimeKind.Unspecified;

        var dateAsUtc1 = result.Kind == DateTimeKind.Local
            ? result.ToUniversalTime()
            : DateTime.SpecifyKind(result, DateTimeKind.Utc);

        DatePresentation datePresentation = (long) (dateAsUtc1 - DateConstructor.Epoch).TotalMilliseconds;

        if (convertToUtcAfter)
        {
            var offset = GetUtcOffset(datePresentation.Value).TotalMilliseconds;
            datePresentation -= offset;
        }

        epochMilliseconds = datePresentation.Value;
        return true;
    }

    /// <summary>
    /// Supports parsing of large (> 10 000) and negative years, should not be needed that often...
    /// </summary>
    private static bool TryParseLargeYear(string date, out long epochMilliseconds)
    {
        epochMilliseconds = long.MinValue;

        var yearString = date.Substring(0, 7);
        if (!int.TryParse(yearString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (year == 0 && date[0] == '-')
        {
            // cannot be negative zero ever
            return false;
        }

        // create replacement string
#pragma warning disable CA1845
        var dateToParse = "2000" + date.Substring(7);
#pragma warning restore CA1845
        if (!DateTime.TryParse(dateToParse, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        var dateTime = parsed.ToUniversalTime();
        var datePresentation = DatePrototype.MakeDate(
            DatePrototype.MakeDay(year, dateTime.Month - 1, dateTime.Day),
            DatePrototype.MakeTime(dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond)
        );

        epochMilliseconds = datePresentation.Value;
        return true;
    }

    public virtual TimeSpan GetUtcOffset(long epochMilliseconds)
    {
        // we have limited capabilities without addon like NodaTime
        if (epochMilliseconds is < -62135596800000L or > 253402300799999L)
        {
            return this.DefaultTimeZone.BaseUtcOffset;
        }

        return this.DefaultTimeZone.GetUtcOffset(DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds));
    }
}
