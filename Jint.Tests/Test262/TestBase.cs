using System;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests
{
    public class TestBase
    {
        private string _basePath;

        public TestBase(params string[] segments)
        {
            _basePath = @"C:\Users\sebros\Documents\My Projects\Jint\Jint.Tests\Scripts\Test262\suite\";
            _basePath = Path.Combine(_basePath, Path.Combine(segments));
        }

        public void Run(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var scriptPath = Path.Combine(_basePath, filename);
            string fileContent = File.ReadAllText(scriptPath);
            RunTest(fileContent);
        }

        private Action<string> ERROR = s => { throw new Exception(s); };

        public void RunTest(string source)
        {
            var engine = new Engine(cfg => cfg
                .WithDelegate("$ERROR", ERROR)
            );

            engine.Execute(source);
        }

       
    }
}
