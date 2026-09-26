#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCustomPropertySerializationTests
{
    [Test]
    public void AuthoredAndInheritedCustomValuesRetainTheirLexicalEnvelope()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        document.AppendChild(parent);
        parent.AppendChild(child);
        parent.SetAttribute("style", @"--width:10px;--escaped:10\70 x;--comments:/*start*/20PX/*end*/;--empty:;--invalid:initial");
        child.SetAttribute("style", "--copy:var(--width);--joined:var(--number)px;--number:10;width:var(--joined)");
        var input = NativeCssStyleSheets.CreateQuery(document, new CssMediaEnvironment(),
            new SelectorEnvironment(document, null, null, null), new CssValueWork(default));
        input.Query.GetProperty(parent, "--width", ref input.Matching).Text.Should().Be("10px");
        input.Query.GetProperty(child, "--width", ref input.Matching).Text.Should().Be("10px");
        input.Query.GetProperty(child, "--copy", ref input.Matching).Text.Should().Be("10px");
        input.Query.GetProperty(child, "--escaped", ref input.Matching).Text.Should().Be(@"10\70 x");
        input.Query.GetProperty(child, "--comments", ref input.Matching).Text.Should().Be("/*start*/20PX/*end*/");
        input.Query.GetProperty(child, "--empty", ref input.Matching).Text.Should().Be(" ");
        input.Query.GetProperty(child, "--invalid", ref input.Matching).Text.Should().BeEmpty();
        input.Query.GetProperty(child, "--joined", ref input.Matching).Text.Should().Be("10/**/px");
        input.Query.GetProperty(child, "width", ref input.Matching).Text.Should().Be("auto");
    }
}
