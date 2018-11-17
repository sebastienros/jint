using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Jint.Tests.Ecma
{
    public class RunnableInDebugOnlyAttribute : FactAttribute
    {
        public RunnableInDebugOnlyAttribute()
        {
            if (!Debugger.IsAttached)
            {
                Skip = "Only running in interactive mode.";
            }
        }
    }
    public class SingleTest : EcmaTest
    {
        // helper to test single test case
        [RunnableInDebugOnly]
        public void TestSingle()
        {
            const string Target = @"ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A3_T3.js";
            var sourceFile = SourceFiles(Target, false)
                .SelectMany(x => x)
                .Cast<SourceFile>()
                .Single();

            var code = File.ReadAllText(Path.Combine(@"..\..\..\TestCases", sourceFile.Source));
            RunTestCode(code, negative: false);
        }
    }
}