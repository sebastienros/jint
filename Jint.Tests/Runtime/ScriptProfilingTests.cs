#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint.Profiling;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ScriptProfilingTests
{
    private static Engine CreateProfilingEngine() => new(options => options.Profiling.Enabled = true);

    /// <summary>
    /// Replays the event stream the way a consumer does — push on open, pop on close — and fails on anything
    /// that is not a properly nested, balanced tree. Returns one line per open frame, indented by depth, so a
    /// test can pin the call tree's shape as text.
    /// </summary>
    private static string RenderCallTree(ScriptProfile profile)
    {
        var builder = new StringBuilder();
        var open = new Stack<int>();
        long previous = 0;

        foreach (var e in profile.Events)
        {
            e.TimestampNanoseconds.Should().BeGreaterThanOrEqualTo(previous, "profile timestamps must be non-decreasing");
            previous = e.TimestampNanoseconds;

            e.FrameIndex.Should().BeInRange(0, profile.Frames.Count - 1);

            if (e.Kind == ScriptProfileEventKind.Open)
            {
                builder.Append(' ', open.Count * 2).Append(profile.Frames[e.FrameIndex].Name).Append('\n');
                open.Push(e.FrameIndex);
            }
            else
            {
                open.Should().NotBeEmpty("a close event must have a matching open");
                open.Pop().Should().Be(e.FrameIndex, "closes must match opens in reverse order");
            }
        }

        open.Should().BeEmpty("every open frame must be closed by the end of the profile");
        return builder.ToString();
    }

    private static int MaxDepth(ScriptProfile profile)
    {
        var depth = 0;
        var max = 0;
        foreach (var e in profile.Events)
        {
            if (e.Kind == ScriptProfileEventKind.Open)
            {
                max = System.Math.Max(max, ++depth);
            }
            else
            {
                depth--;
            }
        }

        return max;
    }

    /// <summary>
    /// The nesting depth at which <paramref name="name"/> is first entered, or -1 if it never is.
    /// </summary>
    private static int DepthOfFirstOpen(ScriptProfile profile, string name)
    {
        var depth = 0;
        foreach (var e in profile.Events)
        {
            if (e.Kind == ScriptProfileEventKind.Close)
            {
                depth--;
                continue;
            }

            if (profile.Frames[e.FrameIndex].Name == name)
            {
                return depth;
            }

            depth++;
        }

        return -1;
    }

    private static IEnumerable<string> OpenedNames(ScriptProfile profile) =>
        profile.Events
            .Where(static e => e.Kind == ScriptProfileEventKind.Open)
            .Select(e => profile.Frames[e.FrameIndex].Name);

    [Fact]
    public void StartProfilingOnADisabledEngineThrowsAndNamesTheOption()
    {
        var engine = new Engine();

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Advanced.StartProfiling());

        exception.Message.Should().Contain("Options.Profiling.Enabled");
        engine.Advanced.IsProfiling.Should().BeFalse();
    }

    [Fact]
    public void ADisabledEngineStillRunsAndRecordsNothing()
    {
        var engine = new Engine();
        engine.Execute("function f() { return 1; } f(); f();");

        engine.Advanced.IsProfiling.Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => engine.Advanced.StopProfiling());
    }

    [Fact]
    public void StopProfilingWithoutStartingThrows()
    {
        var engine = CreateProfilingEngine();

        Assert.Throws<InvalidOperationException>(() => engine.Advanced.StopProfiling());
    }

    [Fact]
    public void StartProfilingTwiceThrows()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();

        Assert.Throws<InvalidOperationException>(() => engine.Advanced.StartProfiling());

        engine.Advanced.StopProfiling();
    }

    [Fact]
    public void AnEnabledButUnstartedEngineRecordsNothing()
    {
        var engine = CreateProfilingEngine();
        engine.Execute("function f() { return 1; } f(); f();");

        engine.Advanced.IsProfiling.Should().BeFalse();

        engine.Advanced.StartProfiling();
        var profile = engine.Advanced.StopProfiling();

        profile.Events.Should().BeEmpty("nothing ran between start and stop");
        profile.Frames.Should().BeEmpty();
        profile.Truncated.Should().BeFalse();
    }

    [Fact]
    public void EveryEnterIsMatchedByAnExit()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("function inner() { return 1; } function outer() { return inner() + inner(); } outer();");
        var profile = engine.Advanced.StopProfiling();

        profile.Events.Should().HaveCount(6);
        RenderCallTree(profile).Should().Be("outer\n  inner\n  inner\n");
    }

    [Fact]
    public void NestedCallTreeShapeIsReconstructable()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("""
            function leaf() { return 1; }
            function middle() { return leaf(); }
            function root() { return middle() + leaf(); }
            root();
            """);
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile).Should().Be("root\n  middle\n    leaf\n  leaf\n");
    }

    [Fact]
    public void AThrowCaughtInScriptStillClosesEveryFrame()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("""
            function thrower() { throw new Error('boom'); }
            function middle() { thrower(); }
            function root() { try { middle(); } catch (e) { return 'caught'; } }
            root();
            """);
        var profile = engine.Advanced.StopProfiling();

        // The tree is what a consumer sees; the assertion that matters is that RenderCallTree can build one
        // at all, since it fails on any unbalanced or improperly nested pair.
        RenderCallTree(profile).Should().StartWith("root\n  middle\n    thrower\n");
        OpenedNames(profile).Should().Contain("thrower");
    }

    [Fact]
    public void AThrowEscapingToTheHostStillClosesEveryFrame()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();

        Assert.Throws<JavaScriptException>(() => engine.Execute("""
            function thrower() { throw new Error('boom'); }
            function middle() { thrower(); }
            function root() { middle(); }
            root();
            """));

        // An escaping throw unwinds through the same finallys a return does, so every frame is popped on the
        // way out and the profiler is back at depth zero for whatever the host runs next.
        engine.Execute("function after() { return 1; } after();");
        var profile = engine.Advanced.StopProfiling();

        // (The tail of the abandoned tree is the Error constructor and the `stack` accessors the unwinding
        // reads, so only its head is pinned.)
        RenderCallTree(profile).Should().StartWith("root\n  middle\n    thrower\n");
        DepthOfFirstOpen(profile, "after").Should().Be(0, "the abandoned frames were closed, not left open");
    }

    [Fact]
    public void ResetCallStackClosesEveryOpenFrame()
    {
        var engine = CreateProfilingEngine();
        engine.SetValue("reset", new Action(() => engine.Advanced.ResetCallStack()));

        engine.Advanced.StartProfiling();
        engine.Execute("""
            function inner() { reset(); }
            function outer() { inner(); }
            function after() { return 1; }
            outer();
            after();
            """);
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile).Should().StartWith("outer\n  inner\n");
        DepthOfFirstOpen(profile, "after").Should().Be(0, "the abandoned frames were closed, not left open");
    }

    [Fact]
    public void RecursionIsRecordedAsNestedActivationsOfOneFrame()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("function down(n) { return n === 0 ? 0 : down(n - 1); } down(10);");
        var profile = engine.Advanced.StopProfiling();

        // Non-strict, so no proper tail call: eleven activations of one function, properly nested.
        RenderCallTree(profile);
        profile.Frames.Should().ContainSingle(f => f.Name == "down");
        MaxDepth(profile).Should().Be(11);
        profile.Events.Should().HaveCount(22);
    }

    /// <summary>
    /// A proper tail call replaces the caller's frame rather than nesting inside it, so the profile shows a
    /// flat run of sibling activations at constant depth instead of a tree that grows with the recursion.
    /// This is the shape the call stack itself has — <c>JintCallStack.ReplaceTop</c> — and pinning it here
    /// is what keeps a future change to tail-call bookkeeping from silently turning a bounded profile into
    /// an unbounded one.
    /// </summary>
    [Fact]
    public void AProperTailCallIsRecordedAsACloseFollowedByAnOpen()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("""
            'use strict';
            function down(n) { return n === 0 ? 0 : down(n - 1); }
            down(50);
            """);
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile);
        profile.Frames.Should().ContainSingle(f => f.Name == "down");

        MaxDepth(profile).Should().Be(1, "a tail call displaces its caller's frame instead of nesting inside it");
        profile.Events.Should().HaveCount(102, "51 activations, each one open and one close");
    }

    [Fact]
    public void ClosuresOfOneSourceFunctionShareAFrame()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("""
            function make(n) { return function adder(x) { return x + n; }; }
            var a = make(1), b = make(2);
            a(1); b(2); a(3);
            """);
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile);
        profile.Frames.Where(f => f.Name == "adder").Should().HaveCount(1, "closures of one source function are one frame");
        OpenedNames(profile).Count(static n => n == "adder").Should().Be(3);
    }

    [Fact]
    public void FramesCarryTheDeclarationName_And_SourceLocation()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("function named() { return 1; }\nnamed();", "profiled.js");
        var profile = engine.Advanced.StopProfiling();

        var frame = profile.Frames.Single(f => f.Name == "named");
        frame.File.Should().Be("profiled.js");
        frame.Line.Should().Be(1);
        frame.Column.Should().Be(1, "columns are reported one-based, as in a stack trace");
    }

    [Fact]
    public void AnInferredNameIsPreferredToAnonymity()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("var inferred = function () { return 1; }; inferred();");
        var profile = engine.Advanced.StopProfiling();

        OpenedNames(profile).Should().Contain("inferred");
    }

    [Fact]
    public void AFunctionWithNoNameAtAllIsAnonymous()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("(function () { return 1; })();", "anon.js");
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile);
        var frame = profile.Frames.Single();
        frame.Name.Should().Be("<anonymous>");
        frame.File.Should().Be("anon.js", "an unnamed function still has a source position");
        frame.Line.Should().Be(1);
        frame.Column.Should().Be(2);
    }

    [Fact]
    public void AGetterIsProfiledUnderTheAccessorName()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("var o = { get p() { return 1; } }; function g() { return o.p; } g();");
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile).Should().Be("g\n  get p\n");
    }

    [Fact]
    public void HostCallablesAreProfiledButCarryNoSourceLocation()
    {
        var engine = CreateProfilingEngine();
        engine.SetValue("hostCall", new Func<int>(static () => 42));

        engine.Advanced.StartProfiling();
        engine.Execute("function caller() { return hostCall(); } caller();");
        var profile = engine.Advanced.StopProfiling();

        RenderCallTree(profile);
        profile.Frames.Should().HaveCount(2);

        var frame = profile.Frames.Single(f => f.Name != "caller");
        frame.File.Should().BeNull("a CLR callable was never parsed from anywhere");
        frame.Line.Should().BeNull();
        frame.Column.Should().BeNull();
    }

    /// <summary>
    /// The three documented elisions, pinned together so widening any of them is a deliberate act rather
    /// than an accident. All three share one property that keeps a profile coherent rather than merely
    /// incomplete: the eliding call never gets a frame either, so anything the elided function calls is
    /// recorded at the depth the call stack really has, never at a wrong one.
    /// </summary>
    public class Elisions
    {
        [Fact]
        public void AFramelessLeafBuiltInDisappearsOnceItsCallSiteIsWarm()
        {
            var engine = CreateProfilingEngine();
            engine.Advanced.StartProfiling();
            engine.Execute("function f(x) { return Math.abs(x); } for (var i = 0; i < 5; i++) { f(-1); }");
            var profile = engine.Advanced.StopProfiling();

            RenderCallTree(profile);

            // The first dispatch has no fast-call shape cached yet and so keeps its frame; from the second
            // on the site takes the leaf lane, which pushes nothing at all.
            OpenedNames(profile).Count(static n => n == "f").Should().Be(5);
            OpenedNames(profile).Count(static n => n == "abs").Should().Be(1);
        }

        [Fact]
        public void ACallbackABuiltInInvokesHasNoFrameButItsCalleesDo()
        {
            var engine = CreateProfilingEngine();
            engine.Advanced.StartProfiling();
            engine.Execute("function helper(v) { return v; } [1, 2].map(function (x) { return helper(x); });");
            var profile = engine.Advanced.StopProfiling();

            // `map` is framed and the callback is not, so `helper` lands directly under `map` — one level
            // short of the truth, never at the wrong depth.
            RenderCallTree(profile).Should().Be("map\n  helper\n  helper\n");
        }

        [Fact]
        public void APromiseReactionHandlerHasNoFrameButItsCalleesDo()
        {
            var engine = CreateProfilingEngine();
            engine.Advanced.StartProfiling();
            engine.Execute("""
                function helper(v) { return v; }
                function reaction(v) { return helper(v); }
                Promise.resolve(1).then(reaction);
                """);
            var profile = engine.Advanced.StopProfiling();

            RenderCallTree(profile);

            // The reaction job invokes the handler directly, so `reaction` never gets a frame; `helper` runs
            // with an empty call stack and is therefore recorded as a root.
            OpenedNames(profile).Should().NotContain("reaction");
            DepthOfFirstOpen(profile, "helper").Should().Be(0, "the reaction job runs with an empty call stack");
        }
    }

    [Fact]
    public void TruncationStopsRecordingAndFlagsTheProfile()
    {
        var engine = new Engine(options =>
        {
            options.Profiling.Enabled = true;
            options.Profiling.MaxEvents = 20;
        });

        engine.Advanced.StartProfiling();
        engine.Execute("function f() { return 1; } for (var i = 0; i < 1000; i++) { f(); }");
        var profile = engine.Advanced.StopProfiling();

        profile.Truncated.Should().BeTrue();
        profile.Events.Count.Should().BeLessThanOrEqualTo(20);
        profile.Events.Should().NotBeEmpty();

        // Truncating must not leave a dangling open frame: the stream stays replayable.
        RenderCallTree(profile);
    }

    [Fact]
    public void TruncationInsideADeepStackClosesEveryOpenFrame()
    {
        var engine = new Engine(options =>
        {
            options.Profiling.Enabled = true;
            options.Profiling.MaxEvents = 12;
        });

        engine.Advanced.StartProfiling();
        engine.Execute("function down(n) { return n === 0 ? 0 : down(n - 1); } down(50);");
        var profile = engine.Advanced.StopProfiling();

        profile.Truncated.Should().BeTrue();
        profile.Events.Count.Should().BeLessThanOrEqualTo(12);
        RenderCallTree(profile);
    }

    [Fact]
    public void AProfileStoppedMidCallClosesWhatIsStillOpen()
    {
        var engine = CreateProfilingEngine();
        ScriptProfile? captured = null;
        engine.SetValue("stop", new Action(() => captured = engine.Advanced.StopProfiling()));

        engine.Advanced.StartProfiling();
        engine.Execute("function inner() { stop(); } function outer() { inner(); } outer();");

        captured.Should().NotBeNull();
        RenderCallTree(captured!).Should().StartWith("outer\n  inner\n");
        engine.Advanced.IsProfiling.Should().BeFalse();
    }

    [Fact]
    public void AProfileStartedMidCallDropsTheExitsItNeverSaw()
    {
        var engine = CreateProfilingEngine();
        engine.SetValue("start", new Action(() => engine.Advanced.StartProfiling()));

        engine.Execute("""
            function inner() { start(); }
            function outer() { inner(); return 1; }
            outer();
            """);
        var profile = engine.Advanced.StopProfiling();

        // outer/inner/start were already on the stack when the session opened, so their pops have nothing to
        // match and are dropped rather than corrupting the stream.
        RenderCallTree(profile);
        profile.Events.Should().BeEmpty();
    }

    [Fact]
    public void ProfilingIsPerEngine()
    {
        var options = new Options();
        options.Profiling.Enabled = true;

        var profiled = new Engine(options);
        var unprofiled = new Engine(options);

        profiled.Advanced.StartProfiling();
        unprofiled.Execute("function other() { return 1; } other();");
        profiled.Execute("function mine() { return 1; } mine();");
        var profile = profiled.Advanced.StopProfiling();

        unprofiled.Advanced.IsProfiling.Should().BeFalse();
        OpenedNames(profile).Should().Equal("mine");
    }

    [Fact]
    public void NonPositiveMaxEventsIsRejected()
    {
        var options = new Options();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Profiling.MaxEvents = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Profiling.MaxEvents = -1);
        options.Profiling.MaxEvents.Should().Be(Options.ProfilingOptions.DefaultMaxEvents);
    }

    [Fact]
    public void DurationCoversTheWholeSession()
    {
        var engine = CreateProfilingEngine();
        engine.Advanced.StartProfiling();
        engine.Execute("function f() { return 1; } for (var i = 0; i < 100; i++) { f(); }");
        var profile = engine.Advanced.StopProfiling();

        // Structural, not timed: the session cannot be shorter than its own last event, and its TimeSpan
        // projection must agree with the nanosecond figure the speedscope export uses.
        profile.DurationNanoseconds.Should().BeGreaterThanOrEqualTo(profile.Events[^1].TimestampNanoseconds);
        profile.Duration.Ticks.Should().Be(profile.DurationNanoseconds / 100);
    }

    [Fact]
    public void ASecondSessionOnTheSameEngineStartsClean()
    {
        var engine = CreateProfilingEngine();
        engine.Execute("function first() { return 1; } function second() { return 2; }");

        engine.Advanced.StartProfiling();
        engine.Execute("first();");
        var one = engine.Advanced.StopProfiling();

        engine.Advanced.StartProfiling();
        engine.Execute("second();");
        var two = engine.Advanced.StopProfiling();

        OpenedNames(one).Should().Equal("first");
        OpenedNames(two).Should().Equal("second");
    }

}
