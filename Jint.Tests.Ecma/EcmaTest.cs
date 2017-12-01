using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jint.Tests.Ecma
{
    public class EcmaTest
    {
        private static string _lastError;
        private static string staSource;
        protected Action<string> Error = s => { _lastError = s; };
        protected string BasePath;

        public EcmaTest()
        {
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
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
            if (staSource == null)
            {
                var driverFilename = Path.Combine(BasePath, "TestCases\\sta.js");
                staSource = File.ReadAllText(driverFilename);
            }

            engine.Execute(staSource);

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

        [Theory(DisplayName = "Ecma")]
        [MemberData(nameof(SourceFiles), false)]
        [MemberData(nameof(SourceFiles), true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            var fullName = Path.Combine(BasePath, sourceFile.BasePath, sourceFile.Source);
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
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            var localPath = assemblyDirectory.Parent.Parent.Parent.FullName;

            var fixturesPath = Path.Combine(localPath, @"TestCases\alltests.json");

            try
            {
                var content = File.ReadAllText(fixturesPath);
                var doc = JArray.Parse(content);
                var results = new List<object[]>();
                var path = Path.Combine(localPath, "TestCases");

                foreach(JObject entry in doc)
                {
                    var sourceFile = new SourceFile(entry, path);
                    
                    if (skipped == sourceFile.Skip)
                    {
                        results.Add(new object [] { sourceFile });
                    }
                }

                return results;
            }
            catch
            {
                throw;
            }
        }

        public class SourceFile
        {
            public SourceFile(JObject node, string basePath)
            {
                Skip = node["skip"].Value<bool>();
                Source = node["source"].ToString();
                Reason = node["reason"].ToString();
                BasePath = basePath;
            }

            public string Source { get; set; }
            public bool Skip { get; set; }
            public string Reason { get; set; }
            public string BasePath { get; }

            public override string ToString()
            {
                return Source;
            }
        }
        
    }
}
