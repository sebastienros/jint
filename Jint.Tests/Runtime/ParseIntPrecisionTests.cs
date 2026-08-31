#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the double <c>parseInt</c> produces. Step 15 of the algorithm is <c>Return 𝔽(sign × mathInt)</c>,
/// so the digits are turned into a number by one rounding of the exact integer they denote, however many
/// of them there are and whatever radix they are read in.
/// </summary>
/// <remarks>
/// Nothing here is checked against a second floating-point conversion. The oracle holds the exact value in
/// a <see cref="BigInteger"/> and compares the double the engine produced against it and both its
/// neighbours in exact integer arithmetic, which is the only way to tell "the nearest double" from "a
/// double that is nearly right".
/// </remarks>
public class ParseIntPrecisionTests
{
    private const string DigitAlphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// The three operands the defect was reported with: the accumulation used to round once per digit,
    /// so it landed one or more ULP away from the number the same digits denote in every other lane.
    /// </summary>
    [TestCase("678851690709701306", "678851690709701200")]
    [TestCase("2499781153938996520", "2499781153938996700")]
    [TestCase("1302819948463508623", "1302819948463508700")]
    [TestCase("9007199254740993", "9007199254740992")]
    [TestCase("18446744073709551615", "18446744073709552000")]
    [TestCase("123456789012345678901234567890", "1.2345678901234568e+29")]
    public void ParseIntReadsTheSameNumberAsEveryOtherLane(string text, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"parseInt('{text}').toString()").AsString().Should().Be(expected);
        engine.Evaluate($"parseInt('{text}') === Number('{text}')").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The digits are the whole of the value, so a radix a power of two and a radix that is not must
    /// agree with each other and with the decimal spelling of the same integer.
    /// </summary>
    [Test]
    public void EveryRadixReadsTheSameNumberForTheSameValue()
    {
        var engine = new Engine();
        engine.Evaluate("""
            parseInt('ffffffffffffffff', 16) === parseInt('18446744073709551615')
            && parseInt('1111111111111111111111111111111111111111111111111111111111111111', 2) === parseInt('18446744073709551615')
            && parseInt('3w5e11264sgsf', 36) === parseInt('18446744073709551615')
            && parseInt('1777777777777777777777', 8) === parseInt('18446744073709551615')
            """).AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// An independent oracle over random operands: whatever the engine holds must be the double nearest
    /// the integer the digits denote, with a tie broken towards an even significand.
    /// </summary>
    [TestCase(2, 70)]
    [TestCase(2, 300)]
    [TestCase(3, 45)]
    [TestCase(4, 40)]
    [TestCase(5, 35)]
    [TestCase(6, 30)]
    [TestCase(7, 28)]
    [TestCase(8, 30)]
    [TestCase(9, 25)]
    [TestCase(10, 17)]
    [TestCase(10, 19)]
    [TestCase(10, 22)]
    [TestCase(10, 40)]
    [TestCase(10, 120)]
    [TestCase(11, 22)]
    [TestCase(13, 20)]
    [TestCase(16, 20)]
    [TestCase(16, 60)]
    [TestCase(24, 18)]
    [TestCase(32, 25)]
    [TestCase(36, 15)]
    [TestCase(36, 80)]
    public void ParseIntRoundsToNearest(int radix, int length)
    {
        var engine = new Engine();
        var random = new Random(20260901 + radix * 1000 + length);
        var failures = 0;
        var first = "";

        for (var i = 0; i < 500; i++)
        {
            var text = RandomDigits(random, radix, length);
            var held = engine.Evaluate($"parseInt('{text}', {radix})").AsNumber();
            if (IsNearestDouble(ExactValue(text, radix), held))
            {
                continue;
            }

            failures++;
            if (first.Length == 0)
            {
                first = $"parseInt('{text}', {radix}) held {Bits(held)}";
            }
        }

        failures.Should().Be(0, $"every operand must read as the nearest double; first miss was {first}");
    }

    /// <summary>
    /// The octave <c>[2^63, 2^64)</c>, where a random 20-digit string almost never lands, and where every
    /// conversion that goes through an intermediate rounding is at its worst.
    /// </summary>
    [Test]
    public void ParseIntRoundsToNearestJustAboveInt64()
    {
        var engine = new Engine();
        var random = new Random(20260901);
        var buffer = new byte[8];
        var failures = 0;
        var first = "";

        for (var i = 0; i < 2000; i++)
        {
            random.NextBytes(buffer);
            var value = BitConverter.ToUInt64(buffer, 0) | (1UL << 63);
            var text = value.ToString(CultureInfo.InvariantCulture);
            var held = engine.Evaluate($"parseInt('{text}')").AsNumber();
            if (IsNearestDouble(new BigInteger(value), held))
            {
                continue;
            }

            failures++;
            if (first.Length == 0)
            {
                first = $"parseInt('{text}') held {Bits(held)}";
            }
        }

        failures.Should().Be(0, $"every operand must read as the nearest double; first miss was {first}");
    }

    /// <summary>
    /// A value exactly between two doubles rounds to the one with an even significand, and one unit either
    /// side of it does not. This is where an implementation that keeps only the leading digits and forgets
    /// whether anything was dropped gets two of the three wrong.
    /// </summary>
    [Test]
    public void AMidpointBetweenTwoDoublesRoundsToEven([Range(2, 36)] int radix)
    {
        var engine = new Engine();
        var random = new Random(20260901 + radix);
        var buffer = new byte[8];

        for (var shift = 1; shift <= 200; shift += 7)
        {
            random.NextBytes(buffer);

            // An odd 54-bit significand is exactly halfway between the two 53-bit ones around it.
            var significand = (BitConverter.ToUInt64(buffer, 0) >> 11) | (1UL << 52);
            var midpoint = ((new BigInteger(significand) << 1) + 1) << shift;

            foreach (var value in new[] { midpoint - 1, midpoint, midpoint + 1 })
            {
                var text = ToRadix(value, radix);
                var held = engine.Evaluate($"parseInt('{text}', {radix})").AsNumber();
                IsNearestDouble(value, held)
                    .Should().BeTrue($"parseInt('{text}', {radix}) held {Bits(held)}");
            }
        }
    }

    /// <summary>
    /// The digit count at which a value has certainly passed every finite double is what bounds the exact
    /// accumulation, so both sides of that count have to be right: one digit short of it the answer is
    /// still a finite number that must be the nearest one, and at it the answer is an infinity.
    /// </summary>
    [Test]
    public void EveryRadixSaturatesAtItsOwnOverflowBoundary([Range(2, 36)] int radix)
    {
        var engine = new Engine();
        var random = new Random(20260901 + radix);

        // The smallest digit count whose least value, radix^(length - 1), is already past 2^1024.
        var overflowLength = 1;
        var least = BigInteger.One;
        while (least < BigInteger.One << 1024)
        {
            least *= radix;
            overflowLength++;
        }

        for (var length = overflowLength - 2; length <= overflowLength + 1; length++)
        {
            for (var i = 0; i < 20; i++)
            {
                var text = RandomDigits(random, radix, length);
                var held = engine.Evaluate($"parseInt('{text}', {radix})").AsNumber();
                IsNearestDouble(ExactValue(text, radix), held)
                    .Should().BeTrue($"a {length}-digit radix-{radix} operand held {Bits(held)}");
            }
        }
    }

    /// <summary>
    /// A digit run long enough to be an attack is answered from its length alone.
    /// </summary>
    /// <remarks>
    /// A value's magnitude is known from its digit count, so past a few hundred digits — 1026 in radix 2,
    /// 311 in radix 10, 201 in radix 36 — the answer is an infinity whatever the remaining digits say, and
    /// the scan stops there. Nothing proportional to the input is ever allocated: the widest exact value
    /// the parser builds is the one just under 2^1024, which is about 130 bytes. The bound below would not
    /// be met by an implementation that accumulated ten million digits into a <see cref="BigInteger"/>,
    /// which is quadratic in the digit count.
    /// </remarks>
    [Test]
    public void ALongDigitRunSaturatesFromItsLengthAlone()
    {
        var engine = new Engine();
        engine.SetValue("ones", new string('1', 10_000_000));
        engine.SetValue("padded", new string('0', 10_000_000) + "5");

        var watch = Stopwatch.StartNew();
        engine.Evaluate("parseInt(ones)").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("parseInt(ones, 2)").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("parseInt(ones, 36)").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("-parseInt(ones)").AsNumber().Should().Be(double.NegativeInfinity);

        // Leading zeros are digits that carry no magnitude, so they are skipped rather than counted.
        engine.Evaluate("parseInt(padded)").AsNumber().Should().Be(5);

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// The value the digits denote is the only thing that changes; which digits are accepted does not.
    /// </summary>
    [TestCase("", double.NaN)]
    [TestCase("   ", double.NaN)]
    [TestCase("x", double.NaN)]
    [TestCase("+", double.NaN)]
    [TestCase("0x", double.NaN)]
    [TestCase("   42  ", 42)]
    [TestCase("42abc", 42)]
    [TestCase("0x1f", 31)]
    [TestCase("-0X10", -16)]
    [TestCase("12.9", 12)]
    [TestCase("1e3", 1)]
    [TestCase("00000000000000000000000000000000000000009", 9)]
    [TestCase("99999999999999999999999x", 1e23)]
    public void ThePrefixTheDigitsFormIsUnchanged(string text, double expected)
    {
        var engine = new Engine();
        engine.Evaluate($"parseInt('{text}')").AsNumber().Should().Be(expected);
    }

    [TestCase("z", 36, 35)]
    [TestCase("Z", 36, 35)]
    [TestCase("zz", 35, double.NaN)]
    [TestCase("8", 8, double.NaN)]
    [TestCase("777", 8, 511)]
    [TestCase("11", 1, double.NaN)]
    [TestCase("11", 37, double.NaN)]
    [TestCase("0x10", 16, 16)]
    [TestCase("0x10", 8, 0)]
    public void ARadixDecidesWhichDigitsAreDigits(string text, int radix, double expected)
    {
        var engine = new Engine();
        engine.Evaluate($"parseInt('{text}', {radix})").AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// Step 15 multiplies by the sign after rounding, and a recorded minus in front of nothing but zeros
    /// is what makes the result a negative zero.
    /// </summary>
    [Test]
    public void ZeroKeepsARecordedSign()
    {
        var engine = new Engine();
        engine.Evaluate("1 / parseInt('-0')").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / parseInt('-000', 8)").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / parseInt('-0x0')").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / parseInt('0')").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("1 / parseInt('+0', 36)").AsNumber().Should().Be(double.PositiveInfinity);
    }

    private static string RandomDigits(Random random, int radix, int length)
    {
        var builder = new StringBuilder(length);
        builder.Append(Cased(random, DigitAlphabet[1 + random.Next(radix - 1)]));
        for (var i = 1; i < length; i++)
        {
            builder.Append(Cased(random, DigitAlphabet[random.Next(radix)]));
        }

        return builder.ToString();
    }

    private static char Cased(Random random, char digit)
        => digit > '9' && random.Next(2) == 0 ? char.ToUpperInvariant(digit) : digit;

    private static BigInteger ExactValue(string digits, int radix)
    {
        var value = BigInteger.Zero;
        foreach (var c in digits)
        {
            value = value * radix + DigitAlphabet.IndexOf(char.ToLowerInvariant(c));
        }

        return value;
    }

    private static string ToRadix(BigInteger value, int radix)
    {
        if (value.IsZero)
        {
            return "0";
        }

        var builder = new StringBuilder();
        while (!value.IsZero)
        {
            builder.Insert(0, DigitAlphabet[(int) (value % radix)]);
            value /= radix;
        }

        return builder.ToString();
    }

    private static string Bits(double value)
        => "0x" + BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>
    /// Decides, in exact integer arithmetic, whether <paramref name="held"/> is the double nearest
    /// <paramref name="value"/>: no adjacent double may be closer, and a value exactly between two of them
    /// must have landed on the one with an even significand.
    /// </summary>
    internal static bool IsNearestDouble(BigInteger value, double held)
    {
        if (double.IsNaN(held))
        {
            return false;
        }

        if (value.IsZero)
        {
            return held == 0;
        }

        if ((value.Sign < 0) != (BitConverter.DoubleToInt64Bits(held) < 0))
        {
            return false;
        }

        var magnitude = BigInteger.Abs(value);

        // Everything at or above the midpoint between the largest finite double and 2^1024 is an infinity.
        if (magnitude >= (BigInteger.One << 1024) - (BigInteger.One << 970))
        {
            return double.IsInfinity(held);
        }

        if (double.IsInfinity(held))
        {
            return false;
        }

        // Every double is an integer multiple of 2^-1074, so scaling by 2^1074 puts the candidate, both
        // its neighbours and the target on one exact integer scale.
        var bits = BitConverter.DoubleToInt64Bits(System.Math.Abs(held));
        var target = magnitude << 1074;
        var distance = BigInteger.Abs(target - Scaled(bits));
        var below = BigInteger.Abs(target - Scaled(bits == 0 ? 0 : bits - 1));
        var above = BigInteger.Abs(target - Scaled(bits + 1));

        if (distance > below || distance > above)
        {
            return false;
        }

        var tie = distance == below || distance == above;
        return !tie || (bits & 1) == 0;
    }

    private static BigInteger Scaled(long bits)
    {
        var biased = (int) ((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        return biased == 0
            ? new BigInteger(fraction)
            : new BigInteger(fraction | (1L << 52)) << (biased - 1);
    }
}
