using System.Diagnostics;
using System.Runtime.InteropServices;
using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what execution constraints bound — and, more importantly, what they do <b>not</b> bound — when a
/// host drives the engine from a C# loop (<c>foreach (var row in rows) predicate.Call(row);</c>) rather
/// than from one script.
/// <para>
/// Every public entry point that runs script — <c>Engine.Execute</c>, <c>Engine.Evaluate</c>,
/// <c>Engine.Invoke</c>, <c>Engine.Call</c> and the <c>JsValue.Call</c> extension helpers — funnels
/// through the same internal <c>ExecuteWithConstraints</c>, which calls <c>Constraint.Reset()</c> both
/// before and after the callback whenever the entry is not nested inside a running evaluation. A single
/// <c>fn.Call(row)</c> is therefore a full top-level run: it gets its own statement budget, its own
/// allocation baseline and, above all, its own freshly armed timeout deadline.
/// </para>
/// <para>
/// <b>What an embedder must take away:</b> constraints bound <em>one</em> entry into the engine, never a
/// sequence of them. If untrusted script is invoked from a host loop, the loop itself is unbounded and
/// must be bounded by the host — check your own <see cref="Stopwatch"/> / <see cref="CancellationToken"/>
/// between iterations, or move the loop into the script (<c>rows.forEach(predicate)</c>) so that the
/// whole traversal is one run and one budget.
/// </para>
/// <para>
/// The one exception is the <em>amortized</em> constraints — cancellation and the timeout's polling
/// cadence. Their check countdown is engine state, not evaluation-context state, so it spans top-level
/// entries: a cancelled token is noticed within 64 statements however those statements are spread over
/// host calls. That bounds detection latency, not the budget — the timeout's deadline is still re-armed
/// per entry, which is why the timeout tests below only ever see it fire inside an entry that itself
/// outlived the interval.
/// </para>
/// <para>
/// These tests deliberately assert the <em>current</em> behaviour so that any change to it is a
/// conscious, reviewed decision rather than a silent one. They are not an endorsement of it.
/// </para>
/// </summary>
public class HostCallLoopConstraintTests
{
    private const int HostCalls = 200;

    /// <summary>
    /// A callee big enough that the interpreter's amortized constraint check (every 64 statements within
    /// one evaluation context) definitely runs several times inside a single invocation. That isolates the
    /// mechanism under test: the timeout below does not fire because its deadline was re-armed on entry,
    /// not because nothing ever looked at it.
    /// </summary>
    private const string FunctionSource = """
        function work(n) {
            var total = 0;
            for (var i = 0; i < 200; i++) {
                total += i * n;
            }
            return total;
        }
        """;

    private static readonly string EquivalentScript = $$"""
        for (var k = 0; k < {{HostCalls}}; k++) {
            work(k);
        }
        """;

    /// <summary>
    /// The same callee, plus a host call that spends a known, deterministic slice of the entry's budget.
    /// <para>
    /// The timeout tests need a host loop whose <em>engine-resident</em> time provably exceeds the
    /// timeout, and a script loop cannot express "take at least this long" — how much wall clock 200
    /// interpreted iterations cost depends on the machine. A <see cref="Thread.Sleep(TimeSpan)"/> behind a
    /// CLR delegate can, and it only ever errs by sleeping longer, which is the direction the premise
    /// needs. The pause is charged against the entry's deadline like any other elapsed time, and is looked
    /// at the moment the delegate returns, because interop call sites re-check the amortized constraints
    /// on return from host code.
    /// </para>
    /// </summary>
    private const string PausingFunctionSource = """
        function pausingWork(n) {
            pause();
            var total = 0;
            for (var i = 0; i < 200; i++) {
                total += i * n;
            }
            return total;
        }
        """;

    /// <summary>
    /// The wall-clock budget one entry into the engine gets in the timeout tests below.
    /// </summary>
    private static readonly TimeSpan HostCallTimeout = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// What each host call spends of that budget. Deliberately a small fraction of
    /// <see cref="HostCallTimeout"/>: the remainder is the headroom an entry has for a scheduling stall
    /// before the engine is entitled to fail it, and the smaller the fraction the more calls it takes for
    /// a deadline that failed to re-arm to accumulate past the timeout — four, with these numbers, which
    /// is well inside the loop.
    /// </summary>
    private static readonly TimeSpan PausePerCall = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Enough calls that the loop's engine-resident time (<see cref="TimedLoopCalls"/> ×
    /// <see cref="PausePerCall"/> = 900 ms) comfortably outlives <see cref="HostCallTimeout"/>.
    /// </summary>
    private const int TimedLoopCalls = 6;

