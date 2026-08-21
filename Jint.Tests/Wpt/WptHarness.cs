#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.WebApi;

namespace Jint.Tests.Wpt;

/// <summary>
/// One test's outcome as the shim recorded it.
/// </summary>
internal readonly record struct WptTestResult(string Name, string Status, string? Message)
{
    internal bool Passed => string.Equals(Status, "PASS", StringComparison.Ordinal);
}

/// <summary>
/// Everything one <c>.any.js</c> file produced.
/// </summary>
/// <param name="Results">The tests it registered, in registration order.</param>
/// <param name="HarnessError">
/// Set when the file could not be run to the point of producing results at all — a throw out of the file's
/// top level, a missing vendored resource, or the harness never reporting itself complete. It is deliberately
/// nothing a per-test exclusion can cover, because there is no test to name.
/// </param>
internal sealed record WptRunOutcome(IReadOnlyList<WptTestResult> Results, string? HarnessError);

/// <summary>
/// Runs one vendored <c>.any.js</c> file on a fresh engine and hands back what the shim recorded.
/// </summary>
/// <remarks>
/// <para>
/// <b>The engine under test supplies its own timers.</b> Unlike the test262 harness — which has no web APIs
/// to enable and therefore shims <c>setTimeout</c> onto the event loop itself — this driver enables
/// <see cref="WebApiFeatures.Timers"/> and installs no timer of its own, so a suite that schedules one
/// exercises the shipped <c>TimerQueue</c> and HTML's ordering rather than a second implementation written
/// for the harness. The streams corpus is what cashed that in: it reaches for <c>step_timeout</c> — the shim
/// forwards it straight onto the engine's <c>setTimeout</c> — at 45 sites, through <c>delay()</c> and
/// <c>flushAsyncEvents()</c> in <c>streams/resources/test-utils.js</c> and directly, so several hundred of
/// its assertions are decided by the queue's own ordering. The clock is <see cref="TimeProvider.System"/> and
/// the drive loop below is what pumps it: a timer fires on a <c>ProcessTasks</c> at or after its due time,
/// exactly as it does for an embedder.
/// </para>
/// <para>
/// <b>The engine also carries the fetch object model, and pointedly not <c>fetch</c>.</b>
/// <c>Headers</c>, <c>Request</c> and <c>Response</c> are not in <see cref="WebApiFeatures.Default"/> —
/// they ship with <see cref="WebApiFeatures.Fetch"/> — but a corpus reaches an algorithm through every
/// entry point the platform gives it, and two vendored suites need exactly that entry point.
/// <c>url/urlencoded-parser.any.js</c> runs each of its 35 inputs through <c>URLSearchParams</c>,
/// <c>Request.formData()</c> <i>and</i> <c>Response.formData()</c>, because a browser parses
/// <c>application/x-www-form-urlencoded</c> with one algorithm in all three places; and the two
/// <c>fetch/api/</c> suites are about the three interfaces and nothing else — every file in them builds its
/// own <c>Headers</c> or its own <c>Response</c> body, which is the property that let them be vendored while
/// the rest of that corpus, which talks to a server, could not. Withholding the three interfaces would not
/// test less of Jint, it would only turn a third of the url file into exclusions and leave the other 30
/// files unrunnable.
/// <c>WebApiRegistration.InstallFetchModel</c> is the same door <c>Engine.Advanced.SetFetchHandler</c>
/// opens for a host that must build a <c>Response</c> without being granted the network, and no shipped
/// feature flag names the model on its own — <see cref="WebApiFeatures.Fetch"/>,
/// <see cref="WebApiFeatures.CacheApi"/> and <see cref="WebApiFeatures.FetchEvents"/> each bring it with
/// something else. <b>Outbound network access is still what no suite gets</b>: the three interfaces
/// construct, parse and serialize, and nothing here can open a socket.
/// </para>
/// <para>
/// <b>Variants are not sharded.</b> A <c>// META: variant=?1-1000</c> line splits a suite across browser
/// runs, and the shard is chosen from <c>location.search</c>, which the shim leaves empty — so
/// <c>subsetTest</c> and <c>subsetTestByKey</c> run every case and one run of a file is the union of all of
/// its variants. That is what a single-process driver wants, and it is why there is one theory case per
/// file rather than one per variant.
/// </para>
/// </remarks>
internal static class WptHarness
{
    /// <summary>
    /// A runaway guard, not an assertion: nothing in the corpus is timing-dependent, and a file that has not
    /// reported itself complete by now is hung rather than slow. Generous enough that a loaded CI machine
    /// running the suites in parallel cannot reach it.
    /// </summary>
    private static readonly TimeSpan _harnessDeadline = TimeSpan.FromMinutes(5);

