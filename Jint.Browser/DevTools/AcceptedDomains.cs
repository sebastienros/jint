using System.Diagnostics;
using Jint.Browser.Runtime;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Performance</c> domain: the few counters this browser can state without inventing one.
/// </summary>
/// <remarks>
/// <para>
/// Puppeteer's page initialisation sends <c>Performance.enable</c> and fails if it errors, which is why the
/// domain is here at all. <c>getMetrics</c> answers Chrome's metric <i>names</i> for the quantities that mean
/// the same thing here and leaves out every one that does not — which is most of them: <c>LayoutCount</c>,
/// <c>RecalcStyleCount</c>, <c>LayoutDuration</c> and their kind count work a renderer does, and reporting
/// zero for each would read as a page that laid out instantly rather than as a browser that never laid out at
/// all.
/// </para>
/// <para>
/// <b><c>metrics</c> is never emitted.</b> Chrome sends it when its own instrumentation ticks; there is no
/// tick here, and a client that wants a reading takes one.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Performance/"/>.
/// </para>
/// </remarks>
internal sealed class PerformanceDomain : Jint.DevTools.Domains.PerformanceDomainBase
{
    private readonly PageTarget _target;

    internal PerformanceDomain(PageTarget target)
    {
        _target = target;
    }

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

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Performance/#method-getMetrics — three numbers,
    /// each of which is the thing Chrome's own metric of that name counts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Timestamp</c> is the monotonic clock Chrome reports it from, in seconds; <c>Documents</c> is one
    /// while a document is loaded, there being one per page here; <c>Nodes</c> is that document's own node
    /// count, walked now.
    /// </para>
    /// <para>
    /// <b><c>JSHeapUsedSize</c> and <c>JSHeapTotalSize</c> are deliberately absent</b>, and it is the same
    /// answer <c>Runtime.getHeapUsage</c> gives at greater length: there is no JavaScript heap to size — a
    /// <c>JsValue</c> is a CLR object on the process's own garbage-collected heap, shared with the host and
    /// with every other page. The engine's memory <i>accounting</i> does exist, but what
    /// <c>MemoryLimitConstraint</c> counts is one turn's allocations rather than a live heap, so reporting it
    /// under a heap's name would be a number that fell back to nearly zero every time it was read.
    /// </para>
    /// </remarks>
    protected override ValueTask<Jint.DevTools.Protocol.Performance.GetMetricsResponse> GetMetricsAsync(
        EmptyParameters parameters,
        CommandContext context)
    {
        var document = PageRuntime.Find(_target.Runtime.Engine)?.Document;

        Jint.DevTools.Protocol.Performance.Metric[] metrics =
        [
            Metric("Timestamp", Stopwatch.GetTimestamp() / (double) Stopwatch.Frequency),
            Metric("Documents", document is null ? 0 : 1),
            Metric("Nodes", document is null ? 0 : Count(document)),
        ];

        return new ValueTask<Jint.DevTools.Protocol.Performance.GetMetricsResponse>(
            new Jint.DevTools.Protocol.Performance.GetMetricsResponse { Metrics = metrics });
    }

    private static Jint.DevTools.Protocol.Performance.Metric Metric(string name, double value)
        => new() { Name = name, Value = value };

    /// <summary>Every node of the document, counted with an explicit stack because the depth is a stranger's.</summary>
    private static int Count(AngleSharp.Dom.INode root)
    {
        var pending = new Stack<AngleSharp.Dom.INode>();
        pending.Push(root);
        var seen = 0;

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            seen++;

            foreach (var child in current.ChildNodes)
            {
                pending.Push(child);
            }
        }

        return seen;
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
