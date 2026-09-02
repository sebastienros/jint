using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Profiler;
using Jint.Runtime.Coverage;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The coverage half of the <c>Profiler</c> domain: which of an engine's code ran, in the protocol's
/// counted-range shape.
/// </summary>
/// <remarks>
/// The property this suite exists for is the one the engine does not give directly. Coverage is a
/// <i>covered set</i> — a construct that never ran has no entry — so the interesting assertion is not that a
/// function that ran is reported, but that a function that did not is reported with <c>count: 0</c>, because
/// that is the whole of what a front end's Coverage panel draws.
/// </remarks>
[NonParallelizable]
public class ProfilerCoverageTests
{
    private const string Source = """
        function used(n) {
            return n + 1;
        }

        function unused(n) {
            return n - 1;
        }

        var total = used(1);
        """;

    [Test]
    public async Task AFunctionThatRanIsCountedAndOneThatDidNotIsReportedAsZero()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Profiler.startPreciseCoverage", """{"callCount":true,"detailed":false}""");
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var scripts = await TakeAsync(session);
        var script = scripts.Single(candidate => candidate.Url == "main.js");

        var functions = script.Functions.ToDictionary(function => function.FunctionName, function => function);
        functions.Should().ContainKeys("used", "unused");

        functions["used"].Ranges.Single().Count.Should().Be(1);
        functions["unused"].Ranges.Single().Count.Should().Be(0, "it is declared and never called, which is what the panel shades");
        functions["unused"].IsBlockCoverage.Should().BeFalse();

        // The script itself is the first, unnamed function, the way V8 reports it.
        script.Functions[0].FunctionName.Should().BeEmpty();
        script.Functions[0].Ranges[0].StartOffset.Should().Be(0);
    }

    /// <summary>A range is an offset into the source, and the source is what it says it is.</summary>
    [Test]
    public async Task ARangeIsTheOffsetTheEngineCounted()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Profiler.startPreciseCoverage");
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var report = await session.Target.PostAsync(engine => engine.Diagnostics.GetCoverage());
        var scripts = await TakeAsync(session);
        var functions = scripts.Single(candidate => candidate.Url == "main.js").Functions;

        var counted = report.Sources
            .Single(source => source.Name == "main.js")
            .Entries
            .Where(entry => entry.Kind == CoverageEntryKind.Function)
            .ToDictionary(entry => entry.Start.Index, entry => entry.HitCount);

        foreach (var function in functions.Where(function => function.FunctionName.Length > 0))
        {
            var range = function.Ranges[0];

            // The offsets slice the function's own body out of the source, whether or not it ran.
            Source.Substring(range.StartOffset, range.EndOffset - range.StartOffset).Should().StartWith("{").And.EndWith("}");

            if (range.Count > 0)
            {
                counted.Should().ContainKey(range.StartOffset, "'{0}' is reported at the offset the engine counted it at", function.FunctionName);
                range.Count.Should().Be((int) counted[range.StartOffset]);
            }
            else
            {
                // The zero comes from the abstract syntax tree, not from the engine: a construct that never
                // ran has no entry at all, which is the gap this domain closes.
                counted.Should().NotContainKey(range.StartOffset, "'{0}' never ran, so the engine counted nothing there", function.FunctionName);
            }
        }
    }

    [Test]
    public async Task DetailedCoverageAddsTheStatementsInsideEachFunction()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Profiler.startPreciseCoverage", """{"callCount":true,"detailed":true}""");
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var scripts = await TakeAsync(session);
        var used = scripts.Single(candidate => candidate.Url == "main.js").Functions.Single(function => function.FunctionName == "used");

        used.IsBlockCoverage.Should().BeTrue();
        used.Ranges.Length.Should().BeGreaterThan(1, "the `return` inside it ran, and has a range of its own");

        foreach (var range in used.Ranges.Skip(1))
        {
            range.StartOffset.Should().BeGreaterThanOrEqualTo(used.Ranges[0].StartOffset);
            range.EndOffset.Should().BeLessThanOrEqualTo(used.Ranges[0].EndOffset);
        }
    }

    /// <summary>Taking resets, which is what makes successive takes incremental rather than cumulative.</summary>
    [Test]
    public async Task TakingResetsAndReadingBestEffortDoesNot()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Profiler.startPreciseCoverage");
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        Counted(await TakeAsync(session), "used").Should().Be(1);
        Counted(await TakeAsync(session), "used").Should().Be(0, "nothing ran between the two takes");

        await session.Target.PostAsync(engine => engine.Evaluate("used(2)"));

        var best = await BestEffortAsync(session);
        Counted(best, "used").Should().Be(1);

        // Reading it again answers the same thing, because a best-effort read takes nothing away.
        Counted(await BestEffortAsync(session), "used").Should().Be(1);
    }

    [Test]
    public async Task TakingWithoutStartingIsRefused()
    {
        await using var session = await CreateAsync();

        var error = await session.ErrorAsync("Profiler.takePreciseCoverage");
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Precise coverage has not been started.");
    }

    /// <summary>
    /// Coverage is decided when the engine is built, so an engine built without it is told so by name rather
    /// than answered with an empty report that reads as a script that never ran.
    /// </summary>
    [Test]
    public async Task AnEngineWithoutCoverageIsRefusedByName()
    {
        await using var session = await AttachedSession.CreateAsync();

        foreach (var method in new[] { "Profiler.startPreciseCoverage", "Profiler.takePreciseCoverage", "Profiler.getBestEffortCoverage" })
        {
            var error = await session.ErrorAsync(method);
            error.GetProperty("code").GetInt32().Should().Be(-32000);
            error.GetProperty("message").GetString().Should().Be("The engine was not built with coverage enabled");
            error.GetProperty("data").GetString().Should().Contain("devTools.Coverage = true");
        }
    }

    /// <summary>Stopping the measurement leaves the engine's own counters where they are.</summary>
    [Test]
    public async Task StoppingLeavesTheEnginesCountersAlone()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Profiler.startPreciseCoverage");
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        await session.ResultAsync("Profiler.stopPreciseCoverage");

        var report = await session.Target.PostAsync(engine => engine.Diagnostics.GetCoverage());
        report.Sources.Should().NotBeEmpty("stopping a measurement is not clearing what the host was collecting");
    }

    private static async Task<AttachedSession> CreateAsync()
        => await AttachedSession.CreateAsync(configureOptions: options => options.Coverage.Enabled = true).ConfigureAwait(false);

    private static int Counted(ScriptCoverage[] scripts, string functionName)
    {
        var script = scripts.SingleOrDefault(candidate => candidate.Url == "main.js");
        var function = script?.Functions.SingleOrDefault(candidate => candidate.FunctionName == functionName);
        return function?.Ranges[0].Count ?? 0;
    }

    private static async Task<ScriptCoverage[]> TakeAsync(AttachedSession session)
    {
        var result = await session.ResultAsync("Profiler.takePreciseCoverage").ConfigureAwait(false);
        result.GetProperty("timestamp").GetDouble().Should().BeGreaterThan(0);
        return Read(result);
    }

    private static async Task<ScriptCoverage[]> BestEffortAsync(AttachedSession session)
        => Read(await session.ResultAsync("Profiler.getBestEffortCoverage").ConfigureAwait(false));

    /// <summary>
    /// Reads a coverage reply back through the generated data transfer objects, so that a reply a client
    /// would reject fails here.
    /// </summary>
    private static ScriptCoverage[] Read(JsonElement result)
    {
        var scripts = result.GetProperty("result").Deserialize(ProtocolJsonContext.Default.ProfilerScriptCoverageArray);
        scripts.Should().NotBeNull();
        return scripts!;
    }
}
