#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class TransformPropertyGrammarTests
{
    [TestCase("translate", "0 -50%", "0px -50%")]
    [TestCase("translate", "-1px", "-1px")]
    [TestCase("translate", "10px 20px 30px", "10px 20px 30px")]
    [TestCase("translate", "10px 0px 0px", "10px")]
    [TestCase("translate", "0 0 0", "0px")]
    [TestCase("translate", "10% 0% 0px", "10% 0%")]
    [TestCase("translate", "calc(-2px + 1px)", "calc(-1px)")]
    [TestCase("rotate", "0deg", "0deg")]
    [TestCase("rotate", "-.5turn", "-0.5turn")]
    [TestCase("rotate", "z 45deg", "45deg")]
    [TestCase("rotate", "45deg X", "x 45deg")]
    [TestCase("rotate", "y 45deg", "y 45deg")]
    [TestCase("rotate", "0 0 2 45deg", "45deg")]
    [TestCase("rotate", "45deg 0 0 2", "45deg")]
    [TestCase("rotate", "0 0 0 45deg", "0 0 0 45deg")]
    [TestCase("rotate", "0 0 1 0deg", "0deg")]
    [TestCase("rotate", "0 1 0 0deg", "y 0deg")]
    [TestCase("rotate", "0 2 0 45deg", "y 45deg")]
    [TestCase("rotate", "-2 0 0 45deg", "x -45deg")]
    [TestCase("rotate", "0 0 -2 45deg", "-45deg")]
    [TestCase("rotate", ".5 1 -2 45deg", "0.5 1 -2 45deg")]
    [TestCase("rotate", "calc(1 + 1) 0 0 calc(45deg)", "x calc(45deg)")]
    [TestCase("scale", "-2", "-2")]
    [TestCase("scale", "-2 .5", "-2 0.5")]
    [TestCase("scale", "-2 .5 3", "-2 0.5 3")]
    [TestCase("scale", "50% -200% 100%", "0.5 -2")]
    [TestCase("scale", "100% 1 1", "1")]
    [TestCase("scale", "calc(50% * 2)", "calc(1)")]
    [TestCase("scale", "calc(-2 * .5)", "calc(-1)")]
    public void FiniteValuesSerializeCanonicallyInBothOrdinaryContexts(string name, string source, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var result = CssPropertyParser.Parse(name, source, context);
            result.Status.Should().Be(CssPropertyStatus.Valid);
            result.Value.Kind.Should().Be(CssPropertyValueKind.Transform);
            result.Value.Serialize().Should().Be(expected);
            CssPropertyParser.Parse(name, expected, context).Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("translate", "1px, 2px")]
    [TestCase("translate", "1px 2px 3%")]
    [TestCase("translate", "1px 2px calc(3%)")]
    [TestCase("translate", "1")]
    [TestCase("translate", "1deg")]
    [TestCase("translate", "1px 2px 3px 4px")]
    [TestCase("translate", "calc(1s)")]
    [TestCase("rotate", "0")]
    [TestCase("rotate", "+0")]
    [TestCase("rotate", "-0")]
    [TestCase("rotate", "x 0")]
    [TestCase("rotate", "0 x")]
    [TestCase("rotate", "0 0 1 0")]
    [TestCase("rotate", "0 1 0 0")]
    [TestCase("rotate", "0 1 2 3")]
    [TestCase("rotate", "calc(0)")]
    [TestCase("rotate", "1")]
    [TestCase("rotate", "1px")]
    [TestCase("rotate", "x y 45deg")]
    [TestCase("rotate", "1 2 45deg")]
    [TestCase("rotate", "1% 2 3 45deg")]
    [TestCase("rotate", "45deg 1 2 3 4")]
    [TestCase("rotate", "1, 2, 3, 45deg")]
    [TestCase("rotate", "calc(1px) x")]
    [TestCase("scale", "1px")]
    [TestCase("scale", "1, 2")]
    [TestCase("scale", "1 2 3 4")]
    [TestCase("scale", "calc(1deg)")]
    [TestCase("scale", "calc(1 + 50%)")]
    [TestCase("scale", "none 1")]
    public void DimensionMismatchesAndMalformedGroupsAreInvalid(string name, string source) =>
        CssPropertyParser.Parse(name, source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("translate")]
    [TestCase("rotate")]
    [TestCase("scale")]
    public void KeywordsReferencesAndDescriptorContextsStayAuthoritative(string name)
    {
        foreach (var keyword in new[] { "none", "inherit", "initial", "unset", "revert", "revert-layer" })
        {
            var value = CssPropertyParser.Parse(name, keyword).Value;
            value.Kind.Should().Be(CssPropertyValueKind.Keyword);
            value.Serialize().Should().Be(keyword);
            Action wrongAccessor = () => _ = value.Transform;
            wrongAccessor.Should().Throw<InvalidOperationException>();
        }
        CssPropertyParser.Parse(name, "var(--value)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse(name, "none", CssDeclarationContext.FontFace).Status
            .Should().Be(CssPropertyStatus.UnimplementedGrammar);
        CssPropertyRegistry.Completed[name].Inherited.Should().BeFalse();
        CssPropertyRegistry.Completed[name].InitialValue.Should().Be("none");
        CssPropertyParser.Parse("transform", "translate(1px)").Status.Should().Be(CssPropertyStatus.Valid);
    }

    [Test]
    public void FixedComponentsAreOwnedAndCarrySourceSpans()
    {
        var parsed = CssPropertyParser.Parse("translate", "  -1px 20%  ").Value;
        var value = parsed.Transform;
        value.Kind.Should().Be(CssTransformKind.Translate);
        value.Span.Start.Should().Be(2);
        value.Span.Length.Should().Be(8);
        value.X.Span.Start.Should().Be(2);
        value.Y.Span.Start.Should().Be(7);
        value.Z.Serialize().Should().Be("0px");
        Action wrongAccessor = () => _ = value.Angle;
        wrongAccessor.Should().Throw<InvalidOperationException>();
        Action invalidComponent = () => _ = new CssTransformValue(CssTransformKind.Translate,
            CssPropertyValue.Keyword("none", default), value.Y, value.Z, default);
        invalidComponent.Should().Throw<ArgumentException>();
    }

    [TestCase("translate", "1." , "px")]
    [TestCase("rotate", "1.", "deg")]
    [TestCase("scale", "1.", "%")]
    public void LongNumericWorkPollsCancellation(string name, string prefix, string suffix)
    {
        var input = CssReferenceInput.Parse(prefix + new string('0', 32768) + "1" + suffix, null, default);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 8) cancellation.Cancel(); });
        Action parse = () => CssPropertyParser.Parse(name, input, CssDeclarationContext.Style, work);
        parse.Should().Throw<OperationCanceledException>();
    }
}
