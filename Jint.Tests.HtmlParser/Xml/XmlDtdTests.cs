#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlDtdTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void SmallExponentialEntityInputIsBoundedByDefault(bool explicitOptions)
    {
        var source = "<!DOCTYPE r [<!ENTITY e0 'lol'>";
        for (var i = 1; i <= 7; i++)
            source += "<!ENTITY e" + i + " '" + string.Concat(Enumerable.Repeat("&e" + (i - 1) + ";", 10)) + "'>";
        source += "]><r>&e7;</r>";
        source.Length.Should().BeLessThan(700);

        var limit = Assert.Throws<ParseLimitException>(() => MarkupParser.ParseXml(source,
            explicitOptions ? new XmlParseOptions { Limits = new ParseLimits() } : null));
        limit!.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        limit.Limit.Should().Be(10_000_000);
        limit.Observed.Should().Be(10_000_001);
    }

    [Test]
    public void ExplicitUnboundedLimitsStillAllowTrustedEntities()
    {
        var root = MarkupParser.ParseXml("<!DOCTYPE r [<!ENTITY e 'hello'>]><r>&e;</r>",
            new XmlParseOptions { Limits = ParseLimits.Unbounded }).DocumentElement!;
        ((Text) root.FirstChild!).Data.Should().Be("hello");
    }

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
    public void RepeatedElementsReuseMergedDeclarationsWithFirstDeclarationWinning()
    {
        // XML 1.0 §3.3: repeated ATTLISTs merge, and the first declaration of an attribute binds.
        const string source = "<!DOCTYPE r [" +
            "<!ATTLIST x tokens NMTOKENS #IMPLIED key ID #IMPLIED plain CDATA #IMPLIED " +
            "a CDATA 'first' a CDATA 'duplicate' xmlns:p CDATA 'urn:test'>" +
            "<!ATTLIST x tokens CDATA #IMPLIED key CDATA #IMPLIED plain NMTOKENS #IMPLIED " +
            "a CDATA 'later' b CDATA 'second' ignored CDATA #IMPLIED>" +
            "<!ATTLIST x ignored CDATA 'must-not-appear'>" +
            "<!ATTLIST y tokens CDATA #IMPLIED a CDATA 'other'>]>" +
            "<r><x key='one' tokens=' a  b ' plain=' c  d '><p:item/></x><y tokens=' a  b '/>" +
            "<x key='two' tokens=' e  f ' plain=' g  h ' a='explicit'/></r>";
        var document = MarkupParser.ParseXml(source);
        var children = document.DocumentElement!.ChildNodes.Cast<Element>().ToArray();
        children[0].GetAttribute("tokens").Should().Be("a b");
        children[0].GetAttribute("plain").Should().Be(" c  d ");
        children[0].Attributes.Select(a => a.Name).Should().Equal("key", "tokens", "plain", "a", "xmlns:p", "b");
        children[0].GetAttribute("a").Should().Be("first");
        ((Element) children[0].FirstChild!).NamespaceUri.Should().Be("urn:test");
        children[1].GetAttribute("tokens").Should().Be(" a  b ");
        children[1].GetAttribute("a").Should().Be("other");
        children[2].GetAttribute("tokens").Should().Be("e f");
        children[2].GetAttribute("plain").Should().Be(" g  h ");
        children[2].GetAttribute("a").Should().Be("explicit");
        foreach (var child in new[] { children[0], children[2] })
        {
            child.GetAttribute("b").Should().Be("second");
            child.GetAttribute("ignored").Should().BeNull();
        }
        var navigator = NativeXPath.CreateNavigator(document, default);
        navigator.MoveToId("one").Should().BeTrue();
        navigator.UnderlyingObject.Should().BeSameAs(children[0]);
        navigator.MoveToId("two").Should().BeTrue();
        navigator.UnderlyingObject.Should().BeSameAs(children[2]);
    }

    [Test]
    public void ImpliedDeclarationsAndRepeatedElementsScaleWithInputSize()
    {
        // Count parser work and managed allocations, not wall time. Quadrupling both declarations
        // and tags must not multiply their product: unused declarations add no per-tag work.
        var small = Parse(256);
        var large = Parse(1024);
        large.Polls.Should().BeLessThan(small.Polls * 6);
        large.Allocations.Should().BeLessThan(small.Allocations * 6);

        static (int Polls, long Allocations) Parse(int count)
        {
            var source = new System.Text.StringBuilder("<!DOCTYPE r [<!ATTLIST x tokens NMTOKENS #IMPLIED");
            for (var i = 0; i < count; i++)
                source.Append(" a").Append(i).Append(i % 2 == 0 ? " CDATA #IMPLIED" : " CDATA #REQUIRED");
            source.Append(">]><r>");
            for (var i = 0; i < count; i++) source.Append("<x tokens=' a  b '/>");
            source.Append("</r>");
            var input = source.ToString();
            var polls = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var document = XmlTreeParser.ParseDocument(input, ParseLimits.Unbounded, () => polls++, default);
            var allocations = GC.GetAllocatedBytesForCurrentThread() - before;
            var children = document.DocumentElement!.ChildNodes.Cast<Element>().ToArray();
            children.Length.Should().Be(count);
            children.All(child => child.GetAttribute("tokens") == "a b").Should().BeTrue();
            return (polls, allocations);
        }
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
