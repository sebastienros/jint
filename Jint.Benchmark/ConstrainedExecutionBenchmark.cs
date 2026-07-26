using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Per-statement constraint check overhead. A registered constraint used to force a virtual
/// Check() call before every executed statement and disarm the tight for-body lane; the
/// amortized-constraint partition removed that for the constraints it can legally apply to.
/// Which constraint you register therefore decides what you pay:
///
/// <list type="bullet">
/// <item><description>
/// <see cref="TimeoutEnabled"/> — a timeout only observes a clock, which a check reads without
/// consuming, so it is <b>amortized</b>: a countdown decrement per statement, and the tight lane
/// stays armed. The <c>TimeoutEnabled=true</c> row should track the unconstrained row closely.
/// </description></item>
/// <item><description>
/// <see cref="StatementLimitEnabled"/> — a statement limit is an <b>exact</b> constraint: its call
/// frequency <i>is</i> its semantics (it counts the calls), so it cannot be amortized without
/// changing what it means. It forces the per-statement check path and therefore disarms the tight
/// body lane in <c>for</c>, <c>while</c> and <c>do-while</c>. Expect the
/// <c>StatementLimitEnabled=true</c> rows to be visibly slower than the unconstrained row —
/// that gap, not the timeout gap, is what an embedder pays for a runaway-script guard today.
/// </description></item>
/// </list>
///
/// Both flags are independent, so the four rows also show whether the two costs compose or
/// overlap (the exact constraint already forces the per-statement path, so adding a timeout on top
/// of it should cost far less than adding it to the unconstrained row).
/// </summary>
[MemoryDiagnoser]
public class ConstrainedExecutionBenchmark
{
    /// <summary>
    /// Comfortably above the ~2M statements one evaluation executes, so the lane measures the cost
    /// of <i>having</i> an exact constraint registered rather than the cost of tripping it.
    /// Constraints are reset per top-level evaluation, so the budget applies per operation.
    /// <para>
    /// Not <see cref="int.MaxValue"/>: that spelling of "effectively unlimited" registers no
    /// constraint at all (see <see cref="ConstraintsOptionsExtensions.MaxStatements"/>), so the
    /// <c>StatementLimitEnabled=true</c> rows would have measured the unconstrained engine and
    /// reported a zero gap.
    /// </para>
    /// </summary>
    private const int StatementBudget = 100_000_000;

    private Engine _engine = null!;
    private Prepared<Script> _functionLocalLoop;

    [Params(false, true)]
    public bool TimeoutEnabled { get; set; }

    [Params(false, true)]
    public bool StatementLimitEnabled { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _functionLocalLoop = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 1000000; i++) {
                    s += 2;
                }
                return s;
            }
            f();
            """);

        var timeoutEnabled = TimeoutEnabled;
        var statementLimitEnabled = StatementLimitEnabled;
        _engine = new Engine(options =>
        {
            if (timeoutEnabled)
            {
                options.TimeoutInterval(TimeSpan.FromSeconds(30));
            }

            if (statementLimitEnabled)
            {
                options.MaxStatements(StatementBudget);
            }
        });

        _engine.Evaluate(_functionLocalLoop);
    }

    [Benchmark]
    public JsValue FunctionLocalLoop() => _engine.Evaluate(_functionLocalLoop);
}
