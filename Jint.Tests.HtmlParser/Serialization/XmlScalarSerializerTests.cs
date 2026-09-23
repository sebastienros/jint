using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

[TestFixture]
public sealed class XmlScalarSerializerTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void TextEscapesThreeMarkupCharactersAndKeepsOtherData(bool wellFormed)
    {
        var node = Document.CreateXml().CreateTextNode("&<>\"'\u00a0\r😀");
        Serialize(writer => XmlScalarSerializer.WriteText(node, writer, wellFormed))
            .Should().Be("&amp;&lt;&gt;\"'\u00a0\r😀");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AttributeValueEscapesXmlSyntaxAndWhitespace(bool wellFormed)
    {
        Serialize(writer => XmlScalarSerializer.WriteAttributeValue("&\"<>\t\n\r'\u00a0😀", writer, wellFormed))
            .Should().Be("&amp;&quot;&lt;&gt;&#9;&#xA;&#xD;'\u00a0😀");
        Serialize(writer => XmlScalarSerializer.WriteAttributeValue(null, writer, wellFormed)).Should().BeEmpty();
    }

    [Test]
    public void EscapedOutputUsesExactQuota()
    {
        var node = Document.CreateXml().CreateTextNode("x&");
        var exact = new SerializationLimits { MaxOutputCharacters = 6 };
        Serialize(writer => XmlScalarSerializer.WriteText(node, writer, true), exact).Should().Be("x&amp;");

        var failure = Assert.Throws<SerializationLimitException>(() =>
            Serialize(writer => XmlScalarSerializer.WriteText(node, writer, true),
                new SerializationLimits { MaxOutputCharacters = 5 }));
        failure!.Observed.Should().Be(6);
        failure.Limit.Should().Be(5);

        Serialize(writer => XmlScalarSerializer.WriteAttributeValue(">", writer, true),
            new SerializationLimits { MaxOutputCharacters = 4 }).Should().Be("&gt;");
        var attributeFailure = Assert.Throws<SerializationLimitException>(() =>
            Serialize(writer => XmlScalarSerializer.WriteAttributeValue(">", writer, true),
                new SerializationLimits { MaxOutputCharacters = 3 }));
        attributeFailure!.Observed.Should().Be(4);
    }

    [Test]
    public void WellFormedCharacterValidationAcceptsAstralAndRejectsInvalidUtf16()
    {
        var document = Document.CreateXml();
        Serialize(writer => XmlScalarSerializer.WriteText(document.CreateTextNode("😀\t\n\r"), writer, true))
            .Should().Be("😀\t\n\r");
        foreach (var invalid in new[] { "\0", "\u0001", "\uD800", "\uDC00", "\uFFFF" })
        {
            Invalid(() => Serialize(writer => XmlScalarSerializer.WriteText(document.CreateTextNode(invalid), writer, true)));
            Invalid(() => Serialize(writer => XmlScalarSerializer.WriteAttributeValue(invalid, writer, true)));
            Serialize(writer => XmlScalarSerializer.WriteText(document.CreateTextNode(invalid), writer, false))
                .Should().Be(invalid);
        }
    }

    [Test]
    public void CommentValidationChecksDataAndDelimitersOnlyInTrueMode()
    {
        var node = Document.CreateXml().CreateComment("a--b");
        Serialize(writer => XmlScalarSerializer.WriteComment(node, writer, false)).Should().Be("<!--a--b-->");
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteComment(node, writer, true)));
        node.Data = "trailing-";
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteComment(node, writer, true)));
        node.Data = "good😀<&";
        Serialize(writer => XmlScalarSerializer.WriteComment(node, writer, true)).Should().Be("<!--good😀<&-->");
        node.Data = "\uD800";
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteComment(node, writer, true)));
    }

    [Test]
    public void CDataPreservesMutatedDelimiterEvenInTrueMode()
    {
        var node = Document.CreateXml().CreateCDataSection("original");
        node.Data = "x]]>\uD800";
        foreach (var wellFormed in new[] { false, true })
        {
            Serialize(writer => XmlScalarSerializer.WriteCData(node, writer, wellFormed))
                .Should().Be("<![CDATA[x]]>\uD800]]>");
        }
    }

    [Test]
    public void ProcessingInstructionChecksTargetAndMutableData()
    {
        var document = Document.CreateXml();
        var node = document.CreateProcessingInstruction("p:target", "original");
        Serialize(writer => XmlScalarSerializer.WriteProcessingInstruction(node, writer, false))
            .Should().Be("<?p:target original?>");
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteProcessingInstruction(node, writer, true)));
        var xml = document.CreateProcessingInstruction("XmL", "");
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteProcessingInstruction(xml, writer, true)));
        node = document.CreateProcessingInstruction("target", "");
        Serialize(writer => XmlScalarSerializer.WriteProcessingInstruction(node, writer, true))
            .Should().Be("<?target ?>");
        node.Data = "bad?>";
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteProcessingInstruction(node, writer, true)));
        node.Data = "\uDC00";
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteProcessingInstruction(node, writer, true)));
    }

    [Test]
    public void DoctypeUsesSpecifiedIdentifierQuotesAndValidation()
    {
        var document = Document.CreateXml();
        var node = document.CreateDocumentType("root", "pub'good", "sys\"good");
        Serialize(writer => XmlScalarSerializer.WriteDocumentType(node, writer, true))
            .Should().Be("<!DOCTYPE root PUBLIC \"pub'good\" 'sys\"good'>");
        var systemOnly = document.CreateDocumentType("root", systemId: "sys");
        Serialize(writer => XmlScalarSerializer.WriteDocumentType(systemOnly, writer, true))
            .Should().Be("<!DOCTYPE root SYSTEM \"sys\">");
        var bare = document.CreateDocumentType("root");
        Serialize(writer => XmlScalarSerializer.WriteDocumentType(bare, writer, true))
            .Should().Be("<!DOCTYPE root>");
        // DOM Parsing checks the IDs, not the doctype Name; the DOM factory permits this spelling.
        Serialize(writer => XmlScalarSerializer.WriteDocumentType(document.CreateDocumentType("bad?name"), writer, true))
            .Should().Be("<!DOCTYPE bad?name>");
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteDocumentType(
            document.CreateDocumentType("root", publicId: "bad\t"), writer, true)));
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteDocumentType(
            document.CreateDocumentType("root", systemId: "bad\0"), writer, true)));
        Invalid(() => Serialize(writer => XmlScalarSerializer.WriteDocumentType(
            document.CreateDocumentType("root", systemId: "both\"'"), writer, true)));
        Serialize(writer => XmlScalarSerializer.WriteDocumentType(
            document.CreateDocumentType("root", systemId: "both\"'"), writer, false))
            .Should().Be("<!DOCTYPE root SYSTEM 'both\"''>");
    }

    [Test]
    public void ScanCancellationOccursWithinLongUnescapedInput()
    {
        using var cancellation = new CancellationTokenSource();
        var reachedScan = false;
        var work = new SerializationWork(cancellation.Token, stage =>
        {
            if (stage == SerializationStage.Scan)
            {
                reachedScan = true;
                cancellation.Cancel();
            }
        });
        var node = Document.CreateXml().CreateTextNode(new string('x', 2000));
        Assert.Throws<OperationCanceledException>(() => XmlScalarSerializer.WriteText(node, new SerializationWriter(work), true));
        reachedScan.Should().BeTrue();
    }

    private static string Serialize(Action<SerializationWriter> write, SerializationLimits? limits = null)
    {
        var writer = new SerializationWriter(new SerializationWork(default), limits);
        write(writer);
        return writer.Materialize();
    }

    private static void Invalid(Action action)
        => Assert.Throws<DomException>(() => action())!.Name.Should().Be("InvalidStateError");
}
