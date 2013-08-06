using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Fluent.IO;

namespace Jint.Tests.Scaffolding
{
    class Program
    {
        private const string SpecsUrl = "http://www.ecma-international.org/ecma-262/5.1/";
        private static string _specs;
        private static Path _testPath ;
        private static Path _suitePath;

        static void Main(string[] args)
        {
            // load the local specification cache
            if (Path.Get("specs.html").Exists)
            {
                _specs = Path.Get("specs.html").Read();
            }
            else
            {
                _specs = new WebClient().DownloadString(SpecsUrl);
                System.IO.File.WriteAllText("specs.html", _specs);
            }

            var assemblyPath = typeof(Program).Assembly.Location;
            var assemblyDirectory = Path.Get(assemblyPath);
            _testPath = assemblyDirectory.Parent().Parent().Parent().Parent().Combine("Jint.Tests");
            _suitePath = _testPath.Combine("Test262", "suite");

            ProcessFolder(_suitePath);
        }

        static void ProcessFolder(Path path)
        {
            if (path.Files().Any())
            {
                // files are present, we need to create a test class

            }
        }

        static void ProcessFile(Path file)
        {
            
        }
    }
}
