namespace Jint.Tests.Runtime;

public class GlobalTests
{
    [Fact]
    public void UnescapeAtEndOfString()
    {
        var e = new Engine();

        e.Evaluate("unescape('%40');").AsString().Should().Be("@");
        e.Evaluate("unescape('%40_');").AsString().Should().Be("@_");
        e.Evaluate("unescape('%40%40');").AsString().Should().Be("@@");
        e.Evaluate("unescape('%u0040');").AsString().Should().Be("@");
        e.Evaluate("unescape('%u0040%u0040');").AsString().Should().Be("@@");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-parseint-string-radix — steps 4-5 remove the leading sign from S
    /// before step 10 tests S for a "0x"/"0X" prefix, so a signed hexadecimal string is hexadecimal.
    /// Expectations checked against node v24.
    /// </summary>
    [Theory]
    [InlineData("parseInt('  -0X10 ')", -16d)]
    [InlineData("parseInt('-0x10')", -16d)]
    [InlineData("parseInt('+0x10')", 16d)]
    [InlineData("parseInt('  +0X10  ')", 16d)]
    [InlineData("parseInt('-0X1f')", -31d)]
    [InlineData("parseInt('-0x10', 16)", -16d)]
    [InlineData("parseInt('+0x10', 16)", 16d)]
    [InlineData("parseInt('-0X10', 0)", -16d)]
    [InlineData("parseInt('-0x7ffffffff')", -34359738367d)]
    // radix 36 is not 16, so stripPrefix is false and "0Xz" is read as a base-36 numeral
    [InlineData("parseInt('-0Xz', 36)", -1223d)]
    // the unsigned path was always right, and a signed decimal never went near the prefix test
    [InlineData("parseInt('0x10')", 16d)]
    [InlineData("parseInt('-10')", -10d)]
    public void ParseIntStripsTheSignBeforeTestingForAHexPrefix(string source, double expected)
    {
        new Engine().Evaluate(source).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-parseint-string-radix step 13 — an empty Z is NaN, whether the
    /// string ran out after the sign, after the stripped prefix, or on a digit the radix rejects.
    /// </summary>
    [Theory]
    [InlineData("parseInt('-0xg')")]
    [InlineData("parseInt('-0x', 16)")]
    [InlineData("parseInt('-0X')")]
    [InlineData("parseInt('-', 16)")]
    [InlineData("parseInt('-')")]
    [InlineData("parseInt('+')")]
    [InlineData("parseInt('0x')")]
    [InlineData("parseInt('- 0x10')")]
    [InlineData("parseInt('--0x10')")]
    public void ParseIntReturnsNaNWhenNoDigitFollowsTheSign(string source)
    {
        new Engine().Evaluate(source).AsNumber().Should().Be(double.NaN);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-parseint-string-radix step 15 — a zero magnitude carrying a
    /// recorded minus sign is -0, not +0.
    /// </summary>
    [Theory]
    [InlineData("parseInt('-0')")]
    [InlineData("parseInt('-0', 10)")]
    [InlineData("parseInt('-0.9')")]
    [InlineData("parseInt('-0b11')")]
    [InlineData("parseInt('-00x10')")]
    [InlineData("parseInt('-0x0')")]
    [InlineData("parseInt('-0x0', 16)")]
    [InlineData("parseInt('-0x10', 8)")]
    [InlineData("parseInt('-0x10', 10)")]
    public void ParseIntReturnsNegativeZeroForAZeroMagnitudeWithAMinusSign(string source)
    {
        var value = new Engine().Evaluate(source).AsNumber();
        value.Should().Be(0d);
        IsNegativeZero(value).Should().BeTrue("the spec returns -0 when mathInt is 0 and sign is -1");
    }

    [Theory]
    [InlineData("parseInt('0')")]
    [InlineData("parseInt('+0')")]
    [InlineData("parseInt('+0x0')")]
    [InlineData("parseInt('0.9')")]
    public void ParseIntReturnsPositiveZeroWithoutAMinusSign(string source)
    {
        var value = new Engine().Evaluate(source).AsNumber();
        value.Should().Be(0d);
        IsNegativeZero(value).Should().BeFalse();
    }

    private static bool IsNegativeZero(double value)
        => BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(-0d);
}