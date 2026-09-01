using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Schema;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// Answers <c>Schema.getDomains</c> from the generation manifest.
/// </summary>
/// <remarks>
/// <para>
/// The protocol marks this domain deprecated, and it is implemented anyway: it is the one command a client
/// can ask before knowing anything, and the DevTools front end and several client libraries still send it.
/// </para>
/// <para>
/// What it answers is <c>manifest.json</c>'s <c>reportedDomains</c>, which the generator refuses to let name
/// a domain with no implemented command. So a client feature-detecting through this is never told about a
/// domain that would answer <c>-32601</c> to everything.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Schema/#method-getDomains"/>.
/// </para>
/// </remarks>
internal sealed class SchemaDomain : SchemaDomainBase
{
    /// <summary>The answer, built once: the manifest cannot change while the process runs.</summary>
    private static readonly GetDomainsResponse _domains = new() { Domains = [.. ProtocolManifest.ReportedDomains] };

    /// <inheritdoc/>
    protected override ValueTask<GetDomainsResponse> GetDomainsAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<GetDomainsResponse>(_domains);
    }
}