    /// <summary>
    /// Absorbs the constraint's conversion of the configured interval into <see cref="Stopwatch"/> ticks,
    /// which truncates and so can put the deadline a fraction of a tick early. Everything else about the
    /// attribution below is one-sided in the safe direction, so this is the only slack needed — and it is
    /// two orders of magnitude clear of <see cref="PausePerCall"/>, which is what a carried-over deadline
    /// would fail at.
    /// </summary>
    private static readonly TimeSpan AttributionSlack = TimeSpan.FromMilliseconds(1);

    [Fact]
    public void MaxStatementsIsRefundedOnEveryHostCallSoAHostLoopSpendsItOverAndOver()
    {
        // Budget generously above what a single invocation costs, and far below what the whole loop does.
        var budget = StatementsPerInvocation() * 2;
        var engine = new Engine(o => o.MaxStatements(budget));
        engine.Execute(FunctionSource);
        var work = engine.GetValue("work");

        Invoking(() =>
        {
            for (var i = 0; i < HostCalls; i++)
            {
                work.Call(i);
            }
        }).Should().NotThrow("each host call starts a fresh statement budget, so the loop can never exhaust one");

        // The loop really did run far more statements than the budget allows...
        (HostCalls * StatementsPerInvocation()).Should().BeGreaterThan(budget * 10);
    }

    [Fact]
    public void MaxStatementsBoundsTheIdenticalWorkWhenTheLoopLivesInsideTheScript()
    {
        // ...and the identical work, driven from inside one script instead, is bounded as documented.
        var budget = StatementsPerInvocation() * 2;
        var engine = new Engine(o => o.MaxStatements(budget));
        engine.Execute(FunctionSource);

        Invoking(() => engine.Execute(EquivalentScript)).Should().Throw<StatementsCountOverflowException>();
    }

    [Fact]
    public void TimeoutIsRearmedOnEveryHostCallSoItNeverFiresAcrossAHostLoop()
    {
        // The case an embedder is most likely to get wrong: a wall-clock timeout reads as protection
        // against "this script may not run longer than X", but it only ever means "one entry into the
        // engine may not run longer than X".
        WarmUpTheInterpreter();

        var engine = CreatePausingEngine(PausePerCall);
        var work = engine.GetValue("pausingWork");

        var engineTime = TimeSpan.Zero;
        var completed = 0;

        for (var i = 0; i < TimedLoopCalls; i++)
        {
            var iteration = RunOneHostLoopIteration(() => work.Call(i));
            engineTime += iteration.Elapsed;
            if (iteration.Completed)
            {
                completed++;
            }
        }

        // premise: the engine really was resident for longer than one entry's budget, over several
        // separate entries — deterministically so, because each call sleeps for at least PausePerCall
        engineTime.Should().BeGreaterThan(HostCallTimeout);
        completed.Should().BeGreaterThan(
            1,
            "a deadline that carried over would leave only the calls before it expired, and every "
            + "iteration here spends a quarter of the interval");
    }

    [Fact]
    public void APauseLongerThanTheTimeoutFiresInsideTheOneHostCallThatCausedIt()
    {
        // The control the two accumulation tests need: time spent inside a host call really is charged
        // against that entry's deadline, so their "no throw" is a statement about the deadline being
        // re-armed and not about the pause being invisible. Robust in the direction that matters, since
        // Thread.Sleep only ever overshoots and the entry is already past its deadline when it returns.
        WarmUpTheInterpreter();

        var engine = CreatePausingEngine(HostCallTimeout + PausePerCall);
        var work = engine.GetValue("pausingWork");

        Invoking(() => work.Call(1)).Should().Throw<TimeoutException>();
    }

    [Fact]
    public void TimeoutBoundsTheIdenticalWorkWhenTheLoopLivesInsideTheScript()
    {
        var engine = new Engine(o => o.TimeoutInterval(TimeSpan.FromMilliseconds(250)));
        engine.Execute(FunctionSource);

        Invoking(() => engine.Execute("while (true) { work(1); }")).Should().Throw<TimeoutException>();
    }

