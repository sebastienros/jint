#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlCoreTests
{
    [Test]
    public void PreservesCaseNamespacesAttributesAndNodeKinds()
    {
        const string source = "<?xml version='1.0'?><!--before--><P:Root xmlns:P='urn:p' xmlns='urn:d' xmlns:q='urn:q' q:ID='1' ID='2'>" +
            "a\r\nb<![CDATA[c\rd]]><?run data?>&#x1F600;&amp;<Local xmlns=''/>" +
            "</P:Root><?after?>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        var root = document.DocumentElement!;
        root.TagName.Should().Be("P:Root");
        root.NamespaceUri.Should().Be("urn:p");
        root.Attributes.Select(a => a.Name).Should().Equal("xmlns:P", "xmlns", "xmlns:q", "q:ID", "ID");
        root.GetAttributeNS("urn:q", "ID").Should().Be("1");
        root.GetAttributeNS(null, "ID").Should().Be("2");
        document.ChildNodes.Select(n => n.NodeType).Should().Equal(NodeType.Comment, NodeType.Element, NodeType.ProcessingInstruction);
        root.ChildNodes.Select(n => n.NodeType).Should().Equal(NodeType.Text, NodeType.CDataSection,
            NodeType.ProcessingInstruction, NodeType.Text, NodeType.Element);
        ((Text) root.FirstChild!).Data.Should().Be("a\nb");
        ((CDataSection) root.FirstChild!.NextSibling!).Data.Should().Be("c\nd");
        ((Text) root.FirstChild!.NextSibling!.NextSibling!.NextSibling!).Data.Should().Be("😀&");
        ((Element) root.LastChild!).NamespaceUri.Should().BeNull();
    }

    [Test]
    public void FragmentUsesContextBindingsWithoutChangingContext()
    {
        var owner = Document.CreateXml();
        var ancestor = owner.CreateElementNS("urn:outer", "a");
        ancestor.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:first");
        ancestor.SetAttributeNS(Namespaces.Xmlns, "xmlns", "urn:outer");
        var context = owner.CreateElementNS("urn:context", "c");
        context.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:second");
        ancestor.AppendChild(context);
        owner.AppendChild(ancestor);
        var originalChildren = context.ChildCount;

        var fragment = XmlTreeParser.ParseFragment("hello<p:x/><unprefixed/>", context, ParseLimits.Unbounded, default);
        fragment.OwnerDocument.Should().BeSameAs(owner);
        fragment.ChildNodes.Select(n => n.NodeType).Should().Equal(NodeType.Text, NodeType.Element, NodeType.Element);
        ((Element) fragment.FirstChild!.NextSibling!).NamespaceUri.Should().Be("urn:second");
        ((Element) fragment.LastChild!).NamespaceUri.Should().Be("urn:context");
        context.ChildCount.Should().Be(originalChildren);
        context.GetAttributeNS(Namespaces.Xmlns, "p").Should().Be("urn:second");
    }

    [TestCase("", "xml/invalid-document")]
    [TestCase("<a/><b/>", "xml/invalid-document")]
    [TestCase("<a>", "xml/unexpected-eof")]
    [TestCase("<a></b>", "xml/mismatched-end-tag")]
    [TestCase("<a x='1' x='2'/>", "xml/duplicate-attribute")]
    [TestCase("<a xmlns:p='urn:x' xmlns:q='urn:x' p:v='1' q:v='2'/>", "xml/duplicate-attribute")]
    [TestCase("<p:a/>", "xml/namespace-error")]
    [TestCase("<a xmlns:p='urn:x'><p:1/></a>", "xml/namespace-error")]
    [TestCase("<a xmlns:p='urn:x' p:\u0301='v'/>", "xml/namespace-error")]
    [TestCase("<a xmlns:1='urn:x'/>", "xml/namespace-error")]
    [TestCase("<a xmlns:xml='urn:wrong'/>", "xml/namespace-error")]
    [TestCase("<?p:q?><r/>", "xml/namespace-error")]
    [TestCase("<a>&missing;</a>", "xml/undeclared-entity")]
    [TestCase("<a>&#0;</a>", "xml/invalid-character")]
    [TestCase("<a><![CDATA[x]]>y]]></a>", "xml/invalid-markup")]
    [TestCase("<?xml version='1.1'?><a/>", "xml/invalid-declaration")]
    public void RejectsMalformedDocument(string source, string code)
    {
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be(code);
        error.Offset.Should().BeGreaterThanOrEqualTo(0);
        error.Offset.Should().BeLessThanOrEqualTo(source.Length);
    }

    [Test]
    public void AttributeAndTextNormalizationDifferForReferences()
    {
        var root = XmlTreeParser.ParseDocument("<r a='x\r\ny&#10;z&#x9;w&amp;' >a\r\nb&#10;c</r>", ParseLimits.Unbounded, default).DocumentElement!;
        root.GetAttribute("a").Should().Be("x y\nz\tw&");
        ((Text) root.FirstChild!).Data.Should().Be("a\nb\nc");
    }

    [Test]
    public void SourceAndDepthLimitsAreInclusive()
    {
        const string source = "<a><b/></a>";
        XmlTreeParser.ParseDocument(source, new ParseLimits { MaxInputCharacters = source.Length, MaxNestingDepth = 2 }, default)
            .DocumentElement.Should().NotBeNull();
        var input = Assert.Throws<ParseLimitException>(() => XmlTreeParser.ParseDocument(source,
            new ParseLimits { MaxInputCharacters = source.Length - 1 }, default));
        input!.Kind.Should().Be(ParseLimitKind.InputCharacters);
        input.Observed.Should().Be(source.Length);
        var depth = Assert.Throws<ParseLimitException>(() => XmlTreeParser.ParseDocument(source,
            new ParseLimits { MaxNestingDepth = 1 }, default));
        depth!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        depth.Observed.Should().Be(2);
        var token = Assert.Throws<ParseLimitException>(() => XmlTreeParser.ParseDocument("<abcdefghijkl/>",
            new ParseLimits { MaxTokenCharacters = 5 }, default));
        token!.Kind.Should().Be(ParseLimitKind.TokenCharacters);
        token.Observed.Should().Be(6);
    }

    [Test]
    public void AlreadyCancelledInputStopsBeforeMutation()
    {
        var document = Document.CreateXml();
        var context = document.CreateElement("context");
        document.AppendChild(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseFragment("<child/>", context, ParseLimits.Unbounded, cancellation.Token));
        context.ChildCount.Should().Be(0);
    }

    [Test]
    public void CancelsInsideOneLongLexicalToken()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var source = "<!--" + new string('x', 12_000) + "--><r/>";
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded,
            () => { polls++; cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(1);
    }

    [Test]
    public void AcceptsFifthEditionAndSupplementaryNames()
    {
        var document = XmlTreeParser.ParseDocument("<\u037Fnode \U00010000attr='v'/>", ParseLimits.Unbounded, default);
        document.DocumentElement!.LocalName.Should().Be("\u037Fnode");
        document.DocumentElement.Attributes.Single().LocalName.Should().Be("\U00010000attr");
        XmlTreeParser.ParseDocument("<?xml-stylesheet href='x'?><r/>", ParseLimits.Unbounded, default)
            .FirstChild.Should().BeOfType<ProcessingInstruction>();
    }

    [Test]
    public void FragmentCanContainTopLevelCData()
    {
        var document = Document.CreateXml();
        var context = document.CreateElement("context");
        var fragment = XmlTreeParser.ParseFragment("<![CDATA[x]]><child/>", context, ParseLimits.Unbounded, default);
        fragment.FirstChild.Should().BeOfType<CDataSection>().Which.Data.Should().Be("x");
        fragment.LastChild.Should().BeOfType<Element>().Which.LocalName.Should().Be("child");
    }

    [Test]
    public void NamespaceShadowingRestoresBindingsForSiblings()
    {
        var root = XmlTreeParser.ParseDocument(
            "<r xmlns='urn:a' xmlns:p='urn:one'><p:first xmlns:p='urn:two'><p:inner/></p:first><p:second/><plain/></r>",
            ParseLimits.Unbounded, default).DocumentElement!;
        var first = (Element) root.FirstChild!;
        ((Element) first.FirstChild!).NamespaceUri.Should().Be("urn:two");
        ((Element) first.NextSibling!).NamespaceUri.Should().Be("urn:one");
        ((Element) root.LastChild!).NamespaceUri.Should().Be("urn:a");
    }

    [Test]
    public void ParserCanCreateLegalXmlnsElementAndHtmlOwnedCData()
    {
        var document = XmlTreeParser.ParseDocument("<xmlns/>", ParseLimits.Unbounded, default);
        document.DocumentElement!.LocalName.Should().Be("xmlns");
        document.DocumentElement.NamespaceUri.Should().BeNull();

        var html = Document.CreateHtml();
        var context = html.CreateElement("context");
        var fragment = XmlTreeParser.ParseFragment("<![CDATA[x]]>", context, ParseLimits.Unbounded, default);
        fragment.FirstChild.Should().BeOfType<CDataSection>().Which.Data.Should().Be("x");
        fragment.OwnerDocument.Should().BeSameAs(html);
    }
}
