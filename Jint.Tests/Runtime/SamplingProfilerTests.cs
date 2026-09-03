#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jint.Native;
using Jint.Profiling;
using Jint.Runtime;
using Jint.Runtime.Interop;

#pragma warning disable JINT0002 // the sampling profiler is a preview area

namespace Jint.Tests.Runtime;

/// <summary>
/// The sampling profiler, read back through the document it writes. Every assertion here goes through
/// <see cref="SampledProfile.WriteTo(TextWriter)"/> and a JSON parse rather than through an in-memory model,
/// because the document <em>is</em> the deliverable: a profile that is right in memory and wrong on disk is
/// wrong.
/// </summary>
public class SamplingProfilerTests
{
    /// <summary>
    /// Three nested functions and one hot loop, with the loop in the innermost. Whatever the machine, the
    /// overwhelming majority of the run is inside <c>innermost</c>'s loop, so it must dominate self time
    /// while <c>middle</c> and <c>outermost</c> hold nearly all of it as inclusive time.
    /// </summary>
    private const string NestedHotLoop = """
        function innermost(n) {
            var sum = 0;
            for (var i = 0; i < n; i++) {
                sum += i % 7;
            }
            return sum;
        }

        function middle(n) {
            return innermost(n);
        }

        function outermost(n) {
            return middle(n);
        }

        var total = 0;
        for (var round = 0; round < 40; round++) {
            total += outermost(500);
        }
        total;
        """;

    private static Engine CreateProfilingEngine() => new(options => options.Profiling.Enabled = true);

    /// <summary>
    /// Runs <paramref name="script"/> under a session that samples at every check point, which is what makes
    /// the sample count a function of the work done rather than of how fast the machine is.
    /// </summary>
    private static SampledProfile Profile(Engine engine, string script, string source = "profiled.js")
    {
        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute(script, source);
        return engine.Diagnostics.StopSampling();
    }

    private static ParsedProfile Parse(SampledProfile profile)
    {
        using var text = new StringWriter();
        profile.WriteTo(text);
        return new ParsedProfile(text.ToString());
    }

