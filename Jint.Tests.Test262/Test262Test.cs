using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Reflection;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Test262
{
    public class Test262Test
    {
        private static string _lastError;
        private static string[] sources;
        protected Action<string> Error = s => { _lastError = s; };
        protected string BasePath;

        public Test262Test()
        {
            var assemblyPath = new Uri(typeof(Test262Test).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            BasePath = assemblyDirectory.Parent.Parent.Parent.FullName;
        }

        protected void RunTestCode(string code, bool negative)
        {
            _lastError = null;

            //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
            var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var engine = new Engine(cfg => cfg.LocalTimeZone(pacificTimeZone));

            // loading driver
            if (sources == null)
            {
                string[] files =
                {
                    @"harness\sta.js",
                    @"harness\assert.js",
                    @"harness\propertyHelper.js",
                };

                sources = new string[files.Length];
                for (var i = 0; i < files.Length; i++)
                {
                    sources[i] = File.ReadAllText(Path.Combine(BasePath, files[i]));
                }
            }

            for (int i = 0; i < sources.Length; ++i)
            {
                engine.Execute(sources[i]);
            }

            if (negative)
            {
                try
                {
                    engine.Execute(code);
                    Assert.True(_lastError != null);
                    Assert.False(true);
                }
                catch
                {
                    // exception is expected
                }
            }
            else
            {
                try
                {
                    engine.Execute(code);
                }
                catch (JavaScriptException j)
                {
                    _lastError = TypeConverter.ToString(j.Error);
                }
                catch (Exception e)
                {
                    _lastError = e.ToString();
                }

                Assert.Null(_lastError);
            }
        }

        [Theory(DisplayName = "Test256")]
        [MemberData(nameof(SourceFiles), false)]
        [MemberData(nameof(SourceFiles), true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            var fullName = sourceFile.FullPath;
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }

            string code = File.ReadAllText(fullName);
            var negative = code.Contains("@negative");

            RunTestCode(code, negative);
        }

        public static IEnumerable<object[]> SourceFiles(bool skipped)
        {
            var assemblyPath = new Uri(typeof(Test262Test).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            var localPath = assemblyDirectory.Parent.Parent.Parent.FullName;

            // which tests to include, narrower scope and selectively adding tests
            var paths = new[]
            {
                "built-ins\\Map"
            };


            var results = new List<object[]>();
            var fixturesPath = Path.Combine(localPath, "test");
            foreach (var path in paths)
            {
                var searchPath = Path.Combine(fixturesPath, path);
                var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var sourceFile = new SourceFile(
                        path + file.Replace(searchPath, ""),
                        file,
                        skip: false,
                        reason: "");

                    if (skipped == sourceFile.Skip)
                    {
                        results.Add(new object[]
                        {
                            sourceFile
                        });
                    }
                }
            }

            return results;
        }

        public class SourceFile
        {
            public SourceFile(
                string source,
                string fullPath,
                bool skip,
                string reason)
            {
                Skip = skip;
                Source = source;
                Reason = reason;
                FullPath = fullPath;
            }

            public string Source { get; set; }
            public bool Skip { get; set; }
            public string Reason { get; set; }
            public string FullPath { get; }

            public override string ToString()
            {
                return Source;
            }
        }
    }
}