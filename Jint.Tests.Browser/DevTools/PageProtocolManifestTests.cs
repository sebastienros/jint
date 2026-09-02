using System.Globalization;
using System.Reflection;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The page-level half of the manifest is answered by a page target, and this is where that is checked.
/// </summary>
/// <remarks>
/// <para>
/// <c>Jint.Tests.DevTools</c> holds the manifest and the code to each other for everything an <i>engine</i>
/// target answers, and skips the six page-level domains for the obvious reason: it has no page. This test is
/// the other half, over a real one — the same property, the same reflection, the same failure message — so
/// that between the two of them every entry in <c>implementedMethods</c> is accounted for.
/// </para>
/// <para>
/// It is the check that catches the shape of mistake the manifest exists to catch: a command added to
/// <c>manifest.json</c> and never overridden answers <c>-32601</c> while the manifest says it does not, and
/// nothing in the compiler connects the two.
/// </para>
/// </remarks>
[NonParallelizable]
public class PageProtocolManifestTests
{
    /// <summary>The domains a page target answers, which is what this test is responsible for.</summary>
    private static readonly string[] PageDomains = ["Page", "Emulation", "Network", "Fetch", "Performance", "Audits"];

    [Test]
    public async Task EveryPageLevelMethodTheManifestNamesIsOverridden()
    {
        var domains = await RegisteredDomainsAsync();

        var notOverridden = ProtocolManifest.ImplementedMethods
            .Where(IsPageLevel)
            .Where(method => !IsOverridden(domains, method))
            .ToArray();

        Assert.That(
            notOverridden.Length == 0,
            $"""
            manifest.json says {notOverridden.Length} page-level command(s) are implemented that no domain a
            page target registers overrides, so each answers -32601 while the manifest says otherwise:

            {string.Join(Environment.NewLine, notOverridden.Select(method => "  " + method))}
            """);
    }

    [Test]
    public async Task NothingIsAnsweredThatTheManifestDoesNotName()
    {
        var undeclared = new List<string>();

        foreach (var domain in await RegisteredDomainsAsync())
        {
            if (!Array.Exists(PageDomains, name => string.Equals(name, domain.Name, StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var method in domain.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var declared = method.GetBaseDefinition();
                if (declared == method || declared.DeclaringType?.Name.EndsWith("DomainBase", StringComparison.Ordinal) != true)
                {
                    continue;
                }

                var command = Command(domain.Name, method.Name);
                if (!ProtocolManifest.ImplementedMethods.Contains(command, StringComparer.Ordinal))
                {
                    undeclared.Add(command);
                }
            }
        }

        Assert.That(
            undeclared.Count == 0,
            $"""
            {undeclared.Count} command(s) are answered by a page target and are not in manifest.json's
            implementedMethods. The manifest is what documents the surface; a command missing from it is one
            no client is told about.

            {string.Join(Environment.NewLine, undeclared.Select(method => "  " + method))}
            """);
    }

    /// <summary>Every domain one attachment to a page target registers.</summary>
    private static async Task<IReadOnlyCollection<DevToolsDomain>> RegisteredDomainsAsync()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        var child = session.Browser.Session.Domains.ToList();
        foreach (var domain in DomainsOf(session, attachment))
        {
            child.Add(domain);
        }

        return child;
    }

    private static IEnumerable<DevToolsDomain> DomainsOf(PageSession session, string sessionId)
    {
        var child = session.Browser.Session.Child(sessionId);
        child.Should().NotBeNull("a client that attached has a child session on the connection's root");
        return child!.Domains;
    }

    private static bool IsPageLevel(string qualified)
    {
        var separator = qualified.IndexOf('.', StringComparison.Ordinal);
        var domain = qualified.Substring(0, separator);
        return Array.Exists(PageDomains, name => string.Equals(name, domain, StringComparison.Ordinal));
    }

    private static bool IsOverridden(IReadOnlyCollection<DevToolsDomain> domains, string qualified)
    {
        var separator = qualified.IndexOf('.', StringComparison.Ordinal);
        var domainName = qualified.Substring(0, separator);
        var expected = Naming(qualified.Substring(separator + 1)) + "Async";

        foreach (var domain in domains)
        {
            if (!string.Equals(domain.Name, domainName, StringComparison.Ordinal))
            {
                continue;
            }

            var method = domain.GetType().GetMethod(expected, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method is not null && method.GetBaseDefinition() != method)
            {
                return true;
            }
        }

        return false;
    }

    private static string Command(string domainName, string methodName)
    {
        var command = methodName.Substring(0, methodName.Length - "Async".Length);
        return string.Create(CultureInfo.InvariantCulture, $"{domainName}.{char.ToLowerInvariant(command[0])}{command.AsSpan(1)}");
    }

    private static string Naming(string command) => char.ToUpperInvariant(command[0]) + command.Substring(1);
}
