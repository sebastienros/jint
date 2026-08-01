using System.Diagnostics;
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
        var timeout = TimeSpan.FromMilliseconds(250);
        var runFor = TimeSpan.FromMilliseconds(1500);

        var engine = new Engine(o => o.TimeoutInterval(timeout));
        engine.Execute(FunctionSource);
        var work = engine.GetValue("work");

        var elapsed = Stopwatch.StartNew();
        var calls = 0;

        Invoking(() =>
        {
            while (elapsed.Elapsed < runFor)
            {
                work.Call(calls++);
            }
        }).Should().NotThrow("the deadline is re-armed on entry to every host call, so it can only ever expire inside one of them");

        elapsed.Stop();

        // premise checks: the loop really did outlive the timeout, over many separate entries
        elapsed.Elapsed.Should().BeGreaterThan(timeout);
        calls.Should().BeGreaterThan(1);
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
    public void ACancelledTokenIsNotObservedByHostCallsShorterThanTheAmortizationInterval()
    {
        // Cancellation is the one constraint whose Reset() is a no-op, so the cancelled state itself does
        // survive across host calls. It still goes unnoticed here, for an independent reason: the
        // amortized constraints are checked once per 64 statements of a single evaluation context, and a
        // host call gets a brand new context — so a callee shorter than that interval never reaches a check.
        using var cts = new CancellationTokenSource();
        var engine = new Engine(o => o.CancellationToken(cts.Token));
        engine.Execute("function tiny() { return 1; }");
        var tiny = engine.GetValue("tiny");

        cts.Cancel();

        Invoking(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                tiny.Call();
            }
        }).Should().NotThrow("a one-statement callee never reaches the amortized check, however many times it is invoked");
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
        var engine = new Engine(o => o.TimeoutInterval(TimeSpan.FromMilliseconds(250)));
        engine.Execute(FunctionSource);
        var work = engine.GetValue("work");

        var elapsed = Stopwatch.StartNew();

        Invoking(() =>
        {
            while (elapsed.Elapsed < TimeSpan.FromMilliseconds(1000))
            {
                work.Call(1);
                engine.Constraints.Check();
            }
        }).Should().NotThrow();

        elapsed.Stop();
        elapsed.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(250));
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
    public void AnAmortizableUserConstraintStillMissesHostCallsShorterThanTheCheckInterval()
    {
        // The trap that goes with the workaround above: declaring IsAmortizable => true moves the
        // constraint onto the per-64-statement cadence, whose countdown lives on the evaluation context —
        // and a host call gets a fresh one. An always-failing amortizable constraint is therefore never
        // even consulted by a loop of short calls.
        var engine = new Engine(new Options().Constraint(new AlwaysFailingAmortizableConstraint()));
        engine.Execute("function tiny() { return 1; }");
        var tiny = engine.GetValue("tiny");

        Invoking(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                tiny.Call();
            }
        }).Should().NotThrow("the amortized countdown restarts at 64 for every host call");
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
