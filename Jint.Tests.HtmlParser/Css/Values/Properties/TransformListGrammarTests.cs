#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class TransformListGrammarTests
{
    [TestCase("matrix(1,0,0,1,2,3)", "matrix(1, 0, 0, 1, 2, 3)")]
    [TestCase("matrix3d(1,0,0,0,0,1,0,0,0,0,1,0,2,3,4,1)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 2, 3, 4, 1)")]
    [TestCase("translate(10px)", "translate(10px)")]
    [TestCase("translate(10%,0)", "translate(10%, 0px)")]
    [TestCase("TRANSLATEX(10px)", "translateX(10px)")]
    [TestCase("translateY(-20%)", "translateY(-20%)")]
    [TestCase("translateZ(3rem)", "translateZ(3rem)")]
    [TestCase("translate3d(0,20%,3px)", "translate3d(0px, 20%, 3px)")]
    [TestCase("scale(50%)", "scale(0.5)")]
    [TestCase("scale(50%, 200%)", "scale(0.5, 2)")]
    [TestCase("scaleX(calc(50% * 2))", "scaleX(calc(1))")]
    [TestCase("scaleY(-1)", "scaleY(-1)")]
    [TestCase("scaleZ(2)", "scaleZ(2)")]
    [TestCase("scale3d(1,2,3)", "scale3d(1, 2, 3)")]
    [TestCase("rotate(0)", "rotate(0deg)")]
    [TestCase("rotateX(-0)", "rotateX(0deg)")]
    [TestCase("rotateY(.5turn)", "rotateY(0.5turn)")]
    [TestCase("rotateZ(1rad)", "rotateZ(1rad)")]
    [TestCase("rotate3d(0,0,0,45deg)", "rotate3d(0, 0, 0, 45deg)")]
    [TestCase("skew(0,10deg)", "skew(0deg, 10deg)")]
    [TestCase("skewX(0)", "skewX(0deg)")]
    [TestCase("skewY(0)", "skewY(0deg)")]
    [TestCase("perspective(none)", "perspective(none)")]
    [TestCase("perspective(.25px)", "perspective(0.25px)")]
    [TestCase("translate(10px) scale(2)", "translate(10px) scale(2)")]
    public void SpecifiedSerializationKeepsFunctionIdentityAndOnlyAuthoredArguments(string source, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var result = CssPropertyParser.Parse("transform", source, context);
            result.Status.Should().Be(CssPropertyStatus.Valid);
            result.Value.Kind.Should().Be(CssPropertyValueKind.TransformList);
            result.Value.Serialize().Should().Be(expected);
            CssPropertyParser.Parse("transform", expected, context).Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("translate()")]
    [TestCase("translate(1px 2px)")]
    [TestCase("translate(1px,)")]
    [TestCase("translate(,1px)")]
    [TestCase("translate(1px,,2px)")]
    [TestCase("translateX(1px,2px)")]
    [TestCase("translate3d(1px,2px)")]
    [TestCase("translate3d(1px,2px,3%)")]
    [TestCase("translateZ(calc(1%))")]
    [TestCase("scale(1 2)")]
    [TestCase("scale3d(1,2)")]
    [TestCase("scale(1px)")]
    [TestCase("scale(calc(1 + 50%))")]
    [TestCase("rotate(1)")]
    [TestCase("rotate(calc(0))")]
    [TestCase("rotate3d(1,2,45deg)")]
    [TestCase("rotate3d(1%,0,0,45deg)")]
    [TestCase("skew(1deg,2deg,3deg)")]
    [TestCase("matrix(1,0,0,1,0)")]
    [TestCase("matrix(1,0,0,1,0px,0)")]
    [TestCase("matrix3d(1,0,0,1)")]
    [TestCase("perspective(-1px)")]
    [TestCase("perspective(-1e-999px)")]
    [TestCase("perspective(10%)")]
    [TestCase("perspective(1)")]
    [TestCase("none translate(1px)")]
    [TestCase("translate(1px), scale(2)")]
    [TestCase("translate(1px) bogus(2)")]
    [TestCase("translate(1px) scale(1px)")]
    public void InvalidWholeListPublishesNoValidPrefix(string source)
    {
        var result = CssPropertyParser.Parse("transform", source);
        result.Status.Should().Be(CssPropertyStatus.Invalid);
        Action read = () => _ = result.Value;
        read.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void FunctionsAndArgumentsHaveExactSpansAndOwnedCollections()
    {
        var list = CssPropertyParser.Parse("transform", "  translateX(-1px) scale(2, 3)  ").Value.TransformList;
        list.Span.Start.Should().Be(2);
        list.Span.Length.Should().Be(28);
        list[0].Span.Start.Should().Be(2);
        list[0].Span.Length.Should().Be(16);
        list[0].Arguments[0].Span.Start.Should().Be(13);
        list[0].Arguments[0].Span.Length.Should().Be(4);
        var original = list[0].Arguments[0];
        var arguments = new[] { original };
        var function = new CssTransformFunction(list[0].Descriptor, arguments, list[0].Span);
        arguments[0] = CssTransformParser.Constant(99, CssUnit.Px, default, new(default));
        function.Arguments[0].Should().BeSameAs(original);
        var functions = new[] { function };
        var owned = new CssTransformList(functions, list.Span, new(default));
        functions[0] = list[1];
        owned[0].Should().BeSameAs(function);
    }

    [Test]
    public void NoneWideKeywordsAndReferencesUseNormalPropertyMetadata()
    {
        CssPropertyParser.Parse("transform", "none").Value.Kind.Should().Be(CssPropertyValueKind.Keyword);
        CssPropertyParser.Parse("transform", "inherit").Value.Text.Should().Be("inherit");
        CssPropertyParser.Parse("transform", "var(--t)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse("transform", "none", CssDeclarationContext.FontFace).Status
            .Should().Be(CssPropertyStatus.UnimplementedGrammar);
        CssPropertyRegistry.Completed["transform"].Inherited.Should().BeFalse();
        CssPropertyRegistry.Completed["transform"].InitialValue.Should().Be("none");
        CssPropertyParser.Parse("rotate", "0").Status.Should().Be(CssPropertyStatus.Invalid);
        foreach (var box in new[] { "content-box", "border-box", "fill-box", "stroke-box", "view-box" })
            CssPropertyParser.Parse("transform-box", box).Value.Text.Should().Be(box);
        CssPropertyParser.Parse("transform-box", "bogus").Status.Should().Be(CssPropertyStatus.Invalid);
    }

    [Test]
    public void LongListParsingPollsSharedWorkAndCancelsBeforePublication()
    {
        var input = CssReferenceInput.Parse(string.Concat(Enumerable.Repeat("translateX(1px) ", 8192)), null, default);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 12) cancellation.Cancel(); });
        Action parse = () => CssPropertyParser.Parse("transform", input, CssDeclarationContext.Style, work);
        parse.Should().Throw<OperationCanceledException>();
    }
}
