using System.Globalization;
using System.Reflection;
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
    private static readonly DevToolsSession _session = BuiltInDomains.RegisterOn(new DevToolsSession(new InProcessConnection()));

    [Test]
    public void EveryImplementedMethodTheManifestNamesIsOverridden()
    {
        var notOverridden = ProtocolManifest.ImplementedMethods
            .Where(method => !IsOverridden(method))
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

        foreach (var domain in _session.Domains)
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
            _session.Domains.Should().Contain(
                domain => domain.Name == reported.Name,
                "Schema.getDomains reports '{0}', so a session has to have it registered",
                reported.Name);

            ProtocolManifest.ImplementedMethods.Should().Contain(
                method => method.StartsWith(reported.Name + ".", StringComparison.Ordinal),
                "Schema.getDomains reports '{0}', so something in it has to answer",
                reported.Name);
        }
    }

    private static bool IsOverridden(string qualified)
    {
        var separator = qualified.IndexOf('.', StringComparison.Ordinal);
        var domainName = qualified.Substring(0, separator);
        var expected = Naming(qualified.Substring(separator + 1)) + "Async";

        var domain = _session.Domains.FirstOrDefault(candidate => string.Equals(candidate.Name, domainName, StringComparison.Ordinal));
        if (domain is null)
        {
            return false;
        }

        var method = domain.GetType().GetMethod(expected, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        return method is not null && method.GetBaseDefinition() != method;
    }

    private static string Command(string domainName, string methodName)
    {
        var command = methodName.Substring(0, methodName.Length - "Async".Length);
        return string.Create(CultureInfo.InvariantCulture, $"{domainName}.{char.ToLowerInvariant(command[0])}{command.AsSpan(1)}");
    }

    private static string Naming(string command) => char.ToUpperInvariant(command[0]) + command.Substring(1);
}
