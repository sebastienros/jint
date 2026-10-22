using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The fixed cost of one host entry into a warm engine: a cached <see cref="Prepared{T}"/> script so small
/// that entering the engine dominates running it, which is the shape of a rule or condition engine evaluating
/// many tiny expressions. Each row has its own engine (<see cref="IsolatedScript"/>), built and warmed in
/// <c>[GlobalSetup]</c>, so engine construction never enters the measurement.
///
/// <para><see cref="FunctionBody"/> is the control row: the same expression inside a host-invoked function,
/// so it reaches neither script evaluation nor global declaration instantiation, and a change to the script
/// entry cannot move it.</para>
/// </summary>
[MemoryDiagnoser]
public class TinyScriptEntryBenchmark
{
    private IsolatedScript _expression;
    private IsolatedScript _comparison;
    private IsolatedScript _declaration;
    private Engine _functionEngine = null!;
    private JsValue _function = null!;

    [GlobalSetup]
    public void Setup()
    {
        static Engine Fixture()
        {
            var engine = new Engine();
            engine.Execute("var a = 2, b = 3, o = { x: 4 };");
            return engine;
        }

        _expression = IsolatedScript.Warm("a + b", Fixture);
        _comparison = IsolatedScript.Warm("o.x > a && b !== 0", Fixture);
        _declaration = IsolatedScript.Warm("var c = a + b; c", Fixture);

        _functionEngine = Fixture();
        _function = _functionEngine.Evaluate("(function () { return a + b; })");
        _functionEngine.Invoke(_function);
    }

    [Benchmark]
    public object Expression() => _expression.Run();

    [Benchmark]
    public object Comparison() => _comparison.Run();

    [Benchmark]
    public object Declaration() => _declaration.Run();

    [Benchmark]
    public object FunctionBody() => _functionEngine.Invoke(_function);
}
