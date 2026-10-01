#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlDtdTests
{
    [TestCase(0)]
    [TestCase(127)]
    [TestCase(128)]
    [TestCase(10000)]
    public void DefaultAttributeBuffersPreserveExpansionAndWhitespaceNormalization(int length)
    {
        var prefix = new string('x', length);
        var source = "<!DOCTYPE r [<!ENTITY e 'entity'><!ATTLIST r a CDATA '" + prefix +
            "\r\n&#x1F600;&e;' tokens NMTOKENS '  " + prefix + " \t tail  '>]><r/>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.GetAttribute("a").Should().Be(prefix + " \U0001F600entity");
        root.GetAttribute("tokens").Should().Be(length == 0 ? "tail" : prefix + " tail");
    }

    [Test]
    public void DoctypeAndInternalEntityProduceNativeNodes()
    {
        var source = "<!DOCTYPE r [<!ENTITY e 'hello'>]><r>&e;</r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.Doctype!.Name.Should().Be("r");
        ((Text) document.DocumentElement!.FirstChild!).Data.Should().Be("hello");
        document.SkippedXmlEntities.Should().BeEmpty();
    }

    [Test]
    public void InternalEntityMayContainMarkupAndNestedReference()
    {
        var source = "<!DOCTYPE r [<!ENTITY a '<x>one</x>'><!ENTITY b '&a;<y/>'>]><r>&b;</r>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.ChildNodes.Select(n => ((Element) n).LocalName).Should().Equal("x", "y");
        ((Text) root.FirstChild!.FirstChild!).Data.Should().Be("one");
    }

    [Test]
    public void ExternalGeneralAndSubsetAreReportedWithoutRetrieval()
    {
        var source = "<!DOCTYPE r SYSTEM 'missing.dtd' [<!ENTITY ext SYSTEM 'missing.xml'>]><r>a&ext;b</r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        ((Text) document.DocumentElement!.FirstChild!).Data.Should().Be("ab");
        document.SkippedXmlEntities.Select(e => e.Kind).Should()
            .Equal(XmlSkippedEntityKind.ExternalSubset, XmlSkippedEntityKind.General);
        document.SkippedXmlEntities[1].Name.Should().Be("ext");
        document.SkippedXmlEntities[1].SystemId.Should().Be("missing.xml");
        document.SkippedXmlEntities[1].Offset.Should().Be(source.IndexOf("&ext;", StringComparison.Ordinal));
    }

    [Test]
    public void ExpansionLimitAndRecursionAreDistinct()
    {
        const string source = "<!DOCTYPE r [<!ENTITY e 'hello'>]><r>&e;</r>";
        var limit = Assert.Throws<ParseLimitException>(() => XmlTreeParser.ParseDocument(source,
            new ParseLimits { MaxEntityExpansionCharacters = 4 }, default));
        limit!.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        limit.Observed.Should().Be(5);

        var recursive = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(
            "<!DOCTYPE r [<!ENTITY e '&e;'>]><r>&e;</r>", ParseLimits.Unbounded, default));
        recursive!.Code.Should().Be("xml/recursive-entity");
    }

    [Test]
    public void DefaultsFollowSpecifiedAttributesAndTokenizedValuesCollapse()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r tokens NMTOKENS ' a   b ' other CDATA 'x&#10;y' declared CDATA 'default'>]><r other='given' tokens=' c  d '/>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.Attributes.Select(a => a.Name).Should().Equal("other", "tokens", "declared");
        root.GetAttribute("tokens").Should().Be("c d");
        root.GetAttribute("other").Should().Be("given");
        root.GetAttribute("declared").Should().Be("default");
    }

    [Test]
    public void ParameterEntitySuppliesDeclarationAndUnreadOneStopsLaterDefaults()
    {
        const string included = "<!DOCTYPE r [<!ENTITY % p '<!ENTITY e \"ok\">'>%p;]><r>&e;</r>";
        var document = XmlTreeParser.ParseDocument(included, ParseLimits.Unbounded, default);
        ((Text) document.DocumentElement!.FirstChild!).Data.Should().Be("ok");

        const string skipped = "<!DOCTYPE r [<!ENTITY % ext SYSTEM 'missing.dtd'><!ATTLIST r a CDATA 'before'>%ext;<!ATTLIST r b CDATA 'after'>]><r/>";
        var withoutStandAlone = XmlTreeParser.ParseDocument(skipped, ParseLimits.Unbounded, default);
        withoutStandAlone.DocumentElement!.GetAttribute("a").Should().Be("before");
        withoutStandAlone.DocumentElement.GetAttribute("b").Should().BeNull();
        withoutStandAlone.SkippedXmlEntities.Should().ContainSingle();
        withoutStandAlone.SkippedXmlEntities[0].Kind.Should().Be(XmlSkippedEntityKind.Parameter);

        var standAlone = XmlTreeParser.ParseDocument("<?xml version='1.0' standalone='yes'?>" + skipped,
            ParseLimits.Unbounded, default);
        standAlone.DocumentElement!.GetAttribute("b").Should().Be("after");
    }

    [Test]
    public void EntityValueCharacterReferenceCanCreateMarkup()
    {
        const string source = "<!DOCTYPE r [<!ENTITY markup '&#60;x/>'>]><r>&markup;</r>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.FirstChild.Should().BeOfType<Element>().Which.LocalName.Should().Be("x");
    }

    [Test]
    public void KnownPublicIdentifierUsesPinnedLocalCharacterCatalog()
    {
        const string publicId = "-//W3C//DTD XHTML 1.0 Strict//EN";
        var source = "<!DOCTYPE r PUBLIC '" + publicId + "' 'https://example.invalid/unused.dtd'><r a='&AMP;&LT;&quot;&Tab;'>&nvlt;&AMP;&LT;</r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.SkippedXmlEntities.Should().BeEmpty();
        document.DocumentElement!.GetAttribute("a").Should().Be("&<\" ");
        ((Text) document.DocumentElement.FirstChild!).Data.Should().Be("<\u20D2&<");
        document.Doctype!.PublicId.Should().Be(publicId);

        var overridden = XmlTreeParser.ParseDocument("<!DOCTYPE r PUBLIC '" + publicId +
            "' 'unused' [<!ENTITY copy 'mine'>]><r>&copy;</r>", ParseLimits.Unbounded, default);
        ((Text) overridden.DocumentElement!.FirstChild!).Data.Should().Be("mine");
    }

    [Test]
    public void SupplementaryCharactersSurviveEntityAndDefaultAttributeProcessing()
    {
        const string source = "<!DOCTYPE r [<!ENTITY e '😀'><!ATTLIST r a CDATA '😀'>]><r b='&e;'>&e;</r>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.GetAttribute("a").Should().Be("😀");
        root.GetAttribute("b").Should().Be("😀");
        ((Text) root.FirstChild!).Data.Should().Be("😀");
    }

    [Test]
    public void EmptyDeclaredPublicIdentifierRemainsDistinctFromSystemOnly()
    {
        const string publicSource = "<!DOCTYPE r PUBLIC '' 'unread.dtd'><r/>";
        const string systemSource = "<!DOCTYPE r SYSTEM 'unread.dtd'><r/>";
        var publicDocument = XmlTreeParser.ParseDocument(publicSource, ParseLimits.Unbounded, default);
        var systemDocument = XmlTreeParser.ParseDocument(systemSource, ParseLimits.Unbounded, default);
        publicDocument.SkippedXmlEntities[0].PublicId.Should().BeEmpty();
        systemDocument.SkippedXmlEntities[0].PublicId.Should().BeNull();
    }

    [Test]
    public void ParsesElementModelsAndNotationDeclarationsWithoutValidation()
    {
        const string source = "<!DOCTYPE r [<!ELEMENT r (a,(b|c)*)><!ELEMENT a EMPTY><!ELEMENT b ANY><!ELEMENT c (#PCDATA|a)*><!NOTATION n PUBLIC 'id'>]><r><a/><b/><c>text</c></r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.DocumentElement!.ChildCount.Should().Be(3);
    }

    [TestCase("<!DOCTYPE r [<!ELEMENT r ()>]><r/>")]
    [TestCase("<!DOCTYPE r [<!ELEMENT r (a|)>]><r/>")]
    [TestCase("<!DOCTYPE r [<!ELEMENT r (a|b,c)>]><r/>")]
    [TestCase("<!DOCTYPE r [<!NOTATION n PUBLIC>]><r/>")]
    public void RejectsMalformedDtdDeclarationGrammar(string source)
    {
        Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
    }

    [Test]
    public void SkippedNestedExternalEntityUsesOutermostSourceOffset()
    {
        const string source = "<!DOCTYPE r [<!ENTITY ext SYSTEM 'missing.xml'><!ENTITY wrapper '&ext;&ext;'>]><r>&wrapper;&wrapper;</r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.SkippedXmlEntities.Select(x => x.Name).Should().Equal("ext", "ext", "ext", "ext");
        document.SkippedXmlEntities.Select(x => x.Offset).Should().Equal(
            source.IndexOf("&wrapper;", StringComparison.Ordinal),
            source.IndexOf("&wrapper;", StringComparison.Ordinal),
            source.LastIndexOf("&wrapper;", StringComparison.Ordinal),
            source.LastIndexOf("&wrapper;", StringComparison.Ordinal));
    }

    [Test]
    public void ExternalAttributeReferencesRemainFatal()
    {
        const string source = "<!DOCTYPE r [<!ENTITY ext SYSTEM 'missing.xml'><!ENTITY indirect '&ext;'>]><r a='&indirect;'/>";
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be("xml/invalid-markup");
    }

    [Test]
    public void XhtmlTemplateChildrenUseNativeInertContentOwner()
    {
        const string source = "<html xmlns='http://www.w3.org/1999/xhtml'><template><span/>text</template></html>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        var template = (Element) document.DocumentElement!.FirstChild!;
        template.ChildCount.Should().Be(0);
        var content = template.TemplateContent!;
        content.ChildCount.Should().Be(2);
        content.OwnerDocument.Should().NotBeSameAs(document);
        content.FirstChild!.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
        content.LastChild!.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
    }

    [Test]
    public void NestedXhtmlTemplateChildrenStayInTheInertDocument()
    {
        const string source = "<html xmlns='http://www.w3.org/1999/xhtml'><template><template><span/></template></template></html>";
        var document = MarkupParser.ParseXml(source);
        var outer = (Element) document.DocumentElement!.FirstChild!;
        var inner = (Element) outer.TemplateContent!.FirstChild!;
        var leaf = (Element) inner.TemplateContent!.FirstChild!;

        outer.ChildCount.Should().Be(0);
        inner.ChildCount.Should().Be(0);
        inner.OwnerDocument.Should().BeSameAs(outer.TemplateContent.OwnerDocument);
        inner.TemplateContent.OwnerDocument.Should().BeSameAs(outer.TemplateContent.OwnerDocument);
        leaf.OwnerDocument.Should().BeSameAs(outer.TemplateContent.OwnerDocument);
    }
}