    [Fact]
    public void MemoryLimitIsRebaselinedOnEveryHostCallSoAHostLoopCanAllocateWithoutBound()
    {
        // 2000 small objects per call — comfortably under the limit on its own, many times over it in total.
        const string AllocatingFunction = """
            function allocate() {
                var a = [];
                for (var i = 0; i < 2000; i++) {
                    a[i] = { index: i };
                }
                return a.length;
            }
            """;

        var engine = new Engine(o => o.LimitMemory(8_000_000));
        engine.Execute(AllocatingFunction);
        var allocate = engine.GetValue("allocate");

        Invoking(() =>
        {
            for (var i = 0; i < HostCalls; i++)
            {
                allocate.Call();
            }
        }).Should().NotThrow("each host call re-reads the allocation baseline, so only one call's allocations are ever counted");
    }

    [Fact]
    public void MemoryLimitBoundsTheIdenticalWorkWhenTheLoopLivesInsideTheScript()
    {
        const string AllocatingFunction = """
            function allocate() {
                var a = [];
                for (var i = 0; i < 2000; i++) {
                    a[i] = { index: i };
                }
                return a.length;
            }
            """;

        var engine = new Engine(o => o.LimitMemory(8_000_000));
        engine.Execute(AllocatingFunction);

        Invoking(() => engine.Execute($"for (var k = 0; k < {HostCalls}; k++) {{ allocate(); }}"))
            .Should().Throw<MemoryLimitExceededException>();
    }

    [Fact]
    public void EngineInvokeRefundsTheBudgetExactlyLikeJsValueCallDoes()
    {
        // Engine.Invoke is the same top-level entry as JsValue.Call: neither accumulates.
        var budget = StatementsPerInvocation() * 2;
        var engine = new Engine(o => o.MaxStatements(budget));
        engine.Execute(FunctionSource);

        Invoking(() =>
        {
            for (var i = 0; i < HostCalls; i++)
            {
                engine.Invoke("work", i);
            }
        }).Should().NotThrow();
    }

    [Fact]
    public void EveryTopLevelHostEntryResetsEveryRegisteredConstraintTwice()
    {
        // The mechanism, observed from the public surface: Engine.ExecuteWithConstraints resets before
        // running the callback and again in its finally block, for every non-nested entry.
        var constraint = new ResetCountingConstraint();
        var engine = new Engine(new Options().Constraint(constraint));
        engine.Execute(FunctionSource);
        var work = engine.GetValue("work");

        constraint.Resets = 0;
        work.Call(1);
        constraint.Resets.Should().Be(2, "a single host call is a complete top-level run");

        constraint.Resets = 0;
        engine.Invoke("work", 1);
        constraint.Resets.Should().Be(2);

        constraint.Resets = 0;
        engine.Evaluate("work(1)");
        constraint.Resets.Should().Be(2);

        // A callback re-entering the engine from inside a running script is nested and must NOT reset,
        // otherwise `while (true) hostCallback()` would re-arm the budget on every iteration.
        engine.SetValue("reenter", new Action(() => engine.Evaluate("work(1)")));
        constraint.Resets = 0;
        engine.Evaluate("reenter()");
        constraint.Resets.Should().Be(2, "only the outermost entry resets");
    }

