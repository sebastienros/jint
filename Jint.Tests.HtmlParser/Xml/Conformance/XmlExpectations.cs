#nullable enable
using System.Text.Json;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

internal sealed class XmlCaseExpectation
{
    public string Key { get; init; } = "";
    public string? Status { get; init; }
    public XmlSkippedExpectation[]? Skipped { get; init; }
    public string? Outcome { get; init; }
    public XmlProjectionEntry[]? Projection { get; init; }
    public string? OutputPolicy { get; init; }
    public string? ProjectionSha256 { get; init; }
    public string? OriginalOutputSha256 { get; init; }
    public string? OutputAlternative { get; init; }
    public XmlNotationExpectation[]? Notations { get; init; }
    public string? Review { get; init; }
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
    private static readonly Lazy<IReadOnlyDictionary<string, XmlCaseExpectation>> Expectations =
        new(() => Load<XmlCaseExpectation>("expectations.json", item => item.Key));
    private static readonly Lazy<IReadOnlyDictionary<string, XmlCaseExpectation>> Optional =
        new(() => Load<XmlCaseExpectation>("optional-policies.json", item => item.Key));
    private static readonly Lazy<IReadOnlyDictionary<string, XmlCaseDeviation>> Deviations =
        new(() => Load<XmlCaseDeviation>("deviations.json", item => item.Key));

    internal static IReadOnlyDictionary<string, XmlCaseExpectation> Reviewed => Expectations.Value;
    internal static IReadOnlyDictionary<string, XmlCaseExpectation> OptionalPolicies => Optional.Value;
    internal static IReadOnlyDictionary<string, XmlCaseDeviation> KnownFailures => Deviations.Value;

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
