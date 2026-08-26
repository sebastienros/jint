#if NET8_0_OR_GREATER
#nullable enable

using System.Text;
using System.Text.Json;
using Jint.WebApi.Url.Parsing;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The Web Platform Tests URL corpus, run directly against the parser.
/// </summary>
/// <remarks>
/// <para>
/// <c>Jint.WebApi.Url.Parsing</c> mentions no engine type, so a conformance row costs one parse and no engine
/// at all — which is what makes running all 891 parse rows and all 278 setter rows in two facts practical.
/// The corpus files, their provenance and the licence they carry are described in
/// <c>Resources/README.md</c>.
/// </para>
/// <para>
/// <b>The exclusion table is the whole point of the driver.</b> A row that does not pass has to be named in it
/// with the divergence category it belongs to, and a row that <i>does</i> pass while named there fails the run
/// as a stale exclusion — the same discipline the test262 harness uses, so a fix cannot silently leave a
/// permanent exemption behind. Every category is an IDNA one, which is the acceptance bar this implementation
/// was held to: nothing outside <see cref="Idna"/>'s documented divergences is excluded.
/// </para>
/// </remarks>
public class UrlCorpusTests
{
    /// <summary>
    /// The divergence categories, all of them consequences of building on <see cref="IdnMapping"/> rather than
    /// reimplementing UTS-46. <see cref="Idna"/> documents each in full.
    /// </summary>
    private enum Divergence
    {
        /// <summary>
        /// VerifyDnsLength cannot be turned off, so a label over 63 bytes or a domain over 253 is rejected
        /// where the URL Standard accepts it.
        /// </summary>
        IdnaVerifyDnsLength,

        /// <summary>
        /// VerifyDnsLength also rejects an empty label, so a non-ASCII domain with one — including a trailing
        /// dot — is rejected where the URL Standard accepts it.
        /// </summary>
        IdnaEmptyLabel,

        /// <summary>
        /// CheckHyphens cannot be turned off, so a non-ASCII domain whose label starts or ends with a hyphen,
        /// or carries "--" in the third and fourth positions, is rejected where the URL Standard accepts it.
        /// </summary>
        IdnaCheckHyphens,

        /// <summary>
        /// The Unicode version behind the platform's IDNA mapping table decides the outcome, so a code point
        /// whose UTS-46 status changed between releases maps differently.
        /// </summary>
        IdnaUnicodeVersion,
    }

    /// <summary>
    /// Parse rows that do not pass, keyed by the row's input and base.
    /// </summary>
    /// <remarks>
    /// Empty, and meant to stay that way: every row of the corpus passes on a platform whose IDNA fidelity is
    /// <see cref="IdnaFidelity.Full"/>. The table and its staleness check exist so that a divergence which does
    /// appear — a platform ICU update, a corpus bump — is recorded with its category rather than papered over.
    /// </remarks>
    private static readonly Dictionary<(string Input, string? Base), Divergence> _parseExclusions = new();

    /// <summary>
    /// Setter rows that do not pass, keyed by the attribute, the starting href and the assigned value.
    /// </summary>
    private static readonly Dictionary<(string Attribute, string Href, string NewValue), Divergence> _setterExclusions = new();

    [Test]
    public void ParsesEveryRowOfTheWebPlatformTestsCorpus()
    {
        using var document = LoadCorpus("urltestdata.json");

        var failures = new List<string>();
        var stale = new List<string>();
        var checkedRows = 0;

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                // A bare string is a section comment.
                continue;
            }

            var input = element.GetProperty("input").GetString()!;
            var baseHref = element.TryGetProperty("base", out var baseElement) && baseElement.ValueKind == JsonValueKind.String
                ? baseElement.GetString()
                : null;

            var excluded = _parseExclusions.ContainsKey((input, baseHref));
            if (SkipForIdnaFidelity(input, baseHref))
            {
                continue;
            }

