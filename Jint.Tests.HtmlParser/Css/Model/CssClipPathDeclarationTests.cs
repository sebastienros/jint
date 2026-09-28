using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssClipPathDeclarationTests
{
    [TestCase("NONE", "none")]
    [TestCase("margin-box", "margin-box")]
    [TestCase("Border-Box", "border-box")]
    [TestCase("padding-box", "padding-box")]
    [TestCase("content-box", "content-box")]
    [TestCase("fill-box", "fill-box")]
    [TestCase("stroke-box", "stroke-box")]
    [TestCase("view-box", "view-box")]
    [TestCase("URL(#clip)", "url(\"#clip\")")]
    [TestCase("url()", "url(\"\")")]
    [TestCase("src('a\\22 b.svg#clip')", "src(\"a\\\"b.svg#clip\")")]
    public void ClipReferencesAndBoxesRoundTrip(string input, string expected)
    {
        var block = CssDeclarationBlock.Parse("clip-path:" + input + "!important");
        block.GetPropertyValue("clip-path").Should().Be(expected);
        block.GetPropertyPriority("clip-path").Should().Be("important");
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
        block.RemoveProperty("clip-path").Should().Be(expected);
        block.Count.Should().Be(0);
    }

    [TestCase("none border-box")]
    [TestCase("border-box fill-box")]
    [TestCase("text")]
    [TestCase("url(#p) none")]
    [TestCase("url(#p) border-box")]
    [TestCase("src(unquoted)")]
    [TestCase("url('p' 1)")]
    [TestCase("unknown(1)")]
    [TestCase("12px")]
    public void InvalidValuesLeaveTheOldDeclarationIntact(string input)
    {
        var block = CssDeclarationBlock.Parse("clip-path:url(#old)");
        var stamp = block.Stamp;
        CssPropertyParser.Parse("clip-path", input).Status.Should().Be(CssPropertyStatus.Invalid);
        block.SetProperty("clip-path", input);
        block.Stamp.Should().Be(stamp);
        block.GetPropertyValue("clip-path").Should().Be("url(\"#old\")");
    }

    [TestCase("circle(50%)", "V7:clip-path:circle")]
    [TestCase("polygon(0 0, 100% 0, 50% 100%)", "V7:clip-path:polygon")]
    [TestCase("url('p' type('image/svg+xml'))", "V7:clip-path-url-modifiers")]
    public void PendingShapesAndModifiersAreAtomicNamedFailures(string input, string blocker)
    {
        var block = CssDeclarationBlock.Parse("clip-path:none;color:red");
        var stamp = block.Stamp;
        Action replace = () => block.ReplaceText("color:blue;clip-path:" + input);
        replace.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        block.Stamp.Should().Be(stamp);
        block.GetPropertyValue("color").Should().Be("red");
    }

    [Test]
    public void ReferencesRetainTypedUrlsAndSubstitutions()
    {
        var value = CssPropertyParser.Parse("clip-path", "src('clip.svg#p')").Value;
        value.Kind.Should().Be(CssPropertyValueKind.Url);
        value.Url.Url.Should().Be("clip.svg#p");
        value.Url.UsesSrc.Should().BeTrue();
        CssPropertyParser.Parse("clip-path", "src(var(--clip))").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse("clip-path", "none", CssDeclarationContext.FontFace).Status
            .Should().Be(CssPropertyStatus.UnsupportedProperty);
        CssPropertyRegistry.Find("clip-path", CssDeclarationContext.Style)!.Inherited.Should().BeFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action parse = () => CssPropertyParser.Parse("clip-path", "url(#p)", cancellationToken: cancellation.Token);
        parse.Should().Throw<OperationCanceledException>();
    }
}
