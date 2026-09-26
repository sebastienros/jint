#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class FontSizePropertyGrammarTests
{
    [TestCase("MEDIUM", "medium")]
    [TestCase("xxx-large", "xxx-large")]
    [TestCase("larger", "larger")]
    [TestCase("smaller", "smaller")]
    [TestCase("0", "0px")]
    [TestCase("33px", "33px")]
    [TestCase("150%", "150%")]
    [TestCase("2em", "2em")]
    [TestCase("2rem", "2rem")]
    [TestCase("calc(50% + 2px)", "calc(50% + 2px)")]
    [TestCase("initial", "initial")]
    public void StyleAndKeyframeValuesUseTypedNonnegativeLengths(string source, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var parsed = CssPropertyParser.Parse("font-size", source, context);
            parsed.Status.Should().Be(CssPropertyStatus.Valid);
            parsed.Value.Serialize().Should().Be(expected);
            CssPropertyParser.Parse("font-size", expected, context).Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("-1px")]
    [TestCase("-1e-999999px")]
    [TestCase("-1%")]
    [TestCase("2")]
    [TestCase("20deg")]
    [TestCase("large small")]
    [TestCase("calc(2)")]
    [TestCase("auto")]
    public void InvalidValuesCannotEnterTheTypedModel(string source) =>
        CssPropertyParser.Parse("font-size", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [Test]
    public void DeferredValuesAndMathmlScalingKeepTheirDistinctBoundaries()
    {
        CssPropertyParser.Parse("font-size", "var(--size)").Status.Should().Be(CssPropertyStatus.Deferred);
        var math = CssPropertyParser.Parse("font-size", "math");
        math.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        math.Blocker.Should().Be("font-size:mathml-scaling");
        CssPropertyRegistry.Find("font-size", CssDeclarationContext.Style)!.Inherited.Should().BeTrue();
    }

    [Test]
    public void TypedLengthParsingPollsCancellationDuringLongInput()
    {
        var input = CssReferenceInput.Parse("1." + new string('0', 16384) + "1em", null, default);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 4) cancellation.Cancel(); });
        Action parse = () => CssPropertyParser.Parse("font-size", input, CssDeclarationContext.Style, work);
        parse.Should().Throw<OperationCanceledException>();
    }
}
