#nullable enable
using System.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Parsing;

public class XmlNotationContractsTests
{
    [Test]
    public void DefaultRecordAndEmptyDocumentsAreSafeAndShared()
    {
        var record = default(XmlNotationDeclaration);
        record.Name.Should().BeEmpty();
        record.PublicId.Should().BeNull();
        record.SystemId.Should().BeNull();
        record.Offset.Should().Be(0);

        var xml = Document.CreateXml();
        var html = Document.CreateHtml();
        xml.XmlNotations.Should().BeEmpty();
        html.XmlNotations.Should().BeSameAs(xml.XmlNotations);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlNotationDeclaration>)xml.XmlNotations).Add(default));

        xml.PublishXmlNotations(new List<XmlNotationDeclaration>(), CancellationToken.None);
        xml.XmlNotations.Should().BeSameAs(html.XmlNotations);
        Assert.Throws<InvalidOperationException>(() =>
            xml.PublishXmlNotations(new List<XmlNotationDeclaration>(), CancellationToken.None));
    }

    [Test]
    public void PublicationPreservesOrderNullAndEmptyIdentifiersAndDetachesBuilder()
    {
        var document = Document.CreateXml();
        var initialStamp = document.MutationStamp;
        var records = new List<XmlNotationDeclaration>
        {
            new("SystemOnly", null, "", 7),
            new("PublicOnly", "", null, 17),
            new("PublicAndSystem", "public id", "relative.uri", 29),
            new("SystemOnly", null, "other", 7)
        };

        document.PublishXmlNotations(records, CancellationToken.None);
        var snapshot = document.XmlNotations;
        snapshot.Select(record => record.Name).Should().Equal("SystemOnly", "PublicOnly", "PublicAndSystem", "SystemOnly");
        snapshot.Select(record => record.Offset).Should().Equal(7, 17, 29, 7);
        snapshot[0].PublicId.Should().BeNull();
        snapshot[0].SystemId.Should().BeEmpty();
        snapshot[1].PublicId.Should().BeEmpty();
        snapshot[1].SystemId.Should().BeNull();
        snapshot[2].PublicId.Should().Be("public id");
        snapshot[2].SystemId.Should().Be("relative.uri");
        snapshot.Should().NotBeOfType<XmlNotationDeclaration[]>();
        document.MutationStamp.Should().Be(initialStamp);

        records.Clear();
        records.Add(new XmlNotationDeclaration("later", null, "later", 100));
        snapshot.Should().HaveCount(4);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlNotationDeclaration>)snapshot)[0] = default);
        Assert.Throws<NotSupportedException>(() => ((IList)snapshot).Clear());
        Assert.Throws<InvalidOperationException>(() => document.PublishXmlNotations(records, CancellationToken.None));
    }

    [Test]
    public void CancellationBeforePublicationLeavesDocumentUnchanged()
    {
        var document = Document.CreateXml();
        var records = new List<XmlNotationDeclaration> { new("n", null, "s", 4) };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var empty = document.XmlNotations;
        var stamp = document.MutationStamp;

        Assert.Throws<OperationCanceledException>(() => document.PublishXmlNotations(records, cancellation.Token));
        document.XmlNotations.Should().BeSameAs(empty);
        document.MutationStamp.Should().Be(stamp);
        document.PublishXmlNotations(records, CancellationToken.None);
        document.XmlNotations.Should().ContainSingle();
    }

    [Test]
    public void CloningPreservesProvenanceButNodeTransferAndDoctypeRemovalDoNotChangeIt()
    {
        var source = Document.CreateXml();
        var doctype = source.CreateDocumentType("root");
        source.AppendChild(doctype);
        var root = source.CreateElement("root");
        source.AppendChild(root);
        source.PublishXmlNotations(new List<XmlNotationDeclaration> { new("n", null, "s", 12) }, CancellationToken.None);
        var snapshot = source.XmlNotations;

        var shallow = (Document)source.CloneNode();
        var deep = (Document)source.CloneNode(deep: true);
        shallow.XmlNotations.Should().BeSameAs(snapshot);
        deep.XmlNotations.Should().BeSameAs(snapshot);
        shallow.Doctype.Should().BeNull();
        deep.Doctype.Should().NotBeNull();

        var importedInto = Document.CreateXml();
        importedInto.AppendChild(importedInto.ImportNode(root, deep: true));
        importedInto.ImportNode(doctype).Should().BeOfType<DocumentType>();
        importedInto.XmlNotations.Should().BeEmpty();
        var adoptedInto = Document.CreateXml();
        adoptedInto.AppendChild(adoptedInto.AdoptNode(root));
        adoptedInto.XmlNotations.Should().BeEmpty();

        source.RemoveChild(doctype);
        source.XmlNotations.Should().BeSameAs(snapshot);
        adoptedInto.AdoptNode(doctype).Should().BeSameAs(doctype);
        adoptedInto.XmlNotations.Should().BeEmpty();
    }
}
