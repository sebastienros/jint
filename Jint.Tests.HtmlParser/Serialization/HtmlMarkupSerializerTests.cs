#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

// Authored literal fixtures from HTML Standard §13.3 (2026-09-25),
// https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments.
// These are not copied WPT cases or WPT pass claims.
[TestFixture]
public sealed class HtmlMarkupSerializerTests
{
    [Test]
    public void WholeChildrenLeafAndErrorDispatch()
    {
        var document = Document.CreateXml();
        var root = document.CreateElementNS(Namespaces.Html, "div");
        root.AppendChild(document.CreateTextNode("<&>"));
        document.AppendChild(document.CreateDocumentType("html", "public", "system"));
        document.AppendChild(root);
        HtmlMarkupSerializer.Serialize(document).Should().Be("<!DOCTYPE html><div>&lt;&amp;&gt;</div>");
        HtmlMarkupSerializer.Serialize(root).Should().Be("<div>&lt;&amp;&gt;</div>");
        HtmlMarkupSerializer.SerializeChildren(root).Should().Be("&lt;&amp;&gt;");
        HtmlMarkupSerializer.SerializeChildren(document).Should().Be("<!DOCTYPE html><div>&lt;&amp;&gt;</div>");
        HtmlMarkupSerializer.Serialize(document.CreateDocumentFragment()).Should().BeEmpty();
        HtmlMarkupSerializer.Serialize(document.CreateCDataSection("<&>")).Should().Be("&lt;&amp;&gt;");
        HtmlMarkupSerializer.Serialize(document.CreateComment("bad-->tail")).Should().Be("<!--bad-->tail-->");
        HtmlMarkupSerializer.Serialize(document.CreateProcessingInstruction("pi", "")).Should().Be("<?pi ?>");
        var instruction = document.CreateProcessingInstruction("pi", "x");
        instruction.Data = "x?>y";
        HtmlMarkupSerializer.Serialize(instruction).Should().Be("<?pi x?>y?>");
        Assert.Throws<ArgumentNullException>(() => HtmlMarkupSerializer.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => HtmlMarkupSerializer.SerializeChildren(null!));
        Assert.Throws<ArgumentException>(() => HtmlMarkupSerializer.SerializeChildren(document.CreateTextNode("x")));
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlMarkupSerializer.Serialize(document.CreateDocumentFragment(),
            checkpoint: stage => { if (stage == SerializationStage.Final) cancellation.Cancel(); },
            cancellationToken: cancellation.Token));
    }

    [Test]
    public void ElementAndAttributeNamespaceNamesAndOrder()
    {
        var document = Document.CreateXml();
        var fragment = document.CreateDocumentFragment();
        var html = document.CreateParsedElement(Namespaces.Html, "MiXeD", "h");
        var svg = document.CreateParsedElement(Namespaces.Svg, "linearGradient", "s");
        var math = document.CreateParsedElement(Namespaces.MathMl, "mi", "m");
        var other = document.CreateParsedElement("urn:other", "item", "p");
        fragment.AppendChild(html);
        fragment.AppendChild(svg);
        fragment.AppendChild(math);
        fragment.AppendChild(other);
        html.SetAttributeNode(new Attr(document, null, "MiXeD", null, "v"));
        html.SetAttributeNode(new Attr(document, Namespaces.Xml, "lang", "wrong", "en"));
        html.SetAttributeNode(new Attr(document, Namespaces.Xmlns, "xmlns", null, "urn:x"));
        html.SetAttributeNode(new Attr(document, Namespaces.Xmlns, "p", "wrong", "urn:p"));
        html.SetAttributeNode(new Attr(document, "http://www.w3.org/1999/xlink", "href", "wrong", "u"));
        html.SetAttributeNode(new Attr(document, "urn:other", "key", "z", "v"));
        HtmlMarkupSerializer.Serialize(fragment).Should().Be(
            "<MiXeD MiXeD=\"v\" xml:lang=\"en\" xmlns=\"urn:x\" xmlns:p=\"urn:p\" xlink:href=\"u\" z:key=\"v\"></MiXeD>" +
            "<linearGradient></linearGradient><mi></mi><p:item></p:item>");
    }

    [Test]
    public void AllVoidNamesSuppressChildrenButMenuitemAndForeignControlsDoNot()
    {
        var document = Document.CreateHtml();
        var fragment = document.CreateDocumentFragment();
        var names = new[] { "area", "base", "br", "col", "embed", "hr", "img", "input", "link",
            "meta", "source", "track", "wbr", "basefont", "bgsound", "frame", "keygen", "param" };
        foreach (var name in names)
        {
            var element = document.CreateElement(name);
            element.AppendChild(document.CreateTextNode("hidden"));
            fragment.AppendChild(element);
            HtmlMarkupSerializer.Serialize(element).Should().Be("<" + name + ">");
            HtmlMarkupSerializer.SerializeChildren(element).Should().BeEmpty();
        }
        var menuitem = document.CreateElement("menuitem");
        menuitem.AppendChild(document.CreateTextNode("seen"));
        HtmlMarkupSerializer.Serialize(menuitem).Should().Be("<menuitem>seen</menuitem>");
        foreach (var ns in new[] { Namespaces.Svg, Namespaces.MathMl, "urn:other" })
        {
            var foreign = document.CreateElementNS(ns, "br");
            foreign.AppendChild(document.CreateTextNode("seen"));
            HtmlMarkupSerializer.Serialize(foreign).Should().Be("<br>seen</br>");
        }
        HtmlMarkupSerializer.Serialize(document.CreateParsedElement(Namespaces.Html, "BR", null)).Should().Be("<BR></BR>");
    }

    [Test]
    public void RawTextRCDATAAndEscapingAreContextSensitive()
    {
        var document = Document.CreateHtml();
        var data = "&\u00a0<>\"\r\n\t😀\ud800";
        foreach (var name in new[] { "style", "script", "xmp", "iframe", "noembed", "noframes", "plaintext" })
        {
            var element = document.CreateElement(name);
            var text = document.CreateTextNode(data);
            element.AppendChild(text);
            HtmlMarkupSerializer.Serialize(text).Should().Be(data);
            HtmlMarkupSerializer.Serialize(element).Should().Be("<" + name + ">" + data + "</" + name + ">");
        }
        foreach (var name in new[] { "textarea", "title", "noscript" })
        {
            var element = document.CreateElement(name);
            element.AppendChild(document.CreateTextNode(data));
            HtmlMarkupSerializer.SerializeChildren(element).Should().Be("&amp;&nbsp;&lt;&gt;\"\r\n\t😀\ud800");
        }
        var noscript = document.CreateElement("noscript");
        noscript.AppendChild(document.CreateTextNode("<&"));
        HtmlMarkupSerializer.Serialize(noscript, new HtmlSerializationOptions(scriptingEnabled: true))
            .Should().Be("<noscript><&</noscript>");
        var svgScript = document.CreateElementNS(Namespaces.Svg, "script");
        svgScript.AppendChild(document.CreateTextNode("<&"));
        HtmlMarkupSerializer.Serialize(svgScript).Should().Be("<script>&lt;&amp;</script>");
        HtmlMarkupSerializer.Serialize(document.CreateTextNode("<&")).Should().Be("&lt;&amp;");
        var pre = document.CreateElement("pre");
        pre.AppendChild(document.CreateTextNode("\nfirst"));
        HtmlMarkupSerializer.Serialize(pre).Should().Be("<pre>\nfirst</pre>");
        var ending = document.CreateElement("script");
        ending.AppendChild(document.CreateTextNode("</script><x>"));
        HtmlMarkupSerializer.Serialize(ending).Should().Be("<script></script><x></script>");
    }

    [Test]
    public void AttributeEscapesExactlyAndPreservesOrdinaryLongRuns()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttribute("data", "&\u00a0<>\"'\r\n\t😀\ud800");
        element.AppendChild(document.CreateTextNode(new string('a', 2048) + "<&"));
        HtmlMarkupSerializer.Serialize(element).Should().Be("<div data=\"&amp;&nbsp;&lt;&gt;&quot;'\r\n\t😀\ud800\">" +
            new string('a', 2048) + "&lt;&amp;</div>");
    }

    [Test]
    public void CreationIsValueIsReadOnlyAndIndependentOfLiveAttribute()
    {
        var document = Document.CreateXml();
        var element = document.CreateElementNS(Namespaces.Html, "button", "a&\"<");
        HtmlMarkupSerializer.Serialize(element).Should().Be("<button is=\"a&amp;&quot;&lt;\"></button>");
        element.AttributeCount.Should().Be(0);
        element.SetAttributeNS("urn:x", "p:is", "ns");
        HtmlMarkupSerializer.Serialize(element).Should().Be("<button is=\"a&amp;&quot;&lt;\" p:is=\"ns\"></button>");
        element.SetAttribute("is", "live");
        HtmlMarkupSerializer.Serialize(element).Should().Be("<button p:is=\"ns\" is=\"live\"></button>");
        element.SetAttribute("is", "replacement");
        HtmlMarkupSerializer.Serialize(element).Should().Be("<button p:is=\"ns\" is=\"replacement\"></button>");
        element.RemoveAttribute("is");
        HtmlMarkupSerializer.Serialize(element).Should().Be("<button is=\"a&amp;&quot;&lt;\" p:is=\"ns\"></button>");
        HtmlMarkupSerializer.Serialize(document.CreateElementNS(Namespaces.Html, "b", ""))
            .Should().Be("<b is=\"\"></b>");
        HtmlMarkupSerializer.Serialize((Element) element.CloneNode()).Should().Be(HtmlMarkupSerializer.Serialize(element));
        var destination = Document.CreateHtml();
        var imported = (Element) destination.ImportNode(element);
        HtmlMarkupSerializer.Serialize(imported).Should().Be(HtmlMarkupSerializer.Serialize(element));
        destination.AdoptNode(element);
        HtmlMarkupSerializer.Serialize(element).Should().Be(HtmlMarkupSerializer.Serialize(imported));
    }

    [Test]
    public void ParsedHtmlAndXmlCreationValuesDriveSyntheticIsAfterLiveRemoval()
    {
        var html = Document.CreateHtml();
        var session = new HtmlParserSession(html, new HtmlParseOptions(), default);
        session.AppendInput("<button is='x-link'></button>", isFinal: true);
        HtmlParseStep step;
        do step = session.Drive(10_000, default);
        while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var button = (Element) html.DocumentElement!.LastChild!.FirstChild!;
        button.IsValue.Should().Be("x-link");
        button.RemoveAttribute("is");
        HtmlMarkupSerializer.Serialize(button).Should().Be("<button is=\"x-link\"></button>");

        var xml = XmlTreeParser.ParseDocument("<root is='a&amp;b'/>", ParseLimits.Unbounded, default);
        var root = xml.DocumentElement!;
        root.IsValue.Should().Be("a&b");
        root.RemoveAttribute("is");
        HtmlMarkupSerializer.Serialize(root).Should().Be("<root is=\"a&amp;b\"></root>");
    }
}
