using System.Globalization;
using System.Numerics;

namespace Jint.HtmlParser;

internal readonly record struct HtmlInputDateResult(bool HasDate, double UtcMilliseconds);

// The source owns its year spelling; arbitrarily long years are validated by residues, never big integers.
internal readonly record struct HtmlInputTemporalValue(string Source, int YearLength, int SignificantYearStart,
    int YearModulo400, int Month, int Day, int Week, int Hour, int Minute, double Seconds)
{
    internal int SignificantYearLength => YearLength - SignificantYearStart;
}

/// <summary>HTML §2.3.5 date/time validity, distinct microsyntax parsing and input §4.10.5 conversions.</summary>
internal static class HtmlInputTemporalSyntax
{
    internal static bool IsTemporal(HtmlInputType type) => type is HtmlInputType.Date or HtmlInputType.Month
        or HtmlInputType.Week or HtmlInputType.Time or HtmlInputType.DateTimeLocal;

    internal static bool TryParseValue(HtmlInputType type, string source, out HtmlInputTemporalValue value,
        Action<long>? checkpoint = null, CancellationToken cancellationToken = default)
        => Parse(type, source, strict: true, out value, checkpoint, cancellationToken);

    internal static bool TryParseMicrosyntax(HtmlInputType type, string source, out HtmlInputTemporalValue value,
        Action<long>? checkpoint = null, CancellationToken cancellationToken = default)
        => Parse(type, source, strict: false, out value, checkpoint, cancellationToken);

    private static bool Parse(HtmlInputType type, string source, bool strict, out HtmlInputTemporalValue value,
        Action<long>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        return ParseCore(type, source, strict, out value, ref work);
    }

