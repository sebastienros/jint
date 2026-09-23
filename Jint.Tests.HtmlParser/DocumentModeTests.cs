#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class DocumentModeTests
{
    [Test]
    public void FactoriesStartInNoQuirksAndClonesPreserveMode()
    {
        foreach (var document in new[] { Document.CreateHtml(), Document.CreateXml(), Document.CreateXml("application/xhtml+xml") })
        {
            document.Mode.Should().Be(DocumentMode.NoQuirks);
            document.SetParserMode(DocumentMode.LimitedQuirks);
            ((Document)document.CloneNode()).Mode.Should().Be(DocumentMode.LimitedQuirks);
            ((Document)document.CloneNode(deep: true)).Mode.Should().Be(DocumentMode.LimitedQuirks);
        }
    }

    [Test]
    public void DoctypeMutationDoesNotReclassifyMode()
    {
        var document = Document.CreateHtml();
        document.SetParserMode(DocumentMode.Quirks);
        var doctype = document.CreateDocumentType("html");
        document.AppendChild(doctype);
        document.Mode.Should().Be(DocumentMode.Quirks);
        document.RemoveChild(doctype);
        document.Mode.Should().Be(DocumentMode.Quirks);
        document.ReplaceChildren(doctype);
        document.Mode.Should().Be(DocumentMode.Quirks);
    }

    [Test]
    public void AdoptionAndImportRetainDestinationMode()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        source.SetParserMode(DocumentMode.Quirks);
        destination.SetParserMode(DocumentMode.LimitedQuirks);
        var root = source.CreateElement("root");

        destination.ImportNode(root).OwnerDocument!.Mode.Should().Be(DocumentMode.LimitedQuirks);
        destination.AdoptNode(root).OwnerDocument!.Mode.Should().Be(DocumentMode.LimitedQuirks);
        source.Mode.Should().Be(DocumentMode.Quirks);
        destination.Mode.Should().Be(DocumentMode.LimitedQuirks);
    }
}
