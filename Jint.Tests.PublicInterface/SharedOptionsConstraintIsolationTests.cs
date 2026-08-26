using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Building many engines from a single <see cref="Options"/> instance is a supported pattern, so the
/// per-execution state a constraint carries (statement counter, deadline) must belong to one engine
/// only. These tests pin that isolation for the built-in constraints.
/// </summary>
public class SharedOptionsConstraintIsolationTests
{
    private const int EngineCount = 8;

    private const string LoopScript = """
        var total = 0;
        for (var i = 0; i < 2000; i++) {
            total += i;
        }
        total;
        """;

    private const double LoopScriptResult = 1999 * 2000 / 2d;

    [Test]
    public async Task EachEngineGetsItsOwnStatementBudgetWhenRunConcurrently()
    {
        // A budget just above what one run needs: a shared counter would blow it well before all
        // engines finished, an isolated one leaves every engine comfortably inside its own budget.
        var options = new Options().LimitStatements(MeasureStatements(LoopScript) + 10);
        var engines = new Engine[EngineCount];
        for (var i = 0; i < engines.Length; i++)
        {
            engines[i] = new Engine(options);
        }

        using var start = new ManualResetEventSlim(false);
        var tasks = new Task<double>[engines.Length];
        for (var i = 0; i < engines.Length; i++)
        {
            var engine = engines[i];
            tasks[i] = Task.Run(() =>
            {
                start.Wait();
                return engine.Evaluate(LoopScript).AsNumber();
            });
        }

        start.Set();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(x => x == LoopScriptResult);
    }

    [Test]
    public void EachEngineGetsItsOwnStatementConstraintInstance()
    {
        var options = new Options().LimitStatements(100_000);
        var first = new Engine(options);
        var second = new Engine(options);

        var firstConstraint = first.Constraints.Find<MaxStatementsConstraint>();
        var secondConstraint = second.Constraints.Find<MaxStatementsConstraint>();

        firstConstraint.Should().NotBeNull();
        secondConstraint.Should().NotBeNull();
        firstConstraint.Should().NotBeSameAs(secondConstraint);
    }

    [Test]
    public void FindReturnsALiveConstraintThatOnlyAffectsItsOwnEngine()
    {
        var options = new Options().LimitStatements(100_000);
        var restricted = new Engine(options);
        var unrestricted = new Engine(options);

        // an embedder is allowed to retune the limit after the engine exists
        restricted.Constraints.Find<MaxStatementsConstraint>().MaxStatements = 5;

        unrestricted.Constraints.Find<MaxStatementsConstraint>().MaxStatements.Should().Be(100_000);

        Invoking(() => restricted.Evaluate(LoopScript)).Should().Throw<StatementsCountOverflowException>();
        unrestricted.Evaluate(LoopScript).AsNumber().Should().Be(LoopScriptResult);
    }

    [Test]
    public void EachEngineGetsItsOwnTimeoutDeadline()
    {
        var options = new Options().LimitExecutionTime(TimeSpan.FromMilliseconds(100));
        var engine = new Engine(options);
        var other = new Engine(options);

        // Runs past the timeout and then starts an execution on the sibling engine. A shared
        // deadline would be re-armed by the sibling's run and this engine would sail past its own
        // timeout; an isolated deadline has already expired by the time control comes back.
        engine.SetValue("runOnOtherEngine", new Action(() =>
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
            other.Evaluate("1 + 1");
        }));

        var script = """
            runOnOtherEngine();
            var total = 0;
            for (var i = 0; i < 20000; i++) {
                total += i;
            }
            total;
            """;

        Invoking(() => engine.Evaluate(script)).Should().Throw<TimeoutException>();
    }

    [Test]
    public void ConstraintInstanceRegistrationStillWorks()
    {
        // the instance overload stays supported for the single-engine case
        var constraint = new CountingConstraint();
        var engine = new Engine(new Options().AddConstraint(constraint));

        engine.Evaluate(LoopScript).AsNumber().Should().Be(LoopScriptResult);

        constraint.HighWaterMark.Should().BeGreaterThan(0);
    }

    [Test]
    public void ConstraintFactoryRegistrationProducesOneInstancePerEngine()
    {
        var created = new List<CountingConstraint>();
        var options = new Options().AddConstraint(() =>
        {
            var constraint = new CountingConstraint();
            lock (created)
            {
                created.Add(constraint);
            }
            return constraint;
        });

        _ = new Engine(options);
        _ = new Engine(options);
        _ = new Engine(options);

        created.Should().HaveCount(3);
        created.Distinct().Should().HaveCount(3);
    }

    [Test]
    public void RemoveConstraintsRemovesFactoryRegistrations()
    {
        var options = new Options()
            .LimitStatements(5)
            .RemoveConstraints(x => x is MaxStatementsConstraint);

        var engine = new Engine(options);

        engine.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();
        engine.Evaluate(LoopScript).AsNumber().Should().Be(LoopScriptResult);
    }

    [Test]
    public void ReconfiguringAConstraintReplacesTheEarlierRegistration()
    {
        // MaxStatements clears any previous registration before adding its own
        var options = new Options().LimitStatements(5).LimitStatements(100_000);

        var engine = new Engine(options);

        engine.Constraints.Find<MaxStatementsConstraint>().MaxStatements.Should().Be(100_000);
        engine.Evaluate(LoopScript).AsNumber().Should().Be(LoopScriptResult);
    }

    /// <summary>
    /// Counts how many statement checks a script performs. A user-derived constraint is checked at
    /// exactly the same points as <see cref="MaxStatementsConstraint"/>, so the high water mark is the
    /// statement budget the script needs.
    /// </summary>
    private static int MeasureStatements(string script)
    {
        var constraint = new CountingConstraint();
        var engine = new Engine(new Options().AddConstraint(constraint));
        engine.Evaluate(script);
        return constraint.HighWaterMark;
    }

    private sealed class CountingConstraint : Constraint
    {
        private int _count;

        /// <summary>
        /// The highest count reached within a single execution. Tracked separately because the engine
        /// resets constraints both before and after every execution.
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
}
