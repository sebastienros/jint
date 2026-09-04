using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The browser lane's driver: one <see cref="WptServer"/>, one <see cref="Browser"/>, and one fresh
/// <see cref="BrowserContext"/> and <see cref="Page"/> per file, navigated to a document the server really
/// serves and run under upstream's own <c>testharness.js</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What makes this lane different from the <c>.any.js</c> one</b> is not the corpus — it is the same
/// vendored tree at the same pin — but that a <i>document</i> is loaded. The harness is upstream's real file
/// pulled in by a <c>&lt;script src&gt;</c> through the parser driver, the realm is a real <c>Window</c> with
/// a <c>document</c> and a <c>&lt;div id=log&gt;</c>, and the results come back through the
/// <c>testharnessreport.js</c> slot upstream ships a stub for. A shim that quietly passed everything would
/// make a green run mean nothing, which is why the <c>.any.js</c> lane has <c>WptHarnessTests</c>; here there
/// is nothing of Jint's to test, because the harness is upstream's.
/// </para>
/// <para>
/// <b>One server and one browser for the whole lane.</b> The server is what the engine lane's is —
/// process-wide, on an ephemeral loopback port, serving the vendored corpus — and the browser holds no thread
/// of its own, so neither is worth building per fixture. What <i>is</i> per case is a fresh
/// <see cref="BrowserContext"/> (its own cookie jar, its own storage partition) and a fresh
/// <see cref="Page"/> (its own thread, its own engine, its own realm), which is the isolation a conformance
/// driver actually needs. NUnit runs the fixtures of this assembly in parallel and the cases of one fixture
/// one at a time, so several pages are open at once and none of them shares anything.
/// </para>
/// <para>
/// <b>Results cross the thread boundary as strings.</b> The overlay posts one JSON string per subtest and one
/// for the completion through <c>__jintWptReport</c>, a delegate installed on <i>every</i> page engine by a
/// <see cref="BrowserOptions.ConfigureEngine"/> callback — every engine, because a navigation builds a new one
/// and the wrapper documents this lane loads are reached by navigating. Which page a report belongs to is
/// answered by the engine it came from (<c>PageRuntime.Find</c>), not by a slot the driver sets before each
/// case: pages are open concurrently, and a slot would be a race that shows up as one file's results appearing
/// in another file's report.
/// </para>
/// <para>
/// <b>The deadline is the driver's own.</b> <see cref="BrowserOptions.MaxTaskDuration"/> is
/// <see cref="Timeout.InfiniteTimeSpan"/>, so a legitimately slow wpt file is bounded by
/// <see cref="Deadline"/> rather than cut mid-script into a <c>PageErrorKind.BudgetExceeded</c> that would
/// look like an engine defect. Upstream's own harness timeout is untouched and is the one that usually fires
/// first: it is what turns a test waiting on something that never happens into a <c>TIMEOUT</c> row rather
/// than into a file with no report at all.
/// </para>
/// </remarks>
internal sealed class WptBrowserHarness : IDisposable
{
    /// <summary>
    /// How long one file may take before the driver gives up on it, which is a harness error for the file.
    /// </summary>
    /// <remarks>
    /// Upstream's harness times a file out at 10 s (60 s for <c>// META: timeout=long</c>) and reports it, so
    /// this is the backstop for the case where the harness itself never reports — a document that failed to
    /// parse, a script that never ran, a page wedged before <c>testharness.js</c> loaded. Infinite under a
    /// debugger, because a breakpoint is not a hang.
    /// </remarks>
    internal static TimeSpan Deadline { get; } =
        Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(30);

    /// <summary>
    /// The same ceiling as <see cref="Deadline"/>, expressed as something
    /// <see cref="NavigationOptions.Timeout"/> accepts.
    /// </summary>
    /// <remarks>
    /// That property refuses anything but a positive value, deliberately — a navigation nobody bounds is a
    /// call an automation host can never get back from — so the debugger's "no deadline" is spelled here as a
    /// day rather than as <see cref="Timeout.InfiniteTimeSpan"/>. Nothing else about the wait is clamped.
    /// </remarks>
    private static TimeSpan NavigationTimeout =>
        Deadline > TimeSpan.Zero ? Deadline : TimeSpan.FromDays(1);

