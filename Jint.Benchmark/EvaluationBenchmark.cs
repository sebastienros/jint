using BenchmarkDotNet.Attributes;
using Jurassic;

namespace Jint.Benchmark
{
    [MemoryDiagnoser]
    public class EvaluationBenchmark
    {
        private Engine sharedJint;
        private ScriptEngine sharedJurassic;

        private const string Script = @"
            var o = {};
            o.Foo = 'bar';
            o.Baz = 42.0001;
            o.Blah = o.Foo + o.Baz;

            if(o.Blah != 'bar42.0001') throw TypeError;

            function fib(n){
                if(n<2) {
                    return n;
                }

                return fib(n-1) + fib(n-2);
            }

            if(fib(3) != 2) throw TypeError;
        ";

        [GlobalSetup]
        public void Setup()
        {
            sharedJint = new Engine();
            sharedJurassic = new ScriptEngine();
        }

        [Params(10, 20)]
        public int Iterations { get; set; }

        [Params(true, false)]
        public bool ReuseEngine { get; set; }

        [Benchmark]
        public void Jint()
        {
            for (var i = 0; i < Iterations; i++)
            {
                var jint = ReuseEngine ? sharedJint : new Engine();
                jint.Execute(Script);
            }
        }

        [Benchmark]
        public void Jurassic()
        {
            for (var i = 0; i < Iterations; i++)
            {
                var jurassic = ReuseEngine ? sharedJurassic : new ScriptEngine();
                jurassic.Execute(Script);
            }
        }
    }
}