using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Ecma
{
    public class EcmaTest
    {
        private static string _lastError;
        protected Action<string> Error = s => { _lastError = s; };
        protected string BasePath;

        public EcmaTest()
        {
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

#if NET451
            BasePath = assemblyDirectory.Parent.Parent.Parent.Parent.FullName;
#else
            BasePath = assemblyDirectory.Parent.Parent.Parent.FullName;
#endif
        }

        protected void RunTestCode(string code, bool negative)
        {
            _lastError = null;

            //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
            var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var engine = new Engine(cfg => cfg.LocalTimeZone(pacificTimeZone));

            // loading driver

            var driverFilename = Path.Combine(BasePath, "TestCases\\sta.js");
            engine.Execute(File.ReadAllText(driverFilename));

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

        [Theory]
        [MemberData(nameof(SourceFiles), "TestCases")]
        protected void RunTest(string sourceFilename)
        {
            var negative = Path.GetFileNameWithoutExtension(sourceFilename).EndsWith("gs");

            var fullName = Path.Combine(BasePath, sourceFilename);
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }

            string code = File.ReadAllText(fullName);

            RunTestCode(code, negative);

        }

        public static IEnumerable<object[]> SourceFiles(string relativePath)
        {
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            var root = assemblyDirectory.Parent.Parent.Parent.FullName;
            var fixturesPath = Path.Combine(root, relativePath);

            var files = Directory.GetFiles(fixturesPath, "*.js", SearchOption.AllDirectories);

            return files
                .Select(x => new object[] { x })
                .ToList();
        }
    }

    public class EcmaTestTests : EcmaTest
    {
        [Fact]
        public void EcmaTestPassSucceededTestCase()
        {
            RunTestCode(@"
                function testcase() {
                        return true;
                    }
                runTestCase(testcase);
            ", false);
        }

        [Fact]
        public void EcmaTestPassNegativeTestCase()
        {
            RunTestCode(@"
                function testcase() {
                        return false;
                    }
                runTestCase(testcase);
            ", true);
        }


    }
}
