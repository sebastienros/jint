using Jint.DevTools.Protocol;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

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
internal sealed class PerformanceDomain : Jint.DevTools.Domains.PerformanceDomainBase
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
internal sealed class AuditsDomain : Jint.DevTools.Domains.AuditsDomainBase
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
