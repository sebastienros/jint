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

            if(o.Blah != 'bar42') throw TypeError;

            function fib(n){
                if(n<2) { 
                    return n; 
                }
    
                return fib(n-1) + fib(n-2);  
            }

            if(fib(3) != 8) throw TypeError;
        ";

        private static readonly string[] Sunspider = new string[]
            {
                "https://gist.github.com/sebastienros/6274930/raw/bf0ab41e788eb9b978a29180f5cb4d614bc00dc5/gistfile1.js"
            };

        static void Main(string[] args)
        {
            const bool runIronJs = true;
            const bool runJint = true;
            const bool runJurassic = true;

            const int iterations = 1000;
            const bool reuseEngine = false;

            var watch = new Stopwatch();

            foreach (var url in Sunspider)
            {
                var script = new WebClient().DownloadString(url);


                if (runIronJs)
                {
                    IronJS.Hosting.CSharp.Context ironjs;
                    ironjs = new IronJS.Hosting.CSharp.Context();
                    ironjs.Execute(script);
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
                    Engine jint;
                    jint = new Engine();
                    jint.Execute(script);

                    watch.Restart();
                    for (var i = 0; i < iterations; i++)
                    {
                        if (!reuseEngine)
                        {
                            jint = new Jint.Engine();
                        }

                        jint.Execute(script);
                    }

                    Console.WriteLine("{2} | Jint: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds,
                                      Path.GetFileName(url));
                }

                if (runJurassic)
                {
                    Jurassic.ScriptEngine jurassic;
                    jurassic = new Jurassic.ScriptEngine();
                    jurassic.Execute(script);

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
