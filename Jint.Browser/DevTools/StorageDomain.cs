using Jint.Browser.Runtime;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Storage;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Storage</c> domain: the three cookie commands, over the jar the browser context owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three commands of twenty-three, and the other twenty are a decision.</b> Every recorded client reads
/// and writes a page's cookies through <c>Storage.getCookies</c> and <c>Storage.setCookies</c> rather than
/// through <c>Network</c>'s — <c>tools/devtools-protocol/handshakes/matrix.md</c> shows all five doing it —
/// which is the whole reason this domain exists here. The rest of it is IndexedDB, cache storage, shared
/// storage, interest groups, attribution reporting and quota: every one of them names a store this browser
/// does not have, and answering a quota for a store that does not exist would be worse than
/// <c>-32601</c>.
/// </para>
/// <para>
/// <b><c>browserContextId</c> is accepted and ignored.</b> A page target's session addresses one page, and
/// a page belongs to one context whose jar is the one these commands read; a client naming a different
/// context on a page's session is naming something this command has no way to reach, and Chrome answers a
/// page session's cookies from that page's context too.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Storage/"/>.
/// </para>
/// </remarks>
internal sealed class StorageDomain : StorageDomainBase
{
    private readonly PageTarget _target;

    internal StorageDomain(PageTarget target)
    {
        _target = target;
    }

    private PageNetwork Network => _target.Page.Network;

    /// <inheritdoc/>
    protected override ValueTask<GetCookiesResponse> GetCookiesAsync(GetCookiesRequest parameters, CommandContext context)
        => new(new GetCookiesResponse { Cookies = PageCookies.All(Network) });

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetCookiesAsync(SetCookiesRequest parameters, CommandContext context)
    {
        foreach (var cookie in parameters.Cookies)
        {
            PageCookies.Set(Network, cookie, _target.Page.Url);
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> ClearCookiesAsync(ClearCookiesRequest parameters, CommandContext context)
    {
        PageCookies.Clear(Network);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }
}
