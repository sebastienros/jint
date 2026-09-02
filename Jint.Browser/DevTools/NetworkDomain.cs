using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Network;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Network</c> domain, accepted and not yet reporting.
/// </summary>
/// <remarks>
/// <para>
/// Every recorded client sends <c>Network.enable</c> while opening a page and then waits for nothing in
/// particular; what it needs is for the command to succeed, because a refusal is read as a broken target.
/// <b>No network event is emitted yet</b> — <c>requestWillBeSent</c>, <c>responseReceived</c> and their kind
/// arrive with the interception work (campaign item C3), where a request's identifier, its timing and its
/// body all have to agree with what <c>Fetch</c> hands back. Reporting half of that now would give a client a
/// request log it could not act on.
/// </para>
/// <para>
/// <c>setCacheDisabled</c> and <c>setExtraHTTPHeaders</c> are stored on the target for the same campaign
/// item: there is no HTTP cache here to disable, and the headers reach the transport when the network layer
/// is the protocol's rather than the page's.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Network/"/>.
/// </para>
/// </remarks>
internal sealed class NetworkDomain : NetworkDomainBase
{
    private readonly PageTarget _target;

    internal NetworkDomain(PageTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EnableRequest parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetCacheDisabledAsync(SetCacheDisabledRequest parameters, CommandContext context)
    {
        _target.Emulation.CacheDisabled = parameters.CacheDisabled;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Answers success and sends no extra header, for two reasons rather than one.</summary>
    /// <remarks>
    /// The headers do not reach the transport until the network layer is the protocol's rather than the
    /// page's (campaign item C3) — and they are not even readable here yet: the protocol types
    /// <c>Network.Headers</c> as a free-form JSON object, so the generator emits an empty record and the
    /// values a client sent are not in the request object at all. Reading them means teaching the generator
    /// that an object type with no declared properties is a map, which is a change to make when something
    /// acts on the values rather than to make for a command that stores them.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetExtraHTTPHeadersAsync(SetExtraHTTPHeadersRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }
}

/// <summary>
/// The <c>Fetch</c> domain, accepted and intercepting nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>Fetch.enable</c> must not stall a navigation</b>, and that is the whole of why it is answered rather
/// than refused: a client that enables interception and then navigates is waiting for a
/// <c>requestPaused</c> that never comes, and a page that waited for a continuation would never load. So the
/// domain is enabled, no request is paused, and every request goes out as it would have.
/// </para>
/// <para>
/// Real interception is campaign item C3, where a paused request is a hop held in
/// <c>FetchObserver.OnRequestAsync</c> — the engine's own seam, which already answers with a
/// <c>FetchInterception</c> — and <c>Fetch.continueRequest</c> is what releases it.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Fetch/"/>.
/// </para>
/// </remarks>
internal sealed class FetchDomain : FetchDomainBase
{
    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(Jint.DevTools.Protocol.Fetch.EnableRequest parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }
}

/// <summary>
/// The <c>Performance</c> domain, accepted and measuring nothing.
/// </summary>
/// <remarks>
/// Puppeteer's page initialisation sends <c>Performance.enable</c> and fails if it errors, which is the whole
/// reason it is here. <c>getMetrics</c> is deliberately not implemented: its counters are a renderer's —
/// layout objects, recalc styles, frames — and answering a page's own <c>performance</c> entries under those
/// names would tell a client something untrue. A host that wants the engine's numbers asks
/// <c>engine.Diagnostics</c>, and a page's own asks <c>performance</c> in script.
/// </remarks>
internal sealed class PerformanceDomain : PerformanceDomainBase
{
    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(Jint.DevTools.Protocol.Performance.EnableRequest parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }
}

/// <summary>
/// The <c>Audits</c> domain, accepted and reporting nothing.
/// </summary>
/// <remarks>
/// An issue is a browser telling a developer what it did about something questionable — a cookie it refused,
/// a mixed-content subresource it blocked, a deprecation it honoured — and every one of those is a decision
/// made by a layer this browser does not have. Puppeteer enables the domain while opening a page and fails if
/// it errors; it then waits for nothing, so an empty stream is the truthful answer rather than a gap.
/// </remarks>
internal sealed class AuditsDomain : AuditsDomainBase
{
    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }
}
