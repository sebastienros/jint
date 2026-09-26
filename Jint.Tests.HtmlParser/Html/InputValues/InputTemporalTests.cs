#nullable enable
using System.Globalization;
using System.Numerics;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html.InputValues;

public class InputTemporalTests
{
    [TestCase(HtmlInputType.Date, "0001-01-01", -62135596800000d)]
    [TestCase(HtmlInputType.Date, "9999-12-31", 253402214400000d)]
    [TestCase(HtmlInputType.Date, "10000-01-01", 253402300800000d)]
    [TestCase(HtmlInputType.Date, "275760-09-14", 8640000086400000d)]
    [TestCase(HtmlInputType.Month, "275760-10", 3285489d)]
    [TestCase(HtmlInputType.Week, "275760-W38", 8640000172800000d)]
    [TestCase(HtmlInputType.Time, "12:34:56.123", 45296123d)]
    [TestCase(HtmlInputType.DateTimeLocal, "275760-09-13T00:00:00.001", 8640000000000001d)]
    public void NumericCoordinates(object kind, string source, double expected)
    {
        var type = (HtmlInputType) kind;
        HtmlInputTemporalSyntax.TryParseValue(type, source, out _).Should().BeTrue();
        HtmlInputTemporalSyntax.TryGetNumber(type, source, out var number).Should().Be(HtmlInputNumericParseResult.Success);
        number.Should().Be(expected);
    }

    [TestCase(HtmlInputType.Date, "1900-02-29")]
    [TestCase(HtmlInputType.Date, "0000-01-01")]
    [TestCase(HtmlInputType.Date, "001-01-01")]
    [TestCase(HtmlInputType.Date, "2000-1-01")]
    [TestCase(HtmlInputType.Date, "2000-01-01 ")]
    [TestCase(HtmlInputType.Date, "２０００-01-01")]
    [TestCase(HtmlInputType.Month, "2000-13")]
    [TestCase(HtmlInputType.Week, "2021-W53")]
    [TestCase(HtmlInputType.Week, "2020-w53")]
    [TestCase(HtmlInputType.Time, "24:00")]
    [TestCase(HtmlInputType.Time, "12:60")]
    [TestCase(HtmlInputType.Time, "12:00:60")]
    [TestCase(HtmlInputType.Time, "12:00:00.")]
    [TestCase(HtmlInputType.Time, "12:00:001")]
    [TestCase(HtmlInputType.Time, "12:00:00.1.2")]
    [TestCase(HtmlInputType.Time, "12:00:00.1234")]
    [TestCase(HtmlInputType.DateTimeLocal, "2000-01-01\t12:00")]
    public void InvalidGrammar(object kind, string source)
        => HtmlInputTemporalSyntax.TryParseValue((HtmlInputType) kind, source, out _).Should().BeFalse();

