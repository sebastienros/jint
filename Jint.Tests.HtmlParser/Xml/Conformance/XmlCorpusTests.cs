#nullable enable
using System.Text;
using System.Text.Json;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

public class XmlCorpusTests
{
    [Test]
    public void PinLicenseRoutesAndEveryReferencedByteAreVerified()
    {
        XmlCorpus.Files.Should().HaveCount(3386);
        XmlCorpus.Lock.RowCount.Should().Be(2585);
        XmlCorpus.Lock.Categories["valid"].Should().Be(812);
        XmlCorpus.Lock.Categories["invalid"].Should().Be(242);
        XmlCorpus.Lock.Categories["not-wf"].Should().Be(1498);
        XmlCorpus.Lock.Categories["error"].Should().Be(33);
        XmlCorpus.Lock.ClarkChanged.Should().HaveCount(9);
        XmlCorpus.Lock.ClarkAdded.Should().HaveCount(13);
        XmlCorpus.Lock.Files.Count(file => file.Source == "edinburgh-vendor").Should().Be(527);
        XmlCorpus.Lock.Files.Count(file => file.Source == "unchanged-clark-zip").Should().Be(584);
        File.ReadAllText(Path.Combine(XmlCorpus.Root, "Vendor", "README.md"))
            .Should().Contain("unmodified").And.Contain("copyright");
        XmlCorpus.Cases.Count(row => row.Catalog.StartsWith("xmlconf/eduni/errata-2e/", StringComparison.Ordinal) ||
            row.Catalog.StartsWith("xmlconf/eduni/errata-3e/", StringComparison.Ordinal) ||
            row.Catalog.StartsWith("xmlconf/eduni/errata-4e/", StringComparison.Ordinal) ||
            row.Catalog.StartsWith("xmlconf/eduni/namespaces/1.0/", StringComparison.Ordinal) ||
            row.Catalog.StartsWith("xmlconf/eduni/namespaces/errata-1e/", StringComparison.Ordinal))
            .Should().Be(491);
    }

    [Test]
    public void ManifestHasOneDispositionPerFullSuiteRow()
    {
        var cases = XmlCorpus.Cases;
        cases.Should().HaveCount(2585);
        cases.GroupBy(row => row.Category).ToDictionary(group => group.Key, group => group.Count())
            .Should().ContainKey("not-wf").WhoseValue.Should().Be(1498);
        cases.Count(row => row.Disposition == "runnable").Should().Be(1947);
        cases.Count(row => row.Disposition == "outside-input-boundary").Should().Be(18);
        cases.Count(row => row.Disposition == "input-boundary-review").Should().Be(0);
        cases.Count(row => row.Disposition == "outside-profile").Should().Be(593);
        cases.Count(row => row.Disposition == "optional-error-review").Should().Be(27);
        cases.Count(row => row.Disposition == "runnable" && row.ResourceProfile == "unreviewed-external-indication")
            .Should().Be(411);
        cases.Select(row => row.Key).Distinct(StringComparer.Ordinal).Should().HaveCount(2585);
        foreach (var row in cases)
        {
            row.Disposition.Should().BeOneOf("runnable", "input-boundary-review", "outside-profile",
                "outside-input-boundary", "optional-error-review");
            row.Recommendation.Should().NotBeNullOrWhiteSpace();
            row.Sections.Should().NotBeNullOrWhiteSpace();
            row.Uri.Should().NotBeNullOrWhiteSpace();
            row.ResourceProfile.Should().BeOneOf("no-external-indication", "unreviewed-external-indication");
        }
        var corrected = cases.Where(row => row.Catalog == "xmlconf/eduni/misc/ht-bh.xml").ToArray();
        corrected.Should().HaveCount(9);
        corrected.Should().AllSatisfy(row => row.InputPath.Should().StartWith("xmlconf/eduni/misc/"));
    }

