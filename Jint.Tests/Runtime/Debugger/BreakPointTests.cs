using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

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

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
        Assert.Collection(engine.Debugger.BreakPoints,
            breakPoint =>
            {
                Assert.Equal(4, breakPoint.Location.Line);
                Assert.Equal(5, breakPoint.Location.Column);
                Assert.Equal("i === 1", breakPoint.Condition);
            });

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5));
        Assert.Collection(engine.Debugger.BreakPoints,
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

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 6, "j === 2"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(10, 7, "x > 5"));
        Assert.Equal(3, engine.Debugger.BreakPoints.Count);

        engine.Debugger.BreakPoints.RemoveAt(new BreakLocation(null, 4, 5));
        engine.Debugger.BreakPoints.RemoveAt(new BreakLocation(null, 10, 7));

        Assert.Collection(engine.Debugger.BreakPoints,
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

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 6, "j === 2"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(10, 7, "x > 5"));
        Assert.True(engine.Debugger.BreakPoints.Contains(new BreakLocation(null, 5, 6)));
        Assert.False(engine.Debugger.BreakPoints.Contains(new BreakLocation(null, 8, 9)));
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
        engine.Debugger.Break += (sender, info) =>
        {
            Assert.Equal(4, info.Location.Start.Line);
            Assert.Equal(5, info.Location.Start.Column);
            didBreak = true;
            return StepMode.None;
        };

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5));
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

        engine.Debugger.BreakPoints.Set(new BreakPoint("script2", 3, 0));

        bool didBreak = false;
        engine.Debugger.Break += (sender, info) =>
        {
            Assert.Equal("script2", info.Location.SourceFile);
            Assert.Equal(3, info.Location.Start.Line);
            Assert.Equal(0, info.Location.Start.Column);
            didBreak = true;
            return StepMode.None;
        };

        // We need to specify the source to the parser.
        // And we need locations too (Jint specifies that in its default options)
        engine.Execute(script1, "script1");
        Assert.False(didBreak);

        engine.Execute(script2, "script2");
        Assert.False(didBreak);

        // Note that it's actually script3 that executes the function in script2
        // and triggers the breakpoint
        engine.Execute(script3, "script3");
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
        engine.Debugger.Break += (sender, info) =>
        {
            Assert.Equal(PauseType.DebuggerStatement, info.PauseType);
            didBreak = true;
            return StepMode.None;
        };

        engine.Execute(script);

        Assert.True(didBreak);
    }

    [Fact]
    public void DebuggerStatementDoesNotTriggerBreakWhenStepping()
    {
        string script = @"'dummy';
debugger;
'dummy';";

        var engine = new Engine(options => options
            .DebugMode()
            .DebuggerStatementHandling(DebuggerStatementHandling.Script)
            .InitialStepMode(StepMode.Into));

        bool didBreak = false;
        int stepCount = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            didBreak = true;
            return StepMode.None;
        };

        engine.Debugger.Step += (sender, info) =>
        {
            stepCount++;
            return StepMode.Into;
        };

        engine.Execute(script);
        Assert.Equal(3, stepCount);
        Assert.False(didBreak);
    }

    [Fact]
    public void DebuggerStatementDoesNotTriggerBreakWhenAtBreakPoint()
    {
        string script = @"'dummy';
debugger;
'dummy';";

        var engine = new Engine(options => options
            .DebugMode()
            .DebuggerStatementHandling(DebuggerStatementHandling.Script)
            .InitialStepMode(StepMode.None));

        int breakCount = 0;

        engine.Debugger.BreakPoints.Set(new BreakPoint(2, 0));

        engine.Debugger.Break += (sender, info) =>
        {
            Assert.Equal(PauseType.Break, info.PauseType);
            breakCount++;
            return StepMode.None;
        };

        engine.Execute(script);
        Assert.Equal(1, breakCount);
    }

    [Fact]
    public void BreakPointDoesNotTriggerBreakWhenStepping()
    {
        string script = @"
'first breakpoint';
'dummy';
'second breakpoint';";

        var engine = new Engine(options => options
            .DebugMode()
            .InitialStepMode(StepMode.Into));

        bool didStep = true;
        bool didBreak = true;

        engine.Debugger.BreakPoints.Set(new BreakPoint(2, 0));
        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 0));

        engine.Debugger.Break += (sender, info) =>
        {
            didBreak = true;
            // first breakpoint shouldn't cause us to get here, because we're stepping,
            // but when we reach the second, we're running:
            Assert.True(TestHelpers.ReachedLiteral(info, "second breakpoint"));
            return StepMode.None;
        };

        engine.Debugger.Step += (sender, info) =>
        {
            didStep = true;
            if (TestHelpers.ReachedLiteral(info, "first breakpoint"))
            {
                // Run from here
                return StepMode.None;
            }
            return StepMode.Into;
        };

        engine.Execute(script);

        Assert.True(didStep);
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

        engine.Debugger.BreakPoints.Set(new BreakPoint(2, 0));

        int breakTriggered = 0;
        engine.Debugger.Break += (sender, info) =>
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

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 0));
        engine.Debugger.BreakPoints.Set(new BreakPoint(6, 0));

        int step = 0;
        engine.Debugger.Break += (sender, info) =>
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

    [Fact]
    public void ErrorInConditionalBreakpointLeavesCallStackAlone()
    {
        string script = @"
function foo()
{
let x = 0;
'before breakpoint';
'breakpoint 1 here';
'breakpoint 2 here';
'after breakpoint';
}

foo();
";
        var engine = new Engine(options => options.DebugMode().InitialStepMode(StepMode.Into));

        int stepsReached = 0;
        int breakpointsReached = 0;

        // This breakpoint will be hit:
        engine.Debugger.BreakPoints.Set(new BreakPoint(6, 0, "x == 0"));
        // This condition is an error (y is not defined). DebugHandler will
        // treat it as an unmatched breakpoint:
        engine.Debugger.BreakPoints.Set(new BreakPoint(7, 0, "y == 0"));

        engine.Debugger.Step += (sender, info) =>
        {
            if (info.ReachedLiteral("before breakpoint"))
            {
                Assert.Equal(1, engine.CallStack.Count);
                stepsReached++;
                return StepMode.None;
            }
            else if (info.ReachedLiteral("after breakpoint"))
            {
                Assert.Equal(1, engine.CallStack.Count);
                stepsReached++;
                return StepMode.None;
            }
            return StepMode.Into;
        };

        engine.Debugger.Break += (sender, info) =>
        {
            breakpointsReached++;
            return StepMode.Into;
        };

        engine.Execute(script);

        Assert.Equal(1, breakpointsReached);
        Assert.Equal(2, stepsReached);
    }

    private class SimpleHitConditionBreakPoint : BreakPoint
    {
        public SimpleHitConditionBreakPoint(int line, int column, string condition = null,
            int? hitCondition = null) : base(line, column, condition)
        {
            HitCondition = hitCondition;
        }

        public int HitCount { get; set; }
        public int? HitCondition { get; set; }
    }

    [Fact]
    public void BreakPointCanBeExtended()
    {
        // More of a documentation than a required test, this shows the usefulness of BreakPoint being
        // extensible - as a test, at least it ensures that it is.
        var script = @"
for (let i = 0; i < 10; i++)
{
    'breakpoint';
}
";
        var engine = new Engine(options => options.DebugMode().InitialStepMode(StepMode.None));

        engine.Debugger.BreakPoints.Set(
            new SimpleHitConditionBreakPoint(4, 4, condition: null, hitCondition: 5));

        int numberOfBreaks = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            Assert.True(info.ReachedLiteral("breakpoint"));
            var extendedBreakPoint = Assert.IsType<SimpleHitConditionBreakPoint>(info.BreakPoint);
            extendedBreakPoint.HitCount++;
            if (extendedBreakPoint.HitCount == extendedBreakPoint.HitCondition)
            {
                // Here is where we would normally pause the execution.
                // the breakpoint is hit for the fifth time, when i is 4 (off by one)
                Assert.Equal(4, info.CurrentScopeChain[0].GetBindingValue("i").AsInteger());
                numberOfBreaks++;
            }
            return StepMode.None;
        };

        engine.Execute(script);

        Assert.Equal(1, numberOfBreaks);
    }
}