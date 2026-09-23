#nullable enable
using System.Text;
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
        cases.Count(row => row.Disposition == "input-boundary-review").Should().Be(18);
        cases.Count(row => row.Disposition == "outside-profile").Should().Be(593);
        cases.Count(row => row.Disposition == "optional-error-review").Should().Be(27);
        cases.Count(row => row.Disposition == "runnable" && row.ResourceProfile == "unreviewed-external-indication")
            .Should().Be(411);
        cases.Select(row => row.Key).Distinct(StringComparer.Ordinal).Should().HaveCount(2585);
        foreach (var row in cases)
        {
            row.Disposition.Should().BeOneOf("runnable", "input-boundary-review", "outside-profile", "optional-error-review");
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
        var (legacyText, legacyDecision) = XmlByteDecoder.Decode(Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='ISO-8859-1'?><r/>"));
        legacyText.Should().BeNull();
        legacyDecision.Status.Should().Be("declared-encoding-review");
        var (badNameText, badNameDecision) = XmlByteDecoder.Decode(Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='UTF:8'?><r/>"));
        badNameText.Should().NotBeNull();
        badNameDecision.Status.Should().Be("decoded");
    }

    [Test]
    public void SecondCanonicalAdapterHasPositiveAndNegativeEvidence()
    {
        var document = MarkupParser.ParseXml("<r b='2' a='&amp;'><![CDATA[x<y]]><!--ignored--><?pi z?></r>");
        const string expected = "<r a=\"&amp;\" b=\"2\">x&lt;y<?pi z?></r>";
        var actual = XmlEvidence.SecondCanonicalForm(document, Encoding.UTF8.GetBytes(expected));
        actual.Should().Be(expected);
        actual.Should().NotBe("<r b=\"2\" a=\"&amp;\">x&lt;y<?pi z?></r>");
        Assert.Throws<XmlOutputObservationGapException>(() =>
            XmlEvidence.SecondCanonicalForm(document, "<!DOCTYPE r [\n<!NOTATION n SYSTEM 'x'>\n]>\n<r></r>"u8));
        var scalarNames = MarkupParser.ParseXml("<r a😀='x' a豈='y'/>");
        const string scalarOrder = "<r a豈=\"y\" a😀=\"x\"></r>";
        XmlEvidence.SecondCanonicalForm(scalarNames, Encoding.UTF8.GetBytes(scalarOrder)).Should().Be(scalarOrder);
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
            Key = resource.Key, Outcome = "accept", Review = "negative probe",
            Skipped = [new XmlSkippedExpectation { Kind = "General", Name = "not-the-entity", Offset = -1 }]
        });
        wrongSkip.Kind.Should().Be(XmlOutcomeKind.ParserFailure);
        wrongSkip.Signature.Should().StartWith("skip-");

        var externalNegative = XmlCorpus.Case("xmlconf/xmltest/xmltest.xml#not-wf-sa-054");
        XmlConformanceRunner.Run(externalNegative).Signature.Should().Be("resource-profile-review");
    }

    [Test]
    public void ReviewedRecordsAndDeviationsCannotGoStaleSilently()
    {
        var active = XmlCorpus.Cases.Where(row => row.Disposition != "outside-profile")
            .Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, expectation) in XmlExpectations.Reviewed)
        {
            active.Should().Contain(key);
            expectation.Review.Should().NotBeNullOrWhiteSpace();
            expectation.Skipped.Should().NotBeNull();
            expectation.Outcome.Should().BeOneOf("accept", "reject");
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
        var counts = new Dictionary<(string Collection, string Category), (int Inventoried, int Outside, int Runnable,
            int Passing, int KnownFailing, int Unresolved, int HarnessFailure, int OutputEligible, int OutputCompared,
            int OutputPending, int NoFetchAdapted)>();
        var failures = new List<(XmlOutcomeKind Kind, string Text)>();
        foreach (var row in XmlCorpus.Cases)
        {
            var key = (row.Collection, row.Category);
            counts.TryGetValue(key, out var count);
            count.Inventoried++;
            if (row.Disposition == "outside-profile") count.Outside++;
            else
            {
                if (row.Disposition == "runnable") count.Runnable++;
                if (row.OutputPath is not null) count.OutputEligible++;
                var outcome = XmlConformanceRunner.Run(row);
                if (outcome.Kind == XmlOutcomeKind.Pass)
                {
                    count.Passing++;
                    if (row.OutputPath is not null) count.OutputCompared++;
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
                    if (row.OutputPath is not null) count.OutputPending++;
                    failures.Add((outcome.Kind, $"unresolved {row.Key}: {outcome.Signature}: {outcome.Detail}"));
                }
                if (XmlExpectations.Reviewed.TryGetValue(row.Key, out var review) &&
                    review.OutputPolicy == "no-fetch-alternative") count.NoFetchAdapted++;
            }
            counts[key] = count;
        }
        foreach (var (key, count) in counts.OrderBy(item => item.Key.Collection, StringComparer.Ordinal)
                     .ThenBy(item => item.Key.Category, StringComparer.Ordinal))
        {
            TestContext.Progress.WriteLine($"{key.Collection}/{key.Category}: inventoried={count.Inventoried} outside={count.Outside} " +
                $"runnable={count.Runnable} passing={count.Passing} knownFailing={count.KnownFailing} " +
                $"unresolved={count.Unresolved} harnessFailures={count.HarnessFailure} " +
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
                Runnable = counts.Values.Sum(item => item.Runnable),
                Passing = counts.Values.Sum(item => item.Passing),
                Known = counts.Values.Sum(item => item.KnownFailing),
                Unresolved = counts.Values.Sum(item => item.Unresolved),
                Harness = counts.Values.Sum(item => item.HarnessFailure),
                OutputEligible = counts.Values.Sum(item => item.OutputEligible),
                OutputCompared = counts.Values.Sum(item => item.OutputCompared),
                OutputPending = counts.Values.Sum(item => item.OutputPending),
                NoFetch = counts.Values.Sum(item => item.NoFetchAdapted)
            };
            Assert.Fail($"W3C XML profile: inventoried={totals.Inventoried}, outside={totals.Outside}, runnable={totals.Runnable}, " +
                $"passing={totals.Passing}, knownFailing={totals.Known}, unresolved={totals.Unresolved}, " +
                $"harnessFailures={totals.Harness}, outputEligible={totals.OutputEligible}, " +
                $"outputCompared={totals.OutputCompared}, outputPending={totals.OutputPending}, " +
                $"noFetchAdapted={totals.NoFetch}. Failures={failures.Count} ({summary}). " +
                "First 30 parser/harness failures:\n" +
                string.Join("\n", failures.Where(item => item.Kind is XmlOutcomeKind.ParserFailure or XmlOutcomeKind.HarnessFailure)
                    .Take(30).Select(item => item.Text)) + "\nFirst 15 pending:\n" +
                string.Join("\n", failures.Where(item => item.Kind == XmlOutcomeKind.Pending)
                    .Take(15).Select(item => item.Text)));
        }
    }
}
