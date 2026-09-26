#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class TransformMatrixTests
{
    [TestCase("translate(10px)", "matrix(1, 0, 0, 1, 10, 0)")]
    [TestCase("translateY(20px)", "matrix(1, 0, 0, 1, 0, 20)")]
    [TestCase("scale(2,3)", "matrix(2, 0, 0, 3, 0, 0)")]
    [TestCase("scale(50%,200%)", "matrix(0.5, 0, 0, 2, 0, 0)")]
    [TestCase("translate(10px,20px) scale(2,3)", "matrix(2, 0, 0, 3, 10, 20)")]
    [TestCase("scale(2,3) translate(10px,20px)", "matrix(2, 0, 0, 3, 20, 60)")]
    [TestCase("matrix(2,3,4,5,6,7)", "matrix(2, 3, 4, 5, 6, 7)")]
    [TestCase("rotate(90deg)", "matrix(0, 1, -1, 0, 0, 0)")]
    [TestCase("rotate3d(0,0,0,45deg)", "matrix(1, 0, 0, 1, 0, 0)")]
    [TestCase("rotate3d(1e308,1e308,0,180deg)", "matrix3d(0, 1, 0, 0, 1, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1)")]
    [TestCase("translate3d(1px,2px,3px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 2, 3, 1)")]
    [TestCase("scaleZ(2)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1)")]
    [TestCase("rotateX(90deg)", "matrix3d(1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1)")]
    [TestCase("rotateY(90deg)", "matrix3d(0, 0, -1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1)")]
    [TestCase("skewX(45deg)", "matrix(1, 0, 1, 1, 0, 0)")]
    [TestCase("skewY(45deg)", "matrix(1, 1, 0, 1, 0, 0)")]
    [TestCase("skew(45deg,45deg)", "matrix(1, 1, 1, 1, 0, 0)")]
    [TestCase("perspective(100px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -0.01, 0, 0, 0, 1)")]
    [TestCase("perspective(0)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1, 0, 0, 0, 1)")]
    [TestCase("perspective(.25px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1, 0, 0, 0, 1)")]
    [TestCase("perspective(none)", "matrix(1, 0, 0, 1, 0, 0)")]
    [TestCase("scale(1)", "matrix(1, 0, 0, 1, 0, 0)")]
    [TestCase("matrix3d(1,0,0,0,0,1,0,0,0,0,1,0,2,3,0,1)", "matrix(1, 0, 0, 1, 2, 3)")]
    public void ColumnMajorPostmultiplicationAndStructuralTwoDSerialization(string source, string expected)
    {
        var list = CssPropertyParser.Parse("transform", source).Value.TransformList;
        CssTransformMatrix.Resolve(list, new(default)).Should().Be(expected);
    }

    [TestCase("translate(25%,50%)", "matrix(1, 0, 0, 1, 50, 40)")]
    [TestCase("translate(calc(25% + 10px), calc(50% - 2px))", "matrix(1, 0, 0, 1, 60, 38)")]
    [TestCase("translate(calc(25% + 10px), 10px) scale(2)", "matrix(2, 0, 0, 2, 60, 10)")]
    public void OptionalBasesResolvePercentagesPerAxisThroughTypedMath(string source, string expected)
    {
        var list = CssPropertyParser.Parse("transform", source).Value.TransformList;
        CssTransformMatrix.NeedsReferenceBox(list, new(default)).Should().BeTrue();
        CssTransformMatrix.Resolve(list, new(default), 200, 80).Should().Be(expected);
        Action missing = () => CssTransformMatrix.Resolve(list, new(default));
        missing.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:transform-reference-box");
    }

    [Test]
    public void PercentageDependentMathUsesTheSameTopLevelNormalizationAsAbsoluteMath()
    {
        var list = CssPropertyParser.Parse("transform", "translateX(calc(0px / 0% * 1px))").Value.TransformList;
        CssTransformMatrix.Resolve(list, new(default), 200, 80).Should().Be("matrix(1, 0, 0, 1, 0, 0)");
    }

    [Test]
    public void MatrixOverflowCannotSilentlyBecomeIdentity()
    {
        var list = CssPropertyParser.Parse("transform", "scale(1e308) scale(1e308)").Value.TransformList;
        Action resolve = () => CssTransformMatrix.Resolve(list, new(default));
        resolve.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:matrix-arithmetic");
    }

    [Test]
    public void LongListResolutionIsLinearAndPollsSharedCancellation()
    {
        var small = CssPropertyParser.Parse("transform", string.Concat(Enumerable.Repeat("translateX(1px) ", 1024))).Value.TransformList;
        var large = CssPropertyParser.Parse("transform", string.Concat(Enumerable.Repeat("translateX(1px) ", 2048))).Value.TransformList;
        var smallPolls = 0;
        var largePolls = 0;
        CssTransformMatrix.Resolve(small, new(default, () => smallPolls++)).Should().Be("matrix(1, 0, 0, 1, 1024, 0)");
        CssTransformMatrix.Resolve(large, new(default, () => largePolls++)).Should().Be("matrix(1, 0, 0, 1, 2048, 0)");
        largePolls.Should().BeLessThanOrEqualTo(smallPolls * 2 + 4);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        Action resolve = () => CssTransformMatrix.Resolve(large,
            new(cancellation.Token, () => { if (++polls == 10) cancellation.Cancel(); }));
        resolve.Should().Throw<OperationCanceledException>();
    }
}
