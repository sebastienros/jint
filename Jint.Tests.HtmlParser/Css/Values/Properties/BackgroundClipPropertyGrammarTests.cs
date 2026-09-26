#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class BackgroundClipPropertyGrammarTests
{
    [TestCase("border-box", "border-box")]
    [TestCase("padding-box", "padding-box")]
    [TestCase("content-box", "content-box")]
    [TestCase("border-area", "border-area")]
    [TestCase("text", "text")]
    [TestCase("border-area text", "border-area text")]
    [TestCase("text border-area", "border-area text")]
    [TestCase("TEXT /*group*/ BORDER-AREA, c\\6f ntent-box", "border-area text, content-box")]
    [TestCase("border-box,text,border-box,padding-box", "border-box, text, border-box, padding-box")]
    public void LayersAreTypedAndSerializeWithCanonicalCommas(string source, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var result = CssPropertyParser.Parse("background-clip", source, context);
            result.Status.Should().Be(CssPropertyStatus.Valid);
            result.Value.Kind.Should().Be(CssPropertyValueKind.KeywordList);
            result.Value.Serialize().Should().Be(expected);
            foreach (var layer in result.Value.Components) layer.Kind.Should().Be(CssPropertyValueKind.Keyword);
            CssPropertyParser.Parse("background-clip", expected, context).Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("border-box text")]
    [TestCase("border-area border-area")]
    [TestCase("text text")]
    [TestCase("text border-area text")]
    [TestCase("margin-box")]
    [TestCase(",border-box")]
    [TestCase("border-box,")]
    [TestCase("border-box,,text")]
    [TestCase("border-box, initial")]
    [TestCase("inherit, text")]
    [TestCase("clip(text)")]
    [TestCase("\"text\"")]
    [TestCase("1")]
    public void InvalidListsAreRejectedAsAWhole(string source) =>
        CssPropertyParser.Parse("background-clip", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("initial")]
    [TestCase("inherit")]
    [TestCase("unset")]
    [TestCase("revert")]
    [TestCase("revert-layer")]
    [TestCase("revert-rule")]
    public void SoleWideKeywordsUseTheExistingWideValuePath(string source)
    {
        var result = CssPropertyParser.Parse("background-clip", source);
        result.Value.Kind.Should().Be(CssPropertyValueKind.Keyword);
        result.Value.Serialize().Should().Be(source);
    }

    [Test]
    public void ManyRepeatedLayersAreNotCappedOrPaddedAndTheirStorageIsOwned()
    {
        var source = string.Join(", ", Enumerable.Repeat("border-area text", 128));
        var value = CssPropertyParser.Parse("background-clip", source).Value;
        value.Components.Should().HaveCount(128);
        value.Serialize().Should().Be(source);
        var work = new CssValueWork(default);
        var components = new[] { CssPropertyValue.Keyword("text", default) };
        var owned = CssPropertyValue.KeywordList("text", default, components, work);
        components[0] = CssPropertyValue.Keyword("border-box", default);
        owned.Components[0].Serialize().Should().Be("text");
    }

    [Test]
    public void TheFamilyWalkCancelsBeforePublishingAnAlreadyTokenizedList()
    {
        var input = CssReferenceInput.Parse(string.Join(",", Enumerable.Repeat("border-area text", 8192)), null, default);
        var parts = CssPropertyParser.Significant(input.Components, new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 4) cancellation.Cancel(); });
        Action parse = () => CssBackgroundClipPropertyParser.Parse(parts, work);
        parse.Should().Throw<OperationCanceledException>();
        polls.Should().Be(4);
    }
}
