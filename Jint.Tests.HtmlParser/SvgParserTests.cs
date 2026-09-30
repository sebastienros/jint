using Jint.HtmlParser.Svg;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser;

public sealed class SvgParserTests
{
    [TestCase("  -1.25e+2 ", -125)]
    [TestCase(".5", .5)]
    [TestCase("1.", 1)]
    public void Numbers(string source, double expected) => SvgParser.ParseNumber(source).Should().Be(expected);

    [TestCase("")]
    [TestCase("+")]
    [TestCase("1e")]
    [TestCase("1e999")]
    [TestCase("NaN")]
    [TestCase("1 2")]
    public void InvalidNumbers(string source) => SvgParser.ParseNumber(source).Should().BeNull();

    [TestCase("5", 1)]
    [TestCase("5%", 2)]
    [TestCase("5em", 3)]
    [TestCase("5ex", 4)]
    [TestCase("5px", 5)]
    [TestCase("5cm", 6)]
    [TestCase("5mm", 7)]
    [TestCase("5in", 8)]
    [TestCase("5pt", 9)]
    [TestCase("5pc", 10)]
    public void Lengths(string source, int unit) => SvgParser.ParseLength(source).Should().Be(new SvgLength(5, (SvgLengthUnit) unit));

    [TestCase("1 px")]
    [TestCase("2rem")]
    [TestCase("3PX")]
    [TestCase("auto")]
    public void InvalidLengths(string source) => SvgParser.ParseLength(source).Should().BeNull();

    [Test]
    public void NumberPairsAndLists()
    {
        SvgParser.ParseNumberOptionalNumber("2").Should().Be((2d, 2d));
        SvgParser.ParseNumberOptionalNumber("2, 3").Should().Be((2d, 3d));
        SvgParser.ParseNumberOptionalNumber("2 3 4").Should().BeNull();
        SvgParser.ParseNumberOptionalNumber("2,").Should().BeNull();
        SvgParser.ParseNumberList("1, 2\t3\n4.").Should().Equal(1, 2, 3, 4);
        SvgParser.ParseNumberList("").Should().BeEmpty();
        SvgParser.ParseLengthList("1, 2px 3%").Should().Equal(new SvgLength(1), new SvgLength(2, SvgLengthUnit.Px), new SvgLength(3, SvgLengthUnit.Percentage));
        SvgParser.ParseLengthList("1px,").Should().BeNull();
    }

    [TestCase("1,")]
    [TestCase(",1")]
    [TestCase("1,,2")]
    [TestCase("1-2")]
    [TestCase("1.2.3")]
    [TestCase("1\f2")]
    public void InvalidLists(string source) => SvgParser.ParseNumberList(source).Should().BeNull();

    [Test]
    public void PointsAndViewBox()
    {
        SvgParser.ParsePoints("0,1 2,3").Should().Equal(new SvgPoint(0, 1), new SvgPoint(2, 3));
        SvgParser.ParsePoints("0 1 2").Should().BeNull();
        SvgParser.ParseViewBox("-1 -2,3,4").Should().Be(new SvgViewBox(-1, -2, 3, 4));
        SvgParser.ParseViewBox("0 0 -1 2").Should().BeNull();
        SvgParser.ParseViewBox("0 0 1 -2").Should().BeNull();
        SvgParser.ParseViewBox("0 0 0 0").Should().Be(new SvgViewBox());
        SvgParser.ParseViewBox("0 0 1").Should().BeNull();
        SvgParser.ParseViewBox("0 0 1 2 3").Should().BeNull();
    }

    [Test]
    public void AspectRatios()
    {
        SvgParser.ParsePreserveAspectRatio("xMidYMid").Should().Be(new SvgAspectRatio(6, 1));
        SvgParser.ParsePreserveAspectRatio("xMaxYMax slice").Should().Be(new SvgAspectRatio(10, 2));
        SvgParser.ParsePreserveAspectRatio("none meet").Should().Be(new SvgAspectRatio(1, 1));
        SvgParser.ParsePreserveAspectRatio("xmidymid").Should().BeNull();
        SvgParser.ParsePreserveAspectRatio("xMidYMid,slice").Should().BeNull();
        SvgParser.ParsePreserveAspectRatio("xMidYMid slice junk").Should().BeNull();
    }

    [Test]
    public void Transforms()
    {
        var list = SvgParser.ParseTransformList("matrix(1 0 0 1 2 3), translate(4,5) scale(2) rotate(90 1 2) skewX(5) skewY(6)");
        list.Should().HaveCount(6);
        list[1].Should().Be(new SvgTransform(SvgTransformKind.Translate, 4, 5));
        list[2].Should().Be(new SvgTransform(SvgTransformKind.Scale, 2, 2));
        list[3].Should().Be(new SvgTransform(SvgTransformKind.Rotate, 90, 1, 2));
        SvgParser.ParseTransformList("translate (1.)").Should().Equal(new SvgTransform(SvgTransformKind.Translate, 1));
        SvgParser.ParseTransformList("rotate(45)").Should().Equal(new SvgTransform(SvgTransformKind.Rotate, 45));
    }

    [TestCase("translate(1px)")]
    [TestCase("translate(1,)")]
    [TestCase("rotate(1 2)")]
    [TestCase("matrix(1 2 3)")]
    [TestCase("scale(1) garbage")]
    [TestCase("scale(1)translate(2)")]
    [TestCase("scale(1),")]
    [TestCase("skewx(2)")]
    [TestCase("translate(1-2)")]
    [TestCase("translate()")]
    [TestCase("scale(1")]
    public void InvalidTransformsAreEmpty(string source) => SvgParser.ParseTransformList(source).Should().BeEmpty();

    [Test]
    public void LongScansObserveTheCallerCheckpoint()
    {
        var calls = 0;
        void Stop() { if (++calls > 1) throw new OperationCanceledException(); }
        Action whitespace = () => SvgParser.ParseNumber(new string(' ', 10000), Stop);
        Action suffix = () => SvgParser.ParseLength("1" + new string('a', 10000), Stop);
        Action identifier = () => SvgParser.ParsePreserveAspectRatio(new string('a', 10000), Stop);
        Action digits = () => SvgParser.ParseNumber(new string('1', 10000), Stop);
        whitespace.Should().Throw<OperationCanceledException>();
        calls = 0;
        suffix.Should().Throw<OperationCanceledException>();
        calls = 0;
        identifier.Should().Throw<OperationCanceledException>();
        calls = 0;
        digits.Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void SharedNumberScanningPreservesCssTokenLimits()
    {
        foreach (var source in new[] { new string('1', 10000), "1e+3", "1.23" })
        {
            var checkpoints = 0;
            var tokenizer = new CssTokenizer(source, 2, null, default, checkpoint: () => checkpoints++);
            Action read = () => tokenizer.Next();
            read.Should().Throw<ParseLimitException>();
            checkpoints.Should().Be(2);
        }
    }
}