    [Test]
    public void ADefaultEngineIsNotSamplingAndRefusesTo()
    {
        var engine = new Engine();

        engine.Diagnostics.IsSampling.Should().BeFalse();

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Diagnostics.StartSampling())!;
        exception.Message.Should().Contain("Options.Profiling.Enabled");
    }

    [Test]
    public void ASessionIsOpenedOnceAndClosedOnce()
    {
        var engine = CreateProfilingEngine();

        Assert.Throws<InvalidOperationException>(() => engine.Diagnostics.StopSampling());

        engine.Diagnostics.StartSampling();
        engine.Diagnostics.IsSampling.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => engine.Diagnostics.StartSampling());

        engine.Diagnostics.StopSampling();
        engine.Diagnostics.IsSampling.Should().BeFalse();
    }

    [Test]
    public void TheHotFunctionDominatesSelfTime()
    {
        var engine = CreateProfilingEngine();
        var parsed = Parse(Profile(engine, NestedHotLoop));

        parsed.SampleCount.Should().BeGreaterThan(10);

        var self = parsed.SelfWeightByFunction();
        var inclusive = parsed.InclusiveWeightByFunction();
        var total = self.Values.Sum();

        self.Should().ContainKey("innermost");
        // A wall-clock sampler over-attributes to whichever frame is on top when a descheduled thread is
        // sampled, so on a loaded CI runner this share has read 0.881, 0.842 and 0.895 with no profiler change
        // in the pull request; 0.8 still says the loop dominates, which is what the test is about.
        (self["innermost"] / total).Should().BeGreaterThan(0.8, "the loop is the only work the script does");

        // middle and outermost do nothing but delegate, so they own almost no self time and almost all of
        // the inclusive time - which is the shape a call tree has to show for a profile to be worth reading.
        Weight(self, "middle").Should().BeLessThan(self["innermost"]);
        // The inclusive shares drift the same way the self share does on a loaded runner (0.885 seen on a
        // docs-only pull request); 0.8 still says every frame above the loop carries it.
        (inclusive["outermost"] / total).Should().BeGreaterThan(0.8);
        (inclusive["middle"] / total).Should().BeGreaterThan(0.8);
        inclusive["(program)"].Should().BeApproximately(total, 1e-9, "every sample is rooted at the program");
    }

    [Test]
    public void ASampledFrameCarriesTheSourceItCameFrom()
    {
        var engine = CreateProfilingEngine();
        var parsed = Parse(Profile(engine, NestedHotLoop, "nested.js"));

        var innermost = parsed.Function("innermost");
        innermost.FileName.Should().Be("nested.js");
        innermost.LineNumber.Should().Be(1, "innermost is declared on the first line");
        innermost.ColumnNumber.Should().Be(1);

        // The frames of one function are the positions inside it that were caught executing. For innermost
        // that is somewhere in its own seven lines, and overwhelmingly the loop.
        parsed.FrameLines("innermost").Should().NotBeEmpty().And.OnlyContain(line => line >= 1 && line <= 7);
        parsed.DominantSelfLine("innermost").Should().BeInRange(3, 4, "the loop is on line 3 and its body on line 4");

        // middle and outermost do nothing but delegate, so they are only ever caught either at their own
        // entry or at the call they are waiting on - and it is the call that dominates.
        parsed.FrameLines("middle").Should().OnlyContain(line => line == 9 || line == 10);
        parsed.FrameLines("outermost").Should().OnlyContain(line => line == 13 || line == 14);
        parsed.FrameLines("(program)").Should().OnlyContain(line => line >= 17 && line <= 21);
    }

    [Test]
    public void ScriptBuiltInAndHostFramesAreDifferentCategories()
    {
        var engine = CreateProfilingEngine();

        // The embedder's own shape: a host callable that drives a script callback. Its frame is on the
        // stack while the callback runs, so the profile can say how much of the run was inside the host's
        // code and how much was inside the script's - which is the split a native profiler cannot make,
        // because at the CLR level all three are Jint frames.
        engine.SetValue("hostBridge", new ClrFunction(engine, "hostBridge", (_, arguments) =>
        {
            var callback = arguments.At(0);
            for (var i = 0; i < 40; i++)
            {
                engine.Call(callback, JsNumber.Create(i));
            }

            return JsValue.Undefined;
        }));

        // A CLR delegate wrapped for script takes the other route in, and is sampled at the boundary check
        // the wrapper makes on the way out.
        engine.SetValue("hostDelegate", new Func<int, int>(Burn));

        var parsed = Parse(Profile(engine, """
            function scriptWork(n) {
                var sum = 0;
                for (var i = 0; i < n; i++) { sum += i % 7; }
                return sum;
            }

            hostBridge(function fromHost(i) { return scriptWork(400); });

            for (var i = 0; i < 40; i++) {
                hostDelegate(400);
                [3, 1, 2].sort(function (a, b) { return scriptWork(100) % 3 + a - b; });
            }
            """));

        parsed.Category("(program)").Should().Be(ProfileFrameCategory.Script);
        parsed.Category("scriptWork").Should().Be(ProfileFrameCategory.Script);
        parsed.Category("fromHost").Should().Be(ProfileFrameCategory.Script);
        parsed.Category("hostBridge").Should().Be(ProfileFrameCategory.HostInterop, "the delegate behind that ClrFunction is the host's code");
        parsed.Category("delegate").Should().Be(ProfileFrameCategory.HostInterop, "a wrapped CLR delegate is the host's code");
        parsed.Category("sort").Should().Be(ProfileFrameCategory.BuiltIn);

        // The host's frame holds the callback's time as inclusive time and almost none of it as self time,
        // which is what tells an embedder the bridge itself is not the problem.
        var inclusive = parsed.InclusiveWeightByFunction();
        var self = parsed.SelfWeightByFunction();
        inclusive["hostBridge"].Should().BeGreaterThan(0);
        inclusive["hostBridge"].Should().BeGreaterThan(Weight(self, "hostBridge"));

        // isJS / relevantForJS follow the category: a built-in and a host callable did not come from the
        // script, and both belong in a JavaScript-only view of the stack anyway.
        parsed.IsJs("scriptWork").Should().BeTrue();
        parsed.IsJs("hostBridge").Should().BeFalse();
        parsed.RelevantForJs("hostBridge").Should().BeTrue();
        parsed.RelevantForJs("sort").Should().BeTrue();
    }

    [Test]
    public void TheTightLoopLaneIsStillSampled()
    {
        // A for loop with an expression-statement body and no exact constraint runs the interpreter's tight
        // lane, which skips the per-statement checks. Arming the sampler must not disarm that lane - and the
        // lane must still drive the cadence, or a profile of the one shape worth profiling would be empty.
        var engine = CreateProfilingEngine();
        var parsed = Parse(Profile(engine, """
            function spin(n) {
                var sum = 0;
                for (var i = 0; i < n; i++) sum += i % 7;
                return sum;
            }
            spin(200000);
            """));

        parsed.SampleCount.Should().BeGreaterThan(10);
        parsed.SelfWeightByFunction().Should().ContainKey("spin");
    }

    [Test]
    public void SamplingIsRefusedPastTheCapAndTheRefusalsAreCounted()
    {
        var engine = CreateProfilingEngine();
        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero, MaxSamples = 5 });
        engine.Execute(NestedHotLoop);
        var profile = engine.Diagnostics.StopSampling();

        profile.SampleCount.Should().Be(5);
        profile.DroppedSampleCount.Should().BeGreaterThan(0);

        var parsed = Parse(profile);
        parsed.SampleCount.Should().Be(5);
    }

    [Test]
    public void AProfileOfAnEngineThatRanNothingIsStillAValidDocument()
    {
        var engine = CreateProfilingEngine();
        engine.Diagnostics.StartSampling();
        var parsed = Parse(engine.Diagnostics.StopSampling());

        parsed.SampleCount.Should().Be(0);
        parsed.PreprocessedProfileVersion.Should().Be(51);
    }

    [Test]
    public void TheDocumentIsInternallyConsistent()
    {
        var engine = CreateProfilingEngine();
        var parsed = Parse(Profile(engine, NestedHotLoop));

        parsed.AssertWellFormed();
    }

    [Test]
    public void AProfileRetainsNothingOfTheEngineThatProducedIt()
    {
        var engine = CreateProfilingEngine();
        var profile = Profile(engine, NestedHotLoop);

        // Two writes of the same profile produce the same document, which is only possible if the profile
        // holds its own data rather than reaching back into an engine that has moved on.
        engine.Execute("var x = 1;");
        using var first = new StringWriter();
        profile.WriteTo(first);
        using var second = new StringWriter();
        profile.WriteTo(second);

        second.ToString().Should().Be(first.ToString());
    }

    [Test]
    public void ASessionSurvivesAThrowAndKeepsSampling()
    {
        var engine = CreateProfilingEngine();
        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });

        Assert.Throws<JavaScriptException>(() => engine.Execute("""
            function boom(n) {
                var sum = 0;
                for (var i = 0; i < n; i++) { sum += i % 7; }
                throw new Error("boom");
            }
            boom(2000);
            """));

        engine.Execute(NestedHotLoop);
        var parsed = Parse(engine.Diagnostics.StopSampling());

        var self = parsed.SelfWeightByFunction();
        self.Should().ContainKey("boom");
        self.Should().ContainKey("innermost");
    }

    [Test]
    public void StoppingASessionLeavesTheEnginesOwnConstraintsArmed()
    {
        // One flag on the evaluation context gates both the amortized constraints and a sampling session,
        // so a session that closes must not take a configured timeout down with it — a limit that stops
        // limiting is the one failure this area cannot afford, and it would be silent.
        var engine = new Engine(options =>
        {
            options.Profiling.Enabled = true;
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(500));
        });

        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute("function spin(n) { var s = 0; for (var i = 0; i < n; i++) { s += i % 7; } return s; } spin(2000);");
        engine.Diagnostics.StopSampling().SampleCount.Should().BeGreaterThan(0);

        // The spin ends on its own so that a broken gate fails this test rather than hanging it: with the
        // budget armed it throws well before the four seconds, and without it the script simply returns.
        Assert.Throws<TimeoutException>(() => engine.Execute("""
            var end = Date.now() + 4000;
            while (Date.now() < end) { }
            """));
    }

    [Test]
    public void SamplingOptionsRefuseValuesThatCouldNotWork()
    {
        var options = new SamplingOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Interval = TimeSpan.FromMilliseconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxSamples = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxSamples = -1);

        options.Interval = TimeSpan.Zero;
        options.Interval.Should().Be(TimeSpan.Zero);
        options.MaxSamples.Should().Be(SamplingOptions.DefaultMaxSamples);
    }

    /// <summary>
    /// <c>Dictionary.GetValueOrDefault</c> by hand: this project still targets net472, which does not have it.
    /// </summary>
    private static double Weight<TKey>(Dictionary<TKey, double> totals, TKey key) where TKey : notnull
        => totals.TryGetValue(key, out var weight) ? weight : 0;

    private static int Burn(int n)
    {
        var sum = 0;
        for (var i = 0; i < n; i++)
        {
            sum += i % 7;
        }

        return sum;
    }

    /// <summary>
    /// The written document, read back the way a profile viewer reads it: columnar tables cross-referenced
    /// by integer index.
    /// </summary>
    private sealed class ParsedProfile
    {
        private readonly JsonElement _root;
        private readonly JsonElement _thread;
        private readonly string[] _strings;
        private readonly int[] _sampleStacks;
        private readonly double[] _sampleWeights;
        private readonly int[] _stackFrame;
        private readonly int?[] _stackPrefix;
        private readonly int[] _frameFunc;
        private readonly int?[] _frameLine;
        private readonly int[] _funcName;

        internal ParsedProfile(string json)
        {
            _root = JsonDocument.Parse(json).RootElement.Clone();
            _thread = _root.GetProperty("threads")[0];
            _strings = _thread.GetProperty("stringArray").EnumerateArray().Select(static s => s.GetString()!).ToArray();

            var samples = _thread.GetProperty("samples");
            _sampleStacks = Ints(samples, "stack");
            _sampleWeights = samples.GetProperty("weight").EnumerateArray().Select(static v => v.GetDouble()).ToArray();

            var stackTable = _thread.GetProperty("stackTable");
            _stackFrame = Ints(stackTable, "frame");
            _stackPrefix = NullableInts(stackTable, "prefix");

            var frameTable = _thread.GetProperty("frameTable");
            _frameFunc = Ints(frameTable, "func");
            _frameLine = NullableInts(frameTable, "line");

            _funcName = Ints(_thread.GetProperty("funcTable"), "name");
        }

        internal int SampleCount => _sampleStacks.Length;

        internal int PreprocessedProfileVersion => _root.GetProperty("meta").GetProperty("preprocessedProfileVersion").GetInt32();

        private static int[] Ints(JsonElement table, string column)
            => table.GetProperty(column).EnumerateArray().Select(static v => v.GetInt32()).ToArray();

        private static int?[] NullableInts(JsonElement table, string column)
            => table.GetProperty(column).EnumerateArray()
                .Select(static v => v.ValueKind == JsonValueKind.Null ? (int?) null : v.GetInt32())
                .ToArray();

        private int FunctionIndex(string name)
        {
            for (var i = 0; i < _funcName.Length; i++)
            {
                if (string.Equals(_strings[_funcName[i]], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"The profile has no function named '{name}'. It has: {string.Join(", ", _funcName.Select(n => _strings[n]))}");
        }

        private string NameOfFunction(int func) => _strings[_funcName[func]];

        internal FunctionRow Function(string name)
        {
            var index = FunctionIndex(name);
            var funcTable = _thread.GetProperty("funcTable");
            var fileNames = funcTable.GetProperty("fileName");
            var file = fileNames[index];
            return new FunctionRow(
                file.ValueKind == JsonValueKind.Null ? null : _strings[file.GetInt32()],
                NullableInts(funcTable, "lineNumber")[index],
                NullableInts(funcTable, "columnNumber")[index]);
        }

        internal ProfileFrameCategory Category(string name)
        {
            var index = FunctionIndex(name);
            var categories = Ints(_thread.GetProperty("frameTable"), "category");
            for (var frame = 0; frame < _frameFunc.Length; frame++)
            {
                if (_frameFunc[frame] == index)
                {
                    return (ProfileFrameCategory) categories[frame];
                }
            }

            throw new InvalidOperationException($"No frame refers to function '{name}'.");
        }

        internal bool IsJs(string name)
            => _thread.GetProperty("funcTable").GetProperty("isJS")[FunctionIndex(name)].GetBoolean();

        internal bool RelevantForJs(string name)
            => _thread.GetProperty("funcTable").GetProperty("relevantForJS")[FunctionIndex(name)].GetBoolean();

        /// <summary>
        /// The distinct lines the sampler caught <paramref name="name"/> executing.
        /// </summary>
        internal IReadOnlyList<int> FrameLines(string name)
        {
            var index = FunctionIndex(name);
            var lines = new SortedSet<int>();
            for (var frame = 0; frame < _frameFunc.Length; frame++)
            {
                if (_frameFunc[frame] == index && _frameLine[frame] is { } line)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        /// <summary>
        /// The line <paramref name="name"/> was most often caught executing, weighted like self time.
        /// </summary>
        internal int DominantSelfLine(string name)
        {
            var index = FunctionIndex(name);
            var totals = new Dictionary<int, double>();
            for (var i = 0; i < _sampleStacks.Length; i++)
            {
                var frame = _stackFrame[_sampleStacks[i]];
                if (_frameFunc[frame] == index && _frameLine[frame] is { } line)
                {
                    totals[line] = Weight(totals, line) + _sampleWeights[i];
                }
            }

            totals.Should().NotBeEmpty($"'{name}' must appear as a leaf frame somewhere");
            return totals.OrderByDescending(static pair => pair.Value).First().Key;
        }

        /// <summary>
        /// Self weight per function: each sample's weight charged to the function of its leaf frame.
        /// </summary>
        internal Dictionary<string, double> SelfWeightByFunction()
        {
            var totals = new Dictionary<string, double>(StringComparer.Ordinal);
            for (var i = 0; i < _sampleStacks.Length; i++)
            {
                var name = NameOfFunction(_frameFunc[_stackFrame[_sampleStacks[i]]]);
                totals[name] = Weight(totals, name) + _sampleWeights[i];
            }

            return totals;
        }

        /// <summary>
        /// Inclusive weight per function: each sample's weight charged once to every function on its stack.
        /// </summary>
        internal Dictionary<string, double> InclusiveWeightByFunction()
        {
            var totals = new Dictionary<string, double>(StringComparer.Ordinal);
            for (var i = 0; i < _sampleStacks.Length; i++)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int? node = _sampleStacks[i]; node is { } index; node = _stackPrefix[index])
                {
                    var name = NameOfFunction(_frameFunc[_stackFrame[index]]);
                    if (seen.Add(name))
                    {
                        totals[name] = Weight(totals, name) + _sampleWeights[i];
                    }
                }
            }

            return totals;
        }

        /// <summary>
        /// Every cross-reference in the document resolves, every table's <c>length</c> matches its columns,
        /// and the stack table is a tree whose prefixes point strictly backwards — which is what makes it
        /// safe for a reader to walk without a cycle check.
        /// </summary>
        internal void AssertWellFormed()
        {
            var meta = _root.GetProperty("meta");
            meta.GetProperty("interval").GetDouble().Should().BeGreaterThan(0);
            meta.GetProperty("categories").GetArrayLength().Should().Be(4);
            meta.GetProperty("categories")[0].GetProperty("color").GetString().Should().Be("grey", "index 0 is the format's default category");

            AssertLength(_thread.GetProperty("samples"), _sampleStacks.Length);
            AssertLength(_thread.GetProperty("stackTable"), _stackFrame.Length);
            AssertLength(_thread.GetProperty("frameTable"), _frameFunc.Length);
            AssertLength(_thread.GetProperty("funcTable"), _funcName.Length);

            _sampleWeights.Length.Should().Be(_sampleStacks.Length);

            foreach (var stack in _sampleStacks)
            {
                stack.Should().BeInRange(0, _stackFrame.Length - 1);
            }

            for (var i = 0; i < _stackFrame.Length; i++)
            {
                _stackFrame[i].Should().BeInRange(0, _frameFunc.Length - 1);
                if (_stackPrefix[i] is { } prefix)
                {
                    prefix.Should().BeInRange(0, i - 1, "a stack node's prefix is always an earlier node");
                }
            }

            foreach (var func in _frameFunc)
            {
                func.Should().BeInRange(0, _funcName.Length - 1);
            }

            foreach (var name in _funcName)
            {
                name.Should().BeInRange(0, _strings.Length - 1);
            }
        }

        private static void AssertLength(JsonElement table, int expected)
        {
            table.GetProperty("length").GetInt32().Should().Be(expected);
            foreach (var column in table.EnumerateObject())
            {
                if (column.Value.ValueKind == JsonValueKind.Array)
                {
                    column.Value.GetArrayLength().Should().Be(expected, $"column '{column.Name}' must be as long as the table");
                }
            }
        }
    }

    private readonly record struct FunctionRow(string? FileName, int? LineNumber, int? ColumnNumber);
}
