#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlNotationTests
{
    [Test]
    public void RecordsAllReadFormsInEncounterOrderWithExactIdentifiers()
    {
        const string source = "<!DOCTYPE r [<!NOTATION System SYSTEM 'file:///unused'>" +
            "<!NOTATION Public PUBLIC '  A \r\n B  '>" +
            "<!NOTATION Both PUBLIC '' ''>]><r/>";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Select(x => (x.Name, x.PublicId, x.SystemId, x.Offset)).Should().Equal(
            ("System", null, "file:///unused", (long) source.IndexOf("<!NOTATION System", StringComparison.Ordinal)),
            ("Public", "A B", null, (long) source.IndexOf("<!NOTATION Public", StringComparison.Ordinal)),
            ("Both", "", "", (long) source.IndexOf("<!NOTATION Both", StringComparison.Ordinal)));
        document.SkippedXmlEntities.Should().BeEmpty();
        document.DocumentElement!.LocalName.Should().Be("r");
    }

    [Test]
    public void SystemLiteralPreservesSpacingAndReferenceTextButNormalizesSourceLines()
    {
        const string source = "<!DOCTYPE r [<!NOTATION n SYSTEM ' a\r\nb\rc  &amp; file:///missing '>]><r/>";
        var notation = MarkupParser.ParseXml(source).XmlNotations.Single();
        notation.PublicId.Should().BeNull();
        notation.SystemId.Should().Be(" a\nb\nc  &amp; file:///missing ");
    }

    [Test]
    public void ParameterConstructedLineEndingsInSystemLiteralArePreserved()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % p \"<!NOTATION n SYSTEM 'a&#13;&#10;b'>\">%p;]><r/>";
        var notation = MarkupParser.ParseXml(source).XmlNotations.Single();
        notation.SystemId.Should().Be("a\r\nb");
        notation.Offset.Should().Be(source.IndexOf("%p;", StringComparison.Ordinal));
    }

    [Test]
    public void RepeatedParameterEntityKeepsDuplicatesAndInvocationOffsets()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % inner '<!NOTATION n SYSTEM \"one\">'>" +
            "<!ENTITY % outer '&#37;inner;'><!NOTATION n SYSTEM 'two'>" +
            "%outer;%outer;]><r/>";
        var document = MarkupParser.ParseXml(source);
        var first = source.IndexOf("%outer;", StringComparison.Ordinal);
        var second = source.LastIndexOf("%outer;", StringComparison.Ordinal);
        document.XmlNotations.Select(x => (x.Name, x.SystemId, x.Offset)).Should().Equal(
            ("n", "two", (long) source.IndexOf("<!NOTATION n SYSTEM 'two'", StringComparison.Ordinal)),
            ("n", "one", (long) first),
            ("n", "one", (long) second));
    }

    [Test]
    public void NotationsAfterUnreadParameterEntityRemainObserved()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % missing SYSTEM 'missing.dtd'>%missing;" +
            "<!NOTATION n SYSTEM 'urn:n'><!ATTLIST r suppressed CDATA 'x'>]><r/>";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Should().ContainSingle();
        document.XmlNotations[0].Name.Should().Be("n");
        document.XmlNotations[0].Offset.Should().Be(source.IndexOf("<!NOTATION", StringComparison.Ordinal));
        document.DocumentElement!.GetAttribute("suppressed").Should().BeNull();
        document.SkippedXmlEntities.Should().ContainSingle();
        document.SkippedXmlEntities[0].Kind.Should().Be(XmlSkippedEntityKind.Parameter);
    }

    [Test]
    public void UnreadExternalSubsetDoesNotInventNotations()
    {
        const string source = "<!DOCTYPE r SYSTEM 'file:///never-read.dtd'><r/>";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Should().BeEmpty();
        document.SkippedXmlEntities.Should().ContainSingle();
        document.SkippedXmlEntities[0].Kind.Should().Be(XmlSkippedEntityKind.ExternalSubset);
    }

    [Test]
    public void FragmentDoesNotInheritOrOverwriteOwnerNotations()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<!NOTATION n SYSTEM 's'>]><r/>");
        var snapshot = document.XmlNotations;
        var fragment = MarkupParser.ParseXmlFragment("<child/>", document.DocumentElement!);
        fragment.OwnerDocument.Should().BeSameAs(document);
        document.XmlNotations.Should().BeSameAs(snapshot);
        Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXmlFragment("<!NOTATION x SYSTEM 's'>",
            document.DocumentElement!));
        document.XmlNotations.Should().BeSameAs(snapshot);
    }

    [Test]
    public void ParsedInventorySurvivesDocumentCloneButDoesNotTransferWithNodes()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<!NOTATION n SYSTEM 's'>]><r/>");
        var snapshot = document.XmlNotations;
        var shallow = (Document) document.CloneNode();
        var deep = (Document) document.CloneNode(deep: true);
        shallow.XmlNotations.Should().BeSameAs(snapshot);
        deep.XmlNotations.Should().BeSameAs(snapshot);

        var destination = Document.CreateXml();
        destination.ImportNode(document.DocumentElement!).OwnerDocument.Should().BeSameAs(destination);
        destination.XmlNotations.Should().BeEmpty();
        document.RemoveChild(document.Doctype!);
        document.XmlNotations.Should().BeSameAs(snapshot);
    }

    [Test]
    public void RejectsTabInPublicLiteralEvenWhenWhitespaceWouldOtherwiseCollapse()
    {
        const string source = "<!DOCTYPE r [<!NOTATION n PUBLIC ' a\tb'>]><r/>";
        var error = Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXml(source));
        error!.Code.Should().Be("xml/invalid-declaration");
    }

    [Test]
    public void CorpusValidSa069ReportsUnusedNotationAtOriginalOffset()
    {
        const string source = "<!DOCTYPE doc [\r\n<!ELEMENT doc (#PCDATA)>\r\n" +
            "<!NOTATION n PUBLIC \"whatever\">\r\n]>\r\n<doc></doc>\r\n";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Should().ContainSingle();
        var notation = document.XmlNotations[0];
        (notation.Name, notation.PublicId, notation.SystemId, notation.Offset).Should()
            .Be(("n", "whatever", null, 43L));
        document.SkippedXmlEntities.Should().BeEmpty();
    }

    [Test]
    public void CorpusValidSa076ReportsBothReferencedNotationsAtOriginalOffsets()
    {
        const string source = "<!DOCTYPE doc [\r\n<!ELEMENT doc (#PCDATA)>\r\n" +
            "<!ATTLIST doc a NOTATION (n1|n2) #IMPLIED>\r\n" +
            "<!NOTATION n1 SYSTEM \"http://www.w3.org/\">\r\n" +
            "<!NOTATION n2 SYSTEM \"http://www.w3.org/\">\r\n]>\r\n<doc></doc>\r\n";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Select(x => (x.Name, x.PublicId, x.SystemId, x.Offset)).Should().Equal(
            ("n1", null, "http://www.w3.org/", 87L),
            ("n2", null, "http://www.w3.org/", 131L));
        document.DocumentElement!.GetAttribute("a").Should().BeNull();
        document.SkippedXmlEntities.Should().BeEmpty();
    }

    [Test]
    public void CorpusErrataE55ReportsNotationWithoutFetchingItsSystemId()
    {
        const string source = "<!DOCTYPE foo [\n<!ELEMENT foo ANY>\n" +
            "<!ENTITY e \"an &unparsed; entity\">\n" +
            "<!NOTATION gif SYSTEM \"file:///usr/X11R6/bin/xv\">\n" +
            "<!ENTITY unparsed SYSTEM \"xyzzy\" NDATA gif>\n]>\n<foo/>\n";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Should().ContainSingle();
        var notation = document.XmlNotations[0];
        (notation.Name, notation.PublicId, notation.SystemId, notation.Offset).Should()
            .Be(("gif", null, "file:///usr/X11R6/bin/xv", 70L));
        document.SkippedXmlEntities.Should().BeEmpty();
    }

    [Test]
    public void FifthEditionNotationNameAndNotationAttributeValueAreReported()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r format NOTATION (\u037Fgif) #IMPLIED>" +
            "<!NOTATION \u037Fgif SYSTEM 'urn:gif'>]><r format='\u037Fgif'/>";
        var document = MarkupParser.ParseXml(source);
        document.XmlNotations.Should().ContainSingle();
        document.XmlNotations[0].Name.Should().Be("\u037Fgif");
        document.DocumentElement!.GetAttribute("format").Should().Be("\u037Fgif");
    }

    [Test]
    public void NotationMarkupRespectsOriginalDoctypeTokenBoundary()
    {
        const string doctype = "<!DOCTYPE r [<!NOTATION n SYSTEM 's'>]>";
        const string source = doctype + "<r/>";
        MarkupParser.ParseXml(source, new XmlParseOptions
            { Limits = new ParseLimits { MaxTokenCharacters = doctype.Length } })
            .XmlNotations.Should().ContainSingle();
        var error = Assert.Throws<ParseLimitException>(() => MarkupParser.ParseXml(source,
            new XmlParseOptions { Limits = new ParseLimits { MaxTokenCharacters = doctype.Length - 1 } }));
        error!.Kind.Should().Be(ParseLimitKind.TokenCharacters);
        error.Observed.Should().Be(doctype.Length);
    }

    [Test]
    public void ParameterSuppliedNotationRespectsExpansionBudget()
    {
        const string declaration = "<!NOTATION n SYSTEM 's'>";
        const string source = "<!DOCTYPE r [<!ENTITY % p \"" + declaration + "\">%p;]><r/>";
        var consumed = declaration.Length + 2;
        MarkupParser.ParseXml(source, new XmlParseOptions
            { Limits = new ParseLimits { MaxEntityExpansionCharacters = consumed } })
            .XmlNotations.Should().ContainSingle();
        var error = Assert.Throws<ParseLimitException>(() => MarkupParser.ParseXml(source,
            new XmlParseOptions { Limits = new ParseLimits { MaxEntityExpansionCharacters = consumed - 1 } }));
        error!.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        error.Observed.Should().Be(consumed);
    }

    [Test]
    public void CancelsWithinLongNotationSystemLiteral()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var source = "<!DOCTYPE r [<!NOTATION n SYSTEM '" + new string('x', 12_000) + "'>]><r/>";
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded,
            () => { polls++; cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(1);
    }

    [Test]
    public void CancelsDuringPublicIdNormalizationAfterItsLiteralWasScanned()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        // The 3,000-unit literal scan cannot reach the 4,096-unit work poll.
        // Its second pass crosses that boundary within public-id normalization.
        var source = "<!DOCTYPE r [<!NOTATION n PUBLIC '" + new string('x', 3_000) + "'>]><r/>";
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded,
            () => { polls++; cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(1);
    }

    [Test]
    public void CancelsWhileCollectingManyCompleteNotations()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var declarations = string.Concat(Enumerable.Range(0, 32).Select(i =>
            "<!NOTATION n" + i + " SYSTEM 's'>"));
        // Fewer than 4,096 scanner work units precede the 32nd declaration.
        // The per-record checkpoint, before publishing a snapshot, must cancel.
        var source = "<!DOCTYPE r [" + declarations + "]><r/>";
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded,
            () => { if (++polls == 32) cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(32);
    }
}
