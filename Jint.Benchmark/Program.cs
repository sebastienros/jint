using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Jint.Runtime;

namespace Jint.Benchmark
{
    class Program
    {
        private const string simple = @"
            var o = {};
            o.Foo = 'bar';
            o.Baz = 42;
            o.Blah = o.Foo + o.Baz;

            function fib(n){
                if(n<2) { 
                    return n; 
                }
    
                return fib(n-1) + fib(n-2);  
            }

            fib(3);
        ";

        private static readonly string[] Sunspider = new string[]
            {
                "https://gist.github.com/sebastienros/6274930/raw/bf0ab41e788eb9b978a29180f5cb4d614bc00dc5/gistfile1.js"
            };

        static void Main(string[] args)
        {
            bool runIronJs = true;
            bool runJint = true;
            bool runJurassic = true;

            const int iterations = 100;
            const bool reuseEngine = false;

            var watch = new Stopwatch();

            foreach (var url in Sunspider)
            {
                var script = new WebClient().DownloadString(url);

                // warming up engines
                Jurassic.ScriptEngine jurassic;
                if (runJurassic)
                {
                    jurassic = new Jurassic.ScriptEngine();
                    jurassic.Execute(script);
                }

                Jint.Engine jint;
                if (runJint)
                {
                    jint = new Jint.Engine();
                    jint.Execute(script);
                }

                IronJS.Hosting.CSharp.Context ironjs;
                if (runIronJs)
                {
                    ironjs = new IronJS.Hosting.CSharp.Context();
                    ironjs.Execute(script);
                }

                if (runIronJs)
                {
                    watch.Restart();
                    for (var i = 0; i < iterations; i++)
                    {
                        if (!reuseEngine)
                        {
                            ironjs = new IronJS.Hosting.CSharp.Context();
                        }

                        ironjs.Execute(script);
                    }

                    Console.WriteLine("{2} | IronJs: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds,
                                      Path.GetFileName(url));
                }

                if (runJint)
                {
                watch.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    if (!reuseEngine)
                    {
                        jint = new Jint.Engine();
                    }

                    jint.Execute(script);
                }

                Console.WriteLine("{2} | Jint: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds, Path.GetFileName(url));
                }

                if (runJurassic)
                {
                    watch.Restart();
                    for (var i = 0; i < iterations; i++)
                    {
                        if (!reuseEngine)
                        {
                            jurassic = new Jurassic.ScriptEngine();
                        }

                        jurassic.Execute(script);
                    }

                    Console.WriteLine("{2} | Jurassic: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds,
                                      Path.GetFileName(url));
                }
            }
        }
    }
}
