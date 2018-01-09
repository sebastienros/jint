using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Jint;

namespace Esprima.Benchmark
{
    [Config(typeof(Config))]
    [MemoryDiagnoser]
    public class DromaeoBenchmark
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                // if Jint array performance gets better we can go towards defaul 16/16
                Add(Job.ShortRun.WithInvocationCount(4).WithUnrollFactor(4));
            }
        }

        private static readonly Dictionary<string, string> files = new Dictionary<string, string>
        {
            {"dromaeo-3d-cube", null},
            {"dromaeo-core-eval", null},
            {"dromaeo-object-array", null},
            {"dromaeo-object-regexp", null},
            {"dromaeo-object-string", null},
            {"dromaeo-string-base64", null}
        };

        private Engine engine;

        [GlobalSetup]
        public void Setup()
        {
            foreach (var fileName in files.Keys.ToList())
            {
                files[fileName] = File.ReadAllText($"Scripts/dromaeo/{fileName}.js");
            }

            engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(b => { }));

            engine.Execute(@"
var startTest = function () { };
var test = function (name, fn) { fn(); };
var endTest = function () { };
var prep = function (fn) { fn(); };
");
        }

        [ParamsSource(nameof(FileNames))]
        public string FileName { get; set; }

        public IEnumerable<string> FileNames()
        {
            foreach (var entry in files)
            {
                yield return entry.Key;
            }
        }

        [Benchmark]
        public void Run()
        {
            engine.Execute(files[FileName]);
        }
    }
}