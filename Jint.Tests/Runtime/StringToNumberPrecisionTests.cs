#nullable enable
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the double every lane that turns number text into a number produces. .NET Framework's
/// <c>double.Parse</c> is not IEEE correctly-rounded, so <c>parseFloat</c>, <c>Number</c> and
/// <c>JSON.parse</c> used to answer with a different number there than on .NET 10 for the same digits.
/// </summary>
public class StringToNumberPrecisionTests
{
    [Test]
    public void ParseFloatHoldsTheSameNumberAsEveryOtherLane()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (parseFloat('1995089590579635589') === Number('1995089590579635589'))
            && (parseFloat('1995089590579635589') === JSON.parse('1995089590579635589'))
            && (parseFloat('1995089590579635589') === 1995089590579635589)
            && (parseFloat('1995089590579635589') === 1995089590579635712)
            """).AsBoolean().Should().BeTrue();
    }

    // Every expected string is the shortest round-tripping decimal of the double nearest the text's
    // mathematical value, which is what Number::toString must print.
    [TestCase("1995089590579635589", "1995089590579635700")]
    [TestCase("12345678901234567890", "12345678901234567000")]
    [TestCase("123456789012345678901234567890", "1.2345678901234568e+29")]
    [TestCase("1e23", "1e+23")]
    [TestCase("9007199254740993", "9007199254740992")]
    [TestCase("0.1", "0.1")]
    [TestCase("2.2250738585072011e-308", "2.225073858507201e-308")]
    [TestCase("5e-324", "5e-324")]
    [TestCase("2.4703282292062327e-324", "0")]
    [TestCase("2.4703282292062328e-324", "5e-324")]
    [TestCase("1.7976931348623157e308", "1.7976931348623157e+308")]
    [TestCase("1.7976931348623159e308", "Infinity")]
    [TestCase("1e309", "Infinity")]
    [TestCase("1e-400", "0")]
    // The exact midpoint between 1 and the next double is a tie and rounds to even; one more digit
    // anywhere past it, however far out, is no longer a tie.
    [TestCase("1.00000000000000011102230246251565404236316680908203125", "1")]
    [TestCase("1.000000000000000111022302462515654042363166809082031251", "1.0000000000000002")]
    public void EveryLaneReadsTheNearestDouble(string text, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"parseFloat('{text}').toString()").AsString().Should().Be(expected, "parseFloat");
        engine.Evaluate($"Number('{text}').toString()").AsString().Should().Be(expected, "Number");
        engine.Evaluate($"JSON.parse('{text}').toString()").AsString().Should().Be(expected, "JSON.parse");
    }

    /// <summary>
    /// A digit run past the significant digits the parser reads may still not be dropped silently: it is
    /// what decides a value sitting exactly on a rounding boundary.
    /// </summary>
    [Test]
    public void DigitsPastTheReadWindowStillBreakATie()
    {
        var engine = new Engine();
        const string Midpoint = "1.00000000000000011102230246251565404236316680908203125";
        var padded = Midpoint + new string('0', 900) + "1";

        engine.Evaluate($"parseFloat('{padded}').toString()").AsString().Should().Be("1.0000000000000002");
        engine.Evaluate($"Number('{padded}').toString()").AsString().Should().Be("1.0000000000000002");
        engine.Evaluate($"JSON.parse('{padded}').toString()").AsString().Should().Be("1.0000000000000002");
    }

    [Test]
    public void OverflowSaturatesRatherThanFailing()
    {
        var engine = new Engine();
        engine.Evaluate("parseFloat('1e999')").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("Number('-1e999')").AsNumber().Should().Be(double.NegativeInfinity);
        // JSON.parse used to let .NET Framework's OverflowException out of the engine here.
        engine.Evaluate("JSON.parse('1e999')").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("JSON.parse('-1e999')").AsNumber().Should().Be(double.NegativeInfinity);
    }

    [Test]
    public void NegativeZeroKeepsItsSign()
    {
        var engine = new Engine();
        engine.Evaluate("1 / parseFloat('-0')").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / parseFloat('-0.0')").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / Number('-0.0')").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / JSON.parse('-0.0')").AsNumber().Should().Be(double.NegativeInfinity);
        engine.Evaluate("1 / parseFloat('0')").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("1 / Number('0.0')").AsNumber().Should().Be(double.PositiveInfinity);
    }

    /// <summary>
    /// A leading plus is part of StrDecimalLiteral, and the .NET Framework lane rejected it.
    /// </summary>
    [Test]
    public void ALeadingPlusIsAccepted()
    {
        var engine = new Engine();
        engine.Evaluate("Number('+1.5')").AsNumber().Should().Be(1.5);
        engine.Evaluate("parseFloat('+1.5')").AsNumber().Should().Be(1.5);
        engine.Evaluate("Number('+1.7976931348623157e308')").AsNumber().Should().Be(double.MaxValue);
    }

    [TestCase(".")]
    [TestCase("-")]
    [TestCase("+")]
    [TestCase("1e")]
    [TestCase("1e+")]
    [TestCase("e5")]
    [TestCase("1.2.3")]
    [TestCase("1 2")]
    public void TextThatIsNotANumberIsNaN(string text)
    {
        var engine = new Engine();
        engine.Evaluate($"Number('{text}')").AsNumber().Should().Be(double.NaN);
    }

    /// <summary>
    /// An independent oracle: whatever the engine ends up holding must be the double nearest the text's
    /// mathematical value, which exact integer arithmetic decides without redoing the conversion.
    /// </summary>
    [TestCase("parseFloat", NumberShape.WholeNumber)]
    [TestCase("parseFloat", NumberShape.Fraction)]
    [TestCase("parseFloat", NumberShape.Exponent)]
    [TestCase("parseFloat", NumberShape.Subnormal)]
    [TestCase("Number", NumberShape.WholeNumber)]
    [TestCase("Number", NumberShape.Fraction)]
    [TestCase("Number", NumberShape.Exponent)]
    [TestCase("Number", NumberShape.Subnormal)]
    [TestCase("JSON.parse", NumberShape.WholeNumber)]
    [TestCase("JSON.parse", NumberShape.Fraction)]
    [TestCase("JSON.parse", NumberShape.Exponent)]
    [TestCase("JSON.parse", NumberShape.Subnormal)]
    public void EveryLaneRoundsToNearest(string lane, NumberShape shape)
    {
        var engine = new Engine();
        var random = new Random(20260901);

        for (var i = 0; i < 500; i++)
        {
            var text = Generate(random, shape);
            var call = $"{lane}('{text}')";
            var held = engine.Evaluate(call).AsNumber();

            IsNearestDouble(text, held).Should().BeTrue($"{call} must hold the double nearest {text}, not {Bits(held)}");
        }
    }

    public enum NumberShape
    {
        WholeNumber,
        Fraction,
        Exponent,
        Subnormal,
    }

    private static string Generate(Random random, NumberShape shape)
    {
        var length = shape == NumberShape.WholeNumber ? 16 + random.Next(8) : 17 + random.Next(4);
        var digits = new StringBuilder(length);
        digits.Append((char) ('1' + random.Next(9)));
        for (var i = 1; i < length; i++)
        {
            digits.Append((char) ('0' + random.Next(10)));
        }

        var text = digits.ToString();
        var split = 1 + random.Next(text.Length - 1);
        return shape switch
        {
            NumberShape.WholeNumber => text,
            NumberShape.Fraction => text.Substring(0, split) + "." + text.Substring(split),
            NumberShape.Exponent => Scientific(text, (random.Next(2) == 0 ? "+" : "-") + (1 + random.Next(250)).ToString(CultureInfo.InvariantCulture)),
            _ => Scientific(text, "-" + (305 + random.Next(25)).ToString(CultureInfo.InvariantCulture)),
        };
    }

    private static string Scientific(string digits, string exponent)
        => digits.Substring(0, 1) + "." + digits.Substring(1) + "e" + exponent;

    private static string Bits(double value)
        => "0x" + BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>
    /// Decides, in exact integer arithmetic, whether <paramref name="held"/> is the double nearest the
    /// value <paramref name="text"/> denotes: no adjacent double may be closer, and a value exactly
    /// between two of them must have landed on the one with an even significand.
    /// </summary>
    internal static bool IsNearestDouble(string text, double held)
    {
        if (double.IsNaN(held))
        {
            return false;
        }

        var (significand, exponent) = SplitDecimal(text);
        if (significand.IsZero)
        {
            return held == 0;
        }

        if ((significand.Sign < 0) != (BitConverter.DoubleToInt64Bits(held) < 0))
        {
            return false;
        }

        // The value is numerator / denominator, exactly.
        var magnitude = BigInteger.Abs(significand);
        var numerator = exponent >= 0 ? magnitude * BigInteger.Pow(10, exponent) : magnitude;
        var denominator = exponent >= 0 ? BigInteger.One : BigInteger.Pow(10, -exponent);

        // Everything at or above the midpoint between the largest double and 2^1024 is an infinity.
        if (numerator >= ((BigInteger.One << 1024) - (BigInteger.One << 970)) * denominator)
        {
            return double.IsInfinity(held);
        }

        if (double.IsInfinity(held))
        {
            return false;
        }

        // The candidate and its two neighbours, all three lifted onto the smallest of their exponents so
        // that one comparison scale serves all of them.
        var candidateBits = BitConverter.DoubleToInt64Bits(System.Math.Abs(held));
        var candidates = new[]
        {
            Decompose(candidateBits),
            Decompose(candidateBits == 0 ? 0 : candidateBits - 1),
            Decompose(candidateBits + 1),
        };

        var floor = System.Math.Min(candidates[0].Exponent, System.Math.Min(candidates[1].Exponent, candidates[2].Exponent));
        var scale = floor < 0 ? BigInteger.Pow(2, -floor) : BigInteger.One;
        var lift = floor >= 0 ? BigInteger.Pow(2, floor) : BigInteger.One;
        var target = numerator * scale;

        var distances = new BigInteger[3];
        for (var i = 0; i < candidates.Length; i++)
        {
            var (mantissa, mantissaExponent) = candidates[i];
            var aligned = mantissa * BigInteger.Pow(2, mantissaExponent - floor);
            distances[i] = BigInteger.Abs(target - aligned * lift * denominator);
        }

        if (distances[0] > distances[1] || distances[0] > distances[2])
        {
            return false;
        }

        // A tie must have gone to the candidate with an even significand.
        var tie = distances[0] == distances[1] || distances[0] == distances[2];
        return !tie || (candidateBits & 1) == 0;
    }

    private static (BigInteger Mantissa, int Exponent) Decompose(long bits)
    {
        var biased = (int) ((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        return biased == 0
            ? (new BigInteger(fraction), -1074)
            : (new BigInteger(fraction | (1L << 52)), biased - 1075);
    }

    private static (BigInteger Significand, int Exponent) SplitDecimal(string text)
    {
        var digits = new StringBuilder(text.Length);
        var exponent = 0;
        var negative = false;
        var i = 0;

        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }

        for (; i < text.Length && char.IsDigit(text[i]); i++)
        {
            digits.Append(text[i]);
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            for (; i < text.Length && char.IsDigit(text[i]); i++)
            {
                digits.Append(text[i]);
                exponent--;
            }
        }

        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
        {
            i++;
            var exponentNegative = text[i] == '-';
            if (text[i] == '+' || text[i] == '-')
            {
                i++;
            }

            exponent += int.Parse(text.Substring(i), CultureInfo.InvariantCulture) * (exponentNegative ? -1 : 1);
        }

        var value = digits.Length == 0 ? BigInteger.Zero : BigInteger.Parse(digits.ToString(), CultureInfo.InvariantCulture);
        return (negative ? -value : value, exponent);
    }
}