            checkedRows++;
            var failure = CheckParseRow(element, input, baseHref);

            if (failure is null && excluded)
            {
                stale.Add(Describe(input, baseHref));
            }
            else if (failure is not null && !excluded)
            {
                failures.Add(failure);
            }
        }

        // The corpus was actually run: a resource that silently stopped being embedded, or an exclusion rule
        // that swallowed everything, would otherwise be an empty green test.
        checkedRows.Should().BeGreaterThan(800);
        Report(failures, stale);
    }

    [Test]
    public void AppliesEverySetterRowOfTheWebPlatformTestsCorpus()
    {
        using var document = LoadCorpus("setters_tests.json");

        var failures = new List<string>();
        var stale = new List<string>();
        var checkedRows = 0;

        foreach (var group in document.RootElement.EnumerateObject())
        {
            if (string.Equals(group.Name, "comment", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var element in group.Value.EnumerateArray())
            {
                var href = element.GetProperty("href").GetString()!;
                var newValue = element.GetProperty("new_value").GetString()!;

                var excluded = _setterExclusions.ContainsKey((group.Name, href, newValue));
                if (SkipForIdnaFidelity(href, newValue))
                {
                    continue;
                }

                checkedRows++;
                var failure = CheckSetterRow(group.Name, element, href, newValue);

                if (failure is null && excluded)
                {
                    stale.Add($"{group.Name}: {Describe(href, newValue)}");
                }
                else if (failure is not null && !excluded)
                {
                    failures.Add(failure);
                }
            }
        }

        checkedRows.Should().BeGreaterThan(250);
        Report(failures, stale);
    }

    private static string? CheckParseRow(JsonElement element, string input, string? baseHref)
    {
        UrlRecord? baseRecord = null;
        if (baseHref is not null)
        {
            baseRecord = UrlParser.Parse(baseHref);
            if (baseRecord is null)
            {
                // The API URL parser returns failure when the base does not parse.
                return element.TryGetProperty("failure", out _)
                    ? null
                    : $"{Describe(input, baseHref)}: the base did not parse";
            }
        }

        var url = UrlParser.Parse(input, baseRecord);

        if (element.TryGetProperty("failure", out _))
        {
            return url is null ? null : $"{Describe(input, baseHref)}: expected failure but got \"{url.Serialize()}\"";
        }

        if (url is null)
        {
            return $"{Describe(input, baseHref)}: expected \"{element.GetProperty("href").GetString()}\" but the parse failed";
        }

        var mismatches = new List<string>();
        Compare(mismatches, element, "href", url.Serialize());
        Compare(mismatches, element, "protocol", url.SerializeProtocol());
        Compare(mismatches, element, "username", url.Username);
        Compare(mismatches, element, "password", url.Password);
        Compare(mismatches, element, "host", url.SerializeHostAndPort());
        Compare(mismatches, element, "hostname", url.SerializeHost());
        Compare(mismatches, element, "port", url.SerializePort());
        Compare(mismatches, element, "pathname", url.SerializePath());
        Compare(mismatches, element, "search", url.SerializeSearch());
        Compare(mismatches, element, "hash", url.SerializeHash());
        Compare(mismatches, element, "origin", url.SerializeOrigin());
        Compare(mismatches, element, "searchParams", FormUrlEncoded.Serialize(FormUrlEncoded.Parse(url.Query ?? string.Empty)));

        return mismatches.Count == 0 ? null : $"{Describe(input, baseHref)}: {string.Join("; ", mismatches)}";
    }

    private static string? CheckSetterRow(string attribute, JsonElement element, string href, string newValue)
    {
        var url = UrlParser.Parse(href);
        if (url is null)
        {
            return $"{attribute}: {Describe(href, newValue)}: the starting href did not parse";
        }

        switch (attribute)
        {
            case "protocol":
                UrlSetters.SetProtocol(url, newValue);
                break;
            case "username":
                UrlSetters.SetUsername(url, newValue);
                break;
            case "password":
                UrlSetters.SetPassword(url, newValue);
                break;
            case "host":
                UrlSetters.SetHost(url, newValue);
                break;
            case "hostname":
                UrlSetters.SetHostname(url, newValue);
                break;
            case "port":
                UrlSetters.SetPort(url, newValue);
                break;
            case "pathname":
                UrlSetters.SetPathname(url, newValue);
                break;
            case "search":
                UrlSetters.SetSearch(url, newValue);
                break;
            case "hash":
                UrlSetters.SetHash(url, newValue);
                break;
            case "href":
                var replacement = UrlParser.Parse(newValue);
                if (replacement is null)
                {
                    return $"{attribute}: {Describe(href, newValue)}: the assigned href did not parse";
                }

                url = replacement;
                break;
            default:
                return $"{attribute}: {Describe(href, newValue)}: unknown attribute";
        }

        var expected = element.GetProperty("expected");
        var mismatches = new List<string>();
        Compare(mismatches, expected, "href", url.Serialize());
        Compare(mismatches, expected, "protocol", url.SerializeProtocol());
        Compare(mismatches, expected, "username", url.Username);
        Compare(mismatches, expected, "password", url.Password);
        Compare(mismatches, expected, "host", url.SerializeHostAndPort());
        Compare(mismatches, expected, "hostname", url.SerializeHost());
        Compare(mismatches, expected, "port", url.SerializePort());
        Compare(mismatches, expected, "pathname", url.SerializePath());
        Compare(mismatches, expected, "search", url.SerializeSearch());
        Compare(mismatches, expected, "hash", url.SerializeHash());

        return mismatches.Count == 0 ? null : $"{attribute}: {Describe(href, newValue)}: {string.Join("; ", mismatches)}";
    }

    private static void Compare(List<string> mismatches, JsonElement element, string name, string actual)
    {
        if (!element.TryGetProperty(name, out var expected) || expected.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var text = expected.GetString()!;
        if (!string.Equals(text, actual, StringComparison.Ordinal))
        {
            mismatches.Add($"{name} expected \"{text}\" but was \"{actual}\"");
        }
    }

    /// <summary>
    /// A row whose input mentions a non-ASCII code point can only be judged where the platform's IDNA
    /// implementation is the one the URL Standard asks for. On a machine where it is not — transitional
    /// processing, or globalization-invariant mode — such a row is neither run nor checked for staleness.
    /// </summary>
    private static bool SkipForIdnaFidelity(string first, string? second)
    {
        if (Idna.Fidelity == IdnaFidelity.Full)
        {
            return false;
        }

        return !IsAscii(first) || (second is not null && !IsAscii(second));
    }

    private static bool IsAscii(string value)
    {
        foreach (var c in value)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(string first, string? second)
        => second is null ? $"\"{first}\"" : $"\"{first}\" against \"{second}\"";

    private static void Report(List<string> failures, List<string> stale)
    {
        var message = new StringBuilder();

        if (stale.Count > 0)
        {
            message.Append(stale.Count).AppendLine(" stale exclusion(s) — these rows pass and must be removed from the exclusion table:");
            foreach (var row in stale)
            {
                message.Append("  ").AppendLine(row);
            }
        }

        if (failures.Count > 0)
        {
            message.Append(failures.Count).AppendLine(" failing row(s):");
            foreach (var row in failures)
            {
                message.Append("  ").AppendLine(row);
            }
        }

        message.ToString().Should().BeEmpty();
    }

    private static JsonDocument LoadCorpus(string fileName)
    {
        var assembly = typeof(UrlCorpusTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(fileName, StringComparison.Ordinal));
        name.Should().NotBeNull($"the {fileName} corpus must be embedded");

        using var stream = assembly.GetManifestResourceStream(name!)!;
        return JsonDocument.Parse(stream);
    }
}
#endif
