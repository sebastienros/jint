using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark
{
    [MemoryDiagnoser]
    public class ArrayBenchmark
    {
        private const string script = @"
var testArray = [29, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63, 27, 28, 838, 22, 2882, 2, 93, 84, 74, 7, 933, 3754, 3874, 22838, 38464, 3837, 82424, 2927, 2625, 63];
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
            for (int i = 0; i < N; ++i)
            {
                engine.Execute("testArray.slice();");
            }
        }

        [Benchmark]
        public void Concat()
        {
            for (int i = 0; i < N; ++i)
            {
                engine.Execute("[].concat(testArray);");
            }
        }

        [Benchmark]
        public void Unshift()
        {
            for (int i = 0; i < N; ++i)
            {
                engine.Execute(@"
var obj2 = [];
for (var i = testArray.length; i--;) {
    obj2.unshift(testArray[i]);
}
");
            }
        }

        [Benchmark]
        public void Push()
        {
            for (int i = 0; i < N; ++i)
            {
                engine.Execute(@"
var obj2 = [];
for (var i = 0, l = testArray.length; i < l; i++) {
  obj2.push(testArray[i]);
}
");
            }
        }

        [Benchmark]
        public void Index()
        {
            for (int i = 0; i < N; ++i)
            {
                engine.Execute(@"
var obj2 = new Array(testArray.length);
for (var i = 0, l = testArray.length; i < l; i++) {
  obj2[i] = testArray[i];
}
");
            }
        }

        [Benchmark]
        public void Map()
        {
            for (int i = 0; i < N; ++i)
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
            for (int i = 0; i < N; ++i)
            {
                engine.Execute("Array.apply(undefined, testArray);");
            }
        }

        [Benchmark]
        public void JsonStringifyParse()
        {
            for (int i = 0; i < N; ++i)
            {
                engine.Execute("JSON.parse(JSON.stringify(testArray));");
            }
        }
    }
}