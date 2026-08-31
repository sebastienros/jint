using System.Globalization;
using System.Numerics;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the double an integer literal holds when its mathematical value lands in
/// <c>[2^63, 2^64)</c>. The scanner reads those digits into a <see cref="ulong"/> and converts,
/// and that conversion double-rounds on .NET Framework and on .NET 8 and below, so the same source
/// used to hold two different numbers depending only on which target framework was loaded.
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
}
