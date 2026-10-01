#nullable enable
using System.Text.Json;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

internal sealed class XmlCaseExpectation
{
    public string Key { get; init; } = "";
    public string? Status { get; init; }
    public XmlSkippedExpectation[]? Skipped { get; init; }
    public string? Outcome { get; init; }
    public XmlProjectionEntry[]? Projection { get; set; }
    public string? ProjectionSameAs { get; init; }
    public string? OutputPolicy { get; init; }
    public string? ProjectionSha256 { get; init; }
    public string? OriginalOutputSha256 { get; init; }
    public string? OutputAlternative { get; init; }
    public XmlNotationExpectation[]? Notations { get; init; }
    public XmlDtdProcessingInstructionExpectation[]? DtdProcessingInstructions { get; init; }
    public string? Review { get; init; }
}

internal sealed class XmlDtdProcessingInstructionExpectation
{
    public string Target { get; init; } = "";
    public string Data { get; init; } = "";
    public long Offset { get; init; }
}

internal sealed class XmlNotationExpectation
{
    public string Name { get; init; } = "";
    public string? PublicId { get; init; }
    public string? SystemId { get; init; }
    public long Offset { get; init; }
}

internal sealed class XmlSkippedExpectation
{
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string? PublicId { get; init; }
    public string? SystemId { get; init; }
    public long Offset { get; init; }
}

internal sealed class XmlCaseDeviation
{
    public string Key { get; init; } = "";
    public string Issue { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Citation { get; init; } = "";
}

internal static class XmlExpectations
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<(IReadOnlyDictionary<string, XmlCaseExpectation> Reviewed,
        IReadOnlyDictionary<string, XmlCaseExpectation> Optional)> Reviews = new(LoadReviews);
    private static readonly Lazy<IReadOnlyDictionary<string, XmlCaseDeviation>> Deviations =
        new(() => Load<XmlCaseDeviation>("deviations.json", item => item.Key));

    internal static IReadOnlyDictionary<string, XmlCaseExpectation> Reviewed => Reviews.Value.Reviewed;
    internal static IReadOnlyDictionary<string, XmlCaseExpectation> OptionalPolicies => Reviews.Value.Optional;
    internal static IReadOnlyDictionary<string, XmlCaseDeviation> KnownFailures => Deviations.Value;

    private static (IReadOnlyDictionary<string, XmlCaseExpectation>, IReadOnlyDictionary<string, XmlCaseExpectation>) LoadReviews()
    {
        var reviewed = Load<XmlCaseExpectation>("expectations.json", item => item.Key);
        var optional = Load<XmlCaseExpectation>("optional-policies.json", item => item.Key);
        foreach (var item in reviewed.Values.Concat(optional.Values))
            ShareProjection(item, reviewed, optional);
        return (reviewed, optional);
    }

    // projectionSameAs is storage only (Tools/format_reviews.py): the row is held to exactly the entries
    // the named row stores, so a reference can never weaken the comparison or chain to another reference.
    internal static void ShareProjection(XmlCaseExpectation item,
        IReadOnlyDictionary<string, XmlCaseExpectation> reviewed, IReadOnlyDictionary<string, XmlCaseExpectation> optional)
    {
        if (item.ProjectionSameAs is not { } source)
            return;
        if (item.Projection is not null ||
            !(reviewed.TryGetValue(source, out var owner) || optional.TryGetValue(source, out owner)) ||
            owner.ProjectionSameAs is not null || owner.Projection is null)
            throw new InvalidDataException($"Invalid projectionSameAs on {item.Key}: {source}");
        item.Projection = owner.Projection;
    }

    private static IReadOnlyDictionary<string, T> Load<T>(string file, Func<T, string> keyOf)
    {
        var source = File.ReadAllText(Path.Combine(XmlCorpus.Root, file));
        var items = JsonSerializer.Deserialize<T[]>(source, Options)
            ?? throw new InvalidDataException($"Empty XML review file: {file}");
        var indexed = new Dictionary<string, T>(StringComparer.Ordinal);
        var caseKeys = XmlCorpus.Cases.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = keyOf(item);
            if (!caseKeys.Contains(key) || !indexed.TryAdd(key, item))
                throw new InvalidDataException($"Stale or duplicate XML review key in {file}: {key}");
        }
        return indexed;
    }
}