    /// <summary>
    /// How long the driver waits between asking the page whether it has gone idle, while it waits for the
    /// harness to report completion.
    /// </summary>
    /// <remarks>
    /// A page is not idle while the harness's own timeout timer is scheduled, and it can be idle and not done
    /// — a request in flight is a thread-pool completion nothing on the engine's queue reports, which is the
    /// same reason the engine lane's server-lane drive loop polls. So the wait is a slice rather than a single
    /// call: short enough that a file which finished is not held for long, long enough that a file which is
    /// working is not interrupted a thousand times a second.
    /// </remarks>
    private static readonly TimeSpan _pollSlice = TimeSpan.FromMilliseconds(25);

    private static readonly Lazy<WptBrowserHarness> _instance =
        new(static () => new WptBrowserHarness(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The lane's server and browser, started on first use and alive until the process exits.</summary>
    internal static WptBrowserHarness Instance => _instance.Value;

    private readonly WptServer _server;
    private readonly Browser _browser;
    private readonly ConcurrentDictionary<Page, WptBrowserCollector> _collectors = new();

    private WptBrowserHarness()
    {
        _server = new WptServer(Overlays);

        var options = new BrowserOptions
        {
            // The driver's own per-file deadline is what bounds a file; see the class remarks.
            MaxTaskDuration = Timeout.InfiniteTimeSpan,

            // Errors are read back after the run: a BudgetExceeded is the one thing upstream's harness cannot
            // see for itself, and the console is what a failing file's message often is.
            RecordErrors = true,
            RecordConsoleMessages = true,
        };

        options.ConfigureEngine(o => o.Configure(engine =>
        {
            engine.SetValue("__jintWptReport", new Action<string>(json => Report(engine, json)));
            engine.SetValue("__jintWptInput", new Action<string>(json => WptBrowserInput.Dispatch(engine, json)));
        }));

        _browser = new Browser(options);
    }

    /// <summary>The URL the server serves <paramref name="path"/> at.</summary>
    internal string UrlFor(string path) => _server.UrlFor(path);

    /// <summary>
    /// Runs one document and answers every result it reported, or the harness error that stopped it.
    /// </summary>
    /// <param name="path">
    /// A path in the wpt tree: a vendored document (<c>dom/events/Event-propagation.html</c>) or a wrapper the
    /// server synthesizes for a vendored script (<c>dom/events/Event-constructors.any.html</c>).
    /// </param>
    internal async Task<WptBrowserOutcome> RunAsync(string path)
    {
        var collector = new WptBrowserCollector();

        var context = await _browser.NewContextAsync(new BrowserContextOptions
        {
            // The oldest promise this corpus makes, kept here the way the engine lane keeps it: a suite
            // cannot open a socket to anything but this loopback port, on the first hop and on every
            // redirect.
            UrlFilter = _server.Owns,
        }).ConfigureAwait(false);

        try
        {
            var page = await context.NewPageAsync().ConfigureAwait(false);
            _collectors[page] = collector;

            try
            {
                // Recorded here rather than in the runner, because this is the single funnel every case's
                // outcome comes back through — so the census sees the whole lane whatever the theory then
                // asserts, and a file the theories already ran is tallied rather than run a second time.
                var outcome = await RunAsync(page, collector, path).ConfigureAwait(false);
                WptBrowserCensus.Record(path, outcome);
                WptBrowserCauses.Record(path, outcome);
                return outcome;
            }
            finally
            {
                _collectors.TryRemove(page, out _);
            }
        }
        finally
        {
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<WptBrowserOutcome> RunAsync(Page page, WptBrowserCollector collector, string path)
    {
        var started = Stopwatch.GetTimestamp();
        var url = _server.UrlFor(path);

        try
        {
            await page.NavigateAsync(url, new NavigationOptions
            {
                // Committed rather than Load: the harness reports through its own completion callback, and a
                // file whose one test finishes before `load` would have the driver waiting for a signal that
                // has already been given. Waiting for the navigation to commit is only waiting for the engine
                // the results will come from to exist.
                WaitUntil = WaitUntilState.Commit,
                Timeout = NavigationTimeout,
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return WptBrowserOutcome.Failed($"navigating to {url} failed: {exception.Message}");
        }

        while (!collector.IsComplete)
        {
            if (Remaining(started) is not { } remaining)
            {
                return WptBrowserOutcome.Failed(
                    $"the harness did not report completion within {Deadline.TotalSeconds:N0}s"
                    + Describe(page, collector));
            }

            var slice = remaining > TimeSpan.Zero && remaining < _pollSlice ? remaining : _pollSlice;
            var idle = await page.WaitForIdleAsync(slice).ConfigureAwait(false);

            if (idle && !collector.IsComplete)
            {
                // The page has nothing queued and nothing scheduled, which is not the same as finished: a
                // request in flight completes on the thread pool and puts its continuation on the queue only
                // when it lands. So the driver waits off the page's own thread rather than spinning on it.
                await Task.Delay(_pollSlice).ConfigureAwait(false);
            }
        }

        return collector.Outcome(BudgetFailure(page));
    }

    /// <summary>
    /// The one page error upstream's harness cannot have seen, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Everything else it can. Jint fires a real <c>error</c> event at the global scope for an exception that
    /// escapes a listener, a timer callback or a microtask, and a real <c>unhandledrejection</c> for a
    /// rejection nobody handled — <c>Jint/WebApi/GlobalEvents/GlobalEventTarget.cs</c> — and
    /// <c>testharness.js</c> registers listeners for both. So the harness applies its own rule, including
    /// <c>setup({allow_uncaught_exception: true})</c>, and the driver reporting the same failure a second time
    /// out of <see cref="Page.Errors"/> would both double-count it and override the one property that setup
    /// call exists to declare. That is the one place this lane deliberately does <i>not</i> mirror the
    /// <c>.any.js</c> lane, whose shim has no global event target to listen at.
    /// <para>
    /// A budget failure is the exception: it is the pump abandoning a turn, no exception reaches script, and
    /// nothing fires. It should not happen at all here — <see cref="BrowserOptions.MaxTaskDuration"/> is
    /// infinite — so seeing one means a constraint this lane did not arm is bounding a page, and the file's
    /// results are not trustworthy.
    /// </para>
    /// </remarks>
    private static string? BudgetFailure(Page page)
    {
        foreach (var error in page.Errors)
        {
            if (error.Kind == PageErrorKind.BudgetExceeded)
            {
                return "a page turn ran out of its budget: " + error.Message;
            }
        }

        return null;
    }

    /// <summary>What is worth saying about a page that never reported, for the message.</summary>
    private static string Describe(Page page, WptBrowserCollector collector)
    {
        var described = new StringBuilder();
        described.Append(" (").Append(collector.Results.Count).Append(" result(s) so far");

        foreach (var error in page.Errors)
        {
            described.Append("; ").Append(error.Kind).Append(": ").Append(error.Message);
        }

        return described.Append(')').ToString();
    }

    private static TimeSpan? Remaining(long started)
    {
        if (Deadline == Timeout.InfiniteTimeSpan)
        {
            return Timeout.InfiniteTimeSpan;
        }

        var remaining = Deadline - Stopwatch.GetElapsedTime(started);
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    /// <summary>
    /// Routes one posted report to the page whose engine posted it.
    /// </summary>
    /// <remarks>
    /// Called on that page's own loop thread, from inside whatever turn the harness reported in. It touches
    /// nothing of the engine's: the payload is already a string, and the collector it lands in is the driver's.
    /// </remarks>
    private void Report(Engine engine, string json)
    {
        if (PageRuntime.Find(engine)?.Page is { } page && _collectors.TryGetValue(page, out var collector))
        {
            collector.Add(json);
        }
    }

    /// <summary>
    /// What this lane answers upstream's vendor slots with, keyed by the path the server serves each at.
    /// </summary>
    /// <remarks>
    /// Both are embedded rather than read from the tree, so the server sends the bytes the build compiled;
    /// and both are entries in one map rather than parameters, so a third slot is a line here and nothing
    /// else. <c>testharnessreport.js</c> is the script that posts a page's results back to the driver, and
    /// <c>testdriver-vendor.js</c> is what turns a document that drives input through <c>test_driver</c>
    /// into one that reports.
    /// </remarks>
    private static Dictionary<string, string> Overlays { get; } = new(StringComparer.Ordinal)
    {
        ["resources/testharnessreport.js"] = ReadOverlay("testharnessreport.js"),
        ["resources/testdriver-vendor.js"] = ReadOverlay("testdriver-vendor.js"),
    };

    private static string ReadOverlay(string name)
    {
        var resourceName = "wpt-browser-prelude/" + name;

        var assembly = typeof(WptBrowserHarness).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource \"{resourceName}\" is missing.", resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        _browser.CloseAsync().GetAwaiter().GetResult();
        _server.Dispose();
    }
}

/// <summary>
/// What one page reported, gathered off the page's own thread as the JSON strings the overlay posts.
/// </summary>
/// <remarks>
/// Everything here is written from the page loop and read from the test thread, so the list is guarded and
/// completion is a volatile flag rather than a lock the loop could be held on.
/// </remarks>
internal sealed class WptBrowserCollector
{
    private readonly List<WptBrowserResult> _results = [];
    private readonly object _gate = new();

    private volatile bool _complete;
    private int _harnessStatus;
    private string _harnessMessage = "";

    /// <summary>Whether the harness has run its completion callback.</summary>
    internal bool IsComplete => _complete;

    /// <summary>Everything reported so far, oldest first.</summary>
    internal IReadOnlyList<WptBrowserResult> Results
    {
        get
        {
            lock (_gate)
            {
                return _results.ToArray();
            }
        }
    }

    internal void Add(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.GetProperty("kind").GetString() == "result")
        {
            var result = new WptBrowserResult(
                root.GetProperty("name").GetString() ?? "",
                root.GetProperty("status").GetInt32(),
                root.GetProperty("message").GetString() ?? "");

            lock (_gate)
            {
                _results.Add(result);
            }

            return;
        }

        _harnessStatus = root.GetProperty("status").GetInt32();
        _harnessMessage = root.GetProperty("message").GetString() ?? "";

        // Last, and after the two fields it publishes: the test thread reads them the moment this turns true.
        _complete = true;
    }

    /// <summary>
    /// What the file produced. <paramref name="budgetFailure"/> is a harness error the harness itself could
    /// not have seen; anything else is upstream's own verdict on the file.
    /// </summary>
    internal WptBrowserOutcome Outcome(string? budgetFailure)
    {
        if (budgetFailure is not null)
        {
            return WptBrowserOutcome.Failed(budgetFailure);
        }

        // TestsStatus.statuses: OK 0, ERROR 1, TIMEOUT 2, PRECONDITION_FAILED 3. Anything but OK is a harness
        // error covering the whole file, which is the same unit of report the .any.js lane uses — a file that
        // cannot produce a per-test result cannot be named by a per-test exclusion either.
        if (_harnessStatus != 0)
        {
            var name = _harnessStatus switch
            {
                1 => "ERROR",
                2 => "TIMEOUT",
                3 => "PRECONDITION_FAILED",
                _ => _harnessStatus.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            return WptBrowserOutcome.Failed($"the harness reported {name}: {_harnessMessage}");
        }

        return new WptBrowserOutcome(Results, HarnessError: null);
    }
}

/// <summary>One subtest's outcome, as <c>Test.statuses</c> numbers it.</summary>
/// <param name="Name">The name the file gave the test, which is what an exclusion matches.</param>
/// <param name="Status">PASS 0, FAIL 1, TIMEOUT 2, NOTRUN 3, PRECONDITION_FAILED 4.</param>
/// <param name="Message">What the harness said about it, which is empty for a pass.</param>
internal sealed record WptBrowserResult(string Name, int Status, string Message)
{
    internal bool Passed => Status == 0;

    /// <summary>The name upstream's <c>status_formats</c> gives this status, for a failure report.</summary>
    internal string StatusName => Status switch
    {
        0 => "PASS",
        1 => "FAIL",
        2 => "TIMEOUT",
        3 => "NOTRUN",
        4 => "PRECONDITION_FAILED",
        _ => Status.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// What one file produced: its results, or the harness error that covers the whole of it.
/// </summary>
/// <param name="Results">Every subtest that reported, oldest first.</param>
/// <param name="HarnessError">
/// Why the file produced no usable report, or <see langword="null"/>. A harness error is for the whole file
/// and no per-test exclusion can name it — which is why a file that cannot produce one belongs in the
/// not-vendored table with its reason rather than in the exclusion table.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WptBrowserOutcome(IReadOnlyList<WptBrowserResult> Results, string? HarnessError)
{
    internal static WptBrowserOutcome Failed(string reason) => new([], reason);
}