    [Test]
    public void FractionalComponentParserIsSeparateFromStoredSyntax()
    {
        HtmlInputTemporalSyntax.TryParseMicrosyntax(HtmlInputType.Time, "12:00:00.1234", out _).Should().BeTrue();
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.Time, "12:00:00.1234", out var value).Should().Be(HtmlInputNumericParseResult.Success);
        value.Should().Be(43200123.4);
        HtmlInputTemporalSyntax.Sanitize(HtmlInputType.DateTimeLocal, "02000-02-29 12:00:00.100").Should().Be("02000-02-29T12:00:00.1");
        HtmlInputTemporalSyntax.Sanitize(HtmlInputType.Time, "12:00:00.100").Should().Be("12:00:00.100");
    }

    [TestCase(HtmlInputType.Date, -.5, "1969-12-31")]
    [TestCase(HtmlInputType.Month, -.5, "1969-12")]
    [TestCase(HtmlInputType.Week, -.5, "1970-W01")]
    [TestCase(HtmlInputType.Time, -.5, "23:59:59.999")]
    [TestCase(HtmlInputType.DateTimeLocal, -.5, "1969-12-31T23:59:59.999")]
    [TestCase(HtmlInputType.Time, 1.5, "00:00:00.001")]
    [TestCase(HtmlInputType.Month, 1.5, "1970-02")]
    [TestCase(HtmlInputType.Date, -62135596800001d, "")]
    [TestCase(HtmlInputType.Date, -8640000000000000d, "")]
    [TestCase(HtmlInputType.Date, 8640000000000001d, "275760-09-13")]
    [TestCase(HtmlInputType.DateTimeLocal, 8640000000000001d, "275760-09-13T00:00:00.001")]
    [TestCase(HtmlInputType.Week, 8640000172799999d, "275760-W37")]
    [TestCase(HtmlInputType.Week, 8640000172800000d, "275760-W38")]
    [TestCase(HtmlInputType.Time, 2.7343337071894478e26, "10:54:10.944")]
    [TestCase(HtmlInputType.DateTimeLocal, 2.7343337071894478e26, "8664758583750640-12-03T10:54:10.944")]
    public void FullFiniteFormatting(object kind, double number, string expected)
        => HtmlInputTemporalSyntax.FormatNumber((HtmlInputType) kind, number).Should().Be(expected);

    [Test]
    public void GregorianAndIsoCyclesMatchIndependentBclSubset()
    {
        for (var year = 1; year <= 9999; year += 13)
        for (var month = 1; month <= 12; month++)
        {
            var reference = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var days = (long) (reference - DateTime.UnixEpoch).TotalDays;
            HtmlInputCalendar.DaysFromCivil(year, month, 1).Should().Be(days);
            var actual = HtmlInputCalendar.CivilFromDays(days);
            actual.Year.Should().Be(year);
            actual.Month.Should().Be(month);
            actual.Day.Should().Be(1);
            var (weekYear, week) = HtmlInputCalendar.WeekFromDays(days);
            weekYear.Year.Should().Be(ISOWeek.GetYear(reference));
            week.Should().Be(ISOWeek.GetWeekOfYear(reference));
        }
    }

    [Test]
    public void HugeLexicalYearsUseResiduesAndClassifyOverflow()
    {
        var leadingZeros = new string('0', 10000) + "2000-02-29";
        HtmlInputTemporalSyntax.TryParseValue(HtmlInputType.Date, leadingZeros, out _).Should().BeTrue();
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.Date, leadingZeros, out var number).Should().Be(HtmlInputNumericParseResult.Success);
        number.Should().Be(951782400000d);
        var enormous = "1" + new string('0', 10000) + "-02-29";
        HtmlInputTemporalSyntax.TryParseValue(HtmlInputType.Date, enormous, out _).Should().BeTrue();
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.Date, enormous, out _).Should().Be(HtmlInputNumericParseResult.NonFinite);
        using var cancel = new CancellationTokenSource();
        long units = 0;
        Action action = () => HtmlInputTemporalSyntax.TryParseValue(HtmlInputType.Date, enormous, out _,
            count => { units = count; if (count == 256) cancel.Cancel(); }, cancel.Token);
        action.Should().Throw<OperationCanceledException>();
        units.Should().Be(256);
    }

    [Test]
    public void ExtremeFiniteCalendarUsesExactBitsAndInvertibleCycles()
    {
        var integer = HtmlInputCalendar.FloorFinite(double.MaxValue);
        integer.Should().Be(((BigInteger.One << 53) - 1) << 971);
        var days = HtmlInputCalendar.FloorDivide(integer, 86400000);
        var civil = HtmlInputCalendar.CivilFromDays(days);
        HtmlInputCalendar.DaysFromCivil(civil.GetYear(), civil.Month, civil.Day).Should().Be(days);
        foreach (var type in new[] { HtmlInputType.Date, HtmlInputType.Month, HtmlInputType.Week, HtmlInputType.DateTimeLocal })
        {
            var text = HtmlInputTemporalSyntax.FormatNumber(type, double.MaxValue);
            text.Should().NotBeEmpty();
            HtmlInputTemporalSyntax.TryParseValue(type, text, out _).Should().BeTrue();
        }
        HtmlInputCalendar.FloorFinite(-double.Epsilon).Should().Be(-BigInteger.One);
    }

    [Test]
    public void MaximumFiniteComponentsHaveIndependentExactVectors()
    {
        // Oracle: Python integers, year-1 leap counts, 400-year cycles and January-based
        // month lengths. No March-based civil_from_days equations are used in the oracle.
        const string year = "5696662766614201866343980994479579548686152183704085128339310995661344631130045093093577562014445443639184765167256408801278441811790447403426907221696744254199515548160395277599861041408223739227916197624381909327129668191757649730107285371924492209348920206366721449167149513708399337212618128910";
        const string monthYear = "14980776123852630901210618644308696399839213960487083049909789733596438398335711563382463219397239847628371579459531872019526777240788681897372295558628126415504159214712606839687124174199110745339655709037927828525381936408601912347150713277676945689566485517012060264061514765076608323437533668848677073500";
        HtmlInputTemporalSyntax.FormatNumber(HtmlInputType.Date, double.MaxValue).Should().Be(year + "-04-10");
        HtmlInputTemporalSyntax.FormatNumber(HtmlInputType.DateTimeLocal, double.MaxValue).Should().Be(year + "-04-10T22:14:18.368");
        HtmlInputTemporalSyntax.FormatNumber(HtmlInputType.Week, double.MaxValue).Should().Be(year + "-W15");
        HtmlInputTemporalSyntax.FormatNumber(HtmlInputType.Month, double.MaxValue).Should().Be(monthYear + "-09");
    }

    [Test]
    public void ActualDateResultDistinguishesNullAndTimeClipFailure()
    {
        HtmlInputTemporalSyntax.GetDate(HtmlInputType.DateTimeLocal, "1970-01-01T00:00").HasDate.Should().BeFalse();
        HtmlInputTemporalSyntax.GetDate(HtmlInputType.Date, "invalid").HasDate.Should().BeFalse();
        var valid = HtmlInputTemporalSyntax.GetDate(HtmlInputType.Date, "0001-01-01");
        valid.HasDate.Should().BeTrue();
        valid.UtcMilliseconds.Should().Be(-62135596800000d);
        foreach (var (type, source) in new[] { (HtmlInputType.Date, "275760-09-14"),
            (HtmlInputType.Month, "275760-10"), (HtmlInputType.Week, "275760-W38"),
            (HtmlInputType.Date, "1" + new string('0', 10000) + "-01-01") })
        {
            var result = HtmlInputTemporalSyntax.GetDate(type, source);
            result.HasDate.Should().BeTrue();
            double.IsNaN(result.UtcMilliseconds).Should().BeTrue();
        }
        HtmlInputTemporalSyntax.FormatDate(HtmlInputType.Time, -8640000000000000d).Should().Be("00:00");
        HtmlInputTemporalSyntax.FormatDate(HtmlInputType.Month, 8640000000000000d).Should().Be("275760-09");
        HtmlInputTemporalSyntax.FormatDate(HtmlInputType.Date, -8640000000000000d).Should().BeEmpty();
    }

    [Test]
    public void LongFractionalAttributesRemainCancellableAndNumericallyBounded()
    {
        var source = "12:00:00." + new string('0', 10000) + "1";
        HtmlInputTemporalSyntax.TryParseMicrosyntax(HtmlInputType.Time, source, out var parsed).Should().BeTrue();
        parsed.Seconds.Should().Be(0);
        using var cancellation = new CancellationTokenSource();
        long units = 0;
        Action action = () => HtmlInputTemporalSyntax.TryParseMicrosyntax(HtmlInputType.Time, source, out _,
            count => { units = count; if (count == 256) cancellation.Cancel(); }, cancellation.Token);
        action.Should().Throw<OperationCanceledException>();
        units.Should().Be(256);
    }

    [TestCase("1969-12-31T23:59:59.999999999999", -1e-9)]
    [TestCase("1970-01-01T00:00:00.000000000001", 1e-9)]
    [TestCase("1969-12-31T23:59:59.999999999999999999999999", -1e-21)]
    public void FractionalCoordinatePublishesOnceAfterCalendarCombination(string source, double expected)
    {
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, source, out var value)
            .Should().Be(HtmlInputNumericParseResult.Success);
        value.Should().Be(expected);
    }

    [Test]
    public void FractionalTailPreservesSignedSubnormalsAndExactMidpointDirection()
    {
        var positive = "1970-01-01T00:00:00." + new string('0', 326) + "5";
        var negative = "1969-12-31T23:59:59." + new string('9', 326) + "5";
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, positive, out var value)
            .Should().Be(HtmlInputNumericParseResult.Success);
        BitConverter.DoubleToUInt64Bits(value).Should().Be(1);
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, negative, out value)
            .Should().Be(HtmlInputNumericParseResult.Success);
        BitConverter.DoubleToUInt64Bits(value).Should().Be(0x8000000000000001);

        // Exact seconds corresponding to 2^-1075 milliseconds: 5^1075 / 10^1078.
        var coefficient = BigInteger.Pow(5, 1075);
        var midpoint = coefficient.ToString(CultureInfo.InvariantCulture).PadLeft(1078, '0');
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.Time, "00:00:00." + midpoint + new string('0', 2000), out value);
        BitConverter.DoubleToUInt64Bits(value).Should().Be(0);
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.Time, "00:00:00." + midpoint + new string('0', 2000) + "1", out value);
        BitConverter.DoubleToUInt64Bits(value).Should().Be(1);
        var complement = (BigInteger.Pow(10, 1078) - coefficient).ToString(CultureInfo.InvariantCulture).PadLeft(1078, '0');
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, "1969-12-31T23:59:59." + complement, out value);
        BitConverter.DoubleToUInt64Bits(value).Should().Be(0); // HTML excludes either underflow zero sign.
        var below = (BigInteger.Pow(10, 3079) - coefficient * BigInteger.Pow(10, 2001) - 1)
            .ToString(CultureInfo.InvariantCulture).PadLeft(3079, '0');
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, "1969-12-31T23:59:59." + below, out value);
        BitConverter.DoubleToUInt64Bits(value).Should().Be(0x8000000000000001);
    }

    [Test]
    public void TinyDiscardedTailMovesAnIntegerCalendarMidpointUp()
    {
        const long midnight = 31494784780800000; // Independent year-1 Gregorian leap-count oracle.
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, "1000000-01-01T00:00:00.002", out var tie);
        tie.Should().Be((double) midnight);
        var source = "1000000-01-01T00:00:00.002" + new string('0', 10000) + "1";
        HtmlInputTemporalSyntax.TryGetNumber(HtmlInputType.DateTimeLocal, source, out var above);
        above.Should().Be(Math.BitIncrement((double) midnight));
    }

    [Test]
    public void NormalizationCopySharesTheScannerWorkCounterAndCancels()
    {
        var source = new string('0', 10000) + "2000-01-01 12:00";
        long units = 0;
        HtmlInputTemporalSyntax.Sanitize(HtmlInputType.DateTimeLocal, source, count => units = count, default)
            .Should().Be(new string('0', 10000) + "2000-01-01T12:00");
        units.Should().Be(source.Length * 2);
        using var cancel = new CancellationTokenSource();
        Action action = () => HtmlInputTemporalSyntax.Sanitize(HtmlInputType.DateTimeLocal, source,
            count => { units = count; if (count == 10240) cancel.Cancel(); }, cancel.Token);
        action.Should().Throw<OperationCanceledException>();
        units.Should().Be(10240); // Parsing completed; cancellation interrupts the actual year copy.
        var normalized = "02000-01-01T12:00";
        ReferenceEquals(HtmlInputTemporalSyntax.Sanitize(HtmlInputType.DateTimeLocal, normalized), normalized).Should().BeTrue();
    }
}
