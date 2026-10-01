#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ParsedAttributeMergeTests
{
    [Test]
    public void PublishedElementKeepsExistingAttributesAndAppendsMissingParsedNames()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("html");
        element.SetAttribute("id", "original");
        var existing = element.GetAttributeNode("id")!;
        document.AppendChild(element);

        element.AddMissingParsedAttributes(
        [
            new ParserAttribute(null, "id", null, "ignored"),
            new ParserAttribute(null, "=x", null, "1"),
            new ParserAttribute(null, "p:name", null, "2"),
            new ParserAttribute(null, "xmlns", null, "3")
        ], CancellationToken.None);

        element.Attributes.Select(attribute => attribute.Name).Should().Equal("id", "=x", "p:name", "xmlns");
        element.Attributes.Select(attribute => attribute.Value).Should().Equal("original", "1", "2", "3");
        element.Attributes.First().Should().BeSameAs(existing);
        element.Attributes.All(attribute => ReferenceEquals(attribute.OwnerElement, element) &&
                                            ReferenceEquals(attribute.OwnerDocument, document)).Should().BeTrue();
        element.GetAttributeNode("=x")!.NamespaceUri.Should().BeNull();
        element.GetAttributeNode("p:name")!.Prefix.Should().BeNull();
        element.GetAttributeNode("xmlns")!.NamespaceUri.Should().BeNull();
        Assert.That(Assert.Throws<DomException>(() => document.CreateAttribute("=x"))!.Name,
            Is.EqualTo("InvalidCharacterError"));
    }

    [Test]
    public void ExpandedNameMatchingKeepsExistingIdentityAndSourceOrder()
    {
        var document = Document.CreateXml();
        var element = document.CreateParsedElement(null, "root", null);
        element.InitializeParsedAttributes(
        [
            new ParserAttribute("urn:one", "name", "a", "old"),
            new ParserAttribute(null, "plain", null, "same")
        ], CancellationToken.None);
        var original = element.Attributes.First();
        document.AppendChild(element);

        element.AddMissingParsedAttributes(
        [
            new ParserAttribute("urn:one", "name", "changed", "ignored"),
            new ParserAttribute(null, "plain", null, "ignored"),
            new ParserAttribute("urn:two", "name", "b", "new"),
            new ParserAttribute(null, "tail", null, "last")
        ], CancellationToken.None);

        element.Attributes.Select(attribute => attribute.Name).Should().Equal("a:name", "plain", "b:name", "tail");
        element.Attributes.Select(attribute => attribute.Value).Should().Equal("old", "same", "new", "last");
        element.Attributes.First().Should().BeSameAs(original);
    }

    [Test]
    public void CancellationAfterIndexLeavesPublishedElementUnchanged()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("body");
        document.AppendChild(element);
        element.SetAttribute("id", "old");
        using var canceled = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => element.AddMissingParsedAttributes(
            [new ParserAttribute(null, "new", null, "value")],
            (stage, _) =>
            {
                if (stage == ParsedAttributeMergeCheckpoint.AfterIndex)
                {
                    canceled.Cancel();
                }
            }, canceled.Token));

        element.Attributes.Select(attribute => attribute.Name).Should().Equal("id");
    }

    [Test]
    public void PreCanceledMergeDoesNotInspectOrChangePublishedAttributes()
    {
        var element = Document.CreateHtml().CreateElement("body");
        element.SetAttribute("id", "old");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => element.AddMissingParsedAttributes(
            [new ParserAttribute(null, "new", null, "value")], canceled.Token));
        element.Attributes.Select(attribute => attribute.Name).Should().Equal("id");
    }

    [Test]
    public void CancellationAfterCommitLeavesCompletePrefix()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("body");
        document.AppendChild(element);
        using var canceled = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => element.AddMissingParsedAttributes(
            [
                new ParserAttribute(null, "first", null, "one"),
                new ParserAttribute(null, "second", null, "two")
            ],
            (stage, count) =>
            {
                if (stage == ParsedAttributeMergeCheckpoint.AfterCommit && count == 1)
                {
                    canceled.Cancel();
                }
            }, canceled.Token));

        element.Attributes.Select(attribute => attribute.Name).Should().Equal("first");
        element.Attributes.Single().Value.Should().Be("one");
        element.Attributes.Single().OwnerElement.Should().BeSameAs(element);
        element.Attributes.Single().OwnerDocument.Should().BeSameAs(document);
    }

    [Test]
    public void WideMergeKeepsPrefixAndAddsOnlyMissingKeys()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("html");
        var initial = new ParserAttribute[4_000];
        var incoming = new ParserAttribute[8_000];
        for (var i = 0; i < initial.Length; i++)
        {
            initial[i] = new ParserAttribute(null, "a" + i, null, "old");
        }

        for (var i = 0; i < incoming.Length; i++)
        {
            incoming[i] = new ParserAttribute(null, "a" + i, null, "new");
        }

        element.InitializeParsedAttributes(initial, CancellationToken.None);
        document.AppendChild(element);
        var first = element.Attributes.First();
        element.AddMissingParsedAttributes(incoming, CancellationToken.None);
        element.AttributeCount.Should().Be(8_000);
        element.Attributes.First().Should().BeSameAs(first);
        element.Attributes.First().Value.Should().Be("old");
        element.Attributes.Last().Name.Should().Be("a7999");
    }
}
