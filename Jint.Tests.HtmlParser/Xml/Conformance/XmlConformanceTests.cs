#nullable enable
using System.Security.Cryptography;
using System.Text;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

public class XmlConformanceTests
{
    public static IEnumerable<TestCaseData> Cases => XmlCorpus.Cases
        .Where(row => row.Disposition != "outside-profile")
        .Select(row => new TestCaseData(row.Key).SetName($"W3C XML 20130923 {row.Collection} {row.Id}"));

    [TestCaseSource(nameof(Cases))]
    public void PinnedXml10FifthEditionNamespaceProfile(string key)
    {
        var outcome = XmlConformanceRunner.Run(XmlCorpus.Case(key));
        if (XmlExpectations.KnownFailures.TryGetValue(key, out var deviation))
        {
            outcome.Signature.Should().Be(deviation.Signature,
                $"the reviewed failure for {key} must remain exact, issue {deviation.Issue}");
            Assert.Fail($"Known required-profile parser defect {key}: {deviation.Issue}; {outcome.Detail}");
        }
        if (outcome.Kind != XmlOutcomeKind.Pass)
            Assert.Fail($"{key}: {outcome.Kind}: {outcome.Signature}: {outcome.Detail}");
    }
}

internal enum XmlOutcomeKind { Pass, Pending, ParserFailure, HarnessFailure }

internal sealed record XmlCaseOutcome(XmlOutcomeKind Kind, string Signature, string Detail);

