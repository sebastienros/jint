using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Per-statement constraint check overhead. A registered constraint used to force a virtual
/// Check() call before every executed statement and disarm the tight for-body lane; the
/// amortized-constraint partition and the inline statement counter each removed that for a class
/// of constraints. Which constraint you register therefore decides what you pay:
///
/// <list type="bullet">
/// <item><description>
/// <see cref="TimeoutEnabled"/> — a timeout only observes a clock, which a check reads without
/// consuming, so it is <b>amortized</b>: a countdown decrement per statement, and the tight lane
/// stays armed. The <c>TimeoutEnabled=true</c> row should track the unconstrained row closely.
/// </description></item>
/// <item><description>
/// <see cref="MemoryLimitEnabled"/> — allocation is an <b>exact</b> constraint and a single statement may
/// allocate without bound, so every statement reads the current thread's allocation counter. The engine also
/// snapshots each top-level execution segment so asynchronous continuations can carry the accumulated budget
/// across thread hops. These rows measure both costs together.
/// </description></item>
/// <item><description>
/// <see cref="StatementLimitEnabled"/> — a statement limit is an <b>exact</b> constraint: its call
/// frequency <i>is</i> its semantics (it counts the calls), so it cannot be amortized without
/// changing what it means. It is nevertheless not the generic per-statement path: while it is the
/// <em>only</em> exact constraint, the engine reports it as the inline statement counter
/// (<c>Engine.PartitionConstraints</c>) and the interpreter charges it itself through a
/// devirtualized <c>Check()</c> on the sealed constraint, so the tight body lane in <c>for</c>,
/// <c>while</c> and <c>do-while</c> <b>stays armed</b>. Expect the
/// <c>StatementLimitEnabled=true</c> rows to be only modestly slower than the unconstrained row —
/// one counter increment and comparison per executed statement, not a lane change. What still
/// disarms the lane is a <i>second</i> exact constraint (e.g. <c>LimitMemory</c>), a user-derived
/// constraint that did not opt into <c>IsAmortizable</c>, or debug mode; this benchmark registers
/// at most one exact constraint, so no row here measures that.
/// </description></item>
/// </list>
///
/// Both flags are independent, so the four rows also show whether the two costs compose or
/// overlap. Timeout and statement counting compose additively and stay on the fast lane until a memory limit
/// joins them; memory accounting deliberately disarms that lane because its exact check cannot be skipped.
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
    private const long MemoryBudget = 1_000_000_000;

    private Engine _engine = null!;
    private Prepared<Script> _functionLocalLoop;

    [Params(false, true)]
    public bool TimeoutEnabled { get; set; }

    [Params(false, true)]
    public bool StatementLimitEnabled { get; set; }

    [Params(false, true)]
    public bool MemoryLimitEnabled { get; set; }

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
        var memoryLimitEnabled = MemoryLimitEnabled;
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

            if (memoryLimitEnabled)
            {
                options.LimitMemory(MemoryBudget);
            }
        });

        _engine.Evaluate(_functionLocalLoop);
    }

    [Benchmark]
    public JsValue FunctionLocalLoop() => _engine.Evaluate(_functionLocalLoop);
}