    [Test]
    public void ExactReviewedByteBoundariesNeverBecomeParserPasses()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(XmlCorpus.Root, "input-boundary.json")));
        var entries = table.RootElement.EnumerateArray().ToArray();
        entries.Should().HaveCount(18);
        var reviewedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = entry.GetProperty("key").GetString()!;
            reviewedKeys.Add(key).Should().BeTrue();
            var row = XmlCorpus.Case(key);
            row.Disposition.Should().Be("outside-input-boundary");
            row.BoundaryByteOffset.Should().Be(entry.GetProperty("byteOffset").GetInt32());
            row.Reason.Should().Be(entry.GetProperty("reason").GetString());
            row.Decoding.Status.Should().NotBe("decoded");
            entry.GetProperty("stringObligation").GetString().Should().NotBeNullOrWhiteSpace();
        }
        XmlCorpus.Cases.Where(row => row.Disposition == "outside-input-boundary")
            .Select(row => row.Key).Should().BeEquivalentTo(reviewedKeys);
    }

    [TestCase(0xD800, "text")]
    [TestCase(0xDC00, "text")]
    [TestCase(0xD800, "comment")]
    [TestCase(0xDFFF, "comment")]
    [TestCase(0xD800, "name-start")]
    [TestCase(0xD801, "name-start")]
    [TestCase(0xDAFF, "name-start")]
    [TestCase(0xDFFF, "name-start")]
    [TestCase(0xD800, "name-char")]
    [TestCase(0xD801, "name-char")]
    [TestCase(0xDAFF, "name-char")]
    [TestCase(0xDFFF, "name-char")]
    public void DecodedStringSurrogatesRemainIndependentXmlCharAndNameObligations(int codeUnit, string location)
    {
        // XML §2.2 Char and §2.3 Name/NameChar; this is a distinct string API test,
        // never a pass attributed to a byte-invalid W3C row.
        var surrogate = new string((char) codeUnit, 1);
        char.IsSurrogate(surrogate[0]).Should().BeTrue();
        var source = location switch
        {
            "text" => "<r>" + surrogate + "</r>",
            "comment" => "<!--" + surrogate + "--><r/>",
            "name-start" => "<" + surrogate + "n/>",
            _ => "<n" + surrogate + "/>"
        };
        Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXml(source));
    }

    [Test]
    public void DecodedSupplementaryPairIsAcceptedAsOneXmlScalar()
    {
        MarkupParser.ParseXml("<\U00010000/>").DocumentElement!.LocalName.Should().Be("\U00010000");
    }

    [Test]
    public void PathsAndCatalogMetadataCannotEscapeThePinnedGraph()
    {
        foreach (var denied in new[] { "../secret", "xmlconf/../secret", "xmlconf//a", "xmlconf/./a", "xmlconf/a\\b", "http://host/a", "/xmlconf/a" })
            Assert.Throws<InvalidDataException>(() => XmlCorpus.EnsureSafePath(denied));
        XmlCorpus.EnsureSafePath("xmlconf/eduni/errata-4e/xmlconf.xml");
        XmlCorpus.Lock.MetadataPaths.Should().HaveCount(23);
        XmlCorpus.Lock.MetadataPaths.Should().OnlyContain(path => path.EndsWith(".xml", StringComparison.Ordinal) ||
            path == "xmlconf/testcases.dtd");
        XmlCorpus.Cases.Select(row => row.Catalog).Distinct(StringComparer.Ordinal)
            .Should().OnlyContain(path => XmlCorpus.Lock.MetadataPaths.Contains(path));
        Assert.Throws<InvalidDataException>(() => XmlCorpus.VerifyDigest([1, 2, 3],
            XmlCorpus.ArchiveDigest, "negative archive digest probe"));
    }

    [Test]
    public void DecoderIsStrictAndPreservesTextWithoutNewlineRepair()
    {
        var (utf8, utf8Decision) = XmlByteDecoder.Decode("<r>\r\n</r>"u8);
        utf8.Should().Be("<r>\r\n</r>");
        utf8Decision.Decision.Should().Be("utf-8-default");

        var withBom = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes("<r>\r\n</r>")).ToArray();
        var (bomText, bomDecision) = XmlByteDecoder.Decode(withBom);
        bomText.Should().Be("<r>\r\n</r>");
        bomDecision.Decision.Should().Be("utf-8-bom");

        var utf16 = new byte[] { 0xff, 0xfe }.Concat(Encoding.Unicode.GetBytes("<r>\r\n</r>")).ToArray();
        var (utf16Text, utf16Decision) = XmlByteDecoder.Decode(utf16);
        utf16Text.Should().Be("<r>\r\n</r>");
        utf16Decision.Decision.Should().Be("utf-16-le-bom");

        var (badText, badDecision) = XmlByteDecoder.Decode(new byte[] { 0x3c, 0x72, 0x3e, 0xff });
        badText.Should().BeNull();
        badDecision.Status.Should().Be("strict-decode-error");
        var (legacyText, legacyDecision) = XmlByteDecoder.Decode(Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='EUC-JP'?><r/>"));
        legacyText.Should().BeNull();
        legacyDecision.Status.Should().Be("declared-encoding-review");
        var (badNameText, badNameDecision) = XmlByteDecoder.Decode(Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='UTF:8'?><r/>"));
        badNameText.Should().NotBeNull();
        badNameDecision.Status.Should().Be("decoded");
        var latin1 = Encoding.Latin1.GetBytes("<?xml version='1.0' encoding='iso-8859-1'?><r>é</r>");
        var (latin1Text, latin1Decision) = XmlByteDecoder.Decode(latin1);
        latin1Text.Should().EndWith("<r>é</r>");
        latin1Decision.Decision.Should().Be("iso-8859-1-declaration");
        var namespaceCase = XmlCorpus.Case("xmlconf/eduni/namespaces/1.0/rmt-ns10.xml#rmt-ns10-006");
        var (pinnedText, pinnedDecision) = XmlByteDecoder.Decode(XmlCorpus.Bytes(namespaceCase.InputPath));
        pinnedText.Should().Contain("rosé");
        pinnedDecision.Decision.Should().Be(namespaceCase.Decoding.Decision);
    }

    [Test]
    public void SecondCanonicalAdapterHasPositiveAndNegativeEvidence()
    {
        var inputBase = XmlEvidence.CorpusInputBase("xmlconf/probes/input.xml");
        var document = MarkupParser.ParseXml("<r b='2' a='&amp;'><![CDATA[x<y]]><!--ignored--><?pi z?></r>");
        const string expected = "<r a=\"&amp;\" b=\"2\">x&lt;y<?pi z?></r>";
        var actual = XmlEvidence.SecondCanonicalForm(document, inputBase);
        actual.Should().Be(expected);
        actual.Should().NotBe("<r b=\"2\" a=\"&amp;\">x&lt;y<?pi z?></r>");
        var notationDocument = MarkupParser.ParseXml("<!DOCTYPE r [<!NOTATION n SYSTEM 'x'>]><r/>");
        XmlEvidence.SecondCanonicalForm(notationDocument, inputBase).Should().Be("<!DOCTYPE r [\n<!NOTATION n SYSTEM 'x'>\n]>\n<r></r>");
        var unsortedNotations = MarkupParser.ParseXml("<!DOCTYPE r [<!NOTATION z SYSTEM 'z'><!NOTATION a SYSTEM 'a'>]><r/>");
        unsortedNotations.XmlNotations.Select(item => item.Name).Should().Equal("z", "a");
        XmlEvidence.SecondCanonicalForm(unsortedNotations, inputBase).Should().Be(
            "<!DOCTYPE r [\n<!NOTATION a SYSTEM 'a'>\n<!NOTATION z SYSTEM 'z'>\n]>\n<r></r>");
        var bothIdentifiers = MarkupParser.ParseXml("<!DOCTYPE r [<!NOTATION n PUBLIC 'p' 's'>]><r/>");
        XmlEvidence.SecondCanonicalForm(bothIdentifiers, inputBase).Should().Be(
            "<!DOCTYPE r [\n<!NOTATION n PUBLIC 'p' 's'>\n]>\n<r></r>");
        var scalarNames = MarkupParser.ParseXml("<r a😀='x' a豈='y'/>");
        const string scalarOrder = "<r a豈=\"y\" a😀=\"x\"></r>";
        XmlEvidence.SecondCanonicalForm(scalarNames, inputBase).Should().Be(scalarOrder);
    }

    [Test]
    public void SecondCanonicalSystemIdentifiersUseExplicitInputProvenance()
    {
        var inputBase = XmlEvidence.CorpusInputBase("xmlconf/probes/input.xml");
        XmlEvidence.CanonicalSystemId("http://example.org/n#frag", inputBase).Should().Be("http://example.org/n");
        XmlEvidence.CanonicalSystemId("http://example.org/rosé#frag", inputBase)
            .Should().Be("http://example.org/ros%C3%A9");
        XmlEvidence.CanonicalSystemId("./a/../n#frag", inputBase).Should().Be("n");
        XmlEvidence.CanonicalSystemId("n%23part#frag", inputBase).Should().Be("n%23part");
        XmlEvidence.CanonicalSystemId("😀.xml#frag", inputBase).Should().Be("%F0%9F%98%80.xml");
        XmlEvidence.CanonicalSystemId("?q=1", inputBase).Should().Be("?q=1");
        XmlEvidence.CanonicalSystemId("https://xmlconf.invalid/xmlconf/probes/a/../n", inputBase)
            .Should().Be("n");
        XmlEvidence.CanonicalSystemId("https://xmlconf.invalid/xmlconf/probes/", inputBase)
            .Should().Be(".");
        XmlEvidence.CanonicalSystemId("https://xmlconf.invalid/xmlconf/", inputBase)
            .Should().Be("..");
        XmlEvidence.CanonicalSystemId("https://xmlconf.invalid/xmlconf/probes/?q=1", inputBase)
            .Should().Be(".?q=1");
        XmlEvidence.CanonicalSystemId("https://xmlconf.invalid/xmlconf/?q=1", inputBase)
            .Should().Be("..?q=1");
        XmlEvidence.CanonicalSystemId("https://user@xmlconf.invalid/xmlconf/probes/n", inputBase)
            .Should().Be("https://user@xmlconf.invalid/xmlconf/probes/n");
        var deepBase = XmlEvidence.CorpusInputBase("xmlconf/a/b/c/d/input.xml");
        XmlEvidence.CanonicalSystemId("https://xmlconf.invalid/x", deepBase).Should().Be("/x");
        XmlEvidence.CorpusInputBase("xmlconf/probes/in#put?.xml").AbsoluteUri
            .Should().EndWith("/xmlconf/probes/in%23put%3F.xml");
        XmlEvidence.CanonicalSystemId("file:/dev/null", inputBase).Should().Be("file:/dev/null");
        XmlEvidence.CanonicalSystemId("http://www.w3.org/", inputBase).Should().Be("http://www.w3.org/");

        const string raw = "http://example.org/rosé#frag";
        var document = MarkupParser.ParseXml($"<!DOCTYPE r [<!NOTATION n SYSTEM '{raw}'>]><r/>");
        document.XmlNotations.Single().SystemId.Should().Be(raw);
        XmlEvidence.SecondCanonicalForm(document, inputBase).Should().Be(
            "<!DOCTYPE r [\n<!NOTATION n SYSTEM 'http://example.org/ros%C3%A9'>\n]>\n<r></r>");
        document.XmlNotations.Single().SystemId.Should().Be(raw);
    }

    [Test]
    public void WrongBinaryRecordAndOutputCannotReportConformancePass()
    {
        var simple = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-sa-001");
        XmlConformanceRunner.Run(simple).Kind.Should().Be(XmlOutcomeKind.Pass);
        XmlConformanceRunner.Run(simple, new XmlCaseExpectation
        {
            Key = simple.Key, Outcome = "reject", Skipped = [], Review = "negative probe"
        }).Signature.Should().Be("accepted:expected-rejection");
        XmlConformanceRunner.Run(simple, testOutput: "<wrong></wrong>"u8.ToArray()).Signature
            .Should().StartWith("output-mismatch:");

        var resource = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-sa-070");
        var wrongSkip = XmlConformanceRunner.Run(resource, new XmlCaseExpectation
        {
            Key = resource.Key, Outcome = "accept", Review = "negative probe", Projection = [],
            Skipped = [new XmlSkippedExpectation { Kind = "General", Name = "not-the-entity", Offset = -1 }]
        });
        wrongSkip.Kind.Should().Be(XmlOutcomeKind.ParserFailure);
        wrongSkip.Signature.Should().StartWith("skip-");
        var missingProjection = XmlConformanceRunner.Run(resource, new XmlCaseExpectation
        {
            Key = resource.Key, Outcome = "accept", Review = "negative probe", Skipped = []
        });
        missingProjection.Kind.Should().Be(XmlOutcomeKind.HarnessFailure);
        missingProjection.Signature.Should().Be("review-missing-projection");
        var wrongProjection = XmlConformanceRunner.Run(resource, new XmlCaseExpectation
        {
            Key = resource.Key, Outcome = "accept", Review = "negative probe", Skipped = [], Projection = []
        });
        wrongProjection.Kind.Should().Be(XmlOutcomeKind.ParserFailure);
        wrongProjection.Signature.Should().Be("projection-mismatch");

        var externalNegative = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#not-wf-sa-185");
        XmlConformanceRunner.Run(externalNegative).Signature.Should().Be("resource-profile-review");

        var originalAfterOmission = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-sa-097");
        XmlConformanceRunner.Run(originalAfterOmission).Kind.Should().Be(XmlOutcomeKind.Pass);
        XmlConformanceRunner.Run(originalAfterOmission, testOutput: "<wrong></wrong>"u8.ToArray())
            .Signature.Should().StartWith("output-mismatch:");

        var reviewedAlternative = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-not-sa-003");
        var alternative = XmlExpectations.Reviewed[reviewedAlternative.Key];
        XmlConformanceRunner.Run(reviewedAlternative, new XmlCaseExpectation
        {
            Key = reviewedAlternative.Key, Outcome = "accept", Skipped = alternative.Skipped,
            Projection = alternative.Projection, Notations = alternative.Notations,
            OutputPolicy = "no-fetch-alternative", OriginalOutputSha256 = alternative.OriginalOutputSha256,
            ProjectionSha256 = alternative.ProjectionSha256, OutputAlternative = "<wrong></wrong>",
            Review = "negative no-fetch output probe"
        }).Signature.Should().Be("no-fetch-canonical-mismatch");
        var reviewedOriginal = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-not-sa-027");
        XmlConformanceRunner.Run(reviewedOriginal, testOutput: "<wrong></wrong>"u8.ToArray())
            .Signature.Should().StartWith("output-mismatch:");
    }

    [Test]
    public void OptionalErrorsSeparatePolicyEvidenceFromUnreviewedAndAdapterDebt()
    {
        var verified = XmlCorpus.Case("xmlconf/eduni/namespaces/1.0/rmt-ns10.xml#rmt-ns10-004");
        XmlConformanceRunner.Run(verified).Kind.Should().Be(XmlOutcomeKind.OptionalPolicyVerified);
        var policy = XmlExpectations.OptionalPolicies[verified.Key];
        var wrongProjection = XmlConformanceRunner.Run(verified, new XmlCaseExpectation
        {
            Status = "verified", Outcome = "accept", Skipped = [],
            Projection = policy.Projection!.Select((entry, index) =>
                index == 0 ? entry with { Value = "wrong comment" } : entry).ToArray()
        });
        wrongProjection.Kind.Should().Be(XmlOutcomeKind.OptionalPolicyMismatch);
        wrongProjection.Signature.Should().Be("projection-mismatch");
        var wrongOmission = XmlConformanceRunner.Run(verified, new XmlCaseExpectation
        {
            Status = "verified", Outcome = "accept", Projection = policy.Projection,
            Skipped = [new XmlSkippedExpectation { Kind = "ExternalSubset", SystemId = "not-present" }]
        });
        wrongOmission.Kind.Should().Be(XmlOutcomeKind.OptionalPolicyMismatch);
        wrongOmission.Signature.Should().StartWith("skip-count:");

        var newlyReviewed = XmlCorpus.Case("xmlconf/oasis/oasis.xml#o-p11pass1");
        XmlConformanceRunner.Run(newlyReviewed).Kind.Should().Be(XmlOutcomeKind.OptionalPolicyVerified);

        var unsupportedEncoding = XmlCorpus.Case("xmlconf/japanese/japanese.xml#pr-xml-euc-jp");
        var unavailable = XmlConformanceRunner.Run(unsupportedEncoding);
        unavailable.Kind.Should().Be(XmlOutcomeKind.OptionalAdapterDebt);
        unavailable.Detail.Should().Contain("input adapter unavailable");

        var notationDependent = XmlCorpus.Case("xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-55");
        XmlConformanceRunner.Run(notationDependent).Kind.Should().Be(XmlOutcomeKind.OptionalPolicyVerified);
    }

    [Test]
    public void OptionalOutputNeedsPinnedOriginalBytesBeforeVerification()
    {
        var row = XmlCorpus.Case("xmlconf/ibm/ibm_oasis_invalid.xml#ibm-invalid-P68-ibm68i01.xml");
        var projection = new[]
        {
            new XmlProjectionEntry(0, "DocumentType", "root", null, null, null, null, "", "ibm68i01.dtd"),
            new XmlProjectionEntry(0, "Element", "root", null, null, null, [], null, null),
            new XmlProjectionEntry(1, "Text", null, null, null, "\n  pcdata content\n  ", null, null, null),
            new XmlProjectionEntry(1, "Element", "a", null, null, null,
                [new XmlProjectionAttribute("attr1", null, null, "xyz")], null, null),
            new XmlProjectionEntry(1, "Text", null, null, null, "\n", null, null, null),
            new XmlProjectionEntry(0, "Comment", null, null, null,
                "* a invalid test for P68 VC:Entity Declared *", null, null, null)
        };
        const string digest = "0259a806665026c50fa2dbdc8169f3c01cef0d238866f73af33f0aedf1df1539";
        XmlCaseExpectation Policy(string? outputPolicy, string? outputDigest) => new()
        {
            Key = row.Key, Status = "verified", Outcome = "accept", Review = "pinned source output probe",
            Skipped = [new XmlSkippedExpectation
            {
                Kind = "ExternalSubset", Name = "", SystemId = "ibm68i01.dtd", Offset = 23
            }],
            Projection = projection, Notations = [], OutputPolicy = outputPolicy,
            OriginalOutputSha256 = outputDigest
        };

        var reviewed = Policy("original-output-after-omission", digest);
        XmlConformanceRunner.Run(row, reviewed).Kind.Should().Be(XmlOutcomeKind.OptionalPolicyVerified);
        var corrupted = XmlConformanceRunner.Run(row, reviewed, "<wrong></wrong>"u8.ToArray());
        corrupted.Kind.Should().Be(XmlOutcomeKind.OptionalPolicyMismatch);
        corrupted.Signature.Should().StartWith("output-mismatch:");
        XmlConformanceRunner.Run(row, Policy(null, digest)).Signature
            .Should().Be("optional-output-review-missing");
        var wrongDigest = XmlConformanceRunner.Run(row, Policy("original-output-after-omission", new string('0', 64)));
        wrongDigest.Kind.Should().Be(XmlOutcomeKind.HarnessFailure);
        wrongDigest.Signature.Should().Be("reviewed-output-pin-mismatch");

        var withoutOutput = XmlCorpus.Case("xmlconf/eduni/namespaces/1.0/rmt-ns10.xml#rmt-ns10-004");
        var existing = XmlExpectations.OptionalPolicies[withoutOutput.Key];
        XmlConformanceRunner.Run(withoutOutput, new XmlCaseExpectation
        {
            Key = withoutOutput.Key, Status = "verified", Outcome = "accept", Review = "stale output probe",
            Skipped = existing.Skipped, Projection = existing.Projection, Notations = existing.Notations,
            OutputPolicy = "original-output-after-omission", OriginalOutputSha256 = digest
        }).Signature.Should().Be("optional-output-without-output");
    }

    [Test]
    public void NotationMetadataAndOriginalOutputRejectIndependentCorruption()
    {
        var row = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-sa-069");
        var reviewed = XmlExpectations.Reviewed[row.Key];
        var notation = reviewed.Notations!.Single();
        static XmlNotationExpectation Changed(XmlNotationExpectation original, string? name = null,
            string? publicId = null, string? systemId = null, long? offset = null) => new()
        {
            Name = name ?? original.Name,
            PublicId = publicId ?? original.PublicId,
            SystemId = systemId ?? original.SystemId,
            Offset = offset ?? original.Offset
        };
        XmlCaseExpectation WithNotations(XmlNotationExpectation[] notations) => new()
        {
            Key = row.Key, Outcome = "accept", Skipped = reviewed.Skipped,
            Projection = reviewed.Projection, Notations = notations, Review = "negative probe"
        };
        foreach (var changed in new[]
                 {
                     Changed(notation, name: "wrong"),
                     Changed(notation, publicId: "other"),
                     Changed(notation, offset: notation.Offset + 1),
                     new XmlNotationExpectation { Name = notation.Name, PublicId = notation.PublicId,
                         SystemId = "", Offset = notation.Offset }
                 })
        {
            var result = XmlConformanceRunner.Run(row, WithNotations([changed]));
            result.Kind.Should().Be(XmlOutcomeKind.ParserFailure);
            result.Signature.Should().Be("notation-mismatch:0");
        }
        XmlConformanceRunner.Run(row, WithNotations([])).Signature.Should().Be("notation-count:1");
        XmlConformanceRunner.Run(row, WithNotations([notation, notation])).Signature.Should().Be("notation-count:1");

        var ordered = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#valid-sa-076");
        var originalOrder = XmlExpectations.Reviewed[ordered.Key];
        XmlConformanceRunner.Run(ordered, new XmlCaseExpectation
        {
            Key = ordered.Key, Outcome = "accept", Skipped = originalOrder.Skipped,
            Projection = originalOrder.Projection, Notations = originalOrder.Notations!.AsEnumerable().Reverse().ToArray(),
            Review = "negative order probe"
        }).Signature.Should().Be("notation-mismatch:0");
        XmlConformanceRunner.Run(ordered, new XmlCaseExpectation
        {
            Key = ordered.Key, Outcome = "accept", Skipped = originalOrder.Skipped,
            Projection = originalOrder.Projection,
            Notations = [new XmlNotationExpectation { Name = "n1", SystemId = "http://wrong/", Offset = 87 },
                originalOrder.Notations![1]], Review = "negative system identifier probe"
        }).Signature.Should().Be("notation-mismatch:0");

        var pinnedOutput = XmlCorpus.Bytes(row.OutputPath!);
        foreach (var wrongSource in new[]
                 {
                     "<!DOCTYPE doc [<!NOTATION wrong PUBLIC 'whatever'>]><doc/>",
                     "<!DOCTYPE doc [<!NOTATION n PUBLIC 'other'>]><doc/>",
                     "<!DOCTYPE doc []><doc/>",
                     "<!DOCTYPE doc [<!NOTATION n PUBLIC 'whatever'><!NOTATION extra SYSTEM 'x'>]><doc/>"
                 })
        {
            var actual = Encoding.UTF8.GetBytes(XmlEvidence.SecondCanonicalForm(MarkupParser.ParseXml(wrongSource),
                XmlEvidence.CorpusInputBase(row.InputPath)));
            actual.AsSpan().SequenceEqual(pinnedOutput).Should().BeFalse();
        }
    }

    [Test]
    public void ReviewedRecordsAndDeviationsCannotGoStaleSilently()
    {
        var active = XmlCorpus.Cases.Where(row => row.Disposition is not ("outside-profile" or "outside-input-boundary"))
            .Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, expectation) in XmlExpectations.Reviewed)
        {
            active.Should().Contain(key);
            expectation.Review.Should().NotBeNullOrWhiteSpace();
            expectation.Skipped.Should().NotBeNull();
            expectation.Outcome.Should().BeOneOf("accept", "reject");
            if (XmlCorpus.Case(key).ResourceProfile == "unreviewed-external-indication" && expectation.Outcome == "accept")
                expectation.Projection.Should().NotBeNull();
            if (expectation.OutputPolicy is not null)
            {
                expectation.OutputPolicy.Should().BeOneOf("no-fetch-alternative", "original-output-after-omission");
                var output = XmlCorpus.Case(key).OutputPath;
                output.Should().NotBeNull();
                var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(XmlCorpus.Bytes(output!)))
                    .ToLowerInvariant();
                expectation.OriginalOutputSha256.Should().Be(digest);
                if (expectation.OutputPolicy == "no-fetch-alternative")
                {
                    expectation.ProjectionSha256.Should().NotBeNullOrWhiteSpace();
                    expectation.OutputAlternative.Should().NotBeNullOrWhiteSpace();
                }
            }
        }
        foreach (var id in new[] { "069", "076", "090", "091" })
        {
            var key = "xmlconf/xmltest/xmltest.xml#valid-sa-" + id;
            XmlExpectations.Reviewed[key].Notations.Should().NotBeNullOrEmpty();
            XmlCorpus.Case(key).OutputPath.Should().NotBeNull();
        }
        XmlExpectations.OptionalPolicies.Values.Count(item => item.Status == "verified").Should().Be(21);
        foreach (var (key, policy) in XmlExpectations.OptionalPolicies)
        {
            var row = XmlCorpus.Case(key);
            row.Category.Should().Be("error");
            row.Disposition.Should().Be("optional-error-review");
            policy.Status.Should().Be("verified");
            policy.Outcome.Should().Be("accept");
            policy.Skipped.Should().NotBeNull();
            policy.Projection.Should().NotBeNull();
            policy.Review.Should().NotBeNullOrWhiteSpace();
            if (row.OutputPath is not null)
            {
                policy.OutputPolicy.Should().Be("original-output-after-omission");
                policy.Skipped.Should().NotBeEmpty();
                var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(XmlCorpus.Bytes(row.OutputPath)))
                    .ToLowerInvariant();
                policy.OriginalOutputSha256.Should().Be(digest);
            }
            else
            {
                policy.OutputPolicy.Should().BeNull();
                policy.OriginalOutputSha256.Should().BeNull();
            }
            if (key.EndsWith("#rmt-e2e-55", StringComparison.Ordinal))
            {
                policy.Notations.Should().ContainSingle();
                policy.Notations![0].Name.Should().Be("gif");
            }
        }
        foreach (var (key, deviation) in XmlExpectations.KnownFailures)
        {
            active.Should().Contain(key);
            deviation.Issue.Should().NotBeNullOrWhiteSpace();
            deviation.Reason.Should().NotBeNullOrWhiteSpace();
            deviation.Signature.Should().NotBeNullOrWhiteSpace();
            deviation.Citation.Should().StartWith("https://");
        }
    }

    [Test]
    public void FullProfileCensusReportsDebtByCollectionAndCategory()
    {
        var counts = new Dictionary<(string Collection, string Category), (int Inventoried, int Outside, int OutsideInput, int Runnable,
            int Passing, int KnownFailing, int Unresolved, int HarnessFailure, int OutputEligible, int OutputCompared,
            int OutputPending, int NoFetchAdapted, int OptionalObserved, int OptionalAdapter, int OptionalVerified,
            int OptionalMismatch)>();
        var failures = new List<(XmlOutcomeKind Kind, string Text)>();
        foreach (var row in XmlCorpus.Cases)
        {
            var key = (row.Collection, row.Category);
            counts.TryGetValue(key, out var count);
            count.Inventoried++;
            if (row.Disposition == "outside-profile") count.Outside++;
            else if (row.Disposition == "outside-input-boundary") count.OutsideInput++;
            else
            {
                if (row.Disposition == "runnable") count.Runnable++;
                if (row.OutputPath is not null) count.OutputEligible++;
                var outcome = XmlConformanceRunner.Run(row);
                if (outcome.Kind == XmlOutcomeKind.Pass)
                {
                    count.Passing++;
                    if (row.OutputPath is not null) count.OutputCompared++;
                    if (XmlExpectations.Reviewed.TryGetValue(row.Key, out var passingReview) &&
                        passingReview.OutputPolicy == "no-fetch-alternative") count.NoFetchAdapted++;
                }
                else if (outcome.Kind == XmlOutcomeKind.OptionalPolicyVerified)
                {
                    count.OptionalVerified++;
                    if (row.OutputPath is not null) count.OutputCompared++;
                }
                else if (outcome.Kind == XmlOutcomeKind.OptionalObservedUnreviewed)
                {
                    count.OptionalObserved++;
                    failures.Add((outcome.Kind, $"optional-unreviewed {row.Key}: {outcome.Signature}: {outcome.Detail}"));
                }
                else if (outcome.Kind == XmlOutcomeKind.OptionalAdapterDebt)
                {
                    count.OptionalAdapter++;
                    failures.Add((outcome.Kind, $"optional-adapter {row.Key}: {outcome.Signature}: {outcome.Detail}"));
                }
                else if (outcome.Kind == XmlOutcomeKind.OptionalPolicyMismatch)
                {
                    count.OptionalMismatch++;
                    failures.Add((outcome.Kind, $"optional-mismatch {row.Key}: {outcome.Signature}: {outcome.Detail}"));
                }
                else if (XmlExpectations.KnownFailures.TryGetValue(row.Key, out var deviation) &&
                         outcome.Signature == deviation.Signature)
                {
                    count.KnownFailing++;
                    failures.Add((outcome.Kind, $"known {row.Key}: {outcome.Signature}"));
                }
                else if (outcome.Kind == XmlOutcomeKind.HarnessFailure)
                {
                    count.HarnessFailure++;
                    failures.Add((outcome.Kind, $"harness {row.Key}: {outcome.Signature}: {outcome.Detail}"));
                }
                else
                {
                    count.Unresolved++;
                    failures.Add((outcome.Kind, $"unresolved {row.Key}: {outcome.Signature}: {outcome.Detail}"));
                }
                if (row.OutputPath is not null && outcome.Kind is not (XmlOutcomeKind.Pass or XmlOutcomeKind.OptionalPolicyVerified))
                    count.OutputPending++;
            }
            counts[key] = count;
        }
        foreach (var (key, count) in counts)
        {
            if (count.OutputEligible != count.OutputCompared + count.OutputPending)
                Assert.Fail($"Output census lost an eligible assertion for {key.Collection}/{key.Category}");
        }
        foreach (var (key, count) in counts.OrderBy(item => item.Key.Collection, StringComparer.Ordinal)
                     .ThenBy(item => item.Key.Category, StringComparer.Ordinal))
        {
            TestContext.Progress.WriteLine($"{key.Collection}/{key.Category}: inventoried={count.Inventoried} outsideProfile={count.Outside} " +
                $"outsideInput={count.OutsideInput} " +
                $"runnable={count.Runnable} passing={count.Passing} knownFailing={count.KnownFailing} " +
                $"unresolved={count.Unresolved} harnessFailures={count.HarnessFailure} " +
                $"optionalObserved={count.OptionalObserved} optionalAdapter={count.OptionalAdapter} " +
                $"optionalVerified={count.OptionalVerified} optionalMismatch={count.OptionalMismatch} " +
                $"outputEligible={count.OutputEligible} outputCompared={count.OutputCompared} " +
                $"outputPending={count.OutputPending} noFetchAdapted={count.NoFetchAdapted}");
        }
        if (failures.Count > 0)
        {
            var summary = string.Join(", ", failures.GroupBy(item => item.Kind)
                .Select(group => $"{group.Key}={group.Count()}"));
            var totals = new
            {
                Inventoried = counts.Values.Sum(item => item.Inventoried),
                Outside = counts.Values.Sum(item => item.Outside),
                OutsideInput = counts.Values.Sum(item => item.OutsideInput),
                Runnable = counts.Values.Sum(item => item.Runnable),
                Passing = counts.Values.Sum(item => item.Passing),
                Known = counts.Values.Sum(item => item.KnownFailing),
                Unresolved = counts.Values.Sum(item => item.Unresolved),
                Harness = counts.Values.Sum(item => item.HarnessFailure),
                OptionalObserved = counts.Values.Sum(item => item.OptionalObserved),
                OptionalAdapter = counts.Values.Sum(item => item.OptionalAdapter),
                OptionalVerified = counts.Values.Sum(item => item.OptionalVerified),
                OptionalMismatch = counts.Values.Sum(item => item.OptionalMismatch),
                OutputEligible = counts.Values.Sum(item => item.OutputEligible),
                OutputCompared = counts.Values.Sum(item => item.OutputCompared),
                OutputPending = counts.Values.Sum(item => item.OutputPending),
                NoFetch = counts.Values.Sum(item => item.NoFetchAdapted)
            };
            Assert.Fail($"W3C XML profile: inventoried={totals.Inventoried}, outsideProfile={totals.Outside}, " +
                $"outsideInput={totals.OutsideInput}, runnable={totals.Runnable}, " +
                $"passing={totals.Passing}, knownFailing={totals.Known}, unresolved={totals.Unresolved}, " +
                $"harnessFailures={totals.Harness}, optionalObserved={totals.OptionalObserved}, " +
                $"optionalAdapter={totals.OptionalAdapter}, optionalVerified={totals.OptionalVerified}, " +
                $"optionalMismatch={totals.OptionalMismatch}, outputEligible={totals.OutputEligible}, " +
                $"outputCompared={totals.OutputCompared}, outputPending={totals.OutputPending}, " +
                $"noFetchAdapted={totals.NoFetch}. Failures={failures.Count} ({summary}). " +
                "First 30 parser/harness failures:\n" +
                string.Join("\n", failures.Where(item => item.Kind is XmlOutcomeKind.ParserFailure or XmlOutcomeKind.HarnessFailure or XmlOutcomeKind.OptionalPolicyMismatch)
                    .Take(30).Select(item => item.Text)) + "\nFirst 15 pending:\n" +
                string.Join("\n", failures.Where(item => item.Kind is XmlOutcomeKind.Pending or XmlOutcomeKind.OptionalObservedUnreviewed or XmlOutcomeKind.OptionalAdapterDebt)
                    .Take(15).Select(item => item.Text)));
        }
    }
}
