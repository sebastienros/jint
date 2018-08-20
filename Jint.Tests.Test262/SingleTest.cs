using System.IO;
using System.Linq;
using Xunit;

namespace Jint.Tests.Test262
{
    public class SingleTest : Test262Test
    {
        // helper to test single test case
        [Fact]
        public void TestSingle()
        {
            var sourceFile = SourceFiles("built-ins", false)
                .SelectMany(x => x)
                .Cast<SourceFile>()
                .First(x => x.Source == @"built-ins\Set\prototype\keys\keys.js");

            var code = File.ReadAllText(sourceFile.FullPath);
            RunTestCode(code, true);
        }
    }
}