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

        [Params(true, false)]
        public bool ReuseEngine { get; set; }

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
                var jintEngine = ReuseEngine ? sharedJint : new Engine();
                jintEngine.Execute(Script);
                done |= jintEngine.GetValue("done").AsBoolean();
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
                var jurassicEngine = ReuseEngine ? sharedJurassic : new Jurassic.ScriptEngine();
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
                var nilcontext = ReuseEngine ? sharedNilJs : new NiL.JS.Core.Context();
                nilcontext.Eval(Script);
                done |= (bool) nilcontext.GetVariable("done");
            }

            return done;
        }
#endif
    }
}