using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark
{
    [MemoryDiagnoser]
    public abstract class SingleScriptBenchmark
    {
        private Engine sharedJint;

#if ENGINE_COMPARISON
        private Jurassic.ScriptEngine sharedJurassic;
        private NiL.JS.Core.Context sharedNilJs;
#endif

        protected abstract string Script { get; }

        [Params(10)]
        public virtual int N { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            sharedJint = new Engine();
#if ENGINE_COMPARISON
            sharedJurassic = new Jurassic.ScriptEngine();
            sharedNilJs = new NiL.JS.Core.Context();
#endif
        }

        [Benchmark]
        public bool Jint()
        {
            bool done = false;
            for (var i = 0; i < N; i++)
            {
                sharedJint.Execute(Script);
                done |= sharedJint.GetValue("done").AsBoolean();
            }

            return done;
        }

#if ENGINE_COMPARISON
        [Benchmark]
        public bool Jurassic()
        {
            bool done = false;
            for (var i = 0; i < N; i++)
            {
                sharedJurassic.Execute(Script);
                done |= sharedJurassic.GetGlobalValue<bool>("done");
            }

            return done;
        }

        [Benchmark]
        public bool NilJS()
        {
            bool done = false;
            for (var i = 0; i < N; i++)
            {
                sharedNilJs.Eval(Script);
                done |= (bool) sharedNilJs.GetVariable("done");
            }

            return done;
        }
#endif
    }
}