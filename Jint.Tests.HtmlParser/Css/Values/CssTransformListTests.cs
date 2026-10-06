using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Transforms 1 §13 / 2 §12, read the way Geometry's DOMMatrix string initializer needs:
// https://drafts.fxtf.org/geometry/#create-a-dommatrix-from-the-2d-dictionary.
public sealed class CssTransformListTests
{
    [Test]
    public void NoneIsAnEmptyList()
    {
        Parse("none").Should().BeEmpty();
        Parse("  NONE ").Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("none none")]
    [TestCase("translate(1px) none")]
    [TestCase("foo(1)")]
    [TestCase("translate(1px,)")]
    [TestCase("translate(1px 2px)")]
    [TestCase("translate(1px, 2px, 3px)")]
    [TestCase("translate(1)")]
    [TestCase("translate(10%)")]
    [TestCase("translate(1em)")]
    [TestCase("translate(1vw)")]
    [TestCase("translate(calc(1px + 1px))")]
    [TestCase("rotate(1)")]
    [TestCase("rotate(1px)")]
    [TestCase("scale(1px)")]
    [TestCase("matrix(1, 0, 0, 1, 0)")]
    [TestCase("matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0)")]
    [TestCase("perspective(-1px)")]
    [TestCase("inherit")]
    [TestCase("translate(1px);")]
    public void RefusesWhatTheGrammarOrGeometryRefuse(string source)
    {
        CssTransformList.TryParse(source, new CssValueWork(default), out _).Should().BeFalse();
    }

    [Test]
    public void NormalizesLengthsToPixels()
    {
        var functions = Parse("translate(1in, 2.54cm) translateX(10mm) translateY(4Q) translateZ(12pt) translate3d(1pc, 0, -3px)");
        functions.Select(static f => f.Kind).Should().Equal(
            CssTransformFunctionKind.Translate, CssTransformFunctionKind.TranslateX, CssTransformFunctionKind.TranslateY,
            CssTransformFunctionKind.TranslateZ, CssTransformFunctionKind.Translate3d);
        functions[0].Arguments.Should().Equal(96, 96);
        functions[1].Arguments[0].Should().BeApproximately(37.795, 0.001);
        functions[2].Arguments[0].Should().BeApproximately(3.7795, 0.0001);
        functions[3].Arguments.Should().Equal(16);
        functions[4].Arguments.Should().Equal(16, 0, -3);
    }

    [Test]
    public void FillsOmittedArguments()
    {
        Parse("translate(5px)")[0].Arguments.Should().Equal(5, 0);
        Parse("scale(2)")[0].Arguments.Should().Equal(2, 2);
        Parse("skew(10deg)")[0].Arguments.Should().Equal(10, 0);
    }

    [Test]
    public void NormalizesAnglesToDegrees()
    {
        var functions = Parse("rotate(0.5turn) rotateX(100grad) rotateY(0) skewX(1rad) rotate3d(0, 0, 1, 90deg)");
        functions[0].Arguments.Should().Equal(180);
        functions[1].Arguments.Should().Equal(90);
        functions[2].Arguments.Should().Equal(0);
        functions[3].Arguments[0].Should().BeApproximately(57.2958, 0.0001);
        functions[4].Arguments.Should().Equal(0, 0, 1, 90);
    }

    [Test]
    public void ScalesAcceptPercentages()
    {
        Parse("scale(50%, 200%)")[0].Arguments.Should().Equal(0.5, 2);
        Parse("scale3d(1, 2, 300%)")[0].Arguments.Should().Equal(1, 2, 3);
    }

    [Test]
    public void PerspectiveNoneIsInfinite()
    {
        Parse("perspective(none)")[0].Arguments.Should().Equal(double.PositiveInfinity);
        Parse("perspective(100px)")[0].Arguments.Should().Equal(100);
    }

    [Test]
    public void ReportsWhichFunctionsAreTwoDimensional()
    {
        var functions = Parse("matrix(1, 2, 3, 4, 5, 6) matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1) rotate(0) rotateZ(0) scaleZ(1)");
        functions.Select(static f => f.Is2D).Should().Equal(true, false, true, false, false);
        functions[0].Arguments.Should().Equal(1, 2, 3, 4, 5, 6);
    }

    [Test]
    public void FunctionNamesAreCaseInsensitive()
    {
        Parse("TRANSLATEx(1PX) RoTaTe(1DEG)").Select(static f => f.Kind)
            .Should().Equal(CssTransformFunctionKind.TranslateX, CssTransformFunctionKind.Rotate);
    }

    private static CssTransformFunction[] Parse(string source)
    {
        CssTransformList.TryParse(source, new CssValueWork(default), out var functions).Should().BeTrue(source);
        return functions;
    }
}
