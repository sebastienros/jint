using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Jint.Tests.Test262
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

    public class SingleTest : Test262Test
    {
        // helper to test single test case
        [RunnableInDebugOnly]
        public void TestSingle()
        {
            const string Target = @"built-ins/String/prototype/repeat/repeat-string-n-times.js";
            //const string Target = @"built-ins/Array/from/calling-from-valid-2.js";
            var sourceFile = SourceFiles("built-ins", false)
                .SelectMany(x => x)
                .Cast<SourceFile>()
                .First(x => x.Source == Target);

            var code = File.ReadAllText(sourceFile.FullPath);

            if (code.IndexOf("onlyStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(code, strict: false);
            }

            if (code.IndexOf("noStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(code, strict: true);
            }
        }
    }
}