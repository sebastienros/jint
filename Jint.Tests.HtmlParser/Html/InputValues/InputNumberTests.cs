#nullable enable
using System.Globalization;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html.InputValues;

public class InputNumberTests
{
    [TestCase(".1", .1)]
    [TestCase("-0", 0)]
    [TestCase("0001E+2", 100)]
    [TestCase("9007199254740993", 9007199254740992d)]
    [TestCase("2e-324", 0)]
    public void StrictValues(string text, double expected)
    {
        HtmlInputNumberSyntax.TryParseValue(text, out var value).Should().BeTrue();
        value.Should().Be(expected);
        if (value == 0) BitConverter.DoubleToInt64Bits(value).Should().Be(0);
    }

    [TestCase("")]
    [TestCase("+1")]
    [TestCase(" 1")]
    [TestCase("1 ")]
    [TestCase("1.")]
    [TestCase("1e+")]
    [TestCase("1x")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("0x1")]
    [TestCase("١")]
    [TestCase("2e308")]
    public void InvalidStoredValues(string text) => HtmlInputNumberSyntax.TryParseValue(text, out _).Should().BeFalse();

    [TestCase("  +1.5x", 1.5)]
    [TestCase("1.", 1)]
    [TestCase("1e+", 1)]
    [TestCase("1.e2", 100)]
    [TestCase("\t\r\n\f -0x", 0)]
    public void PrefixParsing(string text, double expected)
    {
        HtmlInputNumberSyntax.TryParsePrefix(text, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [TestCase(".")]
    [TestCase("+x")]
    [TestCase("\u00a01")]
    [TestCase("2e308junk")]
    public void InvalidPrefixes(string text) => HtmlInputNumberSyntax.TryParsePrefix(text, out _).Should().BeFalse();

    [TestCase(0d, "0")]
    [TestCase(-0d, "0")]
    [TestCase(.1, "0.1")]
    [TestCase(1e-6, "0.000001")]
    [TestCase(1e-7, "1e-7")]
    [TestCase(1e20, "100000000000000000000")]
    [TestCase(1e21, "1e+21")]
    [TestCase(1000000000000000128d, "1000000000000000100")]
    [TestCase(9007199254740991d, "9007199254740991")]
    [TestCase(9007199254740992d, "9007199254740992")]
    [TestCase(9007199254740994d, "9007199254740994")]
    [TestCase(double.Epsilon, "5e-324")]
    [TestCase(2.2250738585072014e-308, "2.2250738585072014e-308")]
    [TestCase(double.MaxValue, "1.7976931348623157e+308")]
    [TestCase(.00012345678901234567, "0.00012345678901234567")]
    // Independent V8 vectors that exposed the net10 R primitive's shorter, non-round-tripping digits.
    [TestCase(4.1045368012983762e-289, "4.1045368012983762e-289")]
    [TestCase(2.9802322387695312e-8, "2.9802322387695312e-8")]
    public void EcmaLayout(double value, string expected)
    {
        HtmlInputNumberFormatter.FormatFinite(value).Should().Be(expected);
        if (value != 0) HtmlInputNumberFormatter.FormatFinite(-value).Should().Be("-" + expected);
        Span<char> buffer = stackalloc char[32];
        var length = HtmlInputNumberFormatter.WriteFinite(value, buffer);
        new string(buffer[..length]).Should().Be(expected);
    }

    [Test]
    public void BinaryPowersAndNeighborsRoundTrip()
    {
        for (var exponent = -1074; exponent < 1024; exponent++)
        {
            var power = Math.ScaleB(1, exponent);
            foreach (var value in new[] { power, Math.BitDecrement(power), Math.BitIncrement(power) })
            {
                if (!double.IsFinite(value)) continue;
                var text = HtmlInputNumberFormatter.FormatFinite(value);
                BitConverter.DoubleToUInt64Bits(double.Parse(text, CultureInfo.InvariantCulture)).Should().Be(
                    BitConverter.DoubleToUInt64Bits(value), $"ECMA text {text}, R digits {value.ToString("R", CultureInfo.InvariantCulture)}, exponent {exponent}");
            }
        }
    }

    [Test]
    public void ScanningCancellationAndWorkAreDeterministic()
    {
        using var source = new CancellationTokenSource();
        long units = 0;
        Action action = () => HtmlInputNumberSyntax.TryParseValue(new string('0', 100000), out _,
            count => { units = count; if (count == 256) source.Cancel(); }, source.Token);
        action.Should().Throw<OperationCanceledException>();
        units.Should().Be(256);
        HtmlInputNumberSyntax.TryParseValue("0001e2", out _, checkpoint: count => units = count).Should().BeTrue();
        units.Should().Be(12);
    }

    [Test]
    public void CompactConversionKeepsStickyMidpointDirectionAndCancellingExponents()
    {
        // Exact midpoint between 1 and its next binary64; ties-even selects 1.
        const string midpoint = "1.00000000000000011102230246251565404236316680908203125";
        HtmlInputNumberSyntax.TryParseValue(midpoint + new string('0', 2000), out var tie).Should().BeTrue();
        tie.Should().Be(1);
        HtmlInputNumberSyntax.TryParseValue(midpoint + new string('0', 2000) + "1", out var above).Should().BeTrue();
        above.Should().Be(Math.BitIncrement(1));
        HtmlInputNumberSyntax.TryParseValue("0." + new string('0', 10000) + "1e10001", out var canceled).Should().BeTrue();
        canceled.Should().Be(1);
        HtmlInputNumberSyntax.TryParseValue("1" + new string('0', 10000) + "e-10000", out canceled).Should().BeTrue();
        canceled.Should().Be(1);
        HtmlInputNumberSyntax.TryParseValue("-000e" + new string('9', 10000), out var zero).Should().BeTrue();
        BitConverter.DoubleToInt64Bits(zero).Should().Be(0);
        HtmlInputNumberSyntax.TryParseValue("1e" + new string('9', 10000), out _).Should().BeFalse();
        HtmlInputNumberSyntax.TryParseValue("1e-" + new string('9', 10000), out zero).Should().BeTrue();
        zero.Should().Be(0);
        HtmlInputNumberSyntax.TryParseValue("2.4703282292062327e-324", out zero).Should().BeTrue();
        zero.Should().Be(0);
        HtmlInputNumberSyntax.TryParseValue("2.4703282292062328e-324", out var subnormal).Should().BeTrue();
        subnormal.Should().Be(double.Epsilon);
    }
}
