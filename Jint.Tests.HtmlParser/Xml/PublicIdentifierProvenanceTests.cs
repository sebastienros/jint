#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class PublicIdentifierProvenanceTests
{
    // XML 1.0 Fifth Edition §4.2.2 and XML Infoset §2.8: parsed public IDs are normalized.
    [Test]
    public void ParsedExternalSubsetExposesNormalizedPublicIdAndLiteralSystemId()
    {
        const string source = "<!DOCTYPE r PUBLIC '  Alpha \r\n Beta  Gamma \r Delta\n  ' '  ../unread.dtd?x=1  '><r/>";
        var document = MarkupParser.ParseXml(source);

        document.Doctype!.PublicId.Should().Be("Alpha Beta Gamma Delta");
        document.Doctype.SystemId.Should().Be("  ../unread.dtd?x=1  ");
        document.SkippedXmlEntities.Should().ContainSingle();
        var skipped = document.SkippedXmlEntities[0];
        skipped.Kind.Should().Be(XmlSkippedEntityKind.ExternalSubset);
        skipped.Name.Should().BeEmpty();
        skipped.PublicId.Should().Be("Alpha Beta Gamma Delta");
        skipped.SystemId.Should().Be("  ../unread.dtd?x=1  ");
        skipped.Offset.Should().Be(0);
    }

    // XML 1.0 Fifth Edition productions [12] and [13] permit SPACE, CR, LF but not TAB or NBSP.
    [TestCase("\t")]
    [TestCase("\u00a0")]
    public void IllegalPublicIdentifierCharactersAreRejected(string character)
    {
        var source = "<!DOCTYPE r PUBLIC 'a" + character + "b' 'unread.dtd'><r/>";
        var error = Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXml(source));
        error!.Code.Should().Be("xml/invalid-declaration");
    }

    [Test]
    public void OmissionDistinguishesAbsentFromDeclaredEmptyPublicId()
    {
        var system = MarkupParser.ParseXml("<!DOCTYPE r SYSTEM 'unread.dtd'><r/>");
        var emptyPublic = MarkupParser.ParseXml("<!DOCTYPE r PUBLIC '' 'unread.dtd'><r/>");

        system.SkippedXmlEntities.Should().ContainSingle();
        emptyPublic.SkippedXmlEntities.Should().ContainSingle();
        system.SkippedXmlEntities[0].PublicId.Should().BeNull();
        emptyPublic.SkippedXmlEntities[0].PublicId.Should().BeEmpty();
        system.Doctype!.PublicId.Should().BeEmpty();
        emptyPublic.Doctype!.PublicId.Should().BeEmpty();
        system.SkippedXmlEntities[0].SystemId.Should().Be("unread.dtd");
        emptyPublic.SkippedXmlEntities[0].SystemId.Should().Be("unread.dtd");
    }

    [Test]
    public void FactoryAndClonesPreserveSuppliedAndParsedIdentifierValues()
    {
        var owner = Document.CreateXml();
        var supplied = owner.CreateDocumentType("r", " raw  public ", " ../raw path ");
        supplied.PublicId.Should().Be(" raw  public ");
        supplied.SystemId.Should().Be(" ../raw path ");

        var suppliedClone = (DocumentType) supplied.CloneNode();
        suppliedClone.PublicId.Should().Be(" raw  public ");
        suppliedClone.SystemId.Should().Be(" ../raw path ");
        var imported = (DocumentType) Document.CreateXml().ImportNode(supplied);
        imported.PublicId.Should().Be(" raw  public ");
        imported.SystemId.Should().Be(" ../raw path ");

        var parsed = MarkupParser.ParseXml("<!DOCTYPE r PUBLIC ' parsed  id ' 'literal.dtd'><r/>");
        var parsedClone = (Document) parsed.CloneNode(deep: true);
        parsed.Doctype!.PublicId.Should().Be("parsed id");
        parsedClone.Doctype!.PublicId.Should().Be("parsed id");
        parsedClone.Doctype.SystemId.Should().Be("literal.dtd");
        parsedClone.SkippedXmlEntities.Should().ContainSingle();
        parsedClone.SkippedXmlEntities[0].PublicId.Should().Be("parsed id");
    }

    [Test]
    public void NormalizedKnownCatalogPublicIdActivatesLocalCharacterEntities()
    {
        const string source = "<!DOCTYPE r PUBLIC '  -//W3C//DTD  XHTML 1.0 Strict//EN  ' 'unread.dtd'><r>&AMP;</r>";
        var document = MarkupParser.ParseXml(source);

        document.Doctype!.PublicId.Should().Be("-//W3C//DTD XHTML 1.0 Strict//EN");
        document.SkippedXmlEntities.Should().BeEmpty();
        ((Text) document.DocumentElement!.FirstChild!).Data.Should().Be("&");
    }
}
