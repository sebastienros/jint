using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jint.DevTools.ProtocolGenerator;

/// <summary>
/// <c>tools/devtools-protocol/manifest.json</c>: which domains are generated, which of their commands and
/// events Jint.DevTools answers, and what <c>Schema.getDomains</c> reports.
/// </summary>
public sealed class GenerationManifest
{
    private GenerationManifest(
        string digest,
        IReadOnlyList<GeneratedDomain> generatedDomains,
        IReadOnlyList<string> implementedMethods,
        IReadOnlyList<string> implementedEvents,
        IReadOnlyList<ReportedDomain> reportedDomains)
    {
        Digest = digest;
        GeneratedDomains = generatedDomains;
        ImplementedMethods = implementedMethods;
        ImplementedEvents = implementedEvents;
        ReportedDomains = reportedDomains;
    }

    /// <summary>
    /// A short hash of the whole manifest, which the files that are generated from all of it stamp.
    /// </summary>
    public string Digest { get; }

    public IReadOnlyList<GeneratedDomain> GeneratedDomains { get; }

    /// <summary>The names of <see cref="GeneratedDomains"/>, in the order the manifest lists them.</summary>
    public IReadOnlyList<string> GeneratedDomainNames => Array.ConvertAll([.. GeneratedDomains], domain => domain.Name);

    public IReadOnlyList<string> ImplementedMethods { get; }

    public IReadOnlyList<string> ImplementedEvents { get; }

    public IReadOnlyList<ReportedDomain> ReportedDomains { get; }

    /// <summary>One entry of <c>Schema.getDomains</c>' answer.</summary>
    public sealed record ReportedDomain(string Name, string Version);

    /// <summary>
    /// One entry of <c>generatedDomains</c>: a whole domain, or the part of one the manifest names.
    /// </summary>
    /// <param name="Name">The domain.</param>
    /// <param name="Commands">
    /// The commands to generate, or <see langword="null"/> for every command the domain declares.
    /// </param>
    /// <param name="Events">
    /// The events to generate, or <see langword="null"/> for every event the domain declares.
    /// </param>
    /// <remarks>
    /// <para>
    /// A string entry is the whole domain. An object entry generates the commands and events it names and
    /// <i>nothing else</i> - including nothing for a list it leaves out, which is why an object entry that
    /// means to generate an event has to say so. Types are not listed and never could be: they are whatever
    /// the named commands and events transitively need, which is a closure the emitter computes rather than a
    /// list somebody would have to keep in step.
    /// </para>
    /// <para>
    /// What it buys is what <see href="https://github.com/sebastienros/jint/issues/3683">#3683</see> records:
    /// <c>Audits</c> cost 143 KB of data transfer objects for an <c>enable</c> and a <c>disable</c> that are
    /// accepted no-ops, because a domain was generated whole or not at all. A command left out gets no
    /// virtual and falls to the generated dispatch's default, which answers the <c>-32601</c> it already
    /// answered.
    /// </para>
    /// </remarks>
    public sealed record GeneratedDomain(string Name, IReadOnlyList<string>? Commands, IReadOnlyList<string>? Events)
    {
        /// <summary>Whether every command and event the domain declares is generated.</summary>
        public bool IsWhole => Commands is null && Events is null;

        /// <summary>Whether <paramref name="command"/> is one of the domain's generated commands.</summary>
        public bool GeneratesCommand(string command)
            => Commands is null || Commands.Contains(command, StringComparer.Ordinal);

