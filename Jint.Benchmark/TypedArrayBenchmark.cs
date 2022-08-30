using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class TypedArrayBenchmark
{
    private const string script = @"
var testArray = new Int32Array([29, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63]);
";

    private Engine engine;


    [GlobalSetup]
    public void Setup()
    {
        engine = new Engine();
        engine.Execute(script);
    }

    [Params(100)]
    public int N { get; set; }

    [Benchmark]
    public void Slice()
    {
        for (var i = 0; i < N; ++i)
        {
            engine.Execute("testArray.slice();");
        }
    }

    [Benchmark]
    public void Concat()
    {
        // tests conversion performance as TypedArray does not have concat
        for (var i = 0; i < N; ++i)
        {
            engine.Execute("[].concat(testArray);");
        }
    }

    [Benchmark]
    public void Index()
    {
        for (var i = 0; i < N; ++i)
        {
            engine.Execute(@"
var obj2 = new Int32Array(testArray.length);
for (var i = 0, l = testArray.length; i < l; i++) {
  obj2[i] = testArray[i];
}
");
        }
    }

    [Benchmark]
    public void Map()
    {
        for (var i = 0; i < N; ++i)
        {
            engine.Execute(@"
var obj2 = testArray.map(function(i) {
  return i;
});
");
        }
    }

    [Benchmark]
    public void Apply()
    {
        for (var i = 0; i < N; ++i)
        {
            engine.Execute("Array.apply(undefined, testArray);");
        }
    }

    [Benchmark]
    public void JsonStringifyParse()
    {
        for (var i = 0; i < N; ++i)
        {
            engine.Execute("JSON.parse(JSON.stringify(testArray));");
        }
    }

    [Benchmark]
    public void FilterWithNumber()
    {
        for (var i = 0; i < N; ++i)
        {
            engine.Execute("testArray.filter(function(i) { return i > 55; });");
        }
    }
}