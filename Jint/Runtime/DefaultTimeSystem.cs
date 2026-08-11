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

    private const long MillisecondsPerDay = 86_400_000;

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

        // A year DateTime has no room for is taken off the front and re-applied afterwards.
        var yearFieldLength = YearFieldBeyondDateTime(date);
        if (yearFieldLength != 0)
        {
            return TryParseSubstitutingTheYear(date, yearFieldLength, out epochMilliseconds);
        }

        return TryParseThroughDateTime(date, out epochMilliseconds);
    }

    /// <summary>
    /// The length of a leading year field <see cref="DateTime"/> cannot hold, or 0 when the string does
    /// not begin with one. https://tc39.es/ecma262/#sec-date-time-string-format writes the year as four
    /// digits or — per https://tc39.es/ecma262/#sec-expanded-years — a sign and six digits. DateTime
    /// covers years 1 through 9999, so what is out of its reach is every expanded year plus the one
    /// unsigned spelling that names year 0: "0000", which the grammar admits and which means 1 BC.
    ///
    /// The character after the field has to be one the grammar puts there, which is what keeps the
    /// non-standard "0000/01/01" on the loose parser below, where it reads as year 2000 exactly as it
    /// does in V8.
    /// </summary>
    private static int YearFieldBeyondDateTime(string date)
    {
        int length;
        if (date[0] is '+' or '-')
        {
            length = 7;
            if (date.Length < length)
            {
                return 0;
            }

            for (var i = 1; i < length; i++)
            {
                if (!char.IsDigit(date[i]))
                {
                    return 0;
                }
            }
        }
        else if (date.StartsWith("0000", StringComparison.Ordinal))
        {
            length = 4;
        }
        else
        {
            return 0;
        }

        return date.Length == length || date[length] is '-' or 'T' ? length : 0;
    }

    /// <summary>
    /// Parses a date whose year is beyond <see cref="DateTime"/> by handing .NET the same string under a
    /// year it can hold and then moving the answer. The proleptic Gregorian calendar repeats every 400
    /// years, so a substitute congruent to the real year mod 400 has the same leap status, the same month
    /// lengths and the same weekdays: February 29 still parses, the substituted string is accepted on
    /// exactly the terms the original would have been, and the two instants end up a whole number of days
    /// apart. Landing the substitute in 2000-2399 also keeps it four digits wide, which is what every
    /// format string here expects.
    ///
    /// Deferring to the ordinary path is the point rather than a convenience. What the previous
    /// implementation got wrong was the UTC rules: it converted its parse to UTC and then read month, day
    /// and time back out to rebuild the date under the real year, which dropped the date-only-means-UTC
    /// rule and, when the offset crossed midnight, kept the target year beside the shifted month and day
    /// — so "+010000-01-01" came back as the last day of year 10000 rather than the first. Here the
    /// offset and the date-only rule are applied once, by the parser that already implements them, and
    /// only whole days are added afterwards.
    /// </summary>
    private bool TryParseSubstitutingTheYear(string date, int yearFieldLength, out long epochMilliseconds)
    {
        epochMilliseconds = long.MinValue;

        if (!int.TryParse(date.AsSpan(0, yearFieldLength), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (year == 0 && date[0] == '-')
        {
            // https://tc39.es/ecma262/#sec-expanded-years: year 0 is considered positive, so "-000000" is
            // the one expanded year the grammar excludes. It is rejected outright rather than handed on,
            // because the loose parser below would read it as something else.
            return false;
        }

        var substituteYear = 2000 + (year % 400 + 400) % 400;
#pragma warning disable CA1845
        var substituteDate = substituteYear.ToString(CultureInfo.InvariantCulture) + date.Substring(yearFieldLength);
#pragma warning restore CA1845

        if (!TryParseThroughDateTime(substituteDate, out var substituteMilliseconds))
        {
            return false;
        }

        var dayShift = DatePrototype.MakeDay(year, 0, 1) - DatePrototype.MakeDay(substituteYear, 0, 1);
        if (!double.IsFinite(dayShift))
        {
            // MakeDay refuses a year beyond ±1,000,000, which no six-digit field can reach; the guard is
            // here so a future caller cannot turn NaN into a nonsense number of days.
            return false;
        }

        epochMilliseconds = substituteMilliseconds + (long) dayShift * MillisecondsPerDay;
        return true;
    }

    private bool TryParseThroughDateTime(string date, out long epochMilliseconds)
    {
        epochMilliseconds = long.MinValue;

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
                            // Ticks again, and floored the way Math.Floor on the double used to be: the
                            // same lossy conversion reached this branch too, so "Sat, 15 Jun 5627
                            // 12:30:13 PST" — a named zone, which only MimeKit parses — came back a
                            // millisecond early. Floor and truncation differ only below the epoch, and
                            // only for a sub-millisecond remainder MimeKit cannot produce, but keeping
                            // the rounding direction means the fix moves nothing but the precision.
                            var elapsedTicks = mimeKitResult.UtcTicks - DateConstructor.Epoch.Ticks;
                            epochMilliseconds = elapsedTicks / TimeSpan.TicksPerMillisecond;
                            if (elapsedTicks < 0 && epochMilliseconds * TimeSpan.TicksPerMillisecond != elapsedTicks)
                            {
                                epochMilliseconds--;
                            }

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

        // Counted in ticks rather than read off TimeSpan.TotalMilliseconds. That property converts the
        // tick count to a double first, and above 2^53 the conversion is already lossy: the last instant
        // of year 9999 is 253402300799999 ms exactly, but on .NET 8/10 the quotient comes back as
        // 253402300799998.96875 and the truncating cast then dropped the fraction, so
        // Date.parse("9999-12-31T23:59:59.999Z") answered one millisecond short of what toISOString had
        // been given. .NET Framework multiplies by 1e-4 where modern .NET divides by 10000 and happens to
        // round the other way, so the same string parsed to two different numbers depending on the target
        // framework. Integer division truncates toward zero exactly as the cast did, so nothing else about
        // the conversion moves. Same defect as the one #2965 fixed in JsDate.Max, one conversion along.
        DatePresentation datePresentation = (dateAsUtc1.Ticks - DateConstructor.Epoch.Ticks) / TimeSpan.TicksPerMillisecond;

        if (convertToUtcAfter)
        {
            // datePresentation is local time encoded as epoch milliseconds.
            // Two-pass conversion to get correct DST offset near transitions.
            var offset = GetUtcOffset(datePresentation.Value).TotalMilliseconds;
            var estimatedUtc = (datePresentation - offset).Value;
            var refinedOffset = GetUtcOffset(estimatedUtc).TotalMilliseconds;
            datePresentation -= refinedOffset;
        }

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
