using System;
using System.IO;
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
            var assemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            BasePath = assemblyDirectory.Parent.Parent.FullName;

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

        protected void RunTest(string sourceFilename, bool negative)
        {
            var fullName = Path.Combine(BasePath, sourceFilename);
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }
            
            string code = File.ReadAllText(fullName);

            RunTestCode(code, negative);
            
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
