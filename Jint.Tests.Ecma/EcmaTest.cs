using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Jint.Tests
{
    public class EcmaTest
    {
        private static string _lastError;
        protected Action<string> Error = s => { _lastError = s; };
        protected string basePath;

        public EcmaTest()
        {
            var assemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            basePath = assemblyDirectory.Parent.Parent.FullName;

        }

        private const string Driver = @"
            function runTestCase(f) {
                assert(f());
            }
        ";

        private const string NegativeDriver = @"
            function runTestCase(f) {
                assert(!f());
            }
        ";

        protected void RunTest(string sourceFilename, bool negative)
        {
            _lastError = null;

            var fullName = Path.Combine(basePath, sourceFilename);
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }
            
            string code = File.ReadAllText(fullName);

            var engine = new Engine(cfg => cfg
                    .WithDelegate("$ERROR", Error)
                );
            if (negative)
            {
                try
                {
                    engine.Execute(code + Environment.NewLine + NegativeDriver);
                    Assert.NotNull(_lastError);
                    Assert.False(true);
                }
                catch
                {
                    
                }
                
            }
            else
            {
                Assert.DoesNotThrow(() => engine.Execute(code + Environment.NewLine + Driver));
                Assert.Null(_lastError);
            }
        }
    }
}