        /// <summary>Whether <paramref name="name"/> is one of the domain's generated events.</summary>
        public bool GeneratesEvent(string name)
            => Events is null || Events.Contains(name, StringComparer.Ordinal);
    }

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
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes, options);
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
            Hash(File.ReadAllText(path)),
            ReadGeneratedDomains(root),
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
        var generated = new Dictionary<string, GeneratedDomain>(StringComparer.Ordinal);

        foreach (var entry in GeneratedDomains)
        {
            if (!protocol.HasDomain(entry.Name))
            {
                throw new ProtocolGeneratorException($"manifest.json generates domain '{entry.Name}', which the vendored protocol does not describe.");
            }

            if (!generated.TryAdd(entry.Name, entry))
            {
                throw new ProtocolGeneratorException($"manifest.json's generatedDomains names '{entry.Name}' twice.");
            }

            var described = protocol.Domain(entry.Name);
            foreach (var command in entry.Commands ?? [])
            {
                if (!described.Commands.Any(declared => string.Equals(declared.Name, command, StringComparison.Ordinal)))
                {
                    throw new ProtocolGeneratorException(
                        $"manifest.json generates '{entry.Name}.{command}', which the protocol's '{entry.Name}' domain does not declare as a command.");
                }
            }

            foreach (var name in entry.Events ?? [])
            {
                if (!described.Events.Any(declared => string.Equals(declared.Name, name, StringComparison.Ordinal)))
                {
                    throw new ProtocolGeneratorException(
                        $"manifest.json generates the '{entry.Name}.{name}' event, which the protocol's '{entry.Name}' domain does not declare.");
                }
            }
        }

        foreach (var method in ImplementedMethods)
        {
            var (domain, member) = Split(method, "implementedMethods");
            if (!generated.TryGetValue(domain, out var entry))
            {
                throw new ProtocolGeneratorException($"manifest.json implements '{method}', whose domain is not in generatedDomains.");
            }

            if (!entry.GeneratesCommand(member))
            {
                throw new ProtocolGeneratorException(
                    $"manifest.json implements '{method}', which its own generatedDomains entry for '{domain}' does not generate. " +
                    "A command with no generated virtual is one nothing can override, so name it in that entry's commands.");
            }

            if (!protocol.Domain(domain).Commands.Any(command => string.Equals(command.Name, member, StringComparison.Ordinal)))
            {
                throw new ProtocolGeneratorException($"manifest.json implements '{method}', which the vendored protocol's '{domain}' domain does not declare as a command.");
            }
        }

        foreach (var name in ImplementedEvents)
        {
            var (domain, member) = Split(name, "implementedEvents");
            if (!generated.TryGetValue(domain, out var entry))
            {
                throw new ProtocolGeneratorException($"manifest.json implements event '{name}', whose domain is not in generatedDomains.");
            }

            if (!entry.GeneratesEvent(member))
            {
                throw new ProtocolGeneratorException(
                    $"manifest.json implements the event '{name}', which its own generatedDomains entry for '{domain}' does not generate. " +
                    "There would be no factory to build it with, so name it in that entry's events.");
            }

            if (!protocol.Domain(domain).Events.Any(@event => string.Equals(@event.Name, member, StringComparison.Ordinal)))
            {
                throw new ProtocolGeneratorException($"manifest.json implements event '{name}', which the vendored protocol's '{domain}' domain does not declare.");
            }
        }

        foreach (var reported in ReportedDomains)
        {
            if (!generated.ContainsKey(reported.Name))
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

    /// <summary>
    /// A short hash of the part of the manifest one domain's generated file is produced from: what its
    /// <c>generatedDomains</c> entry names, and which of its commands and events are implemented.
    /// </summary>
    /// <remarks>
    /// Per domain rather than over the whole file on purpose. A file's header is provenance, and provenance
    /// that changed because a <i>different</i> domain gained a command would rewrite all twenty-four headers
    /// on every manifest edit and say nothing true about the other twenty-three. This changes when what
    /// shaped the file changes, which is what makes a stale file diagnosable by reading it.
    /// </remarks>
    public string DigestOf(string domain)
    {
        var entry = GeneratedDomains.FirstOrDefault(candidate => string.Equals(candidate.Name, domain, StringComparison.Ordinal))
            ?? throw new ProtocolGeneratorException($"manifest.json does not generate the '{domain}' domain.");

        var text = new StringBuilder();
        text.Append("domain=").Append(domain).Append('\n');
        text.Append("commands=").Append(entry.Commands is null ? "*" : string.Join(",", entry.Commands)).Append('\n');
        text.Append("events=").Append(entry.Events is null ? "*" : string.Join(",", entry.Events)).Append('\n');
        text.Append("implementedMethods=").Append(string.Join(",", Qualified(ImplementedMethods, domain))).Append('\n');
        text.Append("implementedEvents=").Append(string.Join(",", Qualified(ImplementedEvents, domain))).Append('\n');
        return Hash(text.ToString());
    }

    private static IEnumerable<string> Qualified(IReadOnlyList<string> members, string domain)
    {
        var prefix = domain + ".";
        return members.Where(member => member.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The first six bytes of the SHA-256 of <paramref name="text"/>, with line endings normalised first so
    /// that a Windows checkout and a Linux one stamp the same digest into the generated code.
    /// </summary>
    private static string Hash(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('\uFEFF');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..12].ToLowerInvariant();
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

    /// <summary>
    /// Reads <c>generatedDomains</c>, whose entries are either a domain name or an object naming the part of
    /// a domain to generate.
    /// </summary>
    private static List<GeneratedDomain> ReadGeneratedDomains(JsonElement root)
    {
        var domains = new List<GeneratedDomain>();
        if (!root.TryGetProperty("generatedDomains", out var element))
        {
            return domains;
        }

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                domains.Add(new GeneratedDomain(entry.GetString()!, Commands: null, Events: null));
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("domain", out var name))
            {
                throw new ProtocolGeneratorException(
                    "manifest.json's generatedDomains holds an entry that is neither a domain name nor an object naming a 'domain'.");
            }

            domains.Add(new GeneratedDomain(name.GetString()!, Members(entry, "commands"), Members(entry, "events")));
        }

        return domains;
    }

    /// <summary>
    /// One list of a partial entry. An absent list is an empty one rather than "all of them": an entry that
    /// names the part of a domain to generate states the whole of what it generates.
    /// </summary>
    private static List<string> Members(JsonElement entry, string name)
    {
        var members = new List<string>();
        if (!entry.TryGetProperty(name, out var element))
        {
            return members;
        }

        foreach (var value in element.EnumerateArray())
        {
            members.Add(value.GetString()!);
        }

        return members;
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
