using System.Text.RegularExpressions;

namespace Jint.Tests.DevTools.Protocol;

/// <summary>
/// Every Chrome DevTools Protocol URL this repository cites names a domain, method, event or type the
/// vendored description actually has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is a separate test from the ECMAScript one.</b> <c>Jint.Tests/SpecCitationTests.cs</c> checks
/// <c>tc39.es</c> anchors against a register that is refreshed by fetching the living documents. This
/// repository cites a document that is <i>already vendored</i>, so the check needs no register and no
/// network: the pinned JSON is the authority, and it is right there.
/// </para>
/// <para>
/// <b>What it catches.</b> A fragment that no longer exists does not 404 — it silently lands the reader at
/// the top of the page — and almost every citation here is emitted, so a protocol bump that renames a
/// method would otherwise leave hundreds of them pointing nowhere with nothing saying so.
/// </para>
/// <para>
/// <b>Why it reads the whole checkout and not one project.</b> It used to read <c>Jint.DevTools</c> alone,
/// which was true only while that was the one project with domains in it. <c>Jint.Browser/DevTools/</c> now
/// implements the page-level half of the protocol and cites it as densely, and every one of those
/// citations was unchecked; a check that covers the package a bump was written in and not the package it
/// breaks is worse than none, because it reports green.
/// <see cref="EveryProjectThatOwnsADomainIsScanned"/> is what stops the scope from narrowing again.
/// </para>
/// </remarks>
public class ProtocolCitationTests
{
    private static readonly Regex _citation = new(
        @"https://chromedevtools\.github\.io/devtools-protocol/tot/(?<domain>[A-Za-z][A-Za-z0-9]*)/(?:\#(?<kind>method|event|type)-(?<member>[A-Za-z][A-Za-z0-9_]*))?",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A class whose base list names one of the generated <c>&lt;Name&gt;DomainBase</c> types, which is what
    /// implementing a protocol domain means here and is therefore what makes a project one this check owes
    /// its attention to.
    /// </summary>
    private static readonly Regex _domainDeclaration = new(
        @"\bclass\s+\w+\s*:[^{\r\n]*\b\w+DomainBase\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>Directories a checkout carries that are not the repository's own sources.</summary>
    private static readonly string[] _notSourceDirectories =
    [
        ".git", ".vs", "artifacts", "bin", "obj", "node_modules", "packages"
    ];

    [Test]
    public void EveryProtocolCitationNamesSomethingTheVendoredDescriptionHas()
    {
        var protocol = global::Jint.DevTools.ProtocolGenerator.Protocol.Read(RepositoryPaths.ProtocolDirectory);
        var broken = new List<string>();
        var checkedCitations = 0;

        foreach (var file in ScannedFiles())
        {
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
                        broken.Add($"  {Relative(file)}:{line}: {complaint}");
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

    /// <summary>
    /// Every project that implements a protocol domain is one the citation check reads.
    /// </summary>
    /// <remarks>
    /// The two walks are deliberately separate: this one finds the domain owners from the checkout, and
    /// <see cref="ScannedFiles"/> is what the check above reads. Narrowing the check back to one project
    /// therefore fails here, naming the project whose citations stopped being verified — which is the
    /// failure mode this replaced, where twenty of them in <c>Jint.Browser/DevTools/</c> were checked by
    /// nothing at all.
    /// </remarks>
    [Test]
    public void EveryProjectThatOwnsADomainIsScanned()
    {
        var scanned = new HashSet<string>(ScannedFiles().Select(ProjectOf), StringComparer.Ordinal);
        var owners = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(RepositoryPaths.Root, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsSource(file))
            {
                continue;
            }

            foreach (var text in File.ReadLines(file))
            {
                if (_domainDeclaration.IsMatch(text))
                {
                    owners.Add(ProjectOf(file));
                    break;
                }
            }
        }

        owners.Count.Should().BeGreaterThan(
            1,
            "the protocol is implemented in more than one project - Jint.DevTools owns the target-level domains and Jint.Browser the page-level ones - so finding one means this walk stopped looking");

        var unscanned = owners.Where(owner => !scanned.Contains(owner)).ToArray();

        Assert.That(
            unscanned.Length == 0,
            $"""
            {unscanned.Length} project(s) implement a Chrome DevTools Protocol domain and cite the protocol,
            and the citation check does not read them. A citation nothing verifies is one a protocol bump can
            leave pointing at the top of the document with nothing saying so.

            {string.Join(Environment.NewLine, unscanned.Select(owner => "  " + owner))}
            """);
    }

    /// <summary>Every C# file in the checkout the citation check reads.</summary>
    private static IEnumerable<string> ScannedFiles()
        => Directory.EnumerateFiles(RepositoryPaths.Root, "*.cs", SearchOption.AllDirectories).Where(IsSource);

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

    /// <summary>Whether a file is one of the repository's own sources rather than build output.</summary>
    private static bool IsSource(string path)
    {
        foreach (var segment in Relative(path).Split('/'))
        {
            if (Array.IndexOf(_notSourceDirectories, segment) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The top-level directory a file belongs to, which here is its project.</summary>
    private static string ProjectOf(string path) => Relative(path).Split('/')[0];

    private static string Relative(string path)
        => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
}
