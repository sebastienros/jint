using System.Collections.Generic;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A <see cref="Jint.Constraints.MaxStatementsConstraint"/> no longer disarms the interpreter's tight-loop
/// lanes: the lanes charge the counter themselves. These tests pin that the charge cadence — and therefore
/// the exact statement at which the limit throws — is identical with the lane armed and disarmed. The lane
/// is disarmed by registering one extra (non-amortizable) constraint, which pushes the exact partition past
/// the single-statement-counter shape the inline lane requires.
/// </summary>
public class StatementLimitThrowPointTests
{
    private sealed class InertConstraint : Constraint
    {
        public override void Check()
        {
        }

        public override void Reset()
        {
        }
    }

    /// <summary>
    /// Observes external state only, so it is safe to amortize and must not disarm the tight lane.
    /// </summary>
    private sealed class AmortizableTripwire : Constraint
    {
        public bool Armed;
        public int Checks;

        public override bool IsAmortizable => true;

        public override void Check()
        {
            Checks++;
            if (Armed)
            {
                throw new TripwireException();
            }
        }

        public override void Reset()
        {
        }
    }

    private sealed class TripwireException : System.Exception
    {
    }

    private static string Run(string body, int maxStatements, bool disarmTightLane)
    {
        var sink = new List<string>();
        var engine = new Engine(options =>
        {
            options.LimitStatements(maxStatements);
            if (disarmTightLane)
            {
                options.AddConstraint(new InertConstraint());
            }
        });
        engine.SetValue("log", new System.Action<string>(sink.Add));

        var outcome = "ok";
        try
        {
            engine.Evaluate("(function () {" + body + "})()");
        }
        catch (StatementsCountOverflowException)
        {
            outcome = "limit";
        }

        return outcome + "|" + string.Join(",", sink);
    }

    public static TheoryData<string> LoopShapes() =>
    [
        // for: block with several statements (routes through JintStatementList, which charges for the block)
        "for (var i = 0; i < 4; i++) { log('a' + i); log('b' + i); }",
        // for: single-statement block (routes through the block's single-statement lane, no block charge)
        "for (var i = 0; i < 4; i++) { log('a' + i); }",
        // for: bare statement body
        "for (var i = 0; i < 4; i++) log('a' + i);",
        // for: empty block (still a statement list, so still charged per iteration)
        "var n = 0; for (var i = 0; i < 4; i++) { } log('n' + n);",
        // for: empty statement body
        "for (var i = 0; i < 4; i++) ; log('done');",
        // for: if/else chain
        "for (var i = 0; i < 6; i++) { if (i % 2 == 0) log('e' + i); else log('o' + i); }",
        // for: if with a nested block branch (the branch block charges too)
        "for (var i = 0; i < 6; i++) { if (i % 2 == 0) { log('e' + i); log('x' + i); } }",
        // for: var declarations mixed in
        "for (var i = 0; i < 4; i++) { var v = i * 2; log('v' + v); }",
        // for: let body (per-iteration environment / flattening)
        "for (let i = 0; i < 4; i++) { let v = i * 2; log('v' + v); }",
        // while
        "var i = 0; while (i < 4) { log('w' + i); i++; }",
        "var i = 0; while (i < 4) { i++; }  log('i' + i);",
        "var i = 0; while (i < 4) i++; log('i' + i);",
        // do-while
        "var i = 0; do { log('d' + i); i++; } while (i < 4);",
        "var i = 0; do i++; while (i < 4); log('i' + i);",
        // nested loops
        "for (var i = 0; i < 3; i++) { for (var j = 0; j < 3; j++) { log(i + '' + j); } }",
    ];

    [Theory]
    [MemberData(nameof(LoopShapes))]
    public void ThrowPointIsIdenticalWithAndWithoutTheTightLane(string body)
    {
        // sweep the whole interesting range: every limit between "throws on the first statement" and
        // "runs to completion" must place the throw at exactly the same statement in both lanes
        for (var maxStatements = 1; maxStatements <= 80; maxStatements++)
        {
            var armed = Run(body, maxStatements, disarmTightLane: false);
            var disarmed = Run(body, maxStatements, disarmTightLane: true);

            armed.Should().Be(disarmed, $"max statements {maxStatements} for `{body}`");
        }
    }

    [Fact]
    public void StatementLimitStillTripsInsideTightLoops()
    {
        var engine = new Engine(options => options.LimitStatements(50));

        Invoking(() => engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 100000; i++) { x += 1; } })()"))
            .Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Fact]
    public void StatementLimitCountsAcrossEvaluationsAsBefore()
    {
        var engine = new Engine(options => options.LimitStatements(20));

        engine.Evaluate("var a = 1;");
        Invoking(() => engine.Evaluate("(function () { for (var i = 0; i < 100; i++) { a++; } })()"))
            .Should().ThrowExactly<StatementsCountOverflowException>();

        engine.Constraints.Reset();
        engine.Evaluate("var b = 2;");
        engine.Evaluate("b").AsNumber().Should().Be(2);
    }

    [Fact]
    public void UnlimitedStatementConstraintDoesNotThrow()
    {
        // MaxStatements <= 0 means unlimited; the inline charge must reproduce that short-circuit
        var engine = new Engine(options => options.LimitStatements(0));

        engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 10000; i++) { x += 1; } return x; })()")
            .AsNumber().Should().Be(10000);
    }

    [Fact]
    public void AmortizableUserConstraintKeepsTheTightLaneAndTheStatementLimitExact()
    {
        var tripwire = new AmortizableTripwire();
        var engine = new Engine(options =>
        {
            options.LimitStatements(40);
            options.AddConstraint(tripwire);
        });

        // the amortizable constraint joins the amortized partition, so the exact partition is still
        // just the statement counter and the inline lane stays available: the throw point must match
        // an engine configured with the statement limit alone
        var sink = new List<string>();
        engine.SetValue("log", new System.Action<string>(sink.Add));

        Invoking(() => engine.Evaluate("(function () { for (var i = 0; i < 100; i++) { log('x' + i); } })()"))
            .Should().ThrowExactly<StatementsCountOverflowException>();

        var expected = Run("for (var i = 0; i < 100; i++) { log('x' + i); }", 40, disarmTightLane: false);
        ("limit|" + string.Join(",", sink)).Should().Be(expected);
    }

    [Fact]
    public void AmortizableUserConstraintIsStillCheckedInsideATightLoop()
    {
        var tripwire = new AmortizableTripwire();
        var engine = new Engine(options => options.AddConstraint(tripwire));

        // no exact constraints at all: the tight lane runs, and the amortized cadence must still reach
        // the constraint often enough to interrupt an otherwise unbounded loop
        tripwire.Armed = true;
        Invoking(() => engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 1000000; i++) { x += 1; } return x; })()"))
            .Should().ThrowExactly<TripwireException>();

        tripwire.Checks.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NonAmortizableUserConstraintKeepsPerStatementCadence()
    {
        var counting = new CountingConstraint();
        var engine = new Engine(options => options.AddConstraint(counting));

        engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 10; i++) { x += 1; } return x; })()");

        // default IsAmortizable is false, so the constraint stays exact and sees every statement
        counting.Checks.Should().BeGreaterThan(10);
    }

    private sealed class CountingConstraint : Constraint
    {
        public int Checks;

        public override void Check() => Checks++;

        // deliberately not clearing Checks: the engine resets constraints before every evaluation
        public override void Reset()
        {
        }
    }
}
