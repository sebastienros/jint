using Esprima;
using Jint.Runtime.Debugger;
using System.Linq;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class BreakPointTests
    {
        [Fact]
        public void BreakLocationsCompareEqualityByValue()
        {
            var loc1 = new BreakLocation(42, 23);
            var loc2 = new BreakLocation(42, 23);
            var loc3 = new BreakLocation(17, 7);

            Assert.Equal(loc1, loc2);
            Assert.True(loc1 == loc2);
            Assert.True(loc2 != loc3);
            Assert.False(loc1 != loc2);
            Assert.False(loc2 == loc3);
        }

        [Fact]
        public void BreakLocationsWithSourceCompareEqualityByValue()
        {
            var loc1 = new BreakLocation("script1", 42, 23);
            var loc2 = new BreakLocation("script1", 42, 23);
            var loc3 = new BreakLocation("script2", 42, 23);

            Assert.Equal(loc1, loc2);
            Assert.True(loc1 == loc2);
            Assert.True(loc2 != loc3);
            Assert.False(loc1 != loc2);
            Assert.False(loc2 == loc3);
        }

        [Fact]
        public void BreakLocationsOptionalSourceEqualityComparer()
        {
            var script1 = new BreakLocation("script1", 42, 23);
            var script2 = new BreakLocation("script2", 42, 23);
            var script2b = new BreakLocation("script2", 44, 23);
            var any = new BreakLocation(null, 42, 23);

            var comparer = new OptionalSourceBreakLocationEqualityComparer();
            Assert.True(comparer.Equals(script1, any));
            Assert.True(comparer.Equals(script2, any));
            Assert.False(comparer.Equals(script1, script2));
            Assert.False(comparer.Equals(script2, script2b));
            Assert.Equal(comparer.GetHashCode(script1), comparer.GetHashCode(any));
            Assert.Equal(comparer.GetHashCode(script1), comparer.GetHashCode(script2));
            Assert.NotEqual(comparer.GetHashCode(script2), comparer.GetHashCode(script2b));
        }

        [Fact]
        public void BreakPointReplacesPreviousBreakPoint()
        {
            var engine = new Engine(options => options.DebugMode());

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
            Assert.Collection(engine.DebugHandler.BreakPoints,
                breakPoint =>
                {
                    Assert.Equal(4, breakPoint.Location.Line);
                    Assert.Equal(5, breakPoint.Location.Column);
                    Assert.Equal("i === 1", breakPoint.Condition);
                });

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(4, 5));
            Assert.Collection(engine.DebugHandler.BreakPoints,
                breakPoint =>
                {
                    Assert.Equal(4, breakPoint.Location.Line);
                    Assert.Equal(5, breakPoint.Location.Column);
                    Assert.Equal(null, breakPoint.Condition);
                });
        }

        [Fact]
        public void BreakPointRemovesBasedOnLocationEquality()
        {
            var engine = new Engine(options => options.DebugMode());

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
            engine.DebugHandler.BreakPoints.Set(new BreakPoint(5, 6, "j === 2"));
            engine.DebugHandler.BreakPoints.Set(new BreakPoint(10, 7, "x > 5"));
            Assert.Equal(3, engine.DebugHandler.BreakPoints.Count());

            engine.DebugHandler.BreakPoints.RemoveAt(new BreakLocation(null, 4, 5));
            engine.DebugHandler.BreakPoints.RemoveAt(new BreakLocation(null, 10, 7));

            Assert.Collection(engine.DebugHandler.BreakPoints,
                breakPoint =>
                {
                    Assert.Equal(5, breakPoint.Location.Line);
                    Assert.Equal(6, breakPoint.Location.Column);
                    Assert.Equal("j === 2", breakPoint.Condition);
                });
        }

        [Fact]
        public void BreakPointContainsBasedOnLocationEquality()
        {
            var engine = new Engine(options => options.DebugMode());

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
            engine.DebugHandler.BreakPoints.Set(new BreakPoint(5, 6, "j === 2"));
            engine.DebugHandler.BreakPoints.Set(new BreakPoint(10, 7, "x > 5"));
            Assert.True(engine.DebugHandler.BreakPoints.Contains(new BreakLocation(null, 5, 6)));
            Assert.False(engine.DebugHandler.BreakPoints.Contains(new BreakLocation(null, 8, 9)));
        }

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
                Assert.Equal(4, info.Location.Start.Line);
                Assert.Equal(5, info.Location.Start.Column);
                didBreak = true;
                return StepMode.None;
            };

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(4, 5));
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
            
            engine.DebugHandler.BreakPoints.Set(new BreakPoint("script2", 3, 0));

            bool didBreak = false;
            engine.DebugHandler.Break += (sender, info) =>
            {
                Assert.Equal("script2", info.Location.Source);
                Assert.Equal(3, info.Location.Start.Line);
                Assert.Equal(0, info.Location.Start.Column);
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

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(2, 0));

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

            engine.DebugHandler.BreakPoints.Set(new BreakPoint(4, 0));
            engine.DebugHandler.BreakPoints.Set(new BreakPoint(6, 0));

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