    internal static WptRunOutcome Run(string testFilePath)
    {
        var directory = WptCorpus.DirectoryOf(testFilePath);
        return RunsInAWorker(testFilePath)
            ? RunInWorker(testFilePath, directory)
            : Execute(directory, MetaScripts(testFilePath, directory), WptCorpus.Read(testFilePath), testFilePath);
    }

    /// <summary>
    /// Whether a file is run <b>inside a worker</b>: it is in the <c>workers/</c> corpus, and its own
    /// <c>// META: global=</c> line says it can be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The directory decides the scope and the META key decides the suitability</b>, and neither half does
    /// the job alone. The directory alone would force a lane on a <c>workers/</c> file that is really about a
    /// <i>window</i> creating a worker — which is what most of upstream's <c>workers/</c> tree is, and what the
    /// not-vendored table is mostly full of. The META key alone would let a corpus bump move a settled suite
    /// into a worker by editing one comment, and would move exactly the wrong files: it is a list of the
    /// globals a file <i>supports</i>, so <c>global=window,worker</c> is true of both lanes and says nothing
    /// about which one is worth running.
    /// </para>
    /// <para>
    /// What the key genuinely rules out is a file that names no worker global at all: running such a file in a
    /// worker would be asserting window semantics against a global that is not one. And what the directory
    /// rules out is the opposite mistake — running <c>workers/Worker-custom-event.any.js</c> in the driver's
    /// top-level engine, where it would test that engine's own <c>addEventListener</c>, pass, and prove nothing
    /// about a worker. <c>WptTestRunner.EveryWorkerLaneFileIsAWorkersFile</c> pins both directions.
    /// </para>
    /// <para>
    /// Negated entries — upstream's <c>global=worker,!serviceworker</c> form — are subtractions from a group
    /// and can never add a global, so they are skipped rather than parsed.
    /// </para>
    /// </remarks>
    internal static bool RunsInAWorker(string testFilePath)
    {
        if (!testFilePath.StartsWith("workers/", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var value in MetaValues(WptCorpus.Read(testFilePath), "global="))
        {
            foreach (var entry in value.Split(','))
            {
                var name = entry.Trim();
                if (name.Length == 0 || name[0] == '!')
                {
                    continue;
                }

                if (name is "worker" or "dedicatedworker")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The values of one <c>// META:</c> key, in declaration order.
    /// </summary>
    /// <remarks>
    /// The META block is a run of leading comment lines; the first line of code ends it. Both spellings
    /// upstream uses are accepted — <c>// META: global=worker</c> and the space-less <c>//META: global=worker</c>,
    /// which <c>workers/Worker-constructor-proto.any.js</c> is written with. Reading only the first spelling
    /// would drop a <c>script=</c> line silently, which presents as a suite failing on a missing helper rather
    /// than as a parsing bug.
    /// </remarks>
    private static IEnumerable<string> MetaValues(string source, string key)
    {
        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            var payload =
                line.StartsWith("// META:", StringComparison.Ordinal) ? line.Substring("// META:".Length)
                : line.StartsWith("//META:", StringComparison.Ordinal) ? line.Substring("//META:".Length)
                : null;

            if (payload is null)
            {
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                yield break;
            }

            payload = payload.TrimStart();
            if (payload.StartsWith(key, StringComparison.Ordinal))
            {
                yield return payload.Substring(key.Length).Trim();
            }
        }
    }

    /// <summary>
    /// Runs one worker-scoped file by making it the body of a real module worker, then pumping the parent and
    /// the worker cooperatively on this thread until the shim reports every test settled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The results are read straight off the <b>worker</b> engine rather than posted back to the parent. That
    /// is not a shortcut past the message path: the driver holds <c>connection.Worker</c>, pumps it on its own
    /// thread and reads it between turns, so the values are as settled as the parent's would be — and routing
    /// them through <c>postMessage</c> would make every file's outcome depend on the serializer, so a defect in
    /// it would present as every worker suite reporting nothing rather than as a failing assertion.
    /// </para>
    /// <para>
    /// A worker whose module never evaluated ends as <c>StartupFailed</c> carrying the CLR reason, and that is
    /// reported as a harness error for the whole file — which is what it is: no test was registered, so there
    /// is nothing a per-test exclusion could name.
    /// </para>
    /// </remarks>
    private static WptRunOutcome RunInWorker(string testFilePath, string directory)
    {
        // The same three-step composition a top-level suite gets from three Execute calls: the shim, then the
        // file's META helpers, then the file. It is one module because a module worker has one entry script.
        var body = new StringBuilder(WptCorpus.Prelude).Append('\n');
        foreach (var script in MetaScripts(testFilePath, directory))
        {
            body.Append(WptCorpus.Read(script)).Append('\n');
        }

        body.Append(WptCorpus.Read(testFilePath));

        var provider = new WptWorkerProvider(body.ToString(), directory);
        var parent = BuildEngine(directory, provider);

        Engine? worker = null;
        try
        {
            parent.SetValue("__wptWorkerSpecifier", testFilePath);
            parent.Execute("globalThis.__wptWorker = new Worker(__wptWorkerSpecifier, { type: 'module' });");

            var stalled = PumpWorker(parent, provider, out worker);
            return new WptRunOutcome(worker is null ? [] : ReadResults(worker), stalled);
        }
        catch (Exception ex)
        {
            List<WptTestResult> partial;
            try
            {
                partial = worker is null ? [] : ReadResults(worker);
            }
            catch
            {
                partial = [];
            }

            return new WptRunOutcome(partial, Describe(ex));
        }
    }

    /// <summary>
    /// Drives the parent and every live worker in turn until the worker's shim reports itself complete.
    /// </summary>
    /// <remarks>
    /// One thread, no waiting on another: <c>ProcessTasks</c> drains an engine's queue, and a message crossing
    /// between the two only <i>enqueues</i> on the receiver, so a round of the loop that changed anything is
    /// followed by another round that sees it. The idle rule is the top-level loop's, widened over both
    /// engines: when no engine has queued work and none has scheduled any, no amount of pumping can change the
    /// answer.
    /// </remarks>
    private static string? PumpWorker(Engine parent, WptWorkerProvider provider, out Engine? worker)
    {
        var started = Stopwatch.GetTimestamp();
        worker = provider.Started.Count > 0 ? provider.Started[0].Worker : null;

        while (true)
        {
            parent.Advanced.ProcessTasks();

            // A copy, because a worker that ends while being pumped removes itself from the live list.
            foreach (var connection in provider.Live.ToArray())
            {
                connection.Worker.Advanced.ProcessTasks();
            }

            worker ??= provider.Started.Count > 0 ? provider.Started[0].Worker : null;

            if (provider.Started.Count > 0 && provider.Started[0] is { IsFaulted: true } faulted)
            {
                return $"the worker did not start ({faulted.EndReason}): {Describe(faulted.Error!)}";
            }

            // Null until the worker's module has evaluated, which is what the first rounds are waiting for.
            var outstanding = worker is null ? null : Outstanding(worker);
            if (outstanding is not null && IsComplete(outstanding))
            {
                return null;
            }

            if (NextDue(parent, provider) is not { } untilDue)
            {
                return outstanding is null
                    ? "nothing is left to pump and the worker never installed the harness shim"
                    : Stalled(outstanding, "nothing is left to pump");
            }

            if (Stopwatch.GetElapsedTime(started) >= _harnessDeadline)
            {
                return outstanding is null
                    ? $"the worker did not install the harness shim within {_harnessDeadline}"
                    : Stalled(outstanding, $"the harness did not complete within {_harnessDeadline}");
            }

            if (untilDue > TimeSpan.Zero)
            {
                Thread.Sleep(untilDue < TimeSpan.FromMilliseconds(10) ? untilDue : TimeSpan.FromMilliseconds(10));
            }
        }
    }

    /// <summary>
    /// The soonest any of the engines has work, or <see langword="null"/> when none of them has any.
    /// </summary>
    private static TimeSpan? NextDue(Engine parent, WptWorkerProvider provider)
    {
        var soonest = parent.TimeUntilNextPumpScheduledWork();

        foreach (var connection in provider.Live.ToArray())
        {
            if (connection.Worker.TimeUntilNextPumpScheduledWork() is not { } due)
            {
                continue;
            }

            soonest = soonest is { } best && best <= due ? best : due;
        }

        return soonest;
    }

    /// <summary>
    /// Runs a script written here rather than vendored, under the same shim and the same drive loop. This is
    /// what the shim's own tests use: a harness that reported everything as passing would make every suite
    /// green and say nothing, so the assertions and the completion rules need testing in their own right.
    /// </summary>
    /// <param name="source">The script, as if it were the body of a <c>.any.js</c> file.</param>
    /// <param name="directory">
    /// The directory the script's <c>fetch()</c> calls resolve against, so a test can reach a real vendored
    /// corpus file.
    /// </param>
    internal static WptRunOutcome RunInline(string source, string directory = "")
        => Execute(directory, [], source, "inline.any.js");

    private static WptRunOutcome Execute(string directory, List<string> metaScripts, string source, string sourceName)
    {
        var engine = BuildEngine(directory);

        try
        {
            // Before the shim, which reads it: `setup({single_test: true})` names its one test after the file.
            engine.SetValue("__wptTestFile", sourceName);
            engine.Execute(WptCorpus.Prelude, source: "wpt-prelude/testharness-shim.js");

            foreach (var script in metaScripts)
            {
                engine.Execute(WptCorpus.Read(script), source: script);
            }

            engine.Execute(source, source: sourceName);

            // Pump to completion first and read afterwards: the shim records a test's outcome when it
            // finishes, so reading before the drive loop has run would report every async test as NOTRUN.
            var stalled = Outstanding(engine) is { } outstanding
                ? Pump(engine, outstanding)
                : "the harness shim did not install __wpt";
            return new WptRunOutcome(ReadResults(engine), stalled);
        }
        catch (Exception ex)
        {
            // Whatever ran before the throw is still worth reporting, but a second failure while reading it
            // must not replace the first: the harness error is the interesting one.
            List<WptTestResult> partial;
            try
            {
                partial = ReadResults(engine);
            }
            catch
            {
                partial = [];
            }

            return new WptRunOutcome(partial, Describe(ex));
        }
    }

    /// <summary>
    /// The <c>// META: script=</c> lines, in the order they are declared, resolved against the tree.
    /// </summary>
    /// <remarks>
    /// <c>global=</c> is the one other key that is acted on — see <see cref="RunsInAWorker"/>, which is what
    /// keeps a file about the worker global out of the driver's top-level engine. The rest are read and
    /// deliberately ignored: <c>title=</c> and <c>timeout=</c> are for the browser reporter, and
    /// <c>variant=</c> is sharding.
    /// </remarks>
    private static List<string> MetaScripts(string testFilePath, string directory)
    {
        var scripts = new List<string>();

        foreach (var reference in MetaValues(WptCorpus.Read(testFilePath), "script="))
        {
            scripts.Add(WptCorpus.ResolveReference(directory, reference));
        }

        return scripts;
    }

    private static Engine BuildEngine(string directory, WptWorkerProvider? workers = null)
    {
        var engine = new Engine(options =>
        {
            // Everything except outbound network access, which is what a suite under test is allowed to see.
            options.UseWebApis(WebApiFeatures.Default);

            // Only for the worker lane, and only then: an engine that no vendored file asks to create a worker
            // from is byte-for-byte the engine every other suite has always run on. `Worker` is absent without
            // both the flag and a provider, which UseWorkers sets together.
            if (workers is not null)
            {
                options.UseWorkers(workers);
            }

            // A guard on a hung script rather than a budget anything is measured against; see the field.
            options.TimeoutInterval(_harnessDeadline);
        });

        // Headers, Request and Response, which no feature flag names on their own — see the class remarks.
        WebApiRegistration.InstallFetchModel(engine);
        InstallResourceReader(engine, directory);

        return engine;
    }

    /// <summary>
    /// Installs the shim's <c>fetch</c> back-end: a reader over the vendored tree, so that a suite's
    /// <c>fetch("resources/urltestdata.json")</c> finds its corpus, and the askable form of the same question
    /// that its <c>XMLHttpRequest</c> needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A path the corpus does not hold is a vendoring bug rather than a test failure, so it erupts as a CLR
    /// exception and is reported as a harness error for the whole file instead of becoming a rejected promise a
    /// test could mask. The worker lane installs the same reader on the worker engine, so a file's environment
    /// does not depend on which lane ran it.
    /// </para>
    /// <para>
    /// <c>__wptResourceExists</c> is that rule's one exception, and it is why the shim's <c>XMLHttpRequest</c>
    /// can exist at all. <c>fetch</c> deliberately has no failure path; XHR needs one, because the suites that
    /// reach for it ask for wptserve endpoints and "there is no server here" has to arrive as a failing test
    /// rather than as a dead file. So this answers <see langword="false"/> for a path that is not vendored
    /// <i>and</i> for one that would leave the tree, which <c>ResolveReference</c> refuses outright.
    /// </para>
    /// </remarks>
    internal static void InstallResourceReader(Engine engine, string directory)
    {
        engine.SetValue("__wptReadResource", new ClrFunction(engine, "__wptReadResource", (_, args) =>
        {
            var reference = TypeConverter.ToString(args.At(0));
            return WptCorpus.Read(WptCorpus.ResolveReference(directory, reference));
        }));

        engine.SetValue("__wptResourceExists", new ClrFunction(engine, "__wptResourceExists", (_, args) =>
        {
            var reference = TypeConverter.ToString(args.At(0));
            try
            {
                return WptCorpus.Contains(WptCorpus.ResolveReference(directory, reference));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }));
    }

    /// <summary>
    /// Drives the engine until the shim reports every async test and promise test settled. Returns
    /// <see langword="null"/> on completion, or a description of what was still outstanding.
    /// </summary>
    /// <remarks>
    /// The outstanding set is read through the object model, never by evaluating a script:
    /// <c>engine.Evaluate</c> drains the event loop on its way out, so an answer it computed can be out of
    /// date by the time this loop acts on it. That is not theoretical — the window is exactly where a due
    /// timer settles, and it made the loop declare a run stalled whose last test had just finished, on a
    /// machine loaded enough for the timer to come due inside the check.
    /// </remarks>
    private static string? Pump(Engine engine, ObjectInstance outstanding)
    {
        var started = Stopwatch.GetTimestamp();

        while (!IsComplete(outstanding))
        {
            engine.Advanced.ProcessTasks();
            if (IsComplete(outstanding))
            {
                return null;
            }

            // Nothing is queued and the engine has scheduled nothing for itself, so no amount of pumping can
            // change the answer: a test is waiting on something that will never arrive. Reporting that at
            // once beats waiting out the deadline to say the same thing.
            if (engine.TimeUntilNextPumpScheduledWork() is not { } untilDue)
            {
                return Stalled(outstanding, "nothing is left to pump");
            }

            if (Stopwatch.GetElapsedTime(started) >= _harnessDeadline)
            {
                return Stalled(outstanding, $"the harness did not complete within {_harnessDeadline}");
            }

            if (untilDue > TimeSpan.Zero)
            {
                // Sleep no longer than the engine's own next due time, and cap it so the deadline above
                // stays responsive however far out that is.
                Thread.Sleep(untilDue < TimeSpan.FromMilliseconds(10) ? untilDue : TimeSpan.FromMilliseconds(10));
            }
        }

        return null;
    }

    /// <summary>The shim's live outstanding-test array, or <see langword="null"/> if the shim is not there.</summary>
    private static ObjectInstance? Outstanding(Engine engine)
        => engine.GetValue("__wpt") is ObjectInstance wpt ? wpt.Get("outstanding") as ObjectInstance : null;

    private static bool IsComplete(ObjectInstance outstanding)
        => TypeConverter.ToNumber(outstanding.Get("length")) == 0;

    private static string Stalled(ObjectInstance outstanding, string reason)
    {
        var names = new List<string>();
        var length = (long) TypeConverter.ToNumber(outstanding.Get("length"));
        for (var i = 0L; i < length; i++)
        {
            names.Add(TypeConverter.ToString(outstanding.Get(JsNumber.Create(i))));
        }

        return $"{reason}; still outstanding: {string.Join(", ", names)}";
    }

    /// <summary>
    /// Reads the recorded results back as data, through the object model rather than through
    /// <c>JSON.stringify</c>.
    /// </summary>
    /// <remarks>
    /// JSON is the obvious way to move a list of records across and it is the wrong one here: the URL corpus
    /// names tests after their input, several of those inputs are lone surrogates, and a well-formed
    /// <c>JSON.stringify</c> escapes those as <c>\udXXX</c> — which
    /// <see cref="System.Text.Json.JsonDocument"/> then refuses to unescape ("Cannot read incomplete UTF-16
    /// JSON text as string with missing low surrogate"). A .NET string holds an unpaired surrogate perfectly
    /// well, so reading the values straight off the objects loses nothing and needs no encoding both sides
    /// have to agree on.
    /// </remarks>
    private static List<WptTestResult> ReadResults(Engine engine)
    {
        var results = new List<WptTestResult>();

        if (engine.Evaluate("typeof __wpt === 'object' ? __wpt.results : null") is not ObjectInstance array)
        {
            return results;
        }

        var length = (long) TypeConverter.ToNumber(array.Get("length"));
        for (var i = 0L; i < length; i++)
        {
            if (array.Get(JsNumber.Create(i)) is not ObjectInstance entry)
            {
                continue;
            }

            var message = entry.Get("message");
            results.Add(new WptTestResult(
                TypeConverter.ToString(entry.Get("name")),
                TypeConverter.ToString(entry.Get("status")),
                message.IsNull() || message.IsUndefined() ? null : TypeConverter.ToString(message)));
        }

        return results;
    }

    private static string Describe(Exception ex) => ex switch
    {
        JavaScriptException js => $"{js.GetType().Name}: {js.Message}{Environment.NewLine}{js.JavaScriptStackTrace}",
        _ => $"{ex.GetType().Name}: {ex.Message}",
    };
}
#endif
