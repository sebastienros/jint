#nullable enable
using System.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Parsing;

public class XmlSharedContractsTests
{
    [Test]
    public void XmlOptionsAndExpansionBoundHaveValidatedDefaults()
    {
        var first = new XmlParseOptions();
        var second = new XmlParseOptions();
        first.Limits.MaxEntityExpansionCharacters.Should().Be(10_000_000);
        second.Limits.Should().BeSameAs(first.Limits);
        new ParseLimits().MaxEntityExpansionCharacters.Should().Be(10_000_000);
        ParseLimits.Unbounded.MaxEntityExpansionCharacters.Should().Be(0);
        new ParseLimits { MaxEntityExpansionCharacters = 0 }.MaxEntityExpansionCharacters.Should().Be(0);
        new ParseLimits { MaxEntityExpansionCharacters = long.MaxValue }.MaxEntityExpansionCharacters
            .Should().Be(long.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParseLimits { MaxEntityExpansionCharacters = -1 });
        Assert.Throws<ArgumentNullException>(() => new XmlParseOptions { Limits = null! });

        var limits = new ParseLimits { MaxEntityExpansionCharacters = 7 };
        new XmlParseOptions { Limits = limits }.Limits.Should().BeSameAs(limits);
        ParseLimitKind.EntityExpansionCharacters.Should().NotBe(ParseLimitKind.InputCharacters);
    }

    [Test]
    public void MarkupErrorAndSkippedRecordKeepStableFieldsAndSafeDefaults()
    {
        var error = new MarkupParseException("xml/invalid-markup", 23);
        error.Code.Should().Be("xml/invalid-markup");
        error.Offset.Should().Be(23);
        error.Message.Should().Contain("xml/invalid-markup").And.Contain("23");

        var empty = default(XmlSkippedEntity);
        empty.Kind.Should().Be(XmlSkippedEntityKind.General);
        empty.Name.Should().BeEmpty();
        empty.PublicId.Should().BeNull();
        empty.SystemId.Should().BeNull();
        empty.Offset.Should().Be(0);

        var record = new XmlSkippedEntity(XmlSkippedEntityKind.Parameter, "parameters", "", "relative.dtd", 5);
        record.Kind.Should().Be(XmlSkippedEntityKind.Parameter);
        record.Name.Should().Be("parameters");
        record.PublicId.Should().BeEmpty();
        record.SystemId.Should().Be("relative.dtd");
        record.Offset.Should().Be(5);
    }

    [Test]
    public void FreshDocumentsShareAnImmutableEmptySnapshot()
    {
        var xml = Document.CreateXml();
        var html = Document.CreateHtml();
        xml.SkippedXmlEntities.Should().BeEmpty();
        html.SkippedXmlEntities.Should().BeSameAs(xml.SkippedXmlEntities);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<XmlSkippedEntity>)xml.SkippedXmlEntities).Add(default));
    }

    [Test]
    public void PublicationFreezesOrderedOccurrencesAndDetachesBuilder()
    {
        var document = Document.CreateXml();
        var records = new List<XmlSkippedEntity>
        {
            new(XmlSkippedEntityKind.ExternalSubset, "", null, "missing.dtd", 0),
            new(XmlSkippedEntityKind.General, "ext", null, "missing.xml", 18),
            new(XmlSkippedEntityKind.General, "ext", null, "missing.xml", 24),
            new(XmlSkippedEntityKind.Parameter, "p", null, null, 31)
        };
        document.PublishSkippedXmlEntities(records);
        var snapshot = document.SkippedXmlEntities;
        snapshot.Select(record => record.Kind).Should().Equal(XmlSkippedEntityKind.ExternalSubset,
            XmlSkippedEntityKind.General, XmlSkippedEntityKind.General, XmlSkippedEntityKind.Parameter);
        snapshot.Select(record => record.Offset).Should().Equal(0, 18, 24, 31);
        snapshot[0].Name.Should().BeEmpty();
        snapshot[3].PublicId.Should().BeNull();
        snapshot.Should().NotBeOfType<XmlSkippedEntity[]>();

        records.Clear();
        records.Add(new XmlSkippedEntity(XmlSkippedEntityKind.General, "later", null, null, 99));
        snapshot.Should().HaveCount(4);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlSkippedEntity>)snapshot)[0] = default);
        Assert.Throws<NotSupportedException>(() => ((IList)snapshot).Clear());
        Assert.Throws<InvalidOperationException>(() => document.PublishSkippedXmlEntities(records));
    }

    [Test]
    public void ClonePreservesProvenanceButImportAndAdoptionDoNotTransferIt()
    {
        var source = Document.CreateXml();
        var root = source.CreateElement("root");
        source.AppendChild(root);
        source.PublishSkippedXmlEntities(new List<XmlSkippedEntity>
        {
            new(XmlSkippedEntityKind.General, "ext", null, "missing.xml", 8)
        });

        var shallow = (Document)source.CloneNode();
        var deep = (Document)source.CloneNode(deep: true);
        shallow.SkippedXmlEntities.Should().BeSameAs(source.SkippedXmlEntities);
        deep.SkippedXmlEntities.Should().BeSameAs(source.SkippedXmlEntities);
        shallow.DocumentElement.Should().BeNull();
        deep.DocumentElement.Should().NotBeNull();
        deep.DocumentElement.Should().NotBeSameAs(root);

        var importedInto = Document.CreateXml();
        importedInto.AppendChild(importedInto.ImportNode(root, deep: true));
        importedInto.SkippedXmlEntities.Should().BeEmpty();
        var adoptedInto = Document.CreateXml();
        adoptedInto.AppendChild(root);
        adoptedInto.SkippedXmlEntities.Should().BeEmpty();
        source.SkippedXmlEntities.Should().ContainSingle();
        source.DocumentElement.Should().BeNull();
    }
}