internal static class XmlConformanceRunner
{
    internal static XmlCaseOutcome Run(XmlCorpusCase row, XmlCaseExpectation? testExpectation = null,
        byte[]? testOutput = null)
    {
        var bytes = XmlCorpus.Bytes(row.InputPath);
        var (source, decision) = XmlByteDecoder.Decode(bytes);
        if (decision.Decision != row.Decoding.Decision || decision.Status != row.Decoding.Status ||
            decision.Declared != row.Decoding.Declared)
        {
            return new(XmlOutcomeKind.HarnessFailure, "decoder-manifest-drift",
                $"Manifest={row.Decoding.Decision}/{row.Decoding.Status}/{row.Decoding.Declared}, " +
                $"actual={decision.Decision}/{decision.Status}/{decision.Declared}");
        }
        if (row.Disposition == "input-boundary-review")
            return new(XmlOutcomeKind.Pending, "input-boundary-review", row.Decoding.Detail ?? row.Decoding.Declared ?? "strict byte boundary pending review");
        if (row.Disposition == "optional-error-review")
            return new(XmlOutcomeKind.Pending, "optional-error-review", "W3C optional-error policy needs an exact per-case decision");
        if (source is null)
            return new(XmlOutcomeKind.HarnessFailure, "missing-decoded-source", "Runnable case has no decoded string");
        if (row.Disposition != "runnable")
            return new(XmlOutcomeKind.HarnessFailure, "unknown-disposition", row.Disposition);

        var hasReviewedExpectation = XmlExpectations.Reviewed.TryGetValue(row.Key, out var reviewed);
        if (testExpectation is not null)
        {
            reviewed = testExpectation;
            hasReviewedExpectation = true;
        }
        var externalReviewNeeded = row.ResourceProfile == "unreviewed-external-indication";
        if (row.ResourceProfile is not ("unreviewed-external-indication" or "no-external-indication"))
            return new(XmlOutcomeKind.HarnessFailure, "unknown-resource-profile", row.ResourceProfile);

        Document? document = null;
        MarkupParseException? syntax = null;
        try
        {
            document = MarkupParser.ParseXml(source);
        }
        catch (MarkupParseException error)
        {
            syntax = error;
        }
        catch (Exception error)
        {
            // A budget/cancellation/crash/programming error is never a syntax pass.
            return new(XmlOutcomeKind.HarnessFailure, $"unexpected:{error.GetType().FullName}", error.Message);
        }

        if (externalReviewNeeded && !hasReviewedExpectation)
            return new(XmlOutcomeKind.Pending, "resource-profile-review",
                $"signals={string.Join(',', row.ResourceSignals)}; observed=" +
                (syntax is null ? "accepted" : $"rejected:{syntax.Code}@{syntax.Offset}"));

        var expectedOutcome = reviewed?.Outcome ?? (row.Category == "not-wf" ? "reject" : "accept");
        if (expectedOutcome == "reject")
        {
            if (syntax is not null) return new(XmlOutcomeKind.Pass, "rejected:not-wf", syntax.Code);
            return new(XmlOutcomeKind.ParserFailure, "accepted:expected-rejection", "Required well-formedness rejection did not occur");
        }
        if (expectedOutcome != "accept")
            return new(XmlOutcomeKind.HarnessFailure, "unknown-reviewed-outcome", expectedOutcome);
        if (row.Category is not ("valid" or "invalid"))
        {
            if (row.Category != "not-wf" || reviewed?.Outcome != "accept")
                return new(XmlOutcomeKind.HarnessFailure, "unknown-category", row.Category);
        }
        if (syntax is not null)
            return new(XmlOutcomeKind.ParserFailure, $"rejected:{syntax.Code}@{syntax.Offset}", syntax.Message);
        if (document is null || document.DocumentElement is null)
            return new(XmlOutcomeKind.HarnessFailure, "no-document-root", "Parser accepted without a document element");

        if (reviewed is not null)
        {
            if (reviewed.Skipped is null)
                return new(XmlOutcomeKind.HarnessFailure, "review-missing-skipped", "Reviewed resource case must assert the complete skip list");
            var actual = document.SkippedXmlEntities;
            if (actual.Count != reviewed.Skipped.Length)
                return new(XmlOutcomeKind.ParserFailure, $"skip-count:{actual.Count}", $"Expected {reviewed.Skipped.Length} omission records");
            for (var index = 0; index < actual.Count; index++)
            {
                var found = actual[index];
                var expected = reviewed.Skipped[index];
                if (found.Kind.ToString() != expected.Kind || found.Name != expected.Name ||
                    found.PublicId != expected.PublicId || found.SystemId != expected.SystemId || found.Offset != expected.Offset)
                {
                    return new(XmlOutcomeKind.ParserFailure, $"skip-mismatch:{index}",
                        $"Expected {expected.Kind}/{expected.Name}/{expected.PublicId}/{expected.SystemId}@{expected.Offset}; " +
                        $"actual {found.Kind}/{found.Name}/{found.PublicId}/{found.SystemId}@{found.Offset}");
                }
            }
            if (reviewed.Projection is not null)
            {
                var actualProjection = XmlEvidence.Projection(document);
                if (actualProjection != reviewed.Projection)
                    return new(XmlOutcomeKind.ParserFailure, "projection-mismatch", actualProjection);
            }
        }
        else if (document.SkippedXmlEntities.Count != 0)
        {
            return new(XmlOutcomeKind.Pending, "unreviewed-skipped-entities",
                $"Parser returned {document.SkippedXmlEntities.Count} omission records without independent expected records");
        }

        if (row.OutputPath is not null)
        {
            if (document.SkippedXmlEntities.Count != 0)
            {
                if (reviewed?.OutputPolicy != "no-fetch-alternative" || reviewed.OutputSha256 is null)
                    return new(XmlOutcomeKind.Pending, "no-fetch-output-review", "Upstream OUTPUT includes external material");
                var alternative = XmlEvidence.Projection(document);
                var actualSha = Digest(Encoding.UTF8.GetBytes(alternative));
                if (actualSha != reviewed.OutputSha256)
                    return new(XmlOutcomeKind.ParserFailure, $"no-fetch-output-mismatch:{actualSha}", "Reviewed projection digest differs");
            }
            else
            {
                var expected = testOutput ?? XmlCorpus.Bytes(row.OutputPath);
                string actual;
                try
                {
                    actual = XmlEvidence.SecondCanonicalForm(document, expected);
                }
                catch (XmlOutputObservationGapException error)
                {
                    return new(XmlOutcomeKind.Pending, "notation-observation-gap", error.Message);
                }
                var actualBytes = Encoding.UTF8.GetBytes(actual);
                if (!actualBytes.AsSpan().SequenceEqual(expected))
                    return new(XmlOutcomeKind.ParserFailure, $"output-mismatch:{Digest(actualBytes)}",
                        $"Expected SHA-256 {Digest(expected)}; actual {actual}");
            }
        }
        return new(XmlOutcomeKind.Pass, "accepted", "All applicable binary, omission, projection, and OUTPUT checks agree");
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
