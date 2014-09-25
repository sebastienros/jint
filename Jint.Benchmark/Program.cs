using System;
using System.Diagnostics;

namespace Jint.Benchmark
{
    class Program
    {
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

        static void Main()
        {
            const bool runIronJs = true;
            const bool runJint = true;
            const bool runJurassic = true;

            const int iterations = 1000;
            const bool reuseEngine = false;

            var watch = new Stopwatch();


            if (runIronJs)
            {
                IronJS.Hosting.CSharp.Context ironjs;
                ironjs = new IronJS.Hosting.CSharp.Context();
                ironjs.Execute(Script);
                watch.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    if (!reuseEngine)
                    {
                        ironjs = new IronJS.Hosting.CSharp.Context();
                    }

                    ironjs.Execute(Script);
                }

                Console.WriteLine("IronJs: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds);
            }

            if (runJint)
            {
                Engine jint;
                jint = new Engine();
                jint.Execute(Script);

                watch.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    if (!reuseEngine)
                    {
                        jint = new Jint.Engine();
                    }

                    jint.Execute(Script);
                }

                Console.WriteLine("Jint: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds);
            }

            if (runJurassic)
            {
                Jurassic.ScriptEngine jurassic;
                jurassic = new Jurassic.ScriptEngine();
                jurassic.Execute(Script);

                watch.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    if (!reuseEngine)
                    {
                        jurassic = new Jurassic.ScriptEngine();
                    }

                    jurassic.Execute(Script);
                }

                Console.WriteLine("Jurassic: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds);
            }
        }
    }
}
