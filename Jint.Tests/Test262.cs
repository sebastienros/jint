using System;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests
{
    public class Test262
    {
        private string _basePath;

        public Test262()
        {
            _basePath = @"C:\Users\sebros\Documents\My Projects\Jint\Jint.Tests\suite\"; // typeof (Test262).Assembly.Location;
        }

        private void Run(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var scriptPath = Path.Combine(_basePath, filename);
            string fileContent = File.ReadAllText(scriptPath);
            RunTest(fileContent);
        }

        private Action<string> ERROR = s => { throw new Exception(s); };

        private void RunTest(string source)
        {
            var engine = new Engine(cfg => cfg
                .WithDelegate("$ERROR", ERROR)
            );

            engine.Execute(source);
        }

        [Theory]
        [Trait("Description", "Test for handling of supplementary characters")]
        [InlineData("ch06/6.1.js")]
        public void TestForHandlingSupplimentaryCharacters(string file)
        {
            Run(file);
        }
    }
}
