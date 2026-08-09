using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

/// <summary>
/// A parse-only prepared program leaves the AST empty, so the state the analyzer would have published at
/// preparation time is instead published by whichever engine reaches a node first — and with the program shared
/// across a pool, several engines can reach the same node at the same instant. The publications are unsynchronized
/// <c>UserData</c> writes of engine-neutral values, so the intended outcome is last-writer-wins with every engine
/// correct either way; this pins that, rather than leaving it to the one-thread-at-a-time tests to imply.
/// <para>
/// Lives in <c>Jint.Tests</c> rather than beside <c>Jint.Tests.CommonScripts/ConcurrencyTest.cs</c> — its nearest
/// relative — because the convergence half of the assertion names <c>JintFunctionDefinition.State</c> and
/// <c>JintBlockStatement.BlockState</c>, and <c>Jint.Tests</c> is the only one of the two with
/// <c>InternalsVisibleTo</c>. Without that half the test could only re-state what
/// <c>ConcurrentEnginesCanUseSameAst</c> already covers.
/// </para>
/// </summary>
public class ParseOnlyPreparationRaceTests
{
    private const int EngineCount = 16;
    private const int Rounds = 4;

    // fib(18) = 2584, and sum doubles 0..199, so 2 * 19900 = 39800.
    private const int Expected = 2584 + 39800;

    private const string Body = """
        function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }
        function sum(arr) { let total = 0; for (const v of arr) { { let scaled = v * 2; total += scaled; } } return total; }
        """;

    [Fact]
    public void RacingFirstEvaluationsOfAParseOnlyPreparedScriptAllSucceedAndConverge()
    {
        const string Code = $$"""
            {{Body}}
            var data = [];
            for (var i = 0; i < 200; i++) { data.push(i); }
            fib(18) + sum(data);
            """;

        for (var round = 0; round < Rounds; round++)
        {
            var prepared = Engine.PrepareScript(Code, options: new ScriptPreparationOptions { StaticAnalysis = false });
            prepared.Program.ShouldCarryNothing();

            var results = Race(_ => new Engine().Evaluate(prepared).AsNumber());

            results.Should().AllBeEquivalentTo((double) Expected);
            results.Should().HaveCount(EngineCount);
            AssertPublished(prepared.Program.Body[0], prepared.Program.Body[1]);
        }
    }

    [Fact]
    public void RacingFirstImportsOfAParseOnlyPreparedModuleAllSucceedAndConverge()
    {
        const string Code = $$"""
            {{Body}}
            const data = [];
            for (let i = 0; i < 200; i++) { data.push(i); }
            export const result = fib(18) + sum(data);
            """;

        for (var round = 0; round < Rounds; round++)
        {
            var prepared = Engine.PrepareModule(Code, options: new ModulePreparationOptions { StaticAnalysis = false });
            prepared.Program.ShouldCarryNothing();

            var results = Race(_ =>
            {
                var engine = new Engine();
                engine.Modules.Add("main", x => x.AddModule(prepared));
                return engine.Modules.Import("main").Get("result").AsNumber();
            });

            results.Should().AllBeEquivalentTo((double) Expected);
            results.Should().HaveCount(EngineCount);

            AssertPublished(prepared.Program.Body[0], prepared.Program.Body[1]);
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> once per engine, with every worker that got a thread released into its first
    /// evaluation together.
    /// </summary>
    private static List<double> Race(Func<int, double> body)
    {
        var gate = new StartGate(EngineCount);
        var results = new ConcurrentBag<double>();

        Parallel.ForEach(
            Enumerable.Range(0, EngineCount),
            new ParallelOptions { MaxDegreeOfParallelism = EngineCount },
            i =>
            {
                gate.Arrive();
                results.Add(body(i));
            });

        return [.. results];
    }

    /// <summary>
    /// Holds each arriving worker until all of them have arrived — or until a short deadline expires, which is why
    /// this is not a <see cref="Barrier"/>. <see cref="Parallel.ForEach{T}(IEnumerable{T}, ParallelOptions, Action{T})"/>
    /// promises an upper bound on parallelism, never a lower one, so a real barrier would deadlock on any machine
    /// that hands out fewer threads than we asked for. Missing the race degrades this test to a plain correctness
    /// run; deadlocking it would hang the suite.
    /// </summary>
    private sealed class StartGate(int expected)
    {
        private int _arrived;

        public void Arrive()
        {
            Interlocked.Increment(ref _arrived);

            var stopwatch = Stopwatch.StartNew();
            while (Volatile.Read(ref _arrived) < expected && stopwatch.ElapsedMilliseconds < 200)
            {
                Thread.SpinWait(64);
            }
        }
    }

    /// <summary>
    /// The two function declarations the race drives, checked for the state the racing engines published onto them:
    /// the definition state on the function itself, the block state on the nested block inside <c>sum</c>'s loop,
    /// and the folded value on the literal that block multiplies by.
    /// </summary>
    private static void AssertPublished(Node fib, Node sum)
    {
        fib.UserData.Should().BeOfType<JintFunctionDefinition.State>();
        sum.UserData.Should().BeOfType<JintFunctionDefinition.State>();

        var loop = (ForOfStatement) ((FunctionBody) ((FunctionDeclaration) sum).Body).Body[1];
        var loopBody = (NestedBlockStatement) loop.Body;
        var innerBlock = loopBody.Body[0].Should().BeOfType<NestedBlockStatement>().Which;
        innerBlock.UserData.Should().BeOfType<JintBlockStatement.BlockState>();

        var scaled = (NonLogicalBinaryExpression) ((VariableDeclaration) innerBlock.Body[0]).Declarations[0].Init;
        scaled.Right.UserData.Should().Be(JsNumber.Create(2));
    }
}
