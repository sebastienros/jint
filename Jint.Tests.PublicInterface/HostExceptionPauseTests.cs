#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Debugger;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>DebugHandler.PauseOnExceptions</c> from the outside: stopping the engine where a throw happens rather
/// than after it has unwound.
/// </summary>
/// <remarks>
/// This is what a tooling protocol answers <c>Debugger.setPauseOnExceptions</c> with, and it is the first of
/// the three engine gaps the two dead community debug adapters named. What a host gets from it that the
/// first-chance <c>ExceptionThrown</c> event never gave: the engine is still standing on the frame that
/// threw, so its scopes and a frame evaluation answer for it.
/// </remarks>
public class HostExceptionPauseTests
{
    private sealed record Pause(PauseType Type, string ThrownValue, bool IsUncaught, string TopFrame);

    private static List<Pause> Run(
        string script,
        ExceptionPauseMode mode,
        Action<Engine>? configure = null,
        Action<Engine, DebugInformation>? onPause = null)
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.PauseOnExceptions = mode;
        configure?.Invoke(engine);

        var pauses = new List<Pause>();
        engine.Debugger.Break += (sender, info) =>
        {
            pauses.Add(new Pause(
                info.PauseType,
                info.ThrownValue is ObjectInstance error ? error.Get("message").ToString() : info.ThrownValue!.ToString(),
                info.IsUncaught,
                info.CallStack[0].FunctionName));
            onPause?.Invoke(engine, info);
            return StepMode.None;
        };

