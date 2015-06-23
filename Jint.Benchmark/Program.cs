using System;
using System.Diagnostics;

namespace Jint.Benchmark
{
    class Program
    {
        private const string Script = @"
            var a = [];
            for ( var i = 0; i < 10000; i++ ) {
	            a[i] = i;
            }

            //var ret = [], tmp, num = 500, i = 1024;
 
            //for ( var j1 = 0; j1 < i * 15; j1++ ) {
	           // ret = [];
	           // ret.length = i;
            //}
 
            //for ( var j2 = 0; j2 < i * 10; j2++ ) {
            //	ret = new Array(i);
            //}
 
            //ret = [];
            //for ( var j3 = 0; j3 < i; j3++ ) {
            //	ret.unshift(j3);
            //}
 
            //ret = [];
            //for ( var j4 = 0; j4 < i; j4++ ) {
            //	ret.splice(0,0,j4);
            //}
 
            //var a = ret.slice();
            //for ( var j5 = 0; j5 < i; j5++ ) {
            //	tmp = a.shift();
            //}
 
            //var b = ret.slice();
            //for ( var j6 = 0; j6 < i; j6++ ) {
            //	tmp = b.splice(0,1);
            //}
 
            //ret = [];
            //for ( var j7 = 0; j7 < i * 25; j7++ ) {
            //	ret.push(j7);
            //}

            //var c = ret.slice();
            //for ( var j8 = 0; j8 < i * 25; j8++ ) {
            //	tmp = c.pop();
            //}
        ";

        static void Main()
        {
            const bool runIronJs = false;
            const bool runJint = true;
            const bool runJurassic = false;

            const int iterations = 5;
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
