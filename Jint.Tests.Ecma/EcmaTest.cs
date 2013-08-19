using System;
using System.IO;
using System.Reflection;
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

        private const string Driver = @"
            function runTestCase(f) {
                if(!f()) $ERROR('');
            }
        ";

        private const string NegativeDriver = @"
            function runTestCase(f) {
                if(f()) $ERROR('');
            }
        ";

        protected void RunTestCode(string code, bool negative)
        {
            _lastError = null; 
            
            var engine = new Engine(cfg => cfg
                    .WithDelegate("$ERROR", Error)
                );
            if (negative)
            {
                try
                {
                    engine.Execute(code + Environment.NewLine + NegativeDriver);
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
                Assert.DoesNotThrow(() => engine.Execute(code + Environment.NewLine + Driver));
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
