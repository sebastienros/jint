#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class ExceptionThrownTests
{
    [Test]
    public void ExplicitThrowFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        Invoking(() => engine.Execute("throw new Error('test error');")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();

        received.Should().NotBeNull();
        received.ThrownValue.Should().BeAssignableTo<ObjectInstance>();
    }

    [Test]
    public void CaughtExceptionStillFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var thrownValues = new List<JsValue>();
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            thrownValues.Add(args.ThrownValue);
        };

        engine.Execute(@"
            try {
                throw new Error('caught error');
            } catch (e) {
                // swallowed
            }
        ");

        thrownValues.Should().ContainSingle();
    }

    [Test]
    public void ImplicitTypeErrorFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        engine.Execute(@"
            try {
                undefined.foo;
            } catch (e) {
            }
        ");

        received.Should().NotBeNull();
    }

    [Test]
    public void ImplicitReferenceErrorFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        engine.Execute(@"
            try {
                undeclaredVariable;
            } catch (e) {
            }
        ");

        received.Should().NotBeNull();
    }

    [Test]
    public void EventNotFiredWhenDebugModeDisabled()
    {
        var engine = new Engine(); // no debug mode

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            count++;
        };

        engine.Execute(@"
            try {
                throw new Error('test');
            } catch (e) {
            }
        ");

        count.Should().Be(0);
    }

    [Test]
    public void RethrowFiresEventTwice()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            count++;
        };

        engine.Execute(@"
            try {
                try {
                    throw new Error('original');
                } catch (e) {
                    throw e;
                }
            } catch (e) {
            }
        ");

        count.Should().Be(2);
    }

    [Test]
    public void AThrowUnwindingSeveralFramesFiresTheEventOnce()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var frames = new List<string>();
        engine.Debugger.ExceptionThrown += (sender, args) => frames.Add(args.CallStack[0].FunctionName);

        engine.Execute("""
            function inner() { throw new Error('boom'); }
            function middle() { inner(); }
            function outer() { middle(); }
            try { outer(); } catch (e) {}
            """);

        // One throw, one event, reported where the throw happened. Every frame the unwind passes through
        // re-raises the same throw as a new JavaScriptException, and none of those is a throw.
        frames.Should().Equal("inner");
    }

    [Test]
    public void AnImplicitErrorUnwindingSeveralFramesFiresTheEventOnce()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) => count++;

        engine.Execute("""
            function inner() { return undefined.foo; }
            function outer() { return inner(); }
            try { outer(); } catch (e) {}
            """);

        count.Should().Be(1);
    }

    [Test]
    public void AThrowLeavingAGeneratorBodyFiresTheEventOnce()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) => count++;

        engine.Execute("""
            function* g() { yield 1; throw new Error('boom'); }
            var it = g();
            it.next();
            try { it.next(); } catch (e) {}
            """);

        count.Should().Be(1);
    }

    [Test]
    public void AThrowLeavingAnEvalBodyFiresTheEventOnce()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) => count++;

        engine.Execute("try { eval(\"throw new Error('boom')\"); } catch (e) {}");

        count.Should().Be(1);
    }

    [Test]
    public void AFreshThrowOfTheSameValueFiresAgain()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var thrown = new List<JsValue>();
        engine.Debugger.ExceptionThrown += (sender, args) => thrown.Add(args.ThrownValue);

        engine.Execute("""
            function inner() { throw new Error('boom'); }
            try {
                try { inner(); } catch (e) { throw e; }
            } catch (e) {}
            """);

        // The re-raise a body boundary performs is not a throw; `throw e` is, even of the very same value.
        thrown.Should().HaveCount(2);
        thrown[0].Should().BeSameAs(thrown[1]);
    }

    [Test]
    public void ThrowPrimitiveValueFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        engine.Execute(@"
            try {
                throw 42;
            } catch (e) {
            }
        ");

        received.Should().NotBeNull();
        received.ThrownValue.AsNumber().Should().Be(42);
    }

    [Test]
    public void CallStackIsAvailable()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        DebugCallStack? callStack = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            callStack = args.CallStack;
        };

        engine.Execute(@"
            function inner() { throw new Error('deep'); }
            function outer() { inner(); }
            try {
                outer();
            } catch (e) {
            }
        ");

        callStack.Should().NotBeNull();
        callStack.Count.Should().BeGreaterThanOrEqualTo(1, $"Expected call stack frames, got {callStack.Count}");
    }

    [Test]
    public void LocationIsAvailable()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        SourceLocation? location = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            location = args.Location;
        };

        engine.Execute(@"
            try {
                throw new Error('located');
            } catch (e) {
            }
        ");

        location.Should().NotBeNull();
        location.Value.Start.Line.Should().BeGreaterThan(0);
    }

    [Test]
    public void MultipleExceptionsEachFireEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            count++;
        };

        engine.Execute(@"
            try { throw 1; } catch (e) {}
            try { throw 2; } catch (e) {}
            try { throw 3; } catch (e) {}
        ");

        count.Should().Be(3);
    }

    private sealed record Pause(PauseType Type, JsValue? ThrownValue, bool IsUncaught, string TopFrame);

    /// <summary>
    /// Runs <paramref name="script"/> with the given exception pause mode and records every pause the
    /// engine makes, swallowing the JavaScript exception a script that throws for real ends with.
    /// </summary>
    private static List<Pause> Record(
        string script,
        ExceptionPauseMode mode,
        Action<Engine>? configure = null,
        Action<Engine, DebugInformation>? onPause = null)
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });
        engine.Debugger.PauseOnExceptions = mode;
        configure?.Invoke(engine);

        var pauses = new List<Pause>();
        engine.Debugger.Break += (sender, info) =>
        {
            pauses.Add(new Pause(info.PauseType, info.ThrownValue, info.IsUncaught, info.CallStack[0].FunctionName));
            onPause?.Invoke(engine, info);
            return StepMode.None;
        };

        try
        {
            engine.Execute(script);
        }
        catch (Jint.Runtime.JavaScriptException)
        {
            // the script's own uncaught throw; the pauses are what the test is about
        }

        return pauses;
    }

    [Test]
    public void NoneIsTheDefaultAndPausesOnNothing()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.PauseOnExceptions.Should().Be(ExceptionPauseMode.None);

        var pauses = Record("try { throw 1; } catch (e) {}\nvar x = undefined.foo;", ExceptionPauseMode.None);

        pauses.Should().BeEmpty();
    }

    [Test]
    public void AllPausesOnACaughtThrowAtTheThrowSite()
    {
        var pauses = Record(@"
            function inner() { throw new Error('boom'); }
            try { inner(); } catch (e) {}
        ", ExceptionPauseMode.All);

        pauses.Should().ContainSingle();
        pauses[0].Type.Should().Be(PauseType.Exception);
        pauses[0].IsUncaught.Should().BeFalse();
        pauses[0].TopFrame.Should().Be("inner", "the pause is at the throw site, before anything unwinds");
        pauses[0].ThrownValue.Should().BeAssignableTo<ObjectInstance>();
    }

    [Test]
    public void ThrownValueIsTheValueItself()
    {
        var pauses = Record("try { throw 42; } catch (e) {}", ExceptionPauseMode.All);

        pauses.Should().ContainSingle();
        pauses[0].ThrownValue!.AsNumber().Should().Be(42);
    }

    [Test]
    public void UncaughtSkipsACaughtThrowAndStopsAtAnUncaughtOne()
    {
        var pauses = Record(@"
            try { throw 'caught'; } catch (e) {}
            throw 'uncaught';
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue();
        pauses[0].ThrownValue!.AsString().Should().Be("uncaught");
    }

    [Test]
    public void AFinallyOnlyTryDoesNotCatch()
    {
        var pauses = Record(@"
            try { throw 'x'; } finally { }
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue();
    }

    [Test]
    public void ACatchInACallingFrameCounts()
    {
        var pauses = Record(@"
            function deep() { throw 'x'; }
            function middle() { deep(); }
            try { middle(); } catch (e) {}
        ", ExceptionPauseMode.All);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeFalse();
        pauses[0].TopFrame.Should().Be("deep");
    }

    [Test]
    public void ARethrowFromACatchIsUncaughtWhenNothingOuterHoldsIt()
    {
        var pauses = Record(@"
            try { throw 'first'; } catch (e) { throw 'second'; }
        ", ExceptionPauseMode.All);

        pauses.Should().HaveCount(2);
        pauses[0].IsUncaught.Should().BeFalse();
        pauses[1].ThrownValue!.AsString().Should().Be("second");
        pauses[1].IsUncaught.Should().BeTrue("a catch clause is not protected by its own try");
    }

    [Test]
    public void ARethrowIsCaughtByAnOuterTry()
    {
        var pauses = Record(@"
            try {
                try { throw 'first'; } catch (e) { throw 'second'; }
            } catch (e) {}
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().BeEmpty();
    }

    [Test]
    public void AThrowInsideAGeneratorResumedInATryBlockIsCaught()
    {
        var pauses = Record(@"
            function* g() {
                try {
                    yield 1;
                    throw 'after-resume';
                } catch (e) {
                    caught = e;
                }
            }
            var caught = null;
            var it = g();
            it.next();
            it.next();
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().BeEmpty("the try block the generator suspended in is entered again on resume");
    }

    [Test]
    public void AThrowInAGeneratorOutsideATryReachesTheCallersCatch()
    {
        var pauses = Record(@"
            function* g() { yield 1; throw 'x'; }
            var it = g();
            it.next();
            try { it.next(); } catch (e) {}
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().BeEmpty("a generator's throw propagates synchronously to whoever called next()");
    }

    [Test]
    public void AnAsyncFunctionsOwnCatchCounts()
    {
        var pauses = Record(@"
            async function run() {
                try { throw 'x'; } catch (e) {}
            }
            run();
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().BeEmpty();
    }

    [Test]
    public void AnAsyncFunctionThrowIsUncaughtEvenInsideACallersTry()
    {
        var pauses = Record(@"
            async function run() { throw 'x'; }
            try { run(); } catch (e) {}
        ", ExceptionPauseMode.Uncaught);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue(
            "the throw becomes a rejection of run()'s promise; the caller's catch never sees it");
    }

    [Test]
    public void AHostEntryEndsTheSearchForACatch()
    {
        var pauses = Record(@"
            function callback() { throw 'x'; }
            try { reenter(callback); } catch (e) {}
        ", ExceptionPauseMode.Uncaught, configure: engine =>
        {
            engine.SetValue("reenter", new Action<JsValue>(callback => engine.Invoke(callback)));
        });

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue("the host, not the script's catch, is what the throw reaches first");
    }

    [Test]
    public void TheThrowingFramesScopesAreVisibleDuringThePause()
    {
        string? local = null;
        string? caller = null;

        Record(@"
            function inner() { const scoped = 'inner'; throw 'x'; }
            function outer() { const scoped = 'outer'; inner(); }
            try { outer(); } catch (e) {}
        ", ExceptionPauseMode.All, onPause: (engine, info) =>
        {
            local = engine.Debugger.Evaluate("scoped").AsString();
            caller = engine.Debugger.Evaluate("scoped", info.CallStack[1]).AsString();
        });

        local.Should().Be("inner");
        caller.Should().Be("outer");
    }

    [Test]
    public void AThrowInsideTheHandlersOwnEvaluationDoesNotRePause()
    {
        var pauses = Record(@"
            try { throw 'x'; } catch (e) {}
        ", ExceptionPauseMode.All, onPause: (engine, info) =>
        {
            Invoking(() => engine.Debugger.Evaluate("throw 'from-the-handler'"))
                .Should().ThrowExactly<DebugEvaluationException>();
        });

        pauses.Should().ContainSingle();
    }

    [Test]
    public void AnImplicitErrorPausesToo()
    {
        var pauses = Record("undefined.foo;", ExceptionPauseMode.Uncaught);

        pauses.Should().ContainSingle();
        pauses[0].IsUncaught.Should().BeTrue();
        pauses[0].ThrownValue.Should().BeAssignableTo<ObjectInstance>();
    }

    [Test]
    public void ThePauseStillReportsTheFirstChanceEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.PauseOnExceptions = ExceptionPauseMode.All;

        var events = 0;
        var pauses = 0;
        engine.Debugger.ExceptionThrown += (sender, args) => events++;
        engine.Debugger.Break += (sender, info) =>
        {
            pauses++;
            return StepMode.None;
        };

        engine.Execute("try { throw 'x'; } catch (e) {}");

        events.Should().Be(1);
        pauses.Should().Be(1);
    }
}
