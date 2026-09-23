namespace Jint.HtmlParser.Css.Values;

/// <summary>An owned CSS decimal literal. Comparison never converts through floating point.</summary>
internal readonly struct CssNumber
{
    // Exponent differences beyond this cannot be reversed by an Int32-sized input's
    // decimal-position adjustment. The slow path subtracts arbitrary-length exponents
    // digit by digit before saturating, so close huge exponents remain distinguishable.
    private const long ExponentDifferenceLimit = 8L * int.MaxValue;

    private readonly string? _spelling;
    private readonly int _firstSignificant;
    private readonly int _significantCount;
    private readonly int _exponentStart;
    private readonly int _exponentLength;
    private readonly int _exponentSign;
    private readonly long _decimalAdjustment;

    private CssNumber(string spelling, int sign, bool isNegativeZero, int firstSignificant,
        int significantCount, int exponentStart, int exponentLength,
        int exponentSign, long decimalAdjustment)
    {
        _spelling = spelling;
        Sign = sign;
        IsNegativeZero = isNegativeZero;
        _firstSignificant = firstSignificant;
        _significantCount = significantCount;
        _exponentStart = exponentStart;
        _exponentLength = exponentLength;
        _exponentSign = exponentSign;
        _decimalAdjustment = decimalAdjustment;
    }

    public string Spelling => _spelling ?? string.Empty;
    public int Sign { get; }
    public bool IsNegativeZero { get; }

    internal static CssNumber FromValidatedToken(string spelling, CssValueWork work)
    {
        work.CheckCancellation();
        ArgumentException.ThrowIfNullOrEmpty(spelling);

        var start = spelling[0] is '+' or '-' ? 1 : 0;
        var negative = start == 1 && spelling[0] == '-';
        var exponentMarker = spelling.Length;
        var point = -1;
        var first = -1;
        var last = -1;
        var digitCount = 0;
        var firstDigitOrdinal = 0;
        var lastDigitOrdinal = 0;
        var decimalDigits = 0;
        for (var i = start; i < spelling.Length; i++)
        {
            work.Charge(1);
            var ch = spelling[i];
            if (ch is 'e' or 'E')
            {
                exponentMarker = i;
                break;
            }
            if (ch == '.')
            {
                point = i;
                continue;
            }
            if (point < 0) decimalDigits++;
            if (ch != '0')
            {
                if (first < 0)
                {
                    first = i;
                    firstDigitOrdinal = digitCount;
                }
                last = i;
                lastDigitOrdinal = digitCount;
            }
            digitCount++;
        }

        var expStart = exponentMarker == spelling.Length ? spelling.Length : exponentMarker + 1;
        var expSign = 1;
        if (expStart < spelling.Length && spelling[expStart] is '+' or '-')
        {
            if (spelling[expStart] == '-') expSign = -1;
            expStart++;
        }
        while (expStart < spelling.Length && spelling[expStart] == '0')
        {
            work.Charge(1);
            expStart++;
        }
        for (var i = expStart; i < spelling.Length; i++) work.Charge(1);
        var expLength = spelling.Length - expStart;
        if (expLength == 0) expSign = 0;
        var sign = first < 0 ? 0 : negative ? -1 : 1;
        var adjustment = first < 0 ? 0 : (long) decimalDigits - firstDigitOrdinal - 1;
        var result = new CssNumber(spelling, sign, negative && sign == 0, first,
            first < 0 ? 0 : lastDigitOrdinal - firstDigitOrdinal + 1,
            expStart, expLength, expSign, adjustment);
        work.CheckCancellation();
        return result;
    }

    internal int CompareTo(CssNumber other, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        if (_spelling is null || other._spelling is null)
            throw new InvalidOperationException("An uninitialized CSS number cannot be compared.");
        if (Sign != other.Sign)
        {
            work.CheckCancellation();
            return Sign.CompareTo(other.Sign);
        }
        if (Sign == 0)
        {
            work.CheckCancellation();
            return 0;
        }

        var exponentDifference = CompareExponentDifference(other, work);
        var order = exponentDifference + _decimalAdjustment - other._decimalAdjustment;
        if (order != 0)
        {
            work.CheckCancellation();
            return (order > 0 ? 1 : -1) * Sign;
        }

        var left = _firstSignificant;
        var right = other._firstSignificant;
        var maximum = Math.Max(_significantCount, other._significantCount);
        for (var i = 0; i < maximum; i++)
        {
            var a = i < _significantCount ? NextDigit(_spelling, ref left) : '0';
            var b = i < other._significantCount ? NextDigit(other._spelling, ref right) : '0';
            work.Charge(2);
            if (a == b) continue;
            work.CheckCancellation();
            return (a > b ? 1 : -1) * Sign;
        }
        work.CheckCancellation();
        return 0;
    }

    private static char NextDigit(string spelling, ref int index)
    {
        if (spelling[index] == '.') index++;
        return spelling[index++];
    }

    private long CompareExponentDifference(CssNumber other, CssValueWork work)
    {
        if (_exponentSign != other._exponentSign)
        {
            if (_exponentSign == 0) return -other._exponentSign * other.ReadExponentMagnitude(work);
            if (other._exponentSign == 0) return _exponentSign * ReadExponentMagnitude(work);
            var left = ReadExponentMagnitude(work);
            var right = other.ReadExponentMagnitude(work);
            return _exponentSign * Math.Min(ExponentDifferenceLimit, left + right);
        }
        if (_exponentSign == 0) return 0;

        var comparison = _exponentLength.CompareTo(other._exponentLength);
        if (comparison == 0)
        {
            for (var i = 0; i < _exponentLength; i++)
            {
                work.Charge(2);
                comparison = _spelling![_exponentStart + i].CompareTo(other._spelling![other._exponentStart + i]);
                if (comparison != 0) break;
            }
        }
        if (comparison == 0) return 0;

        var larger = comparison > 0 ? this : other;
        var smaller = comparison > 0 ? other : this;
        var difference = SubtractExponentMagnitudes(larger, smaller, work);
        return (comparison > 0 ? _exponentSign : -_exponentSign) * difference;
    }

    private long ReadExponentMagnitude(CssValueWork work)
    {
        long result = 0;
        for (var i = 0; i < _exponentLength; i++)
        {
            work.Charge(1);
            var digit = _spelling![_exponentStart + i] - '0';
            result = Math.Min(ExponentDifferenceLimit, result * 10 + digit);
        }
        return result;
    }

    private static long SubtractExponentMagnitudes(CssNumber larger, CssNumber smaller, CssValueWork work)
    {
        var left = larger._exponentLength - 1;
        var right = smaller._exponentLength - 1;
        var borrow = 0;
        long difference = 0;
        long place = 1;
        while (left >= 0)
        {
            var a = larger._spelling![larger._exponentStart + left--] - '0';
            var b = right >= 0 ? smaller._spelling![smaller._exponentStart + right--] - '0' : 0;
            var digit = a - b - borrow;
            borrow = digit < 0 ? 1 : 0;
            if (digit < 0) digit += 10;
            if (digit != 0) difference = Math.Min(ExponentDifferenceLimit, difference + place * digit);
            place = Math.Min(ExponentDifferenceLimit, place * 10);
            work.Charge(2);
        }
        return difference;
    }
}