    [Fact]
    public void ACancelledTokenIsObservedAcrossAHostLoopOfCallsShorterThanTheAmortizationInterval()
    {
        // Cancellation is the one constraint whose Reset() is a no-op, so the cancelled state survives
        // across host calls — and it is now actually looked at. The amortized check cadence is engine
        // state, not evaluation-context state, so the statements of one short call carry the countdown
        // forward into the next instead of every top-level entry restarting it at 64. This is the one
        // shape in this file where the host loop *is* bounded from inside the engine.
        using var cts = new CancellationTokenSource();
        var engine = new Engine(o => o.CancellationToken(cts.Token));
        engine.Execute("function tiny() { return 1; }");
        var tiny = engine.GetValue("tiny");

        cts.Cancel();

        var calls = 0;

        Invoking(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                calls++;
                tiny.Call();
            }
        }).Should().Throw<ExecutionCanceledException>("the amortized countdown spans top-level entries, so a loop of one-statement calls reaches a check");

        // ...and it is noticed promptly: every call runs at least one statement, so the countdown must
        // reach zero within AmortizedConstraintCheckInterval (64) calls. That constant is internal, hence
        // the literal; the point of the assertion is that detection latency stays bounded, not unbounded.
        calls.Should().BeLessThanOrEqualTo(64);
    }

    [Fact]
    public void ACancelledTokenIsObservedOnceASingleHostCallRunsPastTheAmortizationInterval()
    {
        // The contrast that proves the previous test is about the check cadence and not about the token:
        // one invocation of a callee running well past 64 statements does notice.
        using var cts = new CancellationTokenSource();
        var engine = new Engine(o => o.CancellationToken(cts.Token));
        engine.Execute(FunctionSource);
        var work = engine.GetValue("work");

        cts.Cancel();

        Invoking(() => work.Call(1)).Should().Throw<ExecutionCanceledException>();
    }

    [Fact]
    public void ConstraintsCheckFromTheHostLoopDoesNotAccumulateEitherForATimeout()
    {
        // The public escape hatch does not close the gap on its own: TimeConstraint re-arms its deadline
        // at the end of every run, so a host-loop Constraints.Check() measures the time since the last
        // call returned, never the time the loop has been running.
        WarmUpTheInterpreter();

        var engine = CreatePausingEngine(PausePerCall);
        var work = engine.GetValue("pausingWork");

        var engineTime = TimeSpan.Zero;
        var completed = 0;

        for (var i = 0; i < TimedLoopCalls; i++)
        {
            var iteration = RunOneHostLoopIteration(() =>
            {
                work.Call(i);
                engine.Constraints.Check();
            });

            engineTime += iteration.Elapsed;
            if (iteration.Completed)
            {
                completed++;
            }
        }

        engineTime.Should().BeGreaterThan(HostCallTimeout);
        completed.Should().BeGreaterThan(
            1,
            "the host-loop check measures the time since the last entry returned and re-armed the "
            + "deadline, not the age of the loop");
    }

    [Fact]
    public void AUserConstraintThatDeclinesToResetItselfDoesBoundTheWholeHostLoop()
    {
        // The one thing an embedder can do today without any engine change: a user-derived Constraint
        // whose Reset() is a no-op keeps its state across top-level entries, and because it is exact by
        // default it is checked on every statement — including the first statement of every host call.
        // The cost is that an exact constraint disarms the interpreter's tight-loop fast lanes.
        var budget = StatementsPerInvocation() * 2;
        var engine = new Engine(new Options().Constraint(new SpanningStatementBudget(budget)));
        engine.Execute(FunctionSource);
        var work = engine.GetValue("work");

        Invoking(() =>
        {
            for (var i = 0; i < HostCalls; i++)
            {
                work.Call(i);
            }
        }).Should().Throw<BudgetExhaustedException>("a constraint that does not reset itself accumulates across host calls");
    }

    [Fact]
    public void AnAmortizableUserConstraintIsReachedByAHostLoopOfCallsShorterThanTheCheckInterval()
    {
        // The generalisation of the cancellation case above, for a user-derived constraint: declaring
        // IsAmortizable => true moves the constraint onto the per-64-statement cadence, and that
        // countdown is engine state that spans top-level entries. An always-failing amortizable
        // constraint is therefore consulted — within 64 statements — by a loop of short calls too.
        // It still keeps the tight-loop fast lane armed, unlike the exact constraint above.
        var engine = new Engine(new Options().Constraint(new AlwaysFailingAmortizableConstraint()));
        engine.Execute("function tiny() { return 1; }");
        var tiny = engine.GetValue("tiny");

        var calls = 0;

        Invoking(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                calls++;
                tiny.Call();
            }
        }).Should().Throw<BudgetExhaustedException>("the amortized countdown spans host calls instead of restarting at 64 for each");

        calls.Should().BeLessThanOrEqualTo(64);
    }

    /// <summary>
    /// An engine running <see cref="PausingFunctionSource"/> under <see cref="HostCallTimeout"/>, whose
    /// <c>pause()</c> spends <paramref name="pause"/> of the current entry's budget.
    /// </summary>
    private static Engine CreatePausingEngine(TimeSpan pause)
    {
        var engine = new Engine(o => o.TimeoutInterval(HostCallTimeout));
        engine.SetValue("pause", new Action(() => Thread.Sleep(pause)));
        engine.Execute(PausingFunctionSource);
        return engine;
    }

    /// <summary>
    /// Runs one iteration of a host loop under <see cref="HostCallTimeout"/> and reports whether it
    /// completed, converting the one <see cref="TimeoutException"/> the engine is entitled to throw into
    /// an observation instead of a failure.
    /// <para>
    /// A timeout constraint measures wall clock, so <c>NotThrow</c> over a host loop is a stronger claim
    /// than the engine ever made: if the operating system deschedules the thread for longer than the
    /// interval in the middle of an entry, that entry <em>did</em> outlive its deadline and failing it is
    /// the documented behaviour, not the bug this file guards. Asserting no throw at all therefore made
    /// these tests assertions about a CI agent's scheduling latency, and they failed as such — repeatedly,
    /// on unrelated changes, on every operating system and both target frameworks.
    /// </para>
    /// <para>
    /// What the engine does promise is the implication: a throw can only come from an entry that itself
    /// ran past the interval. That is what is asserted here, and it cannot be falsified by a stall, only
    /// by the regression the tests exist for — a deadline carried over from an earlier host call fires
    /// after an iteration costing <see cref="PausePerCall"/>, a quarter of the interval. The stopwatch
    /// starts before the entry arms its deadline and stops after it throws, so the window it measures is
    /// always a superset of the one the constraint measured; the comparison can only err towards
    /// tolerating a throw, never towards inventing one.
    /// </para>
    /// </summary>
    private static HostLoopIteration RunOneHostLoopIteration(Action iteration)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            iteration();
            return new HostLoopIteration(true, stopwatch.Elapsed);
        }
        catch (TimeoutException)
        {
            var elapsed = stopwatch.Elapsed;
            elapsed.Should().BeGreaterThanOrEqualTo(
                HostCallTimeout - AttributionSlack,
                "the timeout may only fire for an entry that itself outlived the interval; a throw out "
                + "of a shorter iteration means the deadline carried over from an earlier host call");
            return new HostLoopIteration(false, elapsed);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct HostLoopIteration(bool Completed, TimeSpan Elapsed);

    /// <summary>
    /// Pays the JIT cost of the whole call path — the interpreter, the constraint plumbing and the
    /// delegate wrapper — on an engine carrying no timeout, so that the first <em>timed</em> entry is not
    /// also the one compiling it. Cold JIT is process-wide, and charging it to an entry whose budget is
    /// being measured is the one avoidable way to make an entry outlive its deadline.
    /// </summary>
    private static void WarmUpTheInterpreter()
    {
        var engine = new Engine();
        engine.SetValue("pause", new Action(() => { }));
        engine.Execute(PausingFunctionSource);
        var work = engine.GetValue("pausingWork");

        for (var i = 0; i < 3; i++)
        {
            work.Call(i);
            engine.Constraints.Check();
        }
    }

    /// <summary>
    /// How many constraint checks one <c>work()</c> invocation performs. A user-derived constraint is
    /// checked at exactly the points <see cref="MaxStatementsConstraint"/> is, so this is the statement
    /// budget a single host call needs — measured rather than guessed, which keeps the budgets above
    /// meaningful if statement accounting ever shifts.
    /// </summary>
    private static int StatementsPerInvocation()
    {
        var constraint = new HighWaterMarkConstraint();
        var engine = new Engine(new Options().Constraint(constraint));
        engine.Execute(FunctionSource);
        constraint.Reset();
        engine.GetValue("work").Call(1);
        return constraint.HighWaterMark;
    }

    private sealed class HighWaterMarkConstraint : Constraint
    {
        private int _count;

        /// <summary>
        /// The highest count reached within a single run, tracked separately because the engine resets
        /// constraints both before and after every one.
        /// </summary>
        public int HighWaterMark { get; private set; }

        public override void Check()
        {
            _count++;
            if (_count > HighWaterMark)
            {
                HighWaterMark = _count;
            }
        }

        public override void Reset() => _count = 0;
    }

    private sealed class ResetCountingConstraint : Constraint
    {
        public int Resets { get; set; }

        public override void Check()
        {
        }

        public override void Reset() => Resets++;
    }

    /// <summary>
    /// A statement budget that spans every entry into the engine, because it refuses the engine's
    /// invitation to rewind itself. This is what an embedder has to write today to bound a host-driven
    /// call loop from inside the engine.
    /// </summary>
    private sealed class SpanningStatementBudget : Constraint
    {
        private readonly int _budget;
        private int _spent;

        public SpanningStatementBudget(int budget) => _budget = budget;

        public override void Check()
        {
            if (++_spent > _budget)
            {
                throw new BudgetExhaustedException();
            }
        }

        public override void Reset()
        {
            // deliberately empty: the budget covers the embedding operation, not one entry into the engine
        }
    }

    private sealed class AlwaysFailingAmortizableConstraint : Constraint
    {
        public override bool IsAmortizable => true;

        public override void Check() => throw new BudgetExhaustedException();

        public override void Reset()
        {
        }
    }

    private sealed class BudgetExhaustedException : Exception
    {
    }
}