        Caught.Exception(() => engine.Execute(script));
        return pauses;
    }

    /// <summary>
    /// Off by default, and off means the engine behaves exactly as it did before the setting existed.
    /// </summary>
    [Test]
    public void NothingStopsTheEngineUntilAHostAsksForIt()
    {
        new Engine().Debugger.PauseOnExceptions.Should().Be(ExceptionPauseMode.None);

        Run("try { throw new Error('x'); } catch (e) {}\nundefined.foo;", ExceptionPauseMode.None)
            .Should().BeEmpty();
    }

    /// <summary>
    /// The pause is at the throw, before anything unwinds — that is the whole point, and it is what makes the
    /// throwing frame's scopes readable.
    /// </summary>
    [Test]
    public void TheEngineStopsOnTheFrameThatThrew()
    {
        string? scoped = null;

        var pauses = Run("""
            function inner() { const local = 'inner'; throw new Error('boom'); }
            function outer() { inner(); }
            try { outer(); } catch (e) {}
            """,
            ExceptionPauseMode.All,
            onPause: (engine, info) => scoped = engine.Debugger.Evaluate("local").AsString());

        pauses.Should().ContainSingle();
        pauses[0].Type.Should().Be(PauseType.Exception);
        pauses[0].TopFrame.Should().Be("inner");
        pauses[0].ThrownValue.Should().Be("boom");
        scoped.Should().Be("inner", "the frame that threw is still on the stack");
    }

    /// <summary>
    /// One throw, one stop, however many frames it unwinds through. A host that saw the first-chance event
    /// instead would be told once per frame.
    /// </summary>
    [Test]
    public void AThrowThatUnwindsManyFramesStopsTheEngineOnce()
    {
        Run("""
            function a() { throw new Error('deep'); }
            function b() { a(); }
            function c() { b(); }
            try { c(); } catch (e) {}
            """, ExceptionPauseMode.All).Should().ContainSingle();
    }

    /// <summary>
    /// Uncaught means what a user selecting "pause on uncaught exceptions" means by it: no <c>catch</c> clause
    /// is executing anywhere on the stack, in this frame or any calling one.
    /// </summary>
    [Test]
    public void UncaughtIsDecidedByTheWholeStack()
    {
        Run("""
            function thrower() { throw new Error('handled'); }
            try { thrower(); } catch (e) {}
            """, ExceptionPauseMode.Uncaught).Should().BeEmpty();

        var pauses = Run("""
            function thrower() { throw new Error('unhandled'); }
            thrower();
            """, ExceptionPauseMode.Uncaught);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue();
    }

    /// <summary>
    /// A <c>finally</c> runs on the way out; it does not handle anything, so it does not make a throw caught.
    /// </summary>
    [Test]
    public void AFinallyDoesNotMakeAThrowCaught()
    {
        Run("try { throw new Error('x'); } finally { }", ExceptionPauseMode.Uncaught)
            .Should().ContainSingle();
    }

    /// <summary>
    /// An async function's throw becomes a rejection of its own promise, so a <c>try</c> around the call
    /// never sees it. Reporting it as caught would hide exactly the failure the mode exists to catch.
    /// </summary>
    [Test]
    public void AnAsyncFunctionsThrowIsUncaughtDespiteTheCallersTry()
    {
        var pauses = Run("""
            async function run() { throw new Error('rejected'); }
            try { run(); } catch (e) {}
            """, ExceptionPauseMode.Uncaught);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue();

        Run("""
            async function run() { try { throw new Error('handled'); } catch (e) {} }
            run();
            """, ExceptionPauseMode.Uncaught).Should().BeEmpty("the async function catches its own throw");
    }

    /// <summary>
    /// A rejection with no throw behind it is a separate protocol concern and never stops the engine here.
    /// </summary>
    [Test]
    public void APromiseRejectionWithNoThrowDoesNotStopTheEngine()
    {
        Run("Promise.reject(new Error('x')).catch(function () {});", ExceptionPauseMode.All)
            .Should().BeEmpty();
    }

    /// <summary>
    /// The search for a <c>catch</c> stops at a host entry: what the throw actually reaches is the host's own
    /// call, as a <see cref="JavaScriptException"/>, not the script frame that called into the host.
    /// </summary>
    [Test]
    public void AHostEntryEndsTheSearchForACatch()
    {
        var pauses = Run("""
            function callback() { throw new Error('from-callback'); }
            try { reenter(callback); } catch (e) {}
            """,
            ExceptionPauseMode.Uncaught,
            configure: engine => engine.SetValue("reenter", new Action<JsValue>(callback => engine.Invoke(callback))));

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue();
    }

    /// <summary>
    /// The thrown value reaches the host as it is, so a host that renders it does not have to unwrap
    /// anything, and a primitive throw is still a primitive.
    /// </summary>
    [Test]
    public void TheThrownValueIsHandedOverUnwrapped()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.PauseOnExceptions = ExceptionPauseMode.All;

        JsValue? thrown = null;
        engine.Debugger.Break += (sender, info) =>
        {
            thrown = info.ThrownValue;
            return StepMode.None;
        };

        engine.Execute("try { throw 42; } catch (e) {}");

        thrown.Should().NotBeNull();
        thrown!.IsNumber().Should().BeTrue();
        thrown.AsNumber().Should().Be(42);
    }

    /// <summary>
    /// Evaluating during the pause is what a debugger console does, and a failing evaluation must not stop
    /// the engine a second time inside the handler that is already running.
    /// </summary>
    [Test]
    public void AFailedEvaluationDuringThePauseDoesNotStopAgain()
    {
        var pauses = Run(
            "try { throw new Error('x'); } catch (e) {}",
            ExceptionPauseMode.All,
            onPause: (engine, info) =>
                Caught.Exception(() => engine.Debugger.Evaluate("undefinedFunction()"))
                    .Should().BeOfType<DebugEvaluationException>());

        pauses.Should().ContainSingle();
    }

    /// <summary>
    /// Nothing here replaces the first-chance event, which still fires whatever the mode is.
    /// </summary>
    [Test]
    public void TheFirstChanceEventStillFires()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.PauseOnExceptions = ExceptionPauseMode.Uncaught;

        var reported = new List<string>();
        engine.Debugger.ExceptionThrown += (sender, args) => reported.Add(args.ThrownValue.ToString());
        engine.Debugger.Break += (sender, info) => StepMode.None;

        engine.Execute("try { throw 'caught'; } catch (e) {}");

        reported.Should().Contain("caught", "the event reports every throw, caught or not");
    }

    // ---- caught, which is the fourth state a tool offers ----

    /// <summary>
    /// The complement of <see cref="ExceptionPauseMode.Uncaught"/>: a throw something will catch stops the
    /// engine, and one nothing will catch does not.
    /// </summary>
    [Test]
    public void CaughtStopsOnTheThrowsThatUncaughtLetsThrough()
    {
        var pauses = Run("""
            function thrower() { throw new Error('handled'); }
            try { thrower(); } catch (e) {}
            undefined.foo;
            """, ExceptionPauseMode.Caught);

        pauses.Should().ContainSingle("only the throw a catch clause will land in");
        pauses[0].ThrownValue.Should().Be("handled");
        pauses[0].IsUncaught.Should().BeFalse();
    }

    /// <summary>
    /// The four modes partition the throws of one script, which is the property a tool's four-state control
    /// is built on.
    /// </summary>
    [Test]
    public void TheFourModesPartitionTheThrows()
    {
        const string Script = """
            try { throw new Error('caught'); } catch (e) {}
            undefined.foo;
            """;

        Run(Script, ExceptionPauseMode.None).Should().BeEmpty();
        Run(Script, ExceptionPauseMode.Caught).Select(pause => pause.ThrownValue).Should().Equal("caught");
        Run(Script, ExceptionPauseMode.Uncaught).Should().ContainSingle().Which.IsUncaught.Should().BeTrue();
        Run(Script, ExceptionPauseMode.All).Should().HaveCount(2);
    }

    /// <summary>
    /// <see cref="StepMode.Unchanged"/> is what a handler that declines to decide answers, and it is the
    /// only answer that leaves a step in flight alone.
    /// </summary>
    [Test]
    public void UnchangedLeavesAStepInFlightArmed()
    {
        static int StepsAfterTheThrow(StepMode declined)
        {
            var engine = new Engine(options =>
            {
                options.Debugger.Enabled = true;
                options.Debugger.InitialStepMode = StepMode.Into;
            });

            engine.Debugger.PauseOnExceptions = ExceptionPauseMode.All;

            var thrown = false;
            var afterwards = 0;

            engine.Debugger.Break += (sender, info) =>
            {
                // The exception pause, which this handler wants no part of.
                thrown = true;
                return declined;
            };

            engine.Debugger.Step += (sender, info) =>
            {
                if (thrown)
                {
                    afterwards++;
                }

                return StepMode.Into;
            };

            engine.Execute("""
                var before = 1;
                try { throw new Error('x'); } catch (e) {}
                var first = 2;
                var second = 3;
                """);

            thrown.Should().BeTrue("the throw was raised to the handler");
            return afterwards;
        }

        StepsAfterTheThrow(StepMode.Unchanged).Should().BeGreaterThan(0, "the step was still armed");
        StepsAfterTheThrow(StepMode.None).Should().Be(0, "None is a decision, and it cancelled the step");
    }

    /// <summary>
    /// As an engine's initial mode it means <see cref="StepMode.None"/>: there is no mode yet to keep, and
    /// an engine that quietly started stepping would stop on its first statement.
    /// </summary>
    [Test]
    public void UnchangedAsTheInitialModeIsNoStepping()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.InitialStepMode = StepMode.Unchanged;
        });

        var steps = 0;
        engine.Debugger.Step += (sender, info) =>
        {
            steps++;
            return StepMode.Unchanged;
        };

        engine.Execute("var a = 1; var b = 2;");

        steps.Should().Be(0);
    }
}
