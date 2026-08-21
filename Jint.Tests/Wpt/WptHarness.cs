#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
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
/// entry point the platform gives it, and for one file here that means the object model:
/// <c>url/urlencoded-parser.any.js</c> runs each of its 35 inputs through <c>URLSearchParams</c>,
/// <c>Request.formData()</c> <i>and</i> <c>Response.formData()</c>, because a browser parses
/// <c>application/x-www-form-urlencoded</c> with one algorithm in all three places. Withholding the three
/// interfaces would not test less of Jint, it would only turn a third of that file into an exclusion.
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
        return Execute(directory, MetaScripts(testFilePath, directory), WptCorpus.Read(testFilePath), testFilePath);
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
    /// The other keys are read and deliberately ignored: <c>global=</c> names the browser globals the file
    /// supports (this one is none of them — see the shim's <c>GLOBAL</c>), <c>title=</c> and <c>timeout=</c>
    /// are for the browser reporter, and <c>variant=</c> is sharding.
    /// </remarks>
    private static List<string> MetaScripts(string testFilePath, string directory)
    {
        const string ScriptKey = "// META: script=";
        var scripts = new List<string>();

        foreach (var rawLine in WptCorpus.Read(testFilePath).Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith("// META:", StringComparison.Ordinal))
            {
                // The META block is a run of leading comment lines; the first line of code ends it.
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                break;
            }

            if (line.StartsWith(ScriptKey, StringComparison.Ordinal))
            {
                scripts.Add(WptCorpus.ResolveReference(directory, line.Substring(ScriptKey.Length).Trim()));
            }
        }

        return scripts;
    }

    private static Engine BuildEngine(string directory)
    {
        var engine = new Engine(options =>
        {
            // Everything except outbound network access, which is what a suite under test is allowed to see.
            options.UseWebApis(WebApiFeatures.Default);

            // A guard on a hung script rather than a budget anything is measured against; see the field.
            options.TimeoutInterval(_harnessDeadline);
        });

        // Headers, Request and Response, which no feature flag names on their own — see the class remarks.
        WebApiRegistration.InstallFetchModel(engine);

        // The shim's `fetch` is this and nothing else: a reader over the vendored tree, so that a suite's
        // `fetch("resources/urltestdata.json")` finds its corpus. A path the corpus does not hold is a
        // vendoring bug rather than a test failure, so it erupts as a CLR exception and is reported as a
        // harness error for the whole file instead of becoming a rejected promise a test could mask.
        engine.SetValue("__wptReadResource", new ClrFunction(engine, "__wptReadResource", (_, args) =>
        {
            var reference = TypeConverter.ToString(args.At(0));
            return WptCorpus.Read(WptCorpus.ResolveReference(directory, reference));
        }));

        return engine;
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
