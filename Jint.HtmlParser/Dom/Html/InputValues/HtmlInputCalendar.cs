using System.Globalization;
using System.Numerics;

namespace Jint.HtmlParser;

internal readonly struct HtmlInputCivilDate
{
    internal HtmlInputCivilDate(long year, int month, int day) { Year = year; Month = month; Day = day; }
    internal HtmlInputCivilDate(BigInteger year, int month, int day)
    {
        if (year >= long.MinValue && year <= long.MaxValue) Year = (long) year;
        else ExtendedYear = year;
        Month = month;
        Day = day;
    }
    internal long Year { get; }
    internal BigInteger ExtendedYear { get; }
    internal int Month { get; }
    internal int Day { get; }
    internal bool IsPositiveYear => ExtendedYear.IsZero ? Year > 0 : ExtendedYear.Sign > 0;
    internal BigInteger GetYear() => ExtendedYear.IsZero ? Year : ExtendedYear;
    internal string FormatYear() => ExtendedYear.IsZero
        ? Year.ToString("D4", CultureInfo.InvariantCulture)
        : ExtendedYear.ToString("D4", CultureInfo.InvariantCulture);
}

internal readonly record struct HtmlInputIsoWeek(HtmlInputCivilDate Year, int Week);

/// <summary>Proleptic Gregorian/ISO arithmetic for HTML §2.3.5; the 400-year cycle has no CLR year ceiling.</summary>
internal static class HtmlInputCalendar
{
    internal const long MillisecondsPerDay = 86400000;

    internal static long FloorDivide(long value, long divisor)
    {
        var quotient = Math.DivRem(value, divisor, out var remainder);
        return remainder < 0 ? quotient - 1 : quotient;
    }

    internal static BigInteger FloorDivide(BigInteger value, int divisor)
    {
        var quotient = BigInteger.DivRem(value, divisor, out var remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }

    internal static int FloorMod(BigInteger value, int divisor)
    {
        var remainder = (int) (value % divisor);
        return remainder < 0 ? remainder + divisor : remainder;
    }

    internal static bool IsLeapYear(int modulo400) => modulo400 % 4 == 0 && (modulo400 % 100 != 0 || modulo400 == 0);

    internal static int DaysInMonth(int modulo400, int month) => month switch
    {
        2 => IsLeapYear(modulo400) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        _ => 31
    };

    internal static int WeeksInYear(int modulo400)
    {
        var january1 = DaysFromCivil(modulo400 == 0 ? 400 : modulo400, 1, 1);
        var weekday = (int) ((january1 + 3) % 7);
        if (weekday < 0) weekday += 7;
        return weekday == 3 || (weekday == 2 && IsLeapYear(modulo400)) ? 53 : 52;
    }

    internal static long DaysFromCivil(long year, int month, int day)
    {
        checked
        {
            var y = year - (month <= 2 ? 1 : 0);
            var era = FloorDivide(y, 400L);
            var withinYear = y - era * 400;
            var shiftedMonth = month + (month > 2 ? -3 : 9);
            var dayOfYear = (153 * shiftedMonth + 2) / 5 + day - 1;
            return era * 146097 + withinYear * 365 + withinYear / 4 - withinYear / 100 + dayOfYear - 719468;
        }
    }

    internal static BigInteger DaysFromCivil(BigInteger year, int month, int day)
    {
        var y = year - (month <= 2 ? 1 : 0);
        var era = FloorDivide(y, 400);
        var withinYear = (int) (y - era * 400);
        var shiftedMonth = month + (month > 2 ? -3 : 9);
        var dayOfYear = (153 * shiftedMonth + 2) / 5 + day - 1;
        return era * 146097 + withinYear * 365 + withinYear / 4 - withinYear / 100 + dayOfYear - 719468;
    }

    internal static HtmlInputCivilDate CivilFromDays(long days)
    {
        checked
        {
            var shifted = days + 719468;
            var era = FloorDivide(shifted, 146097L);
            var dayOfEra = (int) (shifted - era * 146097);
            Components(dayOfEra, out var yearOfEra, out var month, out var day);
            return new HtmlInputCivilDate(era * 400 + yearOfEra + (month <= 2 ? 1 : 0), month, day);
        }
    }

    internal static HtmlInputCivilDate CivilFromDays(BigInteger days)
    {
        if (days > long.MinValue + 719468 && days < long.MaxValue - 719468)
            return CivilFromDays((long) days);
        var shifted = days + 719468;
        var era = FloorDivide(shifted, 146097);
        var dayOfEra = (int) (shifted - era * 146097);
        Components(dayOfEra, out var yearOfEra, out var month, out var day);
        return new HtmlInputCivilDate(era * 400 + yearOfEra + (month <= 2 ? 1 : 0), month, day);
    }

    private static void Components(int dayOfEra, out int yearOfEra, out int month, out int day)
    {
        yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365;
        var dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100);
        var shiftedMonth = (5 * dayOfYear + 2) / 153;
        day = dayOfYear - (153 * shiftedMonth + 2) / 5 + 1;
        month = shiftedMonth + (shiftedMonth < 10 ? 3 : -9);
    }

    internal static BigInteger WeekMonday(BigInteger year, int week)
    {
        var january4 = DaysFromCivil(year, 1, 4);
        return january4 - FloorMod(january4 + 3, 7) + (week - 1) * 7;
    }

    internal static long WeekMonday(long year, int week)
    {
        checked
        {
            var january4 = DaysFromCivil(year, 1, 4);
            var weekday = (january4 + 3) % 7;
            if (weekday < 0) weekday += 7;
            return january4 - weekday + (week - 1) * 7;
        }
    }

    internal static HtmlInputIsoWeek WeekFromDays(long days)
    {
        checked
        {
            var weekday = (days + 3) % 7;
            if (weekday < 0) weekday += 7;
            var thursday = CivilFromDays(days + 3 - weekday);
            var firstMonday = WeekMonday(thursday.Year, 1);
            return new(thursday, (int) ((days - firstMonday) / 7) + 1);
        }
    }

    internal static HtmlInputIsoWeek WeekFromDays(BigInteger days)
    {
        var weekday = FloorMod(days + 3, 7);
        var thursday = CivilFromDays(days + 3 - weekday);
        var firstMonday = WeekMonday(thursday.GetYear(), 1);
        return new(thursday, (int) ((days - firstMonday) / 7) + 1);
    }

    // Construct the exact integer from IEEE 754 bits, rather than from shortest decimal digits.
    internal static BigInteger FloorFinite(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (TryFloorInt64(value, out var small)) return small;
        var bits = BitConverter.DoubleToUInt64Bits(value);
        var exponent = (int) ((bits >> 52) & 0x7ff);
        var significand = bits & 0x000fffffffffffffUL;
        var shift = -1074;
        if (exponent != 0) { significand |= 1UL << 52; shift = exponent - 1075; }
        var integer = new BigInteger(significand);
        var negative = (bits >> 63) != 0;
        if (shift >= 0) integer <<= shift;
        else
        {
            var divisor = BigInteger.One << -shift;
            integer = BigInteger.DivRem(integer, divisor, out var remainder);
            if (negative && !remainder.IsZero) integer++;
        }
        return negative ? -integer : integer;
    }

    internal static bool TryFloorInt64(double value, out long integer)
    {
        // The positive endpoint is exclusive: the nearest double to Int64.MaxValue is 2^63.
        if (double.IsFinite(value) && value >= -9223372036854775808d && value < 9223372036854775808d)
        {
            integer = (long) Math.Floor(value);
            return true;
        }
        integer = 0;
        return false;
    }
}
