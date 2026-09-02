using System.Globalization;
using System.Reflection;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.Tests.DevTools.Protocol;

/// <summary>
/// The manifest and the code agree about which commands this package answers.
/// </summary>
/// <remarks>
/// <para>
/// The manifest is a claim: <c>manifest.json</c> says which commands are implemented, the generator turns
/// that into <c>ProtocolManifest.ImplementedMethods</c>, and <c>Schema.getDomains</c> reports domains
/// derived from it. Nothing in the compiler connects that claim to whether a virtual was actually
/// overridden, so a domain could be reported to a client while every command in it answers
/// <c>-32601</c>, or a command could be answered without appearing in the manifest that documents the
/// surface.
/// </para>
/// <para>
/// Reflection can tell the two apart exactly: the generated virtual's declaring type is the
/// <c>&lt;Domain&gt;DomainBase</c>, and an override's is the domain class. So this holds the manifest to the
/// code in both directions.
/// </para>
/// </remarks>
public class ProtocolManifestTests
{
    /// <summary>
    /// Every domain a client can reach, across both kinds of session.
    /// </summary>
    /// <remarks>
    /// There are two kinds and the manifest covers both: the browser conversation answers about the server,
    /// and an attachment answers about one engine. Reading only one of them would let half the manifest go
    /// unchecked -- which is exactly the shape of the mistake these tests exist to catch.
    /// </remarks>
    private static readonly IReadOnlyCollection<DevToolsDomain> _domains = RegisteredDomains();

    private static IReadOnlyCollection<DevToolsDomain> RegisteredDomains()
    {
        var server = new DevToolsServer();
        var target = new EngineTarget(new Engine());
        server.AddTarget(target);

        var browser = server.OpenBrowserSession(new InProcessConnection());
        var attachment = browser.Session.CreateChild("S1");
        target.RegisterDomains(attachment, browser);

        var domains = new List<DevToolsDomain>(browser.Session.Domains);
        domains.AddRange(attachment.Domains);
        return domains;
    }

    /// <summary>
    /// The domains a <i>page</i> target answers, which this suite has no target for.
    /// </summary>
    /// <remarks>
    /// <b>They are in the manifest and they are not answered here, and both halves are deliberate.</b> A
    /// page-level command belongs to a target that has a document — <c>Jint.Browser</c>, which is AngleSharp
    /// plus Jint — and an engine target answers every one of them with <c>-32601</c>, which is what
    /// <c>HandshakeReplayTests</c> pins. The same property this test holds for the engine-level domains is
    /// held for these by <c>Jint.Tests.Browser</c>'s own manifest test, over a real page target; naming them
    /// here rather than skipping anything unrecognized is what keeps the two halves adding up to the whole
    /// manifest.
    /// </remarks>
    private static readonly string[] PageDomains = ["Page", "Emulation", "Network", "Fetch", "Performance", "Audits", "DOM", "Input", "Jint"];

    [Test]
    public void EveryImplementedMethodTheManifestNamesIsOverridden()
    {
        var notOverridden = ProtocolManifest.ImplementedMethods
            .Where(method => !IsPageLevel(method) && !IsOverridden(method))
            .ToArray();

        Assert.That(
            notOverridden.Length == 0,
            $"""
            manifest.json says {notOverridden.Length} command(s) are implemented that no registered domain
            overrides, so each answers -32601 while the manifest and Schema.getDomains say otherwise:

            {string.Join(Environment.NewLine, notOverridden.Select(method => "  " + method))}
            """);
    }

    [Test]
    public void NothingIsAnsweredThatTheManifestDoesNotName()
    {
        var undeclared = new List<string>();


        foreach (var domain in _domains)
        {
            foreach (var method in domain.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                // A command's virtual is declared on the generated <Domain>DomainBase; the enable and
                // disable hooks are declared on DevToolsDomain and are not commands.
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
            {undeclared.Count} command(s) are answered by a registered domain and are not in manifest.json's
            implementedMethods. The manifest is what documents the surface and what Schema.getDomains is
            derived from; a command missing from it is one no client is told about.

            {string.Join(Environment.NewLine, undeclared.Select(method => "  " + method))}
            """);
    }

    /// <summary>
    /// A reported domain answers at least one command, which is the property that makes
    /// <c>Schema.getDomains</c> worth asking.
    /// </summary>
    [Test]
    public void EveryReportedDomainIsRegisteredAndAnswersSomething()
    {
        foreach (var reported in ProtocolManifest.ReportedDomains)
        {
            _domains.Should().Contain(
                domain => domain.Name == reported.Name,
                "Schema.getDomains reports '{0}', so a session has to have it registered",
                reported.Name);

            ProtocolManifest.ImplementedMethods.Should().Contain(
                method => method.StartsWith(reported.Name + ".", StringComparison.Ordinal),
                "Schema.getDomains reports '{0}', so something in it has to answer",
                reported.Name);
        }
    }

    /// <summary>
    /// The page-level half of the manifest is answered somewhere else, and this test says where.
    /// </summary>
    [Test]
    public void EveryPageLevelMethodIsCheckedByTheBrowserSuite()
    {
        var unanswered = ProtocolManifest.ImplementedMethods
            .Where(method => IsPageLevel(method) && IsOverridden(method))
            .ToArray();

        Assert.That(
            unanswered.Length == 0,
            $"""
            {unanswered.Length} method(s) of a page-level domain are overridden on an engine-level session.
            A page-level command belongs to a target that has a document; answering one here would answer it
            for a target with no page to answer about:

            {string.Join(Environment.NewLine, unanswered.Select(method => "  " + method))}
            """);
    }

    private static bool IsPageLevel(string qualified)
    {
        var separator = qualified.IndexOf('.', StringComparison.Ordinal);
        var domain = qualified.Substring(0, separator);
        return Array.Exists(PageDomains, name => string.Equals(name, domain, StringComparison.Ordinal));
    }

    private static bool IsOverridden(string qualified)
    {
        var separator = qualified.IndexOf('.', StringComparison.Ordinal);
        var domainName = qualified.Substring(0, separator);
        var expected = Naming(qualified.Substring(separator + 1)) + "Async";

        foreach (var domain in _domains)
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
