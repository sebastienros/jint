using BenchmarkDotNet.Attributes;
using Esprima;

namespace Jint.Benchmark
{
    [MemoryDiagnoser]
    public class EngineConstructionBenchmark
    {
        private readonly Esprima.Ast.Program _program;

        public EngineConstructionBenchmark()
        {
            var parser = new JavaScriptParser("return [].length + ''.length");
            _program = parser.ParseProgram();
        }

        [Benchmark]
        public double BuildEngine()
        {
            var engine = new Engine();
            return engine.Execute(_program).GetCompletionValue().AsNumber();
        }
    }
}