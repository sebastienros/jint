using System.Text.RegularExpressions;

namespace Jint.Tests.DevTools.Protocol;

/// <summary>
/// Every Chrome DevTools Protocol URL this package cites names a domain, method, event or type the vendored
/// description actually has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is a separate test from the ECMAScript one.</b> <c>Jint.Tests/SpecCitationTests.cs</c> checks
/// <c>tc39.es</c> anchors against a register that is refreshed by fetching the living documents. This
/// package cites a document that is <i>already vendored</i>, so the check needs no register and no network:
/// the pinned JSON is the authority, and it is right there.
/// </para>
/// <para>
/// <b>What it catches.</b> A fragment that no longer exists does not 404 — it silently lands the reader at
/// the top of the page — and almost every citation here is emitted, so a protocol bump that renames a
/// method would otherwise leave hundreds of them pointing nowhere with nothing saying so.
/// </para>
/// </remarks>
public class ProtocolCitationTests
{
    private static readonly Regex _citation = new(
        @"https://chromedevtools\.github\.io/devtools-protocol/tot/(?<domain>[A-Za-z][A-Za-z0-9]*)/(?:\#(?<kind>method|event|type)-(?<member>[A-Za-z][A-Za-z0-9_]*))?",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    [Test]
    public void EveryProtocolCitationNamesSomethingTheVendoredDescriptionHas()
    {
        var protocol = global::Jint.DevTools.ProtocolGenerator.Protocol.Read(RepositoryPaths.ProtocolDirectory);
        var broken = new List<string>();
        var checkedCitations = 0;

        foreach (var file in Directory.EnumerateFiles(RepositoryPaths.SourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            var line = 0;
            foreach (var text in File.ReadLines(file))
            {
                line++;
                foreach (Match match in _citation.Matches(text))
                {
                    checkedCitations++;
                    var complaint = Resolve(protocol, match);
                    if (complaint is not null)
                    {
                        broken.Add($"  {Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/')}:{line}: {complaint}");
                    }
                }
            }
        }

        checkedCitations.Should().BeGreaterThan(0, "the emitted code cites the protocol on nearly every declaration, so finding none means this test stopped looking");

        Assert.That(
            broken.Count == 0,
            $"""
            {broken.Count} Chrome DevTools Protocol citation(s) name something the pinned description does not
            have. A fragment that no longer exists does not fail to load - it lands the reader at the top of
            the document - so nothing but this notices.

            {string.Join(Environment.NewLine, broken)}
            """);
    }

    private static string? Resolve(global::Jint.DevTools.ProtocolGenerator.Protocol protocol, Match match)
    {
        var domainName = match.Groups["domain"].Value;
        if (!protocol.HasDomain(domainName))
        {
            return $"the protocol has no '{domainName}' domain";
        }

        if (!match.Groups["kind"].Success)
        {
            return null;
        }

        var domain = protocol.Domain(domainName);
        var member = match.Groups["member"].Value;

        return match.Groups["kind"].Value switch
        {
            "method" when !domain.Commands.Any(command => string.Equals(command.Name, member, StringComparison.Ordinal))
                => $"'{domainName}' declares no '{member}' command",
            "event" when !domain.Events.Any(@event => string.Equals(@event.Name, member, StringComparison.Ordinal))
                => $"'{domainName}' declares no '{member}' event",
            "type" when !domain.Types.Any(type => string.Equals(type.Id, member, StringComparison.Ordinal))
                => $"'{domainName}' declares no '{member}' type",
            _ => null,
        };
    }

    private static bool IsBuildOutput(string path)
    {
        var relative = Path.GetRelativePath(RepositoryPaths.SourceDirectory, path);
        return relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
