using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;

namespace Jint.DevTools.Session;

/// <summary>
/// Routes <c>Domain.method</c> to the registered domain, and answers <c>-32601</c> for everything else.
/// </summary>
/// <remarks>
/// <para>
/// The router only decides which domain a method belongs to. Whether that domain actually answers the
/// method is the generated dispatch's business: every command the pinned protocol declares has a virtual
/// whose default raises the same method-not-found error, so an unregistered domain and an unimplemented
/// command are indistinguishable to a client. That is the contract — an unimplemented method is never a
/// silent success.
/// </para>
/// </remarks>
internal sealed class CommandRouter
{
    private readonly Dictionary<string, DevToolsDomain> _domains = new(StringComparer.Ordinal);

    /// <summary>Gets the registered domains, in registration order.</summary>
    internal IReadOnlyCollection<DevToolsDomain> Domains => _domains.Values;

    /// <summary>Registers one domain, which must be the only one of its name.</summary>
    internal void Add(DevToolsDomain domain)
    {
        if (domain is null)
        {
            Throw.ArgumentNull(nameof(domain));
        }

        if (!_domains.TryAdd(domain.Name, domain))
        {
            Throw.InvalidOperation($"The '{domain.Name}' domain is already registered on this session.");
        }
    }

    /// <summary>Answers one command, or raises the protocol failure that says why it could not be.</summary>
    internal ValueTask<string> DispatchAsync(in ProtocolRequest request, CommandContext context)
    {
        var (domainName, member) = ProtocolMessage.SplitMethod(request.Method);

        if (!_domains.TryGetValue(domainName, out var domain))
        {
            return Throw.MethodNotFound<ValueTask<string>>(request.Method);
        }

        return domain.DispatchAsync(member, request.Parameters, context);
    }
}
