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

        loc2.Should().Be(loc1);
        loc1.Should().Be(loc2);
        loc2.Should().NotBe(loc3);
        loc1.Should().Be(loc2);
        loc2.Should().NotBe(loc3);
    }

    [Fact]
    public void BreakLocationsWithSourceCompareEqualityByValue()
    {
        var loc1 = new BreakLocation("script1", 42, 23);
        var loc2 = new BreakLocation("script1", 42, 23);
        var loc3 = new BreakLocation("script2", 42, 23);

        loc2.Should().Be(loc1);
        loc1.Should().Be(loc2);
        loc2.Should().NotBe(loc3);
        loc1.Should().Be(loc2);
        loc2.Should().NotBe(loc3);
    }

    [Fact]
    public void BreakLocationsOptionalSourceEqualityComparer()
    {
        var script1 = new BreakLocation("script1", 42, 23);
        var script2 = new BreakLocation("script2", 42, 23);
        var script2b = new BreakLocation("script2", 44, 23);
        var any = new BreakLocation(null, 42, 23);

        var comparer = new OptionalSourceBreakLocationEqualityComparer();
        comparer.Equals(script1, any).Should().BeTrue();
        comparer.Equals(script2, any).Should().BeTrue();
        comparer.Equals(script1, script2).Should().BeFalse();
        comparer.Equals(script2, script2b).Should().BeFalse();
        comparer.GetHashCode(any).Should().Be(comparer.GetHashCode(script1));
        comparer.GetHashCode(script2).Should().Be(comparer.GetHashCode(script1));
        comparer.GetHashCode(script2b).Should().NotBe(comparer.GetHashCode(script2));
    }

    [Fact]
    public void BreakPointReplacesPreviousBreakPoint()
    {
        var engine = new Engine(options => options.DebugMode());

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
        engine.Debugger.BreakPoints.Should().SatisfyRespectively(
            breakPoint =>
            {
                breakPoint.Location.Line.Should().Be(4);
                breakPoint.Location.Column.Should().Be(5);
                breakPoint.Condition.Should().Be("i === 1");
            });

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5));
        engine.Debugger.BreakPoints.Should().SatisfyRespectively(
            breakPoint =>
            {
                breakPoint.Location.Line.Should().Be(4);
                breakPoint.Location.Column.Should().Be(5);
                breakPoint.Condition.Should().BeNull();
            });
    }

    [Fact]
    public void BreakPointRemovesBasedOnLocationEquality()
    {
        var engine = new Engine(options => options.DebugMode());

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 6, "j === 2"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(10, 7, "x > 5"));
        engine.Debugger.BreakPoints.Should().HaveCount(3);

        engine.Debugger.BreakPoints.RemoveAt(new BreakLocation(null, 4, 5));
        engine.Debugger.BreakPoints.RemoveAt(new BreakLocation(null, 10, 7));

        engine.Debugger.BreakPoints.Should().SatisfyRespectively(
            breakPoint =>
            {
                breakPoint.Location.Line.Should().Be(5);
                breakPoint.Location.Column.Should().Be(6);
                breakPoint.Condition.Should().Be("j === 2");
            });
    }

    [Fact]
    public void BreakPointContainsBasedOnLocationEquality()
    {
        var engine = new Engine(options => options.DebugMode());

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5, "i === 1"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 6, "j === 2"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(10, 7, "x > 5"));
        engine.Debugger.BreakPoints.Contains(new BreakLocation(null, 5, 6)).Should().BeTrue();
        engine.Debugger.BreakPoints.Contains(new BreakLocation(null, 8, 9)).Should().BeFalse();
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
            info.Location.Start.Line.Should().Be(4);
            info.Location.Start.Column.Should().Be(5);
            didBreak = true;
            return StepMode.None;
        };

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5));
        engine.Execute(script);
        didBreak.Should().BeTrue();
    }

    [Fact]
    public void BreakPointBreaksAtPositionWithPreparedScript()
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
            info.Location.Start.Line.Should().Be(4);
            info.Location.Start.Column.Should().Be(5);
            didBreak = true;
            return StepMode.None;
        };

        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 5));
        var prepared = Engine.PrepareScript(script);
        engine.Execute(prepared);
        didBreak.Should().BeTrue();
    }

    [Fact]
    public void BreakPointBreaksInCorrectSourceWithPreparedScript()
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
            info.Location.SourceFile.Should().Be("script2");
            info.Location.Start.Line.Should().Be(3);
            info.Location.Start.Column.Should().Be(0);
            didBreak = true;
            return StepMode.None;
        };

        engine.Execute(Engine.PrepareScript(script1, "script1"));
        didBreak.Should().BeFalse();

        engine.Execute(Engine.PrepareScript(script2, "script2"));
        didBreak.Should().BeFalse();

        engine.Execute(Engine.PrepareScript(script3, "script3"));
        didBreak.Should().BeTrue();
    }

    [Fact]
    public void BreakPointBreaksWithPreparedScriptDefaultSource()
    {
        string script = @"let x = 1;
x++;";

        var engine = new Engine(options => options.DebugMode());

        bool didBreak = false;
        engine.Debugger.Break += (sender, info) =>
        {
            info.Location.SourceFile.Should().Be("<anonymous>");
            didBreak = true;
            return StepMode.None;
        };

        engine.Debugger.BreakPoints.Set(new BreakPoint("<anonymous>", 2, 0));
        var prepared = Engine.PrepareScript(script);
        engine.Execute(prepared);
        didBreak.Should().BeTrue();
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
            info.Location.SourceFile.Should().Be("script2");
            info.Location.Start.Line.Should().Be(3);
            info.Location.Start.Column.Should().Be(0);
            didBreak = true;
            return StepMode.None;
        };

        // We need to specify the source to the parser.
        // And we need locations too (Jint specifies that in its default options)
        engine.Execute(script1, "script1");
        didBreak.Should().BeFalse();

        engine.Execute(script2, "script2");
        didBreak.Should().BeFalse();

        // Note that it's actually script3 that executes the function in script2
        // and triggers the breakpoint
        engine.Execute(script3, "script3");
        didBreak.Should().BeTrue();
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
            info.PauseType.Should().Be(PauseType.DebuggerStatement);
            didBreak = true;
            return StepMode.None;
        };

        engine.Execute(script);

        didBreak.Should().BeTrue();
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
        stepCount.Should().Be(3);
        didBreak.Should().BeFalse();
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
            info.PauseType.Should().Be(PauseType.Break);
            breakCount++;
            return StepMode.None;
        };

        engine.Execute(script);
        breakCount.Should().Be(1);
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
            TestHelpers.ReachedLiteral(info, "second breakpoint").Should().BeTrue();
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

        didStep.Should().BeTrue();
        didBreak.Should().BeTrue();
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

        breakTriggered.Should().Be(1);
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
                    info.ReachedLiteral("target").Should().BeTrue();
                    break;
            }
            return StepMode.None;
        };

        engine.Execute(script);

        step.Should().Be(2);
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
                engine.CallStack.Count.Should().Be(1);
                stepsReached++;
                return StepMode.None;
            }
            else if (info.ReachedLiteral("after breakpoint"))
            {
                engine.CallStack.Count.Should().Be(1);
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

        breakpointsReached.Should().Be(1);
        stepsReached.Should().Be(2);
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
            info.ReachedLiteral("breakpoint").Should().BeTrue();
            var extendedBreakPoint = info.BreakPoint.Should().BeOfType<SimpleHitConditionBreakPoint>().Which;
            extendedBreakPoint.HitCount++;
            if (extendedBreakPoint.HitCount == extendedBreakPoint.HitCondition)
            {
                // Here is where we would normally pause the execution.
                // the breakpoint is hit for the fifth time, when i is 4 (off by one)
                info.CurrentScopeChain[0].GetBindingValue("i").AsInteger().Should().Be(4);
                numberOfBreaks++;
            }
            return StepMode.None;
        };

        engine.Execute(script);

        numberOfBreaks.Should().Be(1);
    }
}