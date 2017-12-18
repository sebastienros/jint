using BenchmarkDotNet.Attributes;
using Jurassic;
using NiL.JS.Core;

namespace Jint.Benchmark
{
    [MemoryDiagnoser]
    public abstract class SingleScriptBenchmark
    {
        private Engine sharedJint;
        private ScriptEngine sharedJurassic;
        private Context sharedNilJs;

        protected abstract string Script { get; }

        [Params(1)]
        public virtual int N { get; set; }

        [Params(true, false)]
        public bool ReuseEngine { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            sharedJint = new Engine();
            sharedJurassic = new ScriptEngine();
            sharedNilJs = new Context();
        }

        [Benchmark]
        public bool Jint()
        {
            bool done = false;
            for (var i = 0; i < N; i++)
            {
                var jintEngine = ReuseEngine ? sharedJint : new Engine();
                jintEngine.Execute(Script);
                done |= jintEngine.GetValue("done").AsBoolean();
            }

            return done;
        }

        [Benchmark]
        public bool Jurassic()
        {
            bool done = false;
            for (var i = 0; i < N; i++)
            {
                var jurassicEngine = ReuseEngine ? sharedJurassic : new ScriptEngine();
                jurassicEngine.Execute(Script);
                done |= jurassicEngine.GetGlobalValue<bool>("done");
            }

            return done;
        }

        [Benchmark]
        public bool NilJS()
        {
            bool done = false;
            for (var i = 0; i < N; i++)
            {
                var nilcontext = ReuseEngine ? sharedNilJs : new Context();
                nilcontext.Eval(Script);
                done |= (bool) nilcontext.GetVariable("done");
            }

            return done;
        }
    }
}