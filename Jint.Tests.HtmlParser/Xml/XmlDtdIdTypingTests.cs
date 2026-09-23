#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlDtdIdTypingTests
{
    [Test]
    public void ExactIdTypeMarksExplicitAndDefaultedAttributesAfterExistingNormalization()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r explicit ID #IMPLIED defaulted ID '  from   default  ' " +
                              "fixed ID #FIXED 'fixed' reference IDREF #IMPLIED references IDREFS #IMPLIED " +
                              "token NMTOKEN #IMPLIED id CDATA #IMPLIED absent ID #IMPLIED>]><r explicit='  from   source  ' " +
                              "reference='ref' references='refs' token='token' id='plain'/>";
        var root = ParseRoot(source);

        AssertAttribute(root, "explicit", "from source", true);
        AssertAttribute(root, "defaulted", "from default", true);
        AssertAttribute(root, "fixed", "fixed", true);
        AssertAttribute(root, "reference", "ref", false);
        AssertAttribute(root, "references", "refs", false);
        AssertAttribute(root, "token", "token", false);
        AssertAttribute(root, "id", "plain", false);
        root.GetAttributeNode("absent").Should().BeNull();

        var overridden = ParseRoot("<!DOCTYPE r [<!ATTLIST r key ID 'default'>]><r key='explicit'/>");
        AssertAttribute(overridden, "key", "explicit", true);
    }

    [Test]
    public void DtdNamesMatchRawQualifiedNamesRatherThanExpandedNames()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST p:item p:id ID #IMPLIED>]><r xmlns:p='urn:same' " +
                              "xmlns:q='urn:same'><p:item p:id='typed'/>" +
                              "<p:item q:id='ordinary'/><q:item p:id='wrong-element'/></r>";
        var root = ParseRoot(source);
        var declaredElement = (Element) root.FirstChild!;
        var otherAttribute = (Element) declaredElement.NextSibling!;
        var otherElement = (Element) root.LastChild!;

        declaredElement.GetAttributeNode("p:id")!.IsDtdId.Should().BeTrue();
        otherAttribute.GetAttributeNode("q:id")!.IsDtdId.Should().BeFalse();
        otherElement.GetAttributeNode("p:id")!.IsDtdId.Should().BeFalse();
    }

    [Test]
    public void ActualNamespaceDeclarationCanCarryIdTyping()
    {
        var explicitRoot = ParseRoot("<!DOCTYPE r [<!ELEMENT r EMPTY><!ATTLIST r xmlns:p ID #IMPLIED>]><r xmlns:p='key'/>");
        var explicitDeclaration = explicitRoot.GetAttributeNode("xmlns:p")!;
        explicitDeclaration.NamespaceUri.Should().Be(Namespaces.Xmlns);
        explicitDeclaration.Value.Should().Be("key");
        explicitDeclaration.IsDtdId.Should().BeTrue();

        var defaultedRoot = ParseRoot("<!DOCTYPE r [<!ATTLIST r xmlns:p ID 'urn:p'>]><r/>");
        var defaultedDeclaration = defaultedRoot.GetAttributeNode("xmlns:p")!;
        defaultedDeclaration.NamespaceUri.Should().Be(Namespaces.Xmlns);
        defaultedDeclaration.Value.Should().Be("urn:p");
        defaultedDeclaration.IsDtdId.Should().BeTrue();
    }

    [Test]
    public void FirstAcceptedDeclarationDeterminesTyping()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r ordinary CDATA #IMPLIED typed ID #IMPLIED>" +
                              "<!ATTLIST r ordinary ID #IMPLIED typed CDATA #IMPLIED>]" +
                              "><r ordinary='one' typed='two'/>";
        var root = ParseRoot(source);
        root.GetAttributeNode("ordinary")!.IsDtdId.Should().BeFalse();
        root.GetAttributeNode("typed")!.IsDtdId.Should().BeTrue();
    }

    [Test]
    public void UnreadParameterEntitySuppressesLaterTypingExceptInStandaloneDocument()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % missing SYSTEM 'missing.dtd'>" +
                              "<!ATTLIST r early ID #IMPLIED>%missing;" +
                              "<!ATTLIST r late ID #IMPLIED>]><r early='one' late='two'/>";
        var ordinary = ParseRoot(source);
        ordinary.GetAttributeNode("early")!.IsDtdId.Should().BeTrue();
        ordinary.GetAttributeNode("late")!.IsDtdId.Should().BeFalse();

        var standalone = ParseRoot("<?xml version='1.0' standalone='yes'?>" + source);
        standalone.GetAttributeNode("early")!.IsDtdId.Should().BeTrue();
        standalone.GetAttributeNode("late")!.IsDtdId.Should().BeTrue();
    }

    [Test]
    public void FullyReadInternalParameterEntityContributesIdDeclaration()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % ids '<!ATTLIST r fromPe ID #IMPLIED>'>%ids;]>" +
                              "<r fromPe='included'/>";
        ParseRoot(source).GetAttributeNode("fromPe")!.IsDtdId.Should().BeTrue();
    }

    [Test]
    public void UndeclaredNamesAndUnretrievedExternalSubsetDoNotImplyTyping()
    {
        const string source = "<!DOCTYPE r PUBLIC '-//W3C//DTD XHTML 1.0 Strict//EN' 'missing.dtd'>" +
                              "<r id='plain' xml:id='xml-plain' xmlns='urn:default'/>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        var root = document.DocumentElement!;
        root.GetAttributeNode("id")!.IsDtdId.Should().BeFalse();
        root.GetAttributeNode("xml:id")!.IsDtdId.Should().BeFalse();
        root.GetAttributeNode("xmlns")!.IsDtdId.Should().BeFalse();
        document.SkippedXmlEntities.Should().BeEmpty(); // The local catalog supplies characters, not ATTLISTs.

        var unknownExternal = XmlTreeParser.ParseDocument("<!DOCTYPE r SYSTEM 'missing.dtd'><r id='plain'/>",
            ParseLimits.Unbounded, default);
        unknownExternal.DocumentElement!.GetAttributeNode("id")!.IsDtdId.Should().BeFalse();
        unknownExternal.SkippedXmlEntities.Should().ContainSingle();
        unknownExternal.SkippedXmlEntities[0].Kind.Should().Be(XmlSkippedEntityKind.ExternalSubset);

        var declared = ParseRoot("<!DOCTYPE r [<!ATTLIST r xml:id ID #IMPLIED>]><r xml:id='declared'/>");
        declared.GetAttributeNode("xml:id")!.IsDtdId.Should().BeTrue();
    }

    [Test]
    public void FragmentAndReparseWithoutDeclarationDoNotInheritTyping()
    {
        var root = ParseRoot("<!DOCTYPE r [<!ATTLIST item id ID #IMPLIED>]><r><item id='parsed'/></r>");
        var parsed = (Element) root.FirstChild!;
        parsed.GetAttributeNode("id")!.IsDtdId.Should().BeTrue();

        var fragment = XmlTreeParser.ParseFragment("<item id='fragment'/>", root, ParseLimits.Unbounded, default);
        ((Element) fragment.FirstChild!).GetAttributeNode("id")!.IsDtdId.Should().BeFalse();
        ParseRoot("<r><item id='reparsed'/></r>").FirstChild.Should().BeOfType<Element>()
            .Which.GetAttributeNode("id")!.IsDtdId.Should().BeFalse();
    }

    [Test]
    public void ParserProvenanceSurvivesAttributeElementAndDocumentCopies()
    {
        var source = XmlTreeParser.ParseDocument("<!DOCTYPE r [<!ATTLIST r id ID #IMPLIED ordinary CDATA #IMPLIED>]>" +
            "<r id='source' ordinary='plain'/>", ParseLimits.Unbounded, default);
        var root = source.DocumentElement!;
        var typed = root.GetAttributeNode("id")!;
        var untyped = root.GetAttributeNode("ordinary")!;
        var destination = XmlTreeParser.ParseDocument("<!DOCTYPE d [<!ATTLIST r id CDATA #IMPLIED " +
            "ordinary ID #IMPLIED>]><d/>", ParseLimits.Unbounded, default);

        var attrClone = typed.Clone();
        attrClone.Should().NotBeSameAs(typed);
        attrClone.IsDtdId.Should().BeTrue();
        attrClone.OwnerDocument.Should().BeSameAs(source);
        var importedAttr = destination.ImportAttribute(typed);
        importedAttr.Should().NotBeSameAs(typed);
        importedAttr.IsDtdId.Should().BeTrue();
        importedAttr.OwnerDocument.Should().BeSameAs(destination);
        destination.ImportAttribute(untyped).IsDtdId.Should().BeFalse();

        var elementClone = (Element) root.CloneNode();
        elementClone.GetAttributeNode("id")!.Should().NotBeSameAs(typed);
        elementClone.GetAttributeNode("id")!.IsDtdId.Should().BeTrue();
        elementClone.GetAttributeNode("ordinary")!.IsDtdId.Should().BeFalse();
        var importedElement = (Element) destination.ImportNode(root, deep: true);
        importedElement.GetAttributeNode("id")!.IsDtdId.Should().BeTrue();
        importedElement.GetAttributeNode("id")!.OwnerDocument.Should().BeSameAs(destination);
        importedElement.GetAttributeNode("ordinary")!.IsDtdId.Should().BeFalse();

        var documentClone = (Document) source.CloneNode(deep: true);
        documentClone.DocumentElement!.GetAttributeNode("id")!.IsDtdId.Should().BeTrue();
        documentClone.DocumentElement.GetAttributeNode("id")!.OwnerDocument.Should().BeSameAs(documentClone);
    }

    private static Element ParseRoot(string source)
        => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;

    private static void AssertAttribute(Element root, string name, string value, bool isDtdId)
    {
        var attribute = root.GetAttributeNode(name)!;
        attribute.Value.Should().Be(value);
        attribute.IsDtdId.Should().Be(isDtdId);
    }
}
