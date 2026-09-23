#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

public class XmlConformanceTests
{
    public static IEnumerable<TestCaseData> Cases => XmlCorpus.Cases
        .Where(row => row.Disposition is not ("outside-profile" or "outside-input-boundary"))
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
        if (outcome.Kind is not (XmlOutcomeKind.Pass or XmlOutcomeKind.OptionalPolicyVerified))
            Assert.Fail($"{key}: {outcome.Kind}: {outcome.Signature}: {outcome.Detail}");
    }
}

internal enum XmlOutcomeKind
{
    Pass, Pending, ParserFailure, HarnessFailure,
    OptionalObservedUnreviewed, OptionalAdapterDebt, OptionalPolicyVerified, OptionalPolicyMismatch
}

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
        var optionalError = row.Disposition == "optional-error-review";
        if (source is null)
            return optionalError
                ? new(XmlOutcomeKind.OptionalAdapterDebt, "optional-error-adapter-debt",
                    $"input adapter unavailable: {row.Decoding.Status}: {row.Decoding.Detail}")
                : new(XmlOutcomeKind.HarnessFailure, "missing-decoded-source", "Runnable case has no decoded string");
        if (row.Disposition != "runnable" && !optionalError)
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

        if (optionalError)
            return EvaluateOptional(row, document, syntax, testExpectation);

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
        if (externalReviewNeeded && reviewed?.Projection is null)
            return new(XmlOutcomeKind.HarnessFailure, "review-missing-projection",
                "Accepted resource-flagged case needs an independently reviewed surviving DOM projection");

        if (reviewed is not null)
        {
            var evidence = CompareEvidence(document, reviewed, XmlOutcomeKind.ParserFailure);
            if (evidence is not null) return evidence;
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
                if (reviewed?.OutputPolicy == "original-output-after-omission")
                {
                    var original = XmlCorpus.Bytes(row.OutputPath);
                    if (reviewed.OriginalOutputSha256 != Digest(original))
                        return new(XmlOutcomeKind.HarnessFailure, "reviewed-output-pin-mismatch", "Upstream OUTPUT digest changed");
                    var comparison = CompareOriginalOutput(document, testOutput ?? original, row.InputPath);
                    if (comparison is not null) return comparison;
                    return new(XmlOutcomeKind.Pass, "accepted", "Reviewed omission and original OUTPUT both match");
                }
                if (reviewed?.OutputPolicy != "no-fetch-alternative" || reviewed.ProjectionSha256 is null ||
                    reviewed.OriginalOutputSha256 is null || reviewed.OutputAlternative is null)
                    return new(XmlOutcomeKind.Pending, "no-fetch-output-review", "Upstream OUTPUT includes external material");
                if (Digest(XmlCorpus.Bytes(row.OutputPath)) != reviewed.OriginalOutputSha256)
                    return new(XmlOutcomeKind.HarnessFailure, "reviewed-output-pin-mismatch", "Upstream OUTPUT digest changed");
                var alternative = XmlEvidence.Projection(document);
                var actualSha = Digest(Encoding.UTF8.GetBytes(alternative));
                if (actualSha != reviewed.ProjectionSha256)
                    return new(XmlOutcomeKind.ParserFailure, $"no-fetch-output-mismatch:{actualSha}", "Reviewed projection digest differs");
                string canonical;
                try
                {
                    canonical = XmlEvidence.SecondCanonicalForm(document, XmlEvidence.CorpusInputBase(row.InputPath));
                }
                catch (XmlOutputObservationGapException error)
                {
                    return new(XmlOutcomeKind.HarnessFailure, "canonical-output-unavailable", error.Message);
                }
                if (canonical != reviewed.OutputAlternative)
                    return new(XmlOutcomeKind.ParserFailure, "no-fetch-canonical-mismatch", canonical);
            }
            else
            {
                var comparison = CompareOriginalOutput(document, testOutput ?? XmlCorpus.Bytes(row.OutputPath), row.InputPath);
                if (comparison is not null) return comparison;
            }
        }
        return new(XmlOutcomeKind.Pass, "accepted", "All applicable binary, omission, projection, and OUTPUT checks agree");
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static XmlCaseOutcome? CompareOriginalOutput(Document document, byte[] expected, string inputPath)
    {
        string actual;
        try
        {
            actual = XmlEvidence.SecondCanonicalForm(document, XmlEvidence.CorpusInputBase(inputPath));
        }
        catch (XmlOutputObservationGapException error)
        {
            return new(XmlOutcomeKind.HarnessFailure, "canonical-output-unavailable", error.Message);
        }
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return actualBytes.AsSpan().SequenceEqual(expected)
            ? null
            : new(XmlOutcomeKind.ParserFailure, $"output-mismatch:{Digest(actualBytes)}",
                $"Expected SHA-256 {Digest(expected)}; actual {actual}");
    }

    private static XmlCaseOutcome EvaluateOptional(XmlCorpusCase row, Document? document, MarkupParseException? syntax,
        XmlCaseExpectation? testPolicy)
    {
        if (syntax is null && (document is null || document.DocumentElement is null))
            return new(XmlOutcomeKind.HarnessFailure, "no-document-root", "Parser accepted without a document element");
        var observed = syntax is null ? "accepted" : $"rejected:{syntax.Code}@{syntax.Offset}";
        XmlExpectations.OptionalPolicies.TryGetValue(row.Key, out var policy);
        policy = testPolicy ?? policy;
        if (policy is null)
            return new(XmlOutcomeKind.OptionalObservedUnreviewed, "optional-error-unreviewed",
                $"W3C optional-error policy needs exact review; observed={observed}");
        if (policy.Status != "verified" ||
            policy.Outcome != "accept" || policy.Projection is null || policy.Skipped is null)
        {
            return new(XmlOutcomeKind.HarnessFailure, "invalid-optional-policy", row.Key);
        }
        if (syntax is not null)
            return new(XmlOutcomeKind.OptionalPolicyMismatch, $"optional-rejected:{syntax.Code}@{syntax.Offset}",
                $"Reviewed optional policy expects acceptance: {policy.Review}");
        if (document is null)
            return new(XmlOutcomeKind.HarnessFailure, "no-document-root", "Parser accepted without a document element");
        var evidence = CompareEvidence(document, policy, XmlOutcomeKind.OptionalPolicyMismatch);
        if (evidence is not null) return evidence;
        return new(XmlOutcomeKind.OptionalPolicyVerified, "optional-policy-verified",
            "Reviewed optional policy matches the complete surviving projection, omission, and notation lists");
    }

    private static XmlCaseOutcome? CompareEvidence(Document document, XmlCaseExpectation reviewed,
        XmlOutcomeKind mismatchKind)
    {
        if (reviewed.Skipped is null)
            return new(XmlOutcomeKind.HarnessFailure, "review-missing-skipped", "Reviewed case must assert the complete skip list");
        var actual = document.SkippedXmlEntities;
        if (actual.Count != reviewed.Skipped.Length)
            return new(mismatchKind, $"skip-count:{actual.Count}", $"Expected {reviewed.Skipped.Length} omission records");
        for (var index = 0; index < actual.Count; index++)
        {
            var found = actual[index];
            var expected = reviewed.Skipped[index];
            if (found.Kind.ToString() != expected.Kind || found.Name != expected.Name ||
                found.PublicId != expected.PublicId || found.SystemId != expected.SystemId || found.Offset != expected.Offset)
            {
                return new(mismatchKind, $"skip-mismatch:{index}",
                    $"Expected {expected.Kind}/{expected.Name}/{expected.PublicId}/{expected.SystemId}@{expected.Offset}; " +
                    $"actual {found.Kind}/{found.Name}/{found.PublicId}/{found.SystemId}@{found.Offset}");
            }
        }
        if (reviewed.Projection is not null)
        {
            var actualProjection = XmlEvidence.Projection(document);
            if (actualProjection != JsonSerializer.Serialize(reviewed.Projection))
                return new(mismatchKind, "projection-mismatch", actualProjection);
        }
        if (reviewed.Notations is not null)
        {
            var actualNotations = document.XmlNotations;
            if (actualNotations.Count != reviewed.Notations.Length)
                return new(mismatchKind, $"notation-count:{actualNotations.Count}",
                    $"Expected {reviewed.Notations.Length} read notation declarations");
            for (var index = 0; index < actualNotations.Count; index++)
            {
                var found = actualNotations[index];
                var expected = reviewed.Notations[index];
                if (found.Name != expected.Name || found.PublicId != expected.PublicId ||
                    found.SystemId != expected.SystemId || found.Offset != expected.Offset)
                {
                    return new(mismatchKind, $"notation-mismatch:{index}",
                        $"Expected {expected.Name}/{expected.PublicId}/{expected.SystemId}@{expected.Offset}; " +
                        $"actual {found.Name}/{found.PublicId}/{found.SystemId}@{found.Offset}");
                }
            }
        }
        return null;
    }
}
