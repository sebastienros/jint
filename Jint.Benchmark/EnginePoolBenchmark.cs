using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using Jint.Pooling;

namespace Jint.Benchmark
{
    [MemoryDiagnoser]
    public class EnginePoolBenchmark
    {
        private static readonly Options options = new Options().Strict();
        
        private static readonly List<string> files = new List<string>
        {
            "dromaeo-3d-cube",
            "dromaeo-core-eval",
            "dromaeo-object-array",
            "dromaeo-object-regexp",
            "dromaeo-object-string",
            "dromaeo-string-base64"
        };

        private IsolatedEngineFactory factory;
        private Engine _engine;

        [GlobalSetup]
        public void Setup()
        {
            factory = new IsolatedEngineFactory(options, InitializeEngine);

            _engine = new Engine(options);
            InitializeEngine(_engine);
        }

        /// <summary>
        /// Test case where we discard the pool and data.
        /// </summary>
        [Benchmark]
        public void RunUsingPooledIsolated()
        {
            using var engine = factory.Build();
            RunScript(engine);
        }

        /// <summary>
        /// Using one engine that isn't able to clean up.
        /// </summary>
        [Benchmark]
        public void RunUsingDirtiedEngineInstance()
        {
            RunScript(_engine);
        }

        /// <summary>
        /// Test case to do always the manual labour.
        /// </summary>
        [Benchmark]
        public void RunUsingNewEngineInstance()
        {
            var freshEngine = new Engine(options);
            InitializeEngine(freshEngine);
            RunScript(freshEngine);
        }

        private static void RunScript(IScriptExecutor engine)
        {
            for (int i = 5; i < 20; ++i)
            {
                engine.Evaluate($"Init({i});");
            }
        }

        private static void InitializeEngine(IEngine engine)
        {
            engine.SetValue("log", new Action<object>(Console.WriteLine));
            engine.SetValue("assert", new Action<bool>(b => { }));
            engine.SetValue("name", 123);
            engine.Evaluate(@"
                                    var startTest = function () { };
                                    var test = function (name, fn) { fn(); };
                                    var endTest = function () { };
                                    var prep = function (fn) { fn(); };
                    ");

            foreach (var fileName in files)
            {
                var content = File.ReadAllText($"Scripts/dromaeo/{fileName}.js");
                engine.Evaluate(content);
            }
        }
    }
}