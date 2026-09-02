using System.Globalization;
using System.Numerics;
using System.Text;
using Jint.Extensions;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the double a numeric literal holds. The scanner settles a whole-number literal with its own
/// <see cref="ulong"/> accumulator, and that conversion double-rounds on .NET Framework and on .NET 8
/// and below for a value in <c>[2^63, 2^64)</c>; everything it cannot accumulate - a fraction, an
/// exponent, more digits than a <see cref="ulong"/> holds - it hands to <c>double.Parse</c>, which is
/// not IEEE correctly-rounded on .NET Framework. Both used to make the same source hold two different
/// numbers depending only on which target framework was loaded.
/// </summary>
public class NumericLiteralPrecisionTests
{
    // Every expected string is the shortest round-tripping decimal of the double nearest the
    // literal's mathematical value, which is what Number::toString must print.
    [TestCase("12345678901234567890", "12345678901234567000")]
    [TestCase("9223372036854776833", "9223372036854778000")]
    [TestCase("14706402211007785958", "14706402211007785000")]
    [TestCase("10157050723211089077", "10157050723211090000")]
    [TestCase("0xAB54A98CEB1F0AD2", "12345678901234567000")]
    [TestCase("0o1255245230635307605322", "12345678901234567000")]
    [TestCase("0b1010101101010100101010011000110011101011000111110000101011010010", "12345678901234567000")]
    [TestCase("12_345_678_901_234_567_890", "12345678901234567000")]
    [TestCase("01255245230635307605322", "12345678901234567000")]
    // Controls: exactly representable, or outside the range the conversion mishandles.
    [TestCase("12345678901234567168", "12345678901234567000")]
    [TestCase("9223372036854775808", "9223372036854776000")]
    [TestCase("18446744073709551615", "18446744073709552000")]
    [TestCase("18446744073709551616", "18446744073709552000")]
    [TestCase("9007199254740993", "9007199254740992")]
    [TestCase("123456789012345678901234567890", "1.2345678901234568e+29")]
    public void IntegerLiteralHoldsTheNearestDouble(string literal, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"({literal}).toString()").AsString().Should().Be(expected);
    }

    [Test]
    public void LiteralAndItsExactDoubleAreTheSameNumber()
    {
        var engine = new Engine();
        // 12345678901234567168 is the nearest double to 12345678901234567890 and is itself exactly
        // representable, so it scans identically everywhere.
        engine.Evaluate("12345678901234567890 === 12345678901234567168").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void LiteralAgreesWithEveryOtherWayOfWritingTheSameDigits()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (12345678901234567890 === Number('12345678901234567890'))
            && (12345678901234567890 === parseFloat('12345678901234567890'))
            && (12345678901234567890 === JSON.parse('12345678901234567890'))
            && (12345678901234567890 === 1.2345678901234567e19)
            && (12345678901234567890 === 12345678901234567890.0)
            """).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void LiteralPropertyKeyRendersTheNearestDouble()
    {
        var engine = new Engine();
        engine.Evaluate("Object.keys({ 12345678901234567890: 1 })[0]").AsString().Should().Be("12345678901234567000");
    }

    [Test]
    public void CompoundAssignmentWithALiteralOperandUsesTheNearestDouble()
    {
        var engine = new Engine();
        engine.Evaluate("(function () { var a = 0; a += 12345678901234567890; return a.toString(); })()")
            .AsString().Should().Be("12345678901234567000");
    }

    /// <summary>
    /// An independent oracle over the whole affected octave: whatever the engine ends up holding must be
    /// the nearest double to the literal's mathematical value, which exact integer arithmetic can decide
    /// without redoing the conversion the engine performs.
    /// </summary>
    [TestCase(10)]
    [TestCase(16)]
    public void EveryLiteralInTheTopUnsignedOctaveRoundsToNearest(int radix)
    {
        var engine = new Engine();
        var random = new Random(20260831);
        var buffer = new byte[8];

        for (var i = 0; i < 500; i++)
        {
            random.NextBytes(buffer);
            var exact = BitConverter.ToUInt64(buffer, 0) | 0x8000000000000000UL;
            var source = radix == 16
                ? "0x" + exact.ToString("X", CultureInfo.InvariantCulture)
                : exact.ToString(CultureInfo.InvariantCulture);

            var held = engine.Evaluate(source).AsNumber();

            // Every double in [2^63, 2^64) is an integer, so the comparison is exact.
            var error = BigInteger.Abs(new BigInteger(held) - exact);
            const int HalfUlp = 1 << 10; // the octave's ULP is 2^11
            error.Should().BeLessThanOrEqualTo(HalfUlp, $"{source} must round to the nearest double");
            if (error == HalfUlp)
            {
                (BitConverter.DoubleToInt64Bits(held) & 1).Should().Be(0, $"{source} is a tie and must round to even");
            }
        }
    }

    /// <summary>
    /// A radix literal wider than the scanner's <see cref="ulong"/> accumulator is rebuilt one digit at a
    /// time in a <c>double</c>, which rounds once per digit where the literal denotes one rounding of the
    /// whole value. Unlike the octave above, this one was wrong on every target framework: no platform
    /// parser is involved in it at all.
    /// </summary>
    // Every expected string is the shortest round-tripping decimal of the double nearest the literal's
    // mathematical value, which is what Number::toString must print.
    [TestCase("0x1F49E9EE4C1BCE961", "36073444770624370000")]
    [TestCase("0x1F49_E9EE_4C1B_CE961", "36073444770624370000")]
    [TestCase("0b1111110101010110111011001100011110100011000000100101101011000110110000", "1.1683224628333037e+21")]
    [TestCase("0o1777777777777777777777777", "9.44473296573929e+21")]
    [TestCase("0xFFFFFFFFFFFFFFFFF", "295147905179352830000")]
    [TestCase("0b1111111111111111111111111111111111111111111111111111111111111111111111", "1.1805916207174113e+21")]
    // The exact midpoint between two doubles rounds to the one with an even significand, and one unit
    // either side of it does not — which is what the sticky bit past the read window decides.
    [TestCase("0x10000000000000800", "18446744073709552000")]
    [TestCase("0x10000000000001800", "18446744073709560000")]
    [TestCase("0x10000000000000801", "18446744073709556000")]
    [TestCase("0x100000000000007FF", "18446744073709552000")]
    // Controls: exactly representable, so they always scanned correctly.
    [TestCase("0x10000000000000000", "18446744073709552000")]
    [TestCase("0x100000000000000000000000000000000", "3.402823669209385e+38")]
    public void AWideRadixLiteralHoldsTheNearestDouble(string literal, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"({literal}).toString()").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A legacy octal literal too wide for the accumulator moved further than one ULP: the scanner gave up
    /// on its own accumulator and re-read the digits as decimal, which is the wrong base entirely.
    /// </summary>
    [Test]
    public void AWideLegacyOctalLiteralIsStillOctal()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (017777777777777777777777 === 0o17777777777777777777777)
            && (017777777777777777777777 === Number('147573952589676412927'))
            && (01777777777777777777777777 === Number('9444732965739290427391'))
            """).AsBoolean().Should().BeTrue();
        engine.Evaluate("(017777777777777777777777).toString()").AsString().Should().Be("147573952589676410000");
    }

    /// <summary>
    /// One source text of a number denotes one double, and a radix literal is a second spelling of the
    /// digits <c>parseInt</c> reads in the same radix.
    /// </summary>
    [Test]
    public void AWideRadixLiteralAgreesWithParseInt()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (0x1F49E9EE4C1BCE961 === parseInt('1F49E9EE4C1BCE961', 16))
            && (0x1F49E9EE4C1BCE961 === Number('36073444770624366945'))
            && (0o1777777777777777777777777 === parseInt('1777777777777777777777777', 8))
            && (0b1111110101010110111011001100011110100011000000100101101011000110110000
                  === parseInt('1111110101010110111011001100011110100011000000100101101011000110110000', 2))
            """).AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The same exact oracle over random radix literals: whatever the engine holds must be the double
    /// nearest the integer the digits denote, decided in exact integer arithmetic.
    /// </summary>
    [TestCase(2, 70)]
    [TestCase(2, 200)]
    [TestCase(8, 25)]
    [TestCase(8, 90)]
    [TestCase(16, 17)]
    [TestCase(16, 20)]
    [TestCase(16, 60)]
    public void EveryWideRadixLiteralRoundsToNearest(int radix, int length)
    {
        const string Alphabet = "0123456789abcdef";

        var engine = new Engine();
        var random = new Random(20260901 + radix * 1000 + length);
        var prefix = radix switch { 2 => "0b", 8 => "0o", _ => "0x" };
        var failures = 0;
        var first = "";

        for (var i = 0; i < 500; i++)
        {
            var builder = new StringBuilder(length);
            builder.Append(Alphabet[1 + random.Next(radix - 1)]);
            for (var j = 1; j < length; j++)
            {
                builder.Append(Alphabet[random.Next(radix)]);
            }

            var source = prefix + builder;
            var exact = BigInteger.Zero;
            foreach (var c in builder.ToString())
            {
                exact = exact * radix + Alphabet.IndexOf(c);
            }

            if (ParseIntPrecisionTests.IsNearestDouble(exact, engine.Evaluate(source).AsNumber()))
            {
                continue;
            }

            failures++;
            if (first.Length == 0)
            {
                first = source;
            }
        }

        failures.Should().Be(0, $"every literal must hold the double nearest it; first miss was {first}");
    }

    // Every expected string is the shortest round-tripping decimal of the double nearest the literal's
    // mathematical value. These are the spellings the scanner hands to double.Parse: a fraction, an
    // exponent, or more digits than its ulong accumulator holds.
    [TestCase("28643790.0509245228", "28643790.05092452")]
    [TestCase("414404623.816719085", "414404623.8167191")]
    [TestCase("123456789.012345678", "123456789.01234567")]
    [TestCase("1.95232124646081910e-200", "1.952321246460819e-200")]
    [TestCase("4.21015488674265630e+200", "4.2101548867426566e+200")]
    [TestCase("5.14083278005553983e-200", "5.1408327800555395e-200")]
    [TestCase("1898787416396742135391", "1.8987874163967423e+21")]
    [TestCase("5530464618918194971810", "5.530464618918195e+21")]
    [TestCase("12345678901234567890.0", "12345678901234567000")]
    [TestCase("1.2345678901234567e19", "12345678901234567000")]
    // The Number.MIN_VALUE region, where a double.Parse that rounds twice is wrong about half the time.
    [TestCase("3.32605247755496039e-311", "3.326052477555e-311")]
    [TestCase("5e-324", "5e-324")]
    [TestCase("1e-323", "1e-323")]
    [TestCase("4.9406564584124654e-324", "5e-324")]
    [TestCase("2.4703282292062327e-324", "0")]
    [TestCase("2.4703282292062328e-324", "5e-324")]
    [TestCase("2.2250738585072011e-308", "2.225073858507201e-308")]
    // Both ends of the representable range, where a value saturates to an infinity or to a zero.
    [TestCase("1.7976931348623157e308", "1.7976931348623157e+308")]
    [TestCase("1.7976931348623159e308", "Infinity")]
    [TestCase("1e309", "Infinity")]
    [TestCase("1e999", "Infinity")]
    [TestCase("1e-400", "0")]
    [TestCase("0.000000000000000000000000000001", "1e-30")]
    // The boundary spellings: a leading point, a trailing point, a bare exponent, and separators in
    // both the significand and the exponent.
    [TestCase(".5", "0.5")]
    [TestCase("5.", "5")]
    [TestCase("5e3", "5000")]
    [TestCase("0.1", "0.1")]
    [TestCase("1e23", "1e+23")]
    [TestCase("9007199254740993", "9007199254740992")]
    [TestCase("1_000.000_1", "1000.0001")]
    [TestCase("1.234_5e1_0", "12345000000")]
    // The exact midpoint between 1 and the next double is a tie and rounds to even; one more digit
    // anywhere past it, however far out, is no longer a tie.
    [TestCase("1.00000000000000011102230246251565404236316680908203125", "1")]
    [TestCase("1.000000000000000111022302462515654042363166809082031251", "1.0000000000000002")]
    public void DecimalLiteralHoldsTheNearestDouble(string literal, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"({literal}).toString()").AsString().Should().Be(expected);
    }

    /// <summary>
    /// The invariant the whole family restores: one source text of a number denotes one double, whichever
    /// of the routes to a Number reads it.
    /// </summary>
    [TestCase("28643790.0509245228")]
    [TestCase("1.95232124646081910e-200")]
    [TestCase("4.21015488674265630e+200")]
    [TestCase("1898787416396742135391")]
    [TestCase("3.32605247755496039e-311")]
    [TestCase("2.4703282292062328e-324")]
    [TestCase("2.2250738585072011e-308")]
    [TestCase("1.2345678901234567e19")]
    [TestCase("0.1")]
    [TestCase("1e23")]
    public void ALiteralDenotesWhatEveryOtherRouteReads(string text)
    {
        var engine = new Engine();
        engine.Evaluate($@"
            ({text} === Number('{text}'))
            && ({text} === parseFloat('{text}'))
            && ({text} === JSON.parse('{text}'))
            ").AsBoolean().Should().BeTrue(text);
    }

    [Test]
    public void AFractionalLiteralReadsTheSameThroughEveryReader()
    {
        var engine = new Engine();
        engine.Evaluate("Object.keys({ 28643790.0509245228: 1 })[0]").AsString().Should().Be("28643790.05092452");
        engine.Evaluate("(function () { var a = 0; a += 28643790.0509245228; return a.toString(); })()")
            .AsString().Should().Be("28643790.05092452");
    }

    /// <summary>
    /// The same exact oracle the string lanes are held to, over the literal reader: whatever the engine
    /// holds must be the double nearest the literal's mathematical value, decided in exact integer
    /// arithmetic rather than by a second floating-point parse.
    /// </summary>
    [TestCase(LiteralShape.Fraction)]
    [TestCase(LiteralShape.PositiveExponent)]
    [TestCase(LiteralShape.NegativeExponent)]
    [TestCase(LiteralShape.Subnormal)]
    [TestCase(LiteralShape.WideWholeNumber)]
    [TestCase(LiteralShape.ShortFraction)]
    public void EveryDecimalLiteralRoundsToNearest(LiteralShape shape)
    {
        var engine = new Engine();
        var random = new Random(20260901);

        for (var i = 0; i < 500; i++)
        {
            var text = Generate(random, shape);
            var held = engine.Evaluate(text).AsNumber();

            StringToNumberPrecisionTests.IsNearestDouble(text, held)
                .Should().BeTrue($"the literal {text} must hold the double nearest it");
        }
    }

    public enum LiteralShape
    {
        Fraction,
        PositiveExponent,
        NegativeExponent,
        Subnormal,
        WideWholeNumber,
        ShortFraction,
    }

    private static string Generate(Random random, LiteralShape shape)
    {
        var length = shape switch
        {
            LiteralShape.WideWholeNumber => 22,
            LiteralShape.ShortFraction => 6,
            _ => 18,
        };

        var digits = new StringBuilder(length);
        digits.Append((char) ('1' + random.Next(9)));
        for (var i = 1; i < length; i++)
        {
            digits.Append((char) ('0' + random.Next(10)));
        }

        var text = digits.ToString();
        return shape switch
        {
            LiteralShape.WideWholeNumber => text,
            LiteralShape.PositiveExponent => Scientific(text, "+200"),
            LiteralShape.NegativeExponent => Scientific(text, "-200"),
            LiteralShape.Subnormal => Scientific(text, "-" + (310 + random.Next(11)).ToString(CultureInfo.InvariantCulture)),
            _ => text.Substring(0, text.Length / 2) + "." + text.Substring(text.Length / 2),
        };
    }

    private static string Scientific(string digits, string exponent)
        => digits.Substring(0, 1) + "." + digits.Substring(1) + "e" + exponent;

    /// <summary>
    /// Nothing below the affected octave may move: the signed conversion the scanner reaches there is
    /// correctly rounded on every runtime, so the engine must still agree with it exactly.
    /// </summary>
    [Test]
    public void LiteralsBelowTheTopUnsignedOctaveAreUntouched()
    {
        var engine = new Engine();
        var random = new Random(20260901);
        var buffer = new byte[8];

        for (var i = 0; i < 500; i++)
        {
            random.NextBytes(buffer);
            var exact = BitConverter.ToInt64(buffer, 0) & 0x7FFFFFFFFFFFFFFF;
            var source = exact.ToString(CultureInfo.InvariantCulture);
            engine.Evaluate(source).AsNumber().Should().Be(exact, source);
        }
    }

    /// <summary>
    /// The literal-exponent scan stops accumulating past a cap so that a long run of exponent digits cannot
    /// overflow its accumulator. At the old cap of a million an eight-digit exponent lost its low digits, and
    /// a value whose digit count compensated the exponent back into range came out as an infinity or a zero
    /// instead of the double it denotes. https://github.com/sebastienros/jint/issues/3584
    /// </summary>
    [Test]
    public void AnExponentPastSevenDigitsKeepsEveryDigit()
    {
        // 0.(ten million zeros)1 x 10^10000308 = 10^307: the only shape that reaches an eight-digit exponent
        // with a finite, non-zero value, and cheap to scan because every digit the scanner skips is a zero.
        var text = "0." + new string('0', 10_000_000) + "1e10000308";
        NumberParser.TryParseDouble(text.AsSpan(), out var parsed).Should().BeTrue();
        parsed.Should().Be(1e307);

        var engine = new Engine();
        engine.SetValue("text", text);
        engine.Evaluate("Number(text)").AsNumber().Should().Be(1e307);
        engine.Evaluate("parseFloat(text)").AsNumber().Should().Be(1e307);
        engine.Evaluate("JSON.parse(text)").AsNumber().Should().Be(1e307);
    }

    /// <summary>
    /// The wider cap still cannot overflow: an exponent longer than the accumulator holds is decided by its
    /// sign alone, because no digit string a machine can hold compensates it.
    /// </summary>
    [TestCase("1e123456789012345678901234567890", double.PositiveInfinity)]
    [TestCase("1e-123456789012345678901234567890", 0.0)]
    [TestCase("1e99999999999999999", double.PositiveInfinity)]
    [TestCase("1e-99999999999999999", 0.0)]
    public void AnExponentLongerThanTheAccumulatorIsDecidedByItsSign(string text, double expected)
    {
        NumberParser.TryParseDouble(text.AsSpan(), out var parsed).Should().BeTrue();
        parsed.Should().Be(expected);
    }
}