    private static bool ParseCore(HtmlInputType type, string source, bool strict, out HtmlInputTemporalValue value,
        ref HtmlInputValueWork work)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!IsTemporal(type)) throw new ArgumentOutOfRangeException(nameof(type));
        work.Check();
        value = default;
        var i = 0;
        var yearLength = 0;
        var significantStart = 0;
        var modulo = 0;
        var month = 1;
        var day = 1;
        var week = 1;
        var hour = 0;
        var minute = 0;
        var seconds = 0d;
        if (type != HtmlInputType.Time)
        {
            var nonzero = false;
            while (i < source.Length && HtmlInputNumberSyntax.IsDigit(source[i]))
            {
                work.Step();
                var digit = source[i] - '0';
                if (!nonzero && digit != 0) { nonzero = true; significantStart = i; }
                modulo = (modulo * 10 + digit) % 400;
                i++;
            }
            yearLength = i;
            if (i < 4 || !nonzero || !Consume(source, ref i, '-', ref work)) return false;
            if (type == HtmlInputType.Week)
            {
                if (!Consume(source, ref i, 'W', ref work) || !TwoDigits(source, ref i, ref work, out week)
                    || week < 1 || week > HtmlInputCalendar.WeeksInYear(modulo)) return false;
            }
            else
            {
                if (!TwoDigits(source, ref i, ref work, out month) || month is < 1 or > 12) return false;
                if (type != HtmlInputType.Month)
                {
                    if (!Consume(source, ref i, '-', ref work) || !TwoDigits(source, ref i, ref work, out day)
                        || day < 1 || day > HtmlInputCalendar.DaysInMonth(modulo, month)) return false;
                }
            }
        }
        if (type is HtmlInputType.Time or HtmlInputType.DateTimeLocal)
        {
            if (type == HtmlInputType.DateTimeLocal)
            {
                if (i >= source.Length || source[i] is not ('T' or ' ')) return false;
                work.Step(); i++;
            }
            if (!ParseTime(source, ref i, strict, ref work, out hour, out minute, out seconds)) return false;
        }
        work.Check();
        if (i != source.Length) return false;
        value = new(source, yearLength, significantStart, modulo, month, day, week, hour, minute, seconds);
        return true;
    }

    private static bool ParseTime(string source, ref int i, bool strict, ref HtmlInputValueWork work,
        out int hour, out int minute, out double seconds)
    {
        minute = 0; seconds = 0;
        if (!TwoDigits(source, ref i, ref work, out hour) || hour > 23 || !Consume(source, ref i, ':', ref work)
            || !TwoDigits(source, ref i, ref work, out minute) || minute > 59) return false;
        if (i < source.Length && source[i] == ':')
        {
            work.Step(); i++;
            var secondsStart = i;
            if (!TwoDigits(source, ref i, ref work, out var wholeSeconds) || wholeSeconds > 59) return false;
            seconds = wholeSeconds;
            if (i < source.Length && source[i] == '.')
            {
                work.Step(); i++;
                var fractionalStart = i;
                while (i < source.Length && HtmlInputNumberSyntax.IsDigit(source[i])) { work.Step(); i++; }
                var count = i - fractionalStart;
                if (count == 0 || (strict && count > 3)) return false;
                work.Check();
                // BCL nearest binary64 conversion only after the HTML component grammar is established.
                if (!HtmlInputNumberSyntax.Convert(source.AsSpan(secondsStart, i - secondsStart), i - secondsStart,
                    ref work, out seconds)) return false;
                work.Check();
            }
        }
        return true;
    }

    private static bool Consume(string source, ref int i, char c, ref HtmlInputValueWork work)
    {
        if (i >= source.Length || source[i] != c) return false;
        work.Step(); i++;
        return true;
    }

    private static bool TwoDigits(string source, ref int i, ref HtmlInputValueWork work, out int value)
    {
        value = 0;
        if (source.Length - i < 2 || !HtmlInputNumberSyntax.IsDigit(source[i]) || !HtmlInputNumberSyntax.IsDigit(source[i + 1]))
            return false;
        value = (source[i] - '0') * 10 + source[i + 1] - '0';
        work.Step(); work.Step(); i += 2;
        return true;
    }

    internal static string Sanitize(HtmlInputType type, string source, CancellationToken cancellationToken = default)
        => Sanitize(type, source, null, cancellationToken);

    internal static string Sanitize(HtmlInputType type, string source, Action<long>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        if (!ParseCore(type, source, strict: true, out var parsed, ref work)) return string.Empty;
        if (type != HtmlInputType.DateTimeLocal) return source;
        var time = source.AsSpan(parsed.YearLength + 7);
        var milliseconds = parsed.Hour * 3600000 + parsed.Minute * 60000;
        if (time.Length >= 8) milliseconds += ((time[6] - '0') * 10 + time[7] - '0') * 1000;
        if (time.Length > 8)
        {
            var scale = 100;
            for (var i = 9; i < time.Length; i++) { milliseconds += (time[i] - '0') * scale; scale /= 10; }
        }
        var suffix = FormatTime(milliseconds);
        var dateEnd = parsed.YearLength + 6;
        work.Check();
        if (source[dateEnd] == 'T' && source.AsSpan(dateEnd + 1).SequenceEqual(suffix)) return source;
        // A huge lexical year is valid and retains its zeros. Poll while actually copying it.
        var characters = new char[checked(dateEnd + 1 + suffix.Length)];
        for (var i = 0; i < dateEnd; i++) { work.Step(); characters[i] = source[i]; }
        work.Step(); characters[dateEnd] = 'T';
        for (var i = 0; i < suffix.Length; i++) { work.Step(); characters[dateEnd + 1 + i] = suffix[i]; }
        work.Check();
        var normalized = new string(characters);
        work.Check();
        return normalized;
    }

    internal static HtmlInputNumericParseResult TryGetNumber(HtmlInputType type, string source, out double value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        value = double.NaN;
        if (!IsTemporal(type)) return HtmlInputNumericParseResult.Inapplicable;
        if (!TryParseMicrosyntax(type, source, out var parsed, cancellationToken: cancellationToken))
            return HtmlInputNumericParseResult.SyntaxError;
        if (type == HtmlInputType.Time)
        {
            GetTimeCoordinate(parsed, cancellationToken).TryPublish(out value);
            cancellationToken.ThrowIfCancellationRequested();
            return HtmlInputNumericParseResult.Success;
        }
        if (parsed.SignificantYearLength > 309) return HtmlInputNumericParseResult.NonFinite;
        cancellationToken.ThrowIfCancellationRequested();
        var yearDigits = source.AsSpan(parsed.SignificantYearStart, parsed.SignificantYearLength);
        HtmlInputDecimal coordinate;
        if (long.TryParse(yearDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var smallYear)
            && TryCalendarCoordinate(type, smallYear, parsed, out var smallCoordinate))
        {
            coordinate = HtmlInputDecimal.FromInteger(smallCoordinate);
        }
        else
        {
            var year = BigInteger.Parse(yearDigits, NumberStyles.None, CultureInfo.InvariantCulture);
            var integer = type switch
            {
                HtmlInputType.Month => (year - 1970) * 12 + parsed.Month - 1,
                HtmlInputType.Week => HtmlInputCalendar.WeekMonday(year, parsed.Week) * HtmlInputCalendar.MillisecondsPerDay,
                _ => HtmlInputCalendar.DaysFromCivil(year, parsed.Month, parsed.Day) * HtmlInputCalendar.MillisecondsPerDay
            };
            coordinate = HtmlInputDecimal.FromInteger(integer);
        }
        if (type == HtmlInputType.DateTimeLocal)
            coordinate = coordinate.Add(GetTimeCoordinate(parsed, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (!coordinate.TryPublish(out value)) return HtmlInputNumericParseResult.NonFinite;
        cancellationToken.ThrowIfCancellationRequested();
        return HtmlInputNumericParseResult.Success;
    }

    private static bool TryCalendarCoordinate(HtmlInputType type, long year, in HtmlInputTemporalValue parsed, out long coordinate)
    {
        try
        {
            checked
            {
                coordinate = type switch
                {
                    HtmlInputType.Month => (year - 1970) * 12 + parsed.Month - 1,
                    HtmlInputType.Week => HtmlInputCalendar.WeekMonday(year, parsed.Week) * HtmlInputCalendar.MillisecondsPerDay,
                    _ => HtmlInputCalendar.DaysFromCivil(year, parsed.Month, parsed.Day) * HtmlInputCalendar.MillisecondsPerDay
                };
            }
            return true;
        }
        catch (OverflowException) { coordinate = 0; return false; }
    }

    private static HtmlInputDecimal GetTimeCoordinate(in HtmlInputTemporalValue parsed, CancellationToken cancellationToken)
    {
        var work = new HtmlInputValueWork(null, cancellationToken);
        work.Check();
        var start = parsed.YearLength == 0 ? 0 : parsed.YearLength + 7;
        var time = parsed.Source.AsSpan(start);
        long integer = parsed.Hour * 3600000 + parsed.Minute * 60000;
        if (time.Length >= 8) integer += ((time[6] - '0') * 10 + time[7] - '0') * 1000;
        if (time.Length <= 8) return HtmlInputDecimal.FromInteger(integer);
        var fraction = time[9..];
        var scale = 100;
        for (var i = 0; i < Math.Min(3, fraction.Length); i++)
        { work.Step(); integer += (fraction[i] - '0') * scale; scale /= 10; }
        if (fraction.Length <= 3) return HtmlInputDecimal.FromInteger(integer);

        // Retain 1200 fractional millisecond places, not 1200 source-dependent powers of ten.
        // A positive sticky epsilon beyond that prefix preserves the side of every terminating
        // binary64 midpoint, even when a huge integer coordinate is itself exactly a midpoint.
        Span<char> tail = stackalloc char[1201];
        var length = 0;
        var sticky = false;
        for (var i = 3; i < fraction.Length; i++)
        {
            work.Step();
            if (length < 1200) tail[length++] = fraction[i];
            else sticky |= fraction[i] != '0';
        }
        if (sticky) tail[length++] = '1';
        var exponent = -length;
        while (length > 0 && tail[length - 1] == '0') { length--; exponent++; }
        work.Check();
        if (length == 0) return HtmlInputDecimal.FromInteger(integer);
        var fractional = long.TryParse(tail[..length], NumberStyles.None, CultureInfo.InvariantCulture, out var small)
            ? new HtmlInputDecimal(small, exponent)
            : new HtmlInputDecimal(BigInteger.Parse(tail[..length], NumberStyles.None, CultureInfo.InvariantCulture), exponent);
        var coordinate = HtmlInputDecimal.FromInteger(integer).Add(fractional);
        work.Check();
        return coordinate;
    }

    internal static HtmlInputDateResult GetDate(HtmlInputType type, string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (type is not (HtmlInputType.Date or HtmlInputType.Month or HtmlInputType.Week or HtmlInputType.Time)) return default;
        if (!TryParseMicrosyntax(type, source, out var parsed, cancellationToken: cancellationToken)) return default;
        if (type == HtmlInputType.Time)
        {
            GetTimeCoordinate(parsed, cancellationToken).TryPublish(out var time);
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, Math.Truncate(time));
        }
        // TimeClip cannot admit any seven-significant-digit year. Classify before allocating an integer.
        if (parsed.SignificantYearLength > 6) return new(true, double.NaN);
        var year = long.Parse(source.AsSpan(parsed.SignificantYearStart, parsed.SignificantYearLength), CultureInfo.InvariantCulture);
        var days = type == HtmlInputType.Week
            ? HtmlInputCalendar.WeekMonday(year, parsed.Week)
            : HtmlInputCalendar.DaysFromCivil(year, parsed.Month, type == HtmlInputType.Month ? 1 : parsed.Day);
        var coordinate = days * HtmlInputCalendar.MillisecondsPerDay;
        cancellationToken.ThrowIfCancellationRequested();
        return new(true, Math.Abs(coordinate) <= 8640000000000000L ? coordinate : double.NaN);
    }

    internal static string FormatDate(HtmlInputType type, double utcMilliseconds, CancellationToken cancellationToken = default)
    {
        if (type is not (HtmlInputType.Date or HtmlInputType.Month or HtmlInputType.Week or HtmlInputType.Time))
            throw new ArgumentOutOfRangeException(nameof(type));
        cancellationToken.ThrowIfCancellationRequested();
        // This operation receives the actual Date slot after TimeClip, never a host date/time object.
        if (!double.IsFinite(utcMilliseconds)) return string.Empty;
        if (type != HtmlInputType.Month) return FormatNumber(type, utcMilliseconds, cancellationToken);
        var date = HtmlInputCalendar.TryFloorInt64(utcMilliseconds, out var small)
            ? HtmlInputCalendar.CivilFromDays(HtmlInputCalendar.FloorDivide(small, HtmlInputCalendar.MillisecondsPerDay))
            : HtmlInputCalendar.CivilFromDays(HtmlInputCalendar.FloorDivide(HtmlInputCalendar.FloorFinite(utcMilliseconds), (int) HtmlInputCalendar.MillisecondsPerDay));
        return date.IsPositiveYear ? string.Create(CultureInfo.InvariantCulture, $"{date.FormatYear()}-{date.Month:D2}") : string.Empty;
    }

    internal static string FormatNumber(HtmlInputType type, double value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = HtmlInputCalendar.TryFloorInt64(value, out var small)
            ? FormatInteger(type, small, cancellationToken)
            : FormatInteger(type, HtmlInputCalendar.FloorFinite(value), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static string FormatInteger(HtmlInputType type, long integer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (type == HtmlInputType.Month)
        {
            var quotient = HtmlInputCalendar.FloorDivide(integer, 12L);
            var remainder = (int) (integer % 12);
            if (remainder < 0) remainder += 12;
            var year = quotient + 1970;
            return year <= 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{remainder + 1:D2}");
        }
        var days = HtmlInputCalendar.FloorDivide(integer, HtmlInputCalendar.MillisecondsPerDay);
        var time = (int) (integer % HtmlInputCalendar.MillisecondsPerDay);
        if (time < 0) time += (int) HtmlInputCalendar.MillisecondsPerDay;
        if (type == HtmlInputType.Time) return FormatTime(time);
        if (type == HtmlInputType.Week)
        {
            var (year, week) = HtmlInputCalendar.WeekFromDays(days);
            return year.IsPositiveYear ? string.Create(CultureInfo.InvariantCulture, $"{year.FormatYear()}-W{week:D2}") : string.Empty;
        }
        if (type is not (HtmlInputType.Date or HtmlInputType.DateTimeLocal)) throw new ArgumentOutOfRangeException(nameof(type));
        var date = HtmlInputCalendar.CivilFromDays(days);
        if (!date.IsPositiveYear) return string.Empty;
        var result = string.Create(CultureInfo.InvariantCulture, $"{date.FormatYear()}-{date.Month:D2}-{date.Day:D2}");
        if (type == HtmlInputType.DateTimeLocal) result += "T" + FormatTime(time);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static string FormatInteger(HtmlInputType type, BigInteger integer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (integer >= long.MinValue && integer <= long.MaxValue) return FormatInteger(type, (long) integer, cancellationToken);
        string result;
        if (type == HtmlInputType.Month)
        {
            var year = HtmlInputCalendar.FloorDivide(integer, 12) + 1970;
            if (year.Sign <= 0) return string.Empty;
            result = string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{HtmlInputCalendar.FloorMod(integer, 12) + 1:D2}");
        }
        else
        {
            var days = HtmlInputCalendar.FloorDivide(integer, (int) HtmlInputCalendar.MillisecondsPerDay);
            var time = HtmlInputCalendar.FloorMod(integer, (int) HtmlInputCalendar.MillisecondsPerDay);
            if (type == HtmlInputType.Time) result = FormatTime(time);
            else if (type == HtmlInputType.Week)
            {
                var (year, week) = HtmlInputCalendar.WeekFromDays(days);
                if (!year.IsPositiveYear) return string.Empty;
                result = string.Create(CultureInfo.InvariantCulture, $"{year.FormatYear()}-W{week:D2}");
            }
            else if (type is HtmlInputType.Date or HtmlInputType.DateTimeLocal)
            {
                var date = HtmlInputCalendar.CivilFromDays(days);
                if (!date.IsPositiveYear) return string.Empty;
                result = string.Create(CultureInfo.InvariantCulture, $"{date.FormatYear()}-{date.Month:D2}-{date.Day:D2}");
                if (type == HtmlInputType.DateTimeLocal) result += "T" + FormatTime(time);
            }
            else throw new ArgumentOutOfRangeException(nameof(type));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static string FormatTime(int milliseconds)
    {
        var hour = milliseconds / 3600000;
        var minute = milliseconds / 60000 % 60;
        var second = milliseconds / 1000 % 60;
        var fraction = milliseconds % 1000;
        var result = string.Create(CultureInfo.InvariantCulture, $"{hour:D2}:{minute:D2}");
        if (second != 0 || fraction != 0) result += string.Create(CultureInfo.InvariantCulture, $":{second:D2}");
        if (fraction != 0) result += "." + fraction.ToString("D3", CultureInfo.InvariantCulture).TrimEnd('0');
        return result;
    }
}
