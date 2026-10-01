#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlDtdProcessingInstructionTests
{
    [Test]
    public void ReportsDtdInstructionsWithoutAddingDomChildrenOrExpandingReferences()
    {
        const string source = "<?before x?><!DOCTYPE r [<?empty?><?Case \r\n a\r\nb\rc &amp; %p;?>]>" +
            "<r><?inside y?></r><?after z?>";
        var document = MarkupParser.ParseXml(source);
        document.XmlDtdProcessingInstructions.Select(x => (x.Target, x.Data, x.Offset)).Should().Equal(
            ("empty", "", (long) source.IndexOf("<?empty", StringComparison.Ordinal)),
            ("Case", "a\nb\nc &amp; %p;", (long) source.IndexOf("<?Case", StringComparison.Ordinal)));
        document.ChildNodes.Select(x => x.NodeType).Should().Equal(
            NodeType.ProcessingInstruction, NodeType.DocumentType, NodeType.Element, NodeType.ProcessingInstruction);
        document.DocumentElement!.FirstChild.Should().BeOfType<ProcessingInstruction>();
        MarkupSerializer.ToXml(document).Should().NotContain("<?empty").And.NotContain("<?Case");
        NativeXPath.Select(document, "//processing-instruction()").Should().HaveCount(3);
    }

    [Test]
    public void RepeatedNestedParameterInstructionsUseInvocationOffsetsAndKeepConstructedCr()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % inner '<?p a&#13;&#10;b?>'>" +
            "<!ENTITY % outer '&#37;inner;'>%outer;%outer;]><r/>";
        var document = MarkupParser.ParseXml(source);
        document.XmlDtdProcessingInstructions.Select(x => (x.Target, x.Data, x.Offset)).Should().Equal(
            ("p", "a\r\nb", (long) source.IndexOf("%outer;", StringComparison.Ordinal)),
            ("p", "a\r\nb", (long) source.LastIndexOf("%outer;", StringComparison.Ordinal)));
    }

    [Test]
    public void ReadInstructionsAfterAnUnreadEntityAreStillReported()
    {
        const string source = "<!DOCTYPE r SYSTEM 'file:///unread.dtd' [" +
            "<!ENTITY % e SYSTEM 'https://example.invalid/e'>%e;<?read yes?>]><r/>";
        var document = MarkupParser.ParseXml(source);
        document.SkippedXmlEntities.Should().HaveCount(2);
        document.XmlDtdProcessingInstructions.Should().ContainSingle().Which.Target.Should().Be("read");
    }

    [Test]
    public void FragmentAndNodeTransferDoNotReplaceParseMetadata()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<?p data?>]><r/>");
        var snapshot = document.XmlDtdProcessingInstructions;
        MarkupParser.ParseXmlFragment("<?fragment data?>", document.DocumentElement!);
        document.XmlDtdProcessingInstructions.Should().BeSameAs(snapshot);
        ((Document) document.CloneNode()).XmlDtdProcessingInstructions.Should().BeSameAs(snapshot);
        ((Document) document.CloneNode(true)).XmlDtdProcessingInstructions.Should().BeSameAs(snapshot);
        var destination = Document.CreateXml();
        destination.ImportNode(document.DocumentElement!);
        destination.AdoptNode(document.Doctype!);
        destination.XmlDtdProcessingInstructions.Should().BeEmpty();
        document.XmlDtdProcessingInstructions.Should().BeSameAs(snapshot);
    }

    [Test]
    public void PublicationIsReadOnlyAndDetachesItsBuilder()
    {
        var empty = Document.CreateXml().XmlDtdProcessingInstructions;
        Document.CreateHtml().XmlDtdProcessingInstructions.Should().BeSameAs(empty);
        var value = default(XmlDtdProcessingInstruction);
        value.Target.Should().BeEmpty();
        value.Data.Should().BeEmpty();
        value.Offset.Should().Be(0);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDtdProcessingInstruction>) empty).Add(value));

        var document = Document.CreateXml();
        var stamp = document.MutationStamp;
        var records = new List<XmlDtdProcessingInstruction> { new("p", "data", 12), new("p", "", 20) };
        document.PublishXmlDtdProcessingInstructions(records, default);
        records.Clear();
        document.XmlDtdProcessingInstructions.Select(x => x.Data).Should().Equal("data", "");
        document.MutationStamp.Should().Be(stamp);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<XmlDtdProcessingInstruction>) document.XmlDtdProcessingInstructions)[0] = value);
        Assert.Throws<InvalidOperationException>(() => document.PublishXmlDtdProcessingInstructions(records, default));
    }

    [Test]
    public void CancelledPublicationIsAtomicAndCanBeRetried()
    {
        var document = Document.CreateXml();
        var empty = document.XmlDtdProcessingInstructions;
        var records = Enumerable.Range(0, 1024).Select(i => new XmlDtdProcessingInstruction("p", "", i)).ToList();
        using var cancellation = new CancellationTokenSource();
        var copied = 0;
        Assert.Throws<OperationCanceledException>(() =>
            document.PublishXmlDtdProcessingInstructions(records, cancellation.Token, count =>
            {
                copied = count;
                cancellation.Cancel();
            }));
        copied.Should().Be(256);
        document.XmlDtdProcessingInstructions.Should().BeSameAs(empty);
        document.PublishXmlDtdProcessingInstructions(records, default);
        document.XmlDtdProcessingInstructions.Should().HaveCount(1024);
    }

    [Test]
    public void ExactDoctypeAndExpansionLimitsStillApply()
    {
        const string declaration = "<?p data?>";
        const string doctype = "<!DOCTYPE r [<!ENTITY % e '" + declaration + "'>%e;]>";
        const string source = doctype + "<r/>";
        MarkupParser.ParseXml(source, new()
        {
            Limits = new() { MaxTokenCharacters = doctype.Length, MaxEntityExpansionCharacters = declaration.Length + 2 }
        }).XmlDtdProcessingInstructions.Should().ContainSingle();
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseXml(source,
            new() { Limits = new() { MaxTokenCharacters = doctype.Length - 1 } }))!
            .Kind.Should().Be(ParseLimitKind.TokenCharacters);
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseXml(source,
            new() { Limits = new() { MaxEntityExpansionCharacters = declaration.Length + 1 } }))!
            .Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
    }

    [Test]
    public void CancellationDuringInstructionScanningRemainsObservable()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        Assert.Throws<OperationCanceledException>(() =>
            XmlTreeParser.ParseDocument("<!DOCTYPE r [<?p " + new string('x', 12_000) + "?>]><r/>",
                ParseLimits.Unbounded, () => { polls++; cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(1);
    }

    [TestCase("<?xml?>")]
    [TestCase("<?XML data?>")]
    [TestCase("<?p:target data?>")]
    [TestCase("<?p/data?>")]
    [TestCase("<?p unfinished")]
    public void MalformedDtdInstructionStillFailsParsing(string instruction)
    {
        Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXml("<!DOCTYPE r [" + instruction + "]><r/>"));
    }

    [Test]
    public void CanonicalOutputUsesReadDtdInstructionsAndRejectsTheirLoss()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<?p data?><!NOTATION n SYSTEM 'n'>]><r/>");
        var output = Conformance.XmlEvidence.SecondCanonicalForm(document,
            Conformance.XmlEvidence.CorpusInputBase("xmlconf/probes/input.xml"));
        output.Should().Be("<?p data?><!DOCTYPE r [\n<!NOTATION n SYSTEM 'n'>\n]>\n<r></r>");
    }
}
