using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArrayConcatBenchmark
{
    private const string SetupScript = @"
function makeArr(n) {
    var a = new Array(n);
    for (var i = 0; i < n; i++) a[i] = i;
    return a;
}
var a500 = makeArr(500);
var b500 = makeArr(500);
var a100 = makeArr(100);
var b100 = makeArr(100);
var c100 = makeArr(100);
var d100 = makeArr(100);
var e100 = makeArr(100);
";

    private Engine _engine = null!;
    private Prepared<Script> _twoArrays;
    private Prepared<Script> _fiveArrays;
    private Prepared<Script> _withScalar;
    private Prepared<Script> _withPlainObject;
    private Prepared<Script> _withSpreadableObject;
    private Prepared<Script> _withHoleyArray;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupScript);
        _engine.Execute("""
var spreadable100 = { length: 100 };
spreadable100[Symbol.isConcatSpreadable] = true;
for (var i = 0; i < 100; i++) spreadable100[i] = i;
var holey500 = makeArr(500);
delete holey500[100];
delete holey500[200];
""");

        _twoArrays = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { a500.concat(b500); }");
        _fiveArrays = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { a100.concat(b100, c100, d100, e100); }");
        _withScalar = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { a500.concat('x', b500); }");
        // the following all bail the all-JsArray fast path into the slow/builder path
        _withPlainObject = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { a500.concat({}); }");
        _withSpreadableObject = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { a500.concat(spreadable100); }");
        _withHoleyArray = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { a500.concat(holey500, {}); }");
    }

    [Benchmark]
    public void Concat_TwoArrays_500() => _engine.Execute(_twoArrays);

    [Benchmark]
    public void Concat_FiveArrays_100() => _engine.Execute(_fiveArrays);

    [Benchmark]
    public void Concat_WithScalar() => _engine.Execute(_withScalar);

    [Benchmark]
    public void Concat_WithPlainObject() => _engine.Execute(_withPlainObject);

    [Benchmark]
    public void Concat_WithSpreadableObject() => _engine.Execute(_withSpreadableObject);

    [Benchmark]
    public void Concat_WithHoleyArray() => _engine.Execute(_withHoleyArray);
}
