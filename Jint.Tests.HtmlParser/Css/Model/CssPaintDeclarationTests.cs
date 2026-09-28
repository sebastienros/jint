using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssPaintDeclarationTests
{
    [TestCase("NONE", "none")]
    [TestCase("Context-Fill", "context-fill")]
    [TestCase("context-stroke", "context-stroke")]
    [TestCase("currentColor", "currentcolor")]
    [TestCase("url(#paint)", "url(\"#paint\")")]
    [TestCase("URL( 'a\\22 b.svg#paint' ) RED", "url(\"a\\\"b.svg#paint\") red")]
    [TestCase("url(#paint) rgb(0 255 0)", "url(\"#paint\") rgb(0, 255, 0)")]
    [TestCase("url(#paint) NONE", "url(\"#paint\") none")]
    [TestCase("url()", "url(\"\")")]
    [TestCase("src('')", "src(\"\")")]
    [TestCase("src('paint.svg#p') currentColor", "src(\"paint.svg#p\") currentcolor")]
    public void PaintValuesRoundTripWithPriority(string input, string expected)
    {
        foreach (var name in new[] { "fill", "stroke" })
        {
            var block = CssDeclarationBlock.Parse(name + ":" + input + " !important");
            block.GetPropertyValue(name).Should().Be(expected);
            block.GetPropertyPriority(name).Should().Be("important");
            CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
            block.RemoveProperty(name).Should().Be(expected);
            block.Count.Should().Be(0);
        }
    }

    [TestCase("red blue")]
    [TestCase("none red")]
    [TestCase("url(#p) context-fill")]
    [TestCase("url(#p) inherit")]
    [TestCase("url(#p) red blue")]
    [TestCase("url(#p), blue")]
    [TestCase("url(a b)")]
    [TestCase("url(42) red none")]
    [TestCase("src(unquoted)")]
    [TestCase("url('p' 12)")]
    [TestCase("src('p', type('image/svg+xml'))")]
    [TestCase("rgb(1px 0 0)")]
    public void InvalidPaintDoesNotMutateDeclarations(string input)
    {
        var block = CssDeclarationBlock.Parse("fill:red;stroke:none");
        var stamp = block.Stamp;
        foreach (var name in new[] { "fill", "stroke" })
        {
            CssPropertyParser.Parse(name, input).Status.Should().Be(CssPropertyStatus.Invalid);
            block.SetProperty(name, input);
            block.Stamp.Should().Be(stamp);
        }
    }

    [TestCase("url('p' type('image/svg+xml'))", "V7:paint-url-modifiers")]
    [TestCase("url(#p) lab(50% 0 0)", "color:lab")]
    public void PendingPaintGrammarRemainsAnAtomicNamedFailure(string input, string blocker)
    {
        var block = CssDeclarationBlock.Parse("fill:red;stroke:none");
        var stamp = block.Stamp;
        Action set = () => block.SetProperty("fill", input);
        set.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        Action replace = () => block.ReplaceText("stroke:blue;fill:" + input);
        replace.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        block.Stamp.Should().Be(stamp);
        block.GetPropertyValue("stroke").Should().Be("none");
    }

    [Test]
    public void PaintServersRetainTypedFallbacksAndReferences()
    {
        var paint = CssPropertyParser.Parse("fill", "url(#p) currentColor").Value;
        paint.Kind.Should().Be(CssPropertyValueKind.PaintServer);
        paint.PaintUrl.Should().Be("#p");
        paint.PaintFallback!.Kind.Should().Be(CssPropertyValueKind.Color);
        CssPropertyParser.Parse("stroke", "src(var(--url)) var(--fallback)").Status.Should().Be(CssPropertyStatus.Deferred);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action parse = () => CssPropertyParser.Parse("fill", "url(#p) red", cancellationToken: cancellation.Token);
        parse.Should().Throw<OperationCanceledException>();
    }
}
