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

        [Theory(DisplayName = "Ecma")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch06\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.3\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.7\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.8\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch07\7.9\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.3\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.5\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.7\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.8\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch08\8.12\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.3\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.5\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.7\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.8\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch09\9.9\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch10\10.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch10\10.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch10\10.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch10\10.5\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch10\10.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.3\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.5\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.7\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.8\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.9\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.10\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.11\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.12\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.13\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch11\11.14\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.3\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.5\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.7\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.8\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.9\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.10\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.11\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.12\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.13\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch12\12.14\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch13\13.0\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch13\13.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch13\13.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch14\14.0\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch14\14.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.1\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.2\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.3\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.4\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.5\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.6\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.7\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.8\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.9\")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.10")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.11")]
        [MemberData(nameof(SourceFiles), @"TestCases\ch15\15.12")]
        protected void RunTest(string sourceFilename)
        {
            var fullName = Path.Combine(BasePath, sourceFilename);
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }

            string code = File.ReadAllText(fullName);
            var negative = code.Contains("@negative");

            RunTestCode(code, negative);

        }

        public static IEnumerable<object[]> SourceFiles(string relativePath)
        {
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            var root = assemblyDirectory.Parent.Parent.Parent.FullName;

            var fixturesPath = Path.Combine(root, relativePath);

            try
            {
                var files = Directory.GetFiles(fixturesPath, "*.js", SearchOption.AllDirectories);

                return files
                    .Select(x => new object[] { x })
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<object[]>();
            }
        }
    }
}
