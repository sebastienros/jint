#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class AttributeProvenanceTests
{
    [Test]
    public void ParsedTypingBelongsToTheAttributeAndSurvivesIdentityOperations()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateParsedElement(null, "root", null);
        root.InitializeParsedAttributes(
        [
            new ParserAttribute(null, "id", null, "ordinary"),
            new ParserAttribute(Namespaces.Xmlns, "p", "xmlns", "typed-namespace", isDtdId: true)
        ], CancellationToken.None);
        root.AddMissingParsedAttributes(
        [new ParserAttribute(null, "key", null, "typed-later", isDtdId: true)],
            CancellationToken.None);
        source.AppendParsedChild(root);

        var ordinary = root.GetAttributeNode("id")!;
        var namespaceId = root.GetAttributeNodeNS(Namespaces.Xmlns, "p")!;
        var later = root.GetAttributeNode("key")!;
        ordinary.IsDtdId.Should().BeFalse();
        namespaceId.IsDtdId.Should().BeTrue();
        later.IsDtdId.Should().BeTrue();

        root.SetAttribute("key", "edited");
        later.IsDtdId.Should().BeTrue();
        var outgoing = root.SetAttributeNode(source.CreateAttribute("key"));
        outgoing.Should().BeSameAs(later);
        outgoing!.IsDtdId.Should().BeTrue();
        root.GetAttributeNode("key")!.IsDtdId.Should().BeFalse();
        root.SetAttributeNode(outgoing).Should().NotBeNull();
        root.GetAttributeNode("key").Should().BeSameAs(later);

        var clone = namespaceId.Clone();
        var importedAttribute = destination.ImportAttribute(namespaceId);
        foreach (var copy in new[] { clone, importedAttribute })
        {
            copy.Should().NotBeSameAs(namespaceId);
            copy.IsDtdId.Should().BeTrue();
            copy.OwnerElement.Should().BeNull();
            copy.Value.Should().Be("typed-namespace");
        }

        var clonedDocument = (Document)source.CloneNode(deep: true);
        var copied = clonedDocument.DocumentElement!.GetAttributeNodeNS(Namespaces.Xmlns, "p")!;
        copied.IsDtdId.Should().BeTrue();
        copied.Should().NotBeSameAs(namespaceId);
        copied.OwnerDocument.Should().BeSameAs(clonedDocument);

        var importedRoot = (Element)destination.ImportNode(root, deep: true);
        importedRoot.GetAttributeNode("id")!.IsDtdId.Should().BeFalse();
        importedRoot.GetAttributeNodeNS(Namespaces.Xmlns, "p")!.IsDtdId.Should().BeTrue();
        importedRoot.GetAttributeNode("key")!.IsDtdId.Should().BeTrue();
        importedRoot.GetAttributeNode("key").Should().NotBeSameAs(later);

        destination.AdoptNode(root);
        root.GetAttributeNodeNS(Namespaces.Xmlns, "p").Should().BeSameAs(namespaceId);
        namespaceId.OwnerDocument.Should().BeSameAs(destination);
        namespaceId.IsDtdId.Should().BeTrue();
        root.RemoveAttributeNode(namespaceId);
        namespaceId.IsDtdId.Should().BeTrue();
        root.SetAttributeNode(namespaceId);
        namespaceId.OwnerElement.Should().BeSameAs(root);
        root.RemoveAttributeNode(later);
        root.SetAttribute("key", "fresh");
        root.GetAttributeNode("key")!.IsDtdId.Should().BeFalse();
    }

    [Test]
    public void DetachedValueWritesAdvanceStampWithoutMutationRecords()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        var attribute = document.CreateAttribute("key");
        using var subscription = document.ObserveMutations(root,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });

        var before = document.MutationStamp;
        attribute.Value = "";
        document.MutationStamp.Should().Be(before + 1);
        subscription.TakeRecords().Should().BeEmpty();

        before = document.MutationStamp;
        Assert.Throws<ArgumentNullException>(() => attribute.Value = null!);
        document.MutationStamp.Should().Be(before);
        subscription.TakeRecords().Should().BeEmpty();

        root.SetAttributeNode(attribute);
        subscription.TakeRecords();
        before = document.MutationStamp;
        attribute.Value = "attached";
        document.MutationStamp.Should().Be(before + 1);
        subscription.TakeRecords().Select(record => record.OldValue).Should().Equal("");

        root.RemoveAttributeNode(attribute);
        subscription.TakeRecords();
        before = document.MutationStamp;
        attribute.Value = "detached";
        document.MutationStamp.Should().Be(before + 1);
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void CrossDocumentAttributeMovesStampBothOwnersAndSameDocumentRehomeDoesNothing()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var target = destination.CreateElement("target");
        var incoming = source.CreateAttribute("key");
        var oldSource = source.MutationStamp;
        var oldDestination = destination.MutationStamp;

        target.SetAttributeNode(incoming);
        incoming.OwnerDocument.Should().BeSameAs(destination);
        source.MutationStamp.Should().BeGreaterThan(oldSource);
        destination.MutationStamp.Should().BeGreaterThan(oldDestination);

        var sameDocumentStamp = destination.MutationStamp;
        incoming.Rehome(destination);
        destination.MutationStamp.Should().Be(sameDocumentStamp);

        target.RemoveAttributeNode(incoming);
        var sourceTarget = source.CreateElement("source");
        oldSource = source.MutationStamp;
        oldDestination = destination.MutationStamp;
        sourceTarget.SetAttributeNode(incoming);
        incoming.OwnerDocument.Should().BeSameAs(source);
        source.MutationStamp.Should().BeGreaterThan(oldSource);
        destination.MutationStamp.Should().BeGreaterThan(oldDestination);
    }

    [Test]
    public void InternalPrefixChangesInvalidateTheOwningDocument()
    {
        var document = Document.CreateXml();
        var attribute = document.CreateAttributeNS("urn:test", "p:key");
        var before = document.MutationStamp;
        attribute.Prefix = "p";
        document.MutationStamp.Should().Be(before);
        attribute.Prefix = "q";
        document.MutationStamp.Should().Be(before + 1);
    }
}
