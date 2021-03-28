using Esprima;
using Jint.Runtime.Debugger;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class BreakPointTests
    {
       [Fact]
        public void BreakPointBreaksAtPosition()
        {
            string script = @"let x = 1, y = 2;
if (x === 1)
{
x++; y *= 2;
}";

            var engine = new Engine(options => options.DebugMode());

            bool didBreak = false;
            engine.DebugHandler.Break += (sender, info) =>
            {
                Assert.Equal(4, info.CurrentStatement.Location.Start.Line);
                Assert.Equal(5, info.CurrentStatement.Location.Start.Column);
                didBreak = true;
                return StepMode.None;
            };

            engine.DebugHandler.BreakPoints.Add(new BreakPoint(4, 5));
            engine.Execute(script);
            Assert.True(didBreak);
        }

        [Fact]
        public void BreakPointBreaksInCorrectSource()
        {
            string script1 = @"let x = 1, y = 2;
if (x === 1)
{
x++; y *= 2;
}";

            string script2 = @"function test(x)
{
return x + 2;
}";

            string script3 = @"const z = 3;
test(z);";

            var engine = new Engine(options => { options.DebugMode(); });
            
            engine.DebugHandler.BreakPoints.Add(new BreakPoint("script2", 3, 0));

            bool didBreak = false;
            engine.DebugHandler.Break += (sender, info) =>
            {
                Assert.Equal("script2", info.CurrentStatement.Location.Source);
                Assert.Equal(3, info.CurrentStatement.Location.Start.Line);
                Assert.Equal(0, info.CurrentStatement.Location.Start.Column);
                didBreak = true;
                return StepMode.None;
            };

            // We need to specify the source to the parser.
            // And we need locations too (Jint specifies that in its default options)
            engine.Execute(script1, new ParserOptions("script1"));
            Assert.False(didBreak);

            engine.Execute(script2, new ParserOptions("script2"));
            Assert.False(didBreak); 

            // Note that it's actually script3 that executes the function in script2
            // and triggers the breakpoint
            engine.Execute(script3, new ParserOptions("script3"));
            Assert.True(didBreak);
        }

        [Fact]
        public void DebuggerStatementTriggersBreak()
        {
            string script = @"'dummy';
debugger;
'dummy';";

            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.DebugHandler.Break += (sender, info) =>
            {
                didBreak = true;
                return StepMode.None;
            };

            engine.Execute(script);

            Assert.True(didBreak);
        }

        [Fact(Skip = "Non-source breakpoint is triggered before Statement, while debugger statement is now triggered by ExecuteInternal")]
        public void DebuggerStatementAndBreakpointTriggerSingleBreak()
        {
            string script = @"'dummy';
debugger;
'dummy';";

            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            engine.DebugHandler.BreakPoints.Add(new BreakPoint(2, 0));

            int breakTriggered = 0;
            engine.DebugHandler.Break += (sender, info) =>
            {
                breakTriggered++;
                return StepMode.None;
            };

            engine.Execute(script);

            Assert.Equal(1, breakTriggered);
        }

        [Fact]
        public void BreakpointOverridesStepOut()
        {
            string script = @"function test()
{
'dummy';
'source';
'dummy';
'target';
}
test();";

            var engine = new Engine(options => options.DebugMode());

            engine.DebugHandler.BreakPoints.Add(new BreakPoint(4, 0));
            engine.DebugHandler.BreakPoints.Add(new BreakPoint(6, 0));

            int step = 0;
            engine.DebugHandler.Break += (sender, info) =>
            {
                step++;
                switch (step)
                {
                    case 1:
                        return StepMode.Out;
                    case 2:
                        Assert.True(info.ReachedLiteral("target"));
                        break;
                }
                return StepMode.None;
            };

            engine.Execute(script);

            Assert.Equal(2, step);
        }
    }
}
