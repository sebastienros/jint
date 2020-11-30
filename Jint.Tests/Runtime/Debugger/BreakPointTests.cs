using Esprima;
using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class BreakPointTests
    {
        private Regex NewLines = new Regex("\r?\n");

        // To avoid issues with whitespace, and yet allow readable formatting of test scripts,
        // we trim the script (to remove leading/trailing empty lines), then trim each line to
        // remove indent.
        private string TrimLines(string script)
        {
            script = script.Trim();
            var lines = NewLines.Split(script);
            return String.Join("\n", lines.Select(line => line.Trim()));
        }

       [Fact]
        public void BreakPointBreaksAtPosition()
        {
            string script = @"
                let x = 1, y = 2;
                if (x === 1)
                {
                    x++; y *= 2;
                }";

            bool didBreak = false;
            var engine = new Engine(options => options.DebugMode(true));
            engine.Break += (sender, info) =>
            {
                Assert.Equal(4, info.CurrentStatement.Location.Start.Line);
                Assert.Equal(5, info.CurrentStatement.Location.Start.Column);
                didBreak = true;
                return StepMode.None;
            };

            engine.BreakPoints.Add(new BreakPoint(4, 5));
            engine.Execute(TrimLines(script));
            Assert.True(didBreak);
        }

        [Fact]
        public void BreakPointBreaksInCorrectSource()
        {
            string script1 = @"
                let x = 1, y = 2;
                if (x === 1)
                {
                    x++; y *= 2;
                }";

            string script2 = @"
                function test(x)
                {
                    return x + 2;
                }";

            string script3 = @"
                const z = 3;
                test(z);";

            bool didBreak = false;

            var engine = new Engine(options => { options.DebugMode(true); });
            
            engine.BreakPoints.Add(new BreakPoint("script2", 3, 0));

            engine.Break += (sender, info) =>
            {
                Assert.Equal("script2", info.CurrentStatement.Location.Source);
                Assert.Equal(3, info.CurrentStatement.Location.Start.Line);
                Assert.Equal(0, info.CurrentStatement.Location.Start.Column);
                didBreak = true;
                return StepMode.None;
            };

            // We need to specify the source to the parser.
            // And we need locations too (Jint specifies that in its default options)
            engine.Execute(TrimLines(script1), new ParserOptions("script1") { Loc = true });
            Assert.False(didBreak);

            engine.Execute(TrimLines(script2), new ParserOptions("script2") { Loc = true });
            Assert.False(didBreak);

            // Note that it's actually script3 that executes the function in script2
            // and triggers the breakpoint
            engine.Execute(TrimLines(script3), new ParserOptions("script3") { Loc = true });
            Assert.True(didBreak);
        }
    }
}
