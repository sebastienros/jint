using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Jint.Browser;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// Evidence for a harness failure, especially #3947's Windows timeouts. These counters observe the test
/// process, which may run other fixtures concurrently; they are not measurements of the page's CPU usage.
/// No diagnostic affects the harness verdict, a deadline, the event loop, or whether a case is retried.
/// </summary>
internal sealed class WptBrowserDiagnostics
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly TimeSpan? _cpu = ProcessCpu();
    private readonly TimeSpan _gcPause = GC.GetTotalPauseDuration();
    private readonly int _gen0 = GC.CollectionCount(0);
    private readonly int _gen1 = GC.CollectionCount(1);
    private readonly int _gen2 = GC.CollectionCount(2);

    internal string Describe(Page page, WptBrowserCollector collector)
    {
        var cpu = ProcessCpu();
        var cpuMs = cpu is { } current && _cpu is { } initial
            ? (current - initial).TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : "unavailable";

        // Page.Requests contains transport-owned snapshots and is explicitly safe off the page loop.
        // It exposes status, not request timings: do not infer completion duration from these counts.
        var requests = page.Requests;
        var pending = 0;
        var failed = 0;
        foreach (var request in requests)
        {
            if (request.Failed)
            {
                failed++;
            }
            else if (request.Status == 0 && request.NotFetchedReason is null)
            {
                pending++;
            }
        }

        return FormattableString.Invariant(
            $" [WPT diagnostics: runtime={RuntimeInformation.FrameworkDescription}; processors={Environment.ProcessorCount}; elapsedMs={Stopwatch.GetElapsedTime(_started).TotalMilliseconds:F1}; processCpuMs={cpuMs}; processGcPauseMs={(GC.GetTotalPauseDuration() - _gcPause).TotalMilliseconds:F1}; processCollections={GC.CollectionCount(0) - _gen0}/{GC.CollectionCount(1) - _gen1}/{GC.CollectionCount(2) - _gen2}; threadPoolThreads={ThreadPool.ThreadCount}; threadPoolPending={ThreadPool.PendingWorkItemCount}; requests={requests.Count}; awaitingResponse={pending}; failedRequests={failed}; {collector.DescribeProgress()}]");
    }

    private static TimeSpan? ProcessCpu()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Some hosts deny process counters. Diagnostics must preserve the original harness failure.
            return null;
        }
    }
}
