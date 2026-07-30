using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Per-call cost of the built-ins whose bodies were migrated off the raw <c>JsCallArguments</c> array onto
/// positional parameters so they could reach the fast-call lane. On the lane a warm call site hands its
/// arguments over in registers instead of filling a pooled array, so these rows are where the migration
/// shows up.
/// <para>
/// Every row runs its call in a tight loop inside one prepared script on its own long-lived engine, so the
/// site is warm and the measurement is the call, not the parse or the first-touch dispatch. The loop body
/// is deliberately trivial where the method allows it (a short scan, an immediate hit) so the dispatch is
/// not buried under the built-in's own work.
/// </para>
/// <para>
/// <c>Control_ArrayLastIndexOf</c> and <c>Control_ArrayFindLast</c> are the untouched siblings of the
/// migrated pair either side of them: still on <c>JsCallArguments</c>, so they should not move at all and
/// are the cross-run floor for reading the others.
/// </para>
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all ten scripts, so a row
/// was measured on an engine carrying the other nine rows' globals (every script declares <c>r</c> and
/// <c>n</c>) and their handler-tree entries and per-call-site caches — which is what the two control rows
/// exist to be free of, so leaving them exposed to it defeated their purpose. The <c>data</c>/<c>text</c>
/// fixture is still shared, but now by being re-run per engine rather than by the engine being re-used.
/// The rows still measure warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>,
/// outside the measurement. <b>Numbers from this class are not comparable to any published before the
/// harness changed.</b></para>
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

    private IsolatedScript _stringIncludes;
    private IsolatedScript _stringEndsWith;
    private IsolatedScript _arrayIndexOf;
    private IsolatedScript _arraySome;
    private IsolatedScript _arrayFind;
    private IsolatedScript _arrayFindIndex;
    private IsolatedScript _numberToString;
    private IsolatedScript _numberToStringRadix;
    private IsolatedScript _arrayLastIndexOf;
    private IsolatedScript _arrayFindLast;

    /// <summary>Builds a fresh engine carrying the <c>data</c>/<c>text</c>/<c>isThree</c> fixture every row uses.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(Setup);
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
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
    }

    private static IsolatedScript Prepare(string call)
        => IsolatedScript.Warm(
            Engine.PrepareScript("var r; for (var n = 0; n < " + OperationsPerInvoke + "; n++) { r = " + call + "; } r"),
            CreateEngine);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue StringIncludes() => _stringIncludes.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue StringEndsWith() => _stringEndsWith.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArrayIndexOf() => _arrayIndexOf.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArraySome() => _arraySome.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArrayFind() => _arrayFind.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ArrayFindIndex() => _arrayFindIndex.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue NumberToString() => _numberToString.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue NumberToStringRadix() => _numberToStringRadix.Run();

    /// <summary>Untouched sibling of <see cref="ArrayIndexOf"/>: still takes the raw argument array.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Control_ArrayLastIndexOf() => _arrayLastIndexOf.Run();

    /// <summary>Untouched sibling of <see cref="ArrayFind"/>: still takes the raw argument array.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Control_ArrayFindLast() => _arrayFindLast.Run();
}
