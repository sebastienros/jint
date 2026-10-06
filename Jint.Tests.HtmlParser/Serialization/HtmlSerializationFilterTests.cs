#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

// The filter is an internal extraction hook, not HTML §13.3 behavior; these cases pin its contract.
[TestFixture]
public sealed class HtmlSerializationFilterTests
{
    [Test]
    public void SkipsUnwrapsAndFiltersAttributesOfDescendants()
    {
        var document = MarkupParser.ParseHtml(
            "<head><title>t</title><script>x()</script></head>" +
            "<body onload=\"go()\" class=\"b\"><nav><a href=\"/\">home</a></nav>" +
            "<div id=\"wrap\"><p style=\"color:red\" onclick=\"y()\">text</p><style>p{}</style></div>" +
            "<template><script>z()</script><i>kept</i></template></body>");
        var filter = new StripFilter();
        var options = new HtmlSerializationOptions { Filter = filter };

        MarkupSerializer.ToHtml(document.DocumentElement!, options).Should().Be(
            "<html><head><base href=\"https://example.test/\"><title>t</title></head>" +
            "<body class=\"b\"><p>text</p><template><i>kept</i></template></body></html>");
        filter.Offered.Should().NotContain(document.DocumentElement!, "the operation root is never offered");
        filter.Offered.OfType<Element>().Should().Contain(e => e.LocalName == "script" && e.ParentNode is DocumentFragment,
            "template contents are offered like any other descendants");
    }

    [Test]
    public void RootIsSerializedEvenWhenTheFilterWouldSkipIt()
    {
        var document = MarkupParser.ParseHtml("<body><nav><b>x</b></nav></body>");
        var nav = Body(document).FirstChild!;
        var options = new HtmlSerializationOptions { Filter = new StripFilter() };

        MarkupSerializer.ToHtml(nav, options).Should().Be("<nav><b>x</b></nav>");
        MarkupSerializer.ToHtmlChildren(nav, options).Should().Be("<b>x</b>");
    }

    [Test]
    public void UnwrappedHostDropsItsShadowRootButIncludedHostKeepsIt()
    {
        var document = MarkupParser.ParseHtml("<body><div id=\"wrap\"></div><span></span></body>");
        var wrap = (Element) Body(document).FirstChild!;
        var span = (Element) wrap.NextSibling!;
        wrap.AttachShadow(new ShadowRootInit(ShadowRootMode.Open, Serializable: true)).AppendChild(document.CreateTextNode("a"));
        span.AttachShadow(new ShadowRootInit(ShadowRootMode.Open, Serializable: true)).AppendChild(document.CreateTextNode("b"));
        var options = new HtmlSerializationOptions(serializableShadowRoots: true) { Filter = new StripFilter() };

        MarkupSerializer.ToHtmlChildren(Body(document), options).Should().Be(
            "<span><template shadowrootmode=\"open\" shadowrootserializable=\"\">b</template></span>");
    }

    [Test]
    public void FilterMutationInvalidatesTheOperation()
    {
        var document = MarkupParser.ParseHtml("<body><p>a</p><p>b</p></body>");
        var options = new HtmlSerializationOptions { Filter = new MutatingFilter() };

        Assert.Throws<InvalidOperationException>(() => MarkupSerializer.ToHtml(Body(document), options));
    }

    [Test]
    public void NoFilterLeavesOutputUnchanged()
    {
        var document = MarkupParser.ParseHtml("<body><script>x()</script><p onclick=\"y\">t</p></body>");
        MarkupSerializer.ToHtml(Body(document), new HtmlSerializationOptions())
            .Should().Be("<body><script>x()</script><p onclick=\"y\">t</p></body>");
    }

    private static Element Body(Document document) => (Element) document.DocumentElement!.LastChild!;

    private sealed class StripFilter : HtmlSerializationFilter
    {
        internal readonly List<Node> Offered = [];

        internal override HtmlSerializationDecision Decide(Node node)
        {
            Offered.Add(node);
            if (node is not Element element) return HtmlSerializationDecision.Include;
            return element.LocalName switch
            {
                "script" or "style" or "nav" => HtmlSerializationDecision.Skip,
                "div" => HtmlSerializationDecision.Unwrap,
                _ => HtmlSerializationDecision.Include,
            };
        }

        internal override bool FiltersAttributes => true;

        internal override bool IncludeAttribute(Element element, Attr attribute)
            => !attribute.LocalName.StartsWith("on", StringComparison.Ordinal) && attribute.LocalName != "style";

        internal override bool InjectsContent => true;

        internal override string? InjectAfterStartTag(Element element)
            => element.LocalName == "head" ? "<base href=\"https://example.test/\">" : null;
    }

    private sealed class MutatingFilter : HtmlSerializationFilter
    {
        internal override HtmlSerializationDecision Decide(Node node)
        {
            if (node is Element { LocalName: "p" } p && p.NextSibling is { } next) p.ParentNode!.RemoveChild(next);
            return HtmlSerializationDecision.Include;
        }
    }
}
