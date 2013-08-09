using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Jint.Parser;

namespace Jint.Benchmark
{
    class Program
    {
        private const string script = @"
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

        static void Main(string[] args)
        {
            const int iterations = 100000;
            const bool reuseEngine = false;

            var watch = new Stopwatch();

            // warming up engines
            var jurassic = new Jurassic.ScriptEngine();
            jurassic.Execute(script);

            var jint = new Jint.Engine();
            jint.Execute(script);

            var ironjs = new IronJS.Hosting.CSharp.Context();
            ironjs.Execute(script);

            watch.Restart();
            for (var i = 0; i < iterations; i++)
            {
                if (!reuseEngine)
                {
                    ironjs = new IronJS.Hosting.CSharp.Context();
                }

                jurassic.Execute(script);
            }

            Console.WriteLine("IronJs: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds);

            watch.Restart();
            for (var i = 0; i < iterations; i++)
            {
                if (!reuseEngine)
                {
                    jint = new Jint.Engine();
                }

                jint.Execute(script);
            }

            Console.WriteLine("Jint: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds);

            watch.Restart();
            for (var i = 0; i < iterations; i++)
            {
                if (!reuseEngine)
                {
                    jurassic = new Jurassic.ScriptEngine();
                }

                jurassic.Execute(script);
            }

            Console.WriteLine("Jurassic: {0} iterations in {1} ms", iterations, watch.ElapsedMilliseconds);
        }
    }
}
