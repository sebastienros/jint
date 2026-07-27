using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Per-call cost of the built-ins whose bodies were migrated off the raw <c>JsCallArguments</c> array onto
/// positional parameters so they could reach the fast-call lane. On the lane a warm call site hands its
/// arguments over in registers instead of filling a pooled array, so these rows are where the migration
/// shows up.
/// <para>
/// Every row runs its call in a tight loop inside one prepared script on one long-lived engine, so the
/// site is warm and the measurement is the call, not the parse or the first-touch dispatch. The loop body
/// is deliberately trivial where the method allows it (a short scan, an immediate hit) so the dispatch is
/// not buried under the built-in's own work.
/// </para>
/// <para>
/// <c>Control_ArrayLastIndexOf</c> and <c>Control_ArrayFindLast</c> are the untouched siblings of the
/// migrated pair either side of them: still on <c>JsCallArguments</c>, so they should not move at all and
/// are the cross-run floor for reading the others.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class FastCallMigrationBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    private const string Setup = """
        var data = new Array(64);
        for (var i = 0; i < 64; i++) data[i] = i;
        var text = 'hello world foo bar baz';
        var isThree = function (x) { return x === 3; };
        """;

    private Engine _engine = null!;

    private Prepared<Script> _stringIncludes;
    private Prepared<Script> _stringEndsWith;
    private Prepared<Script> _arrayIndexOf;
    private Prepared<Script> _arraySome;
    private Prepared<Script> _arrayFind;
    private Prepared<Script> _arrayFindIndex;
    private Prepared<Script> _numberToString;
    private Prepared<Script> _numberToStringRadix;
    private Prepared<Script> _arrayLastIndexOf;
    private Prepared<Script> _arrayFindLast;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();
        _engine.Execute(Setup);

        _stringIncludes = Prepare("text.includes('bar')");
        _stringEndsWith = Prepare("text.endsWith('baz')");
        _arrayIndexOf = Prepare("data.indexOf(3)");
        _arraySome = Prepare("data.some(isThree)");
        _arrayFind = Prepare("data.find(isThree)");
        _arrayFindIndex = Prepare("data.findIndex(isThree)");
        _numberToString = Prepare("(255).toString()");
        _numberToStringRadix = Prepare("(255).toString(16)");
        _arrayLastIndexOf = Prepare("data.lastIndexOf(3)");
        _arrayFindLast = Prepare("data.findLast(isThree)");

        _engine.Evaluate(_stringIncludes);
        _engine.Evaluate(_stringEndsWith);
        _engine.Evaluate(_arrayIndexOf);
        _engine.Evaluate(_arraySome);
        _engine.Evaluate(_arrayFind);
        _engine.Evaluate(_arrayFindIndex);
        _engine.Evaluate(_numberToString);
        _engine.Evaluate(_numberToStringRadix);
        _engine.Evaluate(_arrayLastIndexOf);
        _engine.Evaluate(_arrayFindLast);
    }

    private static Prepared<Script> Prepare(string call)
        => Engine.PrepareScript("var r; for (var n = 0; n < " + OperationsPerInvoke + "; n++) { r = " + call + "; } r");

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue StringIncludes() => _engine.Evaluate(_stringIncludes);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue StringEndsWith() => _engine.Evaluate(_stringEndsWith);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArrayIndexOf() => _engine.Evaluate(_arrayIndexOf);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArraySome() => _engine.Evaluate(_arraySome);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArrayFind() => _engine.Evaluate(_arrayFind);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArrayFindIndex() => _engine.Evaluate(_arrayFindIndex);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue NumberToString() => _engine.Evaluate(_numberToString);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue NumberToStringRadix() => _engine.Evaluate(_numberToStringRadix);

    /// <summary>Untouched sibling of <see cref="ArrayIndexOf"/>: still takes the raw argument array.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Control_ArrayLastIndexOf() => _engine.Evaluate(_arrayLastIndexOf);

    /// <summary>Untouched sibling of <see cref="ArrayFind"/>: still takes the raw argument array.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Control_ArrayFindLast() => _engine.Evaluate(_arrayFindLast);
}
