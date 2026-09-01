using System.Text.Json;

namespace Jint.DevTools.ProtocolGenerator;

/// <summary>
/// <c>tools/devtools-protocol/manifest.json</c>: which domains are generated, which of their commands and
/// events Jint.DevTools answers, and what <c>Schema.getDomains</c> reports.
/// </summary>
public sealed class GenerationManifest
{
    private GenerationManifest(
        IReadOnlyList<string> generatedDomains,
        IReadOnlyList<string> implementedMethods,
        IReadOnlyList<string> implementedEvents,
        IReadOnlyList<ReportedDomain> reportedDomains)
    {
        GeneratedDomains = generatedDomains;
        ImplementedMethods = implementedMethods;
        ImplementedEvents = implementedEvents;
        ReportedDomains = reportedDomains;
    }

    public IReadOnlyList<string> GeneratedDomains { get; }

    public IReadOnlyList<string> ImplementedMethods { get; }

    public IReadOnlyList<string> ImplementedEvents { get; }

    public IReadOnlyList<ReportedDomain> ReportedDomains { get; }

    /// <summary>One entry of <c>Schema.getDomains</c>' answer.</summary>
    public sealed record ReportedDomain(string Name, string Version);

    /// <summary>
    /// Reads the manifest. Comments and trailing commas are allowed, because it is hand-edited and the
    /// reasoning for each list belongs beside it.
    /// </summary>
    public static GenerationManifest Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new ProtocolGeneratorException($"'{path}' does not exist. The manifest is what says which domains are generated; see tools/devtools-protocol/README.md.");
        }

        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), options);
        var root = document.RootElement;

        var reported = new List<ReportedDomain>();
        if (root.TryGetProperty("reportedDomains", out var reportedElement))
        {
            foreach (var element in reportedElement.EnumerateArray())
            {
                reported.Add(new ReportedDomain(
                    element.GetProperty("name").GetString()!,
                    element.GetProperty("version").GetString()!));
            }
        }

        return new GenerationManifest(
            Strings(root, "generatedDomains"),
            Strings(root, "implementedMethods"),
            Strings(root, "implementedEvents"),
            reported);
    }

    /// <summary>
    /// Holds the manifest to the vendored protocol, so that a typo is a generation failure rather than a
    /// method nothing can ever reach.
    /// </summary>
    public void Validate(Protocol protocol)
    {
        var generated = new HashSet<string>(GeneratedDomains, StringComparer.Ordinal);

        foreach (var domain in GeneratedDomains)
        {
            if (!protocol.HasDomain(domain))
            {
                throw new ProtocolGeneratorException($"manifest.json generates domain '{domain}', which the vendored protocol does not describe.");
            }
        }

        foreach (var method in ImplementedMethods)
        {
            var (domain, member) = Split(method, "implementedMethods");
            if (!generated.Contains(domain))
            {
                throw new ProtocolGeneratorException($"manifest.json implements '{method}', whose domain is not in generatedDomains.");
            }

            if (!protocol.Domain(domain).Commands.Any(command => string.Equals(command.Name, member, StringComparison.Ordinal)))
            {
                throw new ProtocolGeneratorException($"manifest.json implements '{method}', which the vendored protocol's '{domain}' domain does not declare as a command.");
            }
        }

        foreach (var name in ImplementedEvents)
        {
            var (domain, member) = Split(name, "implementedEvents");
            if (!generated.Contains(domain))
            {
                throw new ProtocolGeneratorException($"manifest.json implements event '{name}', whose domain is not in generatedDomains.");
            }

            if (!protocol.Domain(domain).Events.Any(@event => string.Equals(@event.Name, member, StringComparison.Ordinal)))
            {
                throw new ProtocolGeneratorException($"manifest.json implements event '{name}', which the vendored protocol's '{domain}' domain does not declare.");
            }
        }

        foreach (var reported in ReportedDomains)
        {
            if (!generated.Contains(reported.Name))
            {
                throw new ProtocolGeneratorException($"manifest.json reports domain '{reported.Name}' from Schema.getDomains, but does not generate it.");
            }

            var prefix = reported.Name + ".";
            if (!ImplementedMethods.Any(method => method.StartsWith(prefix, StringComparison.Ordinal)))
            {
                throw new ProtocolGeneratorException(
                    $"manifest.json reports domain '{reported.Name}' from Schema.getDomains and implements none of its commands. " +
                    "A client feature-detecting through Schema.getDomains must never be told about a domain that answers nothing.");
            }
        }
    }

    private static (string Domain, string Member) Split(string qualified, string list)
    {
        var separator = qualified.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == qualified.Length - 1)
        {
            throw new ProtocolGeneratorException($"manifest.json's {list} entry '{qualified}' is not of the form 'Domain.member'.");
        }

        return (qualified.Substring(0, separator), qualified.Substring(separator + 1));
    }

    private static List<string> Strings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return [];
        }

        var values = new List<string>();
        foreach (var value in element.EnumerateArray())
        {
            values.Add(value.GetString()!);
        }

        return values;
    }
}
