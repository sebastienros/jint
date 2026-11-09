#nullable enable

using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins, from an embedder's side, that a <c>for await</c> loop whose step settles to something other than an object
/// ends after one call to <c>next</c>, failing with the <c>TypeError</c> of step 8.c of
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</a>,
/// whatever the engine is configured to bound.
/// </summary>
/// <remarks>
/// Jint called <c>next</c> again instead, without end (sebastienros/jint#4489). Every step is a promise job that replays
/// the loop statement and allocates, and the entry drains those jobs inside its constraints, so a statement, time,
/// memory or cancellation bound, or an <c>*Async</c> entry's token, ended the run with its own exception after
/// thousands to millions of calls. Nothing else did: not a default engine, which spun at a flat working set; not
/// <see cref="Options.ConstraintOptions.MaxRecursionDepth"/>, since no step recurses; and not
/// <see cref="Options.ConstraintOptions.PromiseTimeout"/>, since the queue a blocking wait drains never empties. The
/// <c>next</c> here fails the run from the host on its ten-thousandth call, so a build that keeps stepping fails an
/// assertion rather than hanging.
/// </remarks>
public class HostForAwaitStepTerminationTests
{
    private const int Ceiling = 10_000;

    private const string Loop = """
        const iterator = {
            next() { step(); return Promise.resolve(5); },
            return() { closed(); return {}; }
        };
        try { for await (const x of { [Symbol.asyncIterator]() { return iterator; } }) { } result = 'completed'; }
        catch (e) { result = e.name + ': ' + e.message; }
        """;

    private const string Expected = "TypeError: Iterator result 5 is not an object";

    private sealed class Counts
    {
        public int Steps;
        public int Closes;
    }

    private static Engine Build(Counts counts, Action<Options>? configure = null)
    {
        var engine = configure is null ? new Engine() : new Engine(configure);
        engine.SetValue("step", new ClrFunction(engine, "step", (_, _) =>
        {
            if (++counts.Steps >= Ceiling)
            {
                throw new InvalidOperationException($"next was called {counts.Steps} times");
            }

            return JsValue.Undefined;
        }));
        engine.SetValue("closed", new ClrFunction(engine, "closed", (_, _) =>
        {
            counts.Closes++;
            return JsValue.Undefined;
        }));
        return engine;
    }

    private static string AsyncFunction => "(async () => { let result; " + Loop + " return result; })()";

    /// <summary>
    /// A promise an embedder unwraps, on a default engine and under every bound: one step, the <c>TypeError</c>, and
    /// the iterator left open, since the step's own failure is not one IteratorClose answers.
    /// </summary>
    [TestCase("nothing")]
    [TestCase("MaxRecursionDepth")]
    [TestCase("PromiseTimeout")]
    [TestCase("LimitStatements")]
    [TestCase("LimitExecutionTime")]
    [TestCase("LimitMemory")]
    [TestCase("ObserveCancellation")]
    public void AnUnwrappedLoopEndsAfterOneStep(string bound)
    {
        using var unfired = new CancellationTokenSource();
        var counts = new Counts();
        var engine = Build(counts, bound switch
        {
            "MaxRecursionDepth" => o => o.Constraints.MaxRecursionDepth = 20,
            "PromiseTimeout" => o => o.Constraints.PromiseTimeout = TimeSpan.FromSeconds(30),
            "LimitStatements" => o => o.LimitStatements(1_000_000),
            "LimitExecutionTime" => o => o.LimitExecutionTime(TimeSpan.FromSeconds(30)),
            "LimitMemory" => o => o.LimitMemory(1_000_000_000),
            "ObserveCancellation" => o => o.ObserveCancellation(unfired.Token),
            _ => null,
        });

        engine.Evaluate(AsyncFunction).UnwrapIfPromise().Should().Be(Expected);
        counts.Steps.Should().Be(1);
        counts.Closes.Should().Be(0);
    }

    /// <summary>
    /// The same loop through <see cref="Engine.EvaluateAsync(string, string, CancellationToken)"/>, whose token used
    /// to be one of the few things that stopped it.
    /// </summary>
    [Test]
    public async Task AnAwaitedLoopEndsAfterOneStep()
    {
        using var unfired = new CancellationTokenSource();
        var counts = new Counts();
        var engine = Build(counts);

        (await engine.EvaluateAsync(AsyncFunction, cancellationToken: unfired.Token)).Should().Be(Expected);
        counts.Steps.Should().Be(1);
        counts.Closes.Should().Be(0);
    }

    /// <summary>
    /// A module's top-level loop, imported synchronously under a <see cref="Options.ConstraintOptions.PromiseTimeout"/> the
    /// blocking import never got to measure against.
    /// </summary>
    [Test]
    public void AModulesTopLevelLoopEndsAfterOneStep()
    {
        var counts = new Counts();
        var engine = Build(counts, o => o.Constraints.PromiseTimeout = TimeSpan.FromSeconds(30));
        engine.Modules.Add("loop", "let result; " + Loop + " export { result };");

        engine.Modules.Import("loop").Get("result").Should().Be(Expected);
        counts.Steps.Should().Be(1);
        counts.Closes.Should().Be(0);
    }
}
