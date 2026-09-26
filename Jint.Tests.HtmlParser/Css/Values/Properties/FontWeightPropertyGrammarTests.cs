#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class FontWeightPropertyGrammarTests
{
    [TestCase("NORMAL", "normal")]
    [TestCase("b\\6f ld", "bold")]
    [TestCase("bolder", "bolder")]
    [TestCase("lighter", "lighter")]
    [TestCase("1", "1")]
    [TestCase("1000", "1000")]
    [TestCase("456.5", "456.5")]
    [TestCase("+4e2", "400")]
    [TestCase("calc(400 + 50.5)", "calc(450.5)")]
    [TestCase("inherit", "inherit")]
    [TestCase("unset", "unset")]
    public void OrdinaryAndKeyframeValuesHaveCanonicalSpecifiedSerialization(string source, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var result = CssPropertyParser.Parse("FONT-WEIGHT", source, context);
            result.Status.Should().Be(CssPropertyStatus.Valid);
            result.Value.Serialize().Should().Be(expected);
            CssPropertyParser.Parse("font-weight", expected, context).Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("0")]
    [TestCase(".999999999999999999999")]
    [TestCase("1000.00000000000000001")]
    [TestCase("-1e-999999")]
    [TestCase("1e999999")]
    [TestCase("400%")]
    [TestCase("400px")]
    [TestCase("bold normal")]
    [TestCase("400 700")]
    [TestCase("calc(400px)")]
    [TestCase("calc(50%)")]
    [TestCase("bold !important")]
    public void InvalidValuesCannotEnterTheValidatedModel(string source) =>
        CssPropertyParser.Parse("font-weight", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [Test]
    public void DeferredValuesAndDescriptorContextsStayDistinct()
    {
        var deferred = CssPropertyParser.Parse("font-weight", "var(--weight)");
        deferred.Status.Should().Be(CssPropertyStatus.Deferred);
        deferred.Value.Kind.Should().Be(CssPropertyValueKind.Deferred);
        CssPropertyParser.Parse("font-weight", "400", CssDeclarationContext.FontFace).Status
            .Should().Be(CssPropertyStatus.Valid);
        CssPropertyRegistry.Find("font-weight", CssDeclarationContext.Style)!.Inherited.Should().BeTrue();
    }

    [Test]
    public void CancellationIsPolledDuringLongExactRangeValidation()
    {
        var input = CssReferenceInput.Parse("1." + new string('0', 16384) + "1", null, default);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 4) cancellation.Cancel(); });
        Action parse = () => CssPropertyParser.Parse("font-weight", input, CssDeclarationContext.Style, work);
        parse.Should().Throw<OperationCanceledException>();
    }
}
