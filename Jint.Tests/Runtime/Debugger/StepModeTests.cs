using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class StepModeTests
{
    /// <summary>
    /// Helper method to keep tests independent of line numbers, columns or other arbitrary assertions on
    /// the current statement. Steps through script with StepMode.Into until it reaches literal statement
    /// (or directive) 'source'. Then counts the steps needed to reach 'target' using the indicated StepMode.
    /// </summary>
    /// <param name="script">Script used as basis for test</param>
    /// <param name="stepMode">StepMode to use from source to target</param>
    /// <returns>Number of steps from source to target</returns>
    private static int StepsFromSourceToTarget(string script, StepMode stepMode)
    {
        var engine = new Engine(options => options
            .DebugMode()
            .InitialStepMode(StepMode.Into)
            .DebuggerStatementHandling(DebuggerStatementHandling.Script));

        int steps = 0;
        bool sourceReached = false;
        bool targetReached = false;
        engine.Debugger.Step += (sender, info) =>
        {
            if (sourceReached)
            {
                steps++;
                if (info.ReachedLiteral("target"))
                {
                    // Stop stepping
                    targetReached = true;
                    return StepMode.None;
                }
                return stepMode;
            }
            else if (info.ReachedLiteral("source"))
            {
                sourceReached = true;
                return stepMode;
            }
            return StepMode.Into;
        };

        engine.Execute(script);
            
        // Make sure we actually reached the target
        targetReached.Should().BeTrue();

        return steps;
    }

    [Fact]
    public void StepsIntoRegularFunctionCall()
    {
        var script = @"
                'source';
                test(); // first step
                function test()
                {
                    'target'; // second step
                }";

        StepsFromSourceToTarget(script, StepMode.Into).Should().Be(2);
    }

    [Fact]
    public void StepsOverRegularFunctionCall()
    {
        var script = @"
                'source';
                test();
                'target';
                function test()
                {
                    'dummy';
                }";

        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(2);
    }

    [Fact]
    public void StepsOutOfRegularFunctionCall()
    {
        var script = @"
                test();
                'target';

                function test()
                {
                    'source';
                    'dummy';
                }";

        StepsFromSourceToTarget(script, StepMode.Out).Should().Be(1);
    }

    [Fact]
    public void StepsIntoMemberFunctionCall()
    {
        var script = @"
                const obj = {
                    test()
                    {
                        'target'; // second step
                    }
                };
                'source';
                obj.test(); // first step";

        StepsFromSourceToTarget(script, StepMode.Into).Should().Be(2);
    }

    [Fact]
    public void StepsOverMemberFunctionCall()
    {
        var script = @"
                const obj = {
                    test()
                    {
                        'dummy';
                    }
                };
                'source';
                obj.test();
                'target';";

        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(2);
    }

    [Fact]
    public void StepsOutOfMemberFunctionCall()
    {
        var script = @"
                const obj = {
                    test()
                    {
                        'source';
                        'dummy';
                    }
                };
                obj.test();
                'target';";

        StepsFromSourceToTarget(script, StepMode.Out).Should().Be(1);
    }

    [Fact]
    public void StepsIntoCallExpression()
    {
        var script = @"
                function test()
                {
                    'target'; // second step
                    return 42;
                }
                'source';
                const x = test(); // first step";

        StepsFromSourceToTarget(script, StepMode.Into).Should().Be(2);
    }

    [Fact]
    public void StepsOverCallExpression()
    {
        var script = @"
                function test()
                {
                    'dummy';
                    return 42;
                }
                'source';
                const x = test();
                'target';";

        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(2);
    }

    [Fact]
    public void StepsOutOfCallExpression()
    {
        var script = @"
                function test()
                {
                    'source';
                    'dummy';
                    return 42;
                }
                const x = test();
                'target';";

        StepsFromSourceToTarget(script, StepMode.Out).Should().Be(1);
    }

    [Fact]
    public void StepsIntoGetAccessor()
    {
        var script = @"
                const obj = {
                    get test()
                    {
                        'target'; // second step
                        return 144;
                    }
                };
                'source';
                const x = obj.test; // first step";

        StepsFromSourceToTarget(script, StepMode.Into).Should().Be(2);
    }

    [Fact]
    public void StepsOverGetAccessor()
    {
        var script = @"
                const obj = {
                    get test()
                    {
                        return 144;
                    }
                };
                'source';
                const x = obj.test;
                'target';";

        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(2);
    }

    [Fact]
    public void StepsOutOfGetAccessor()
    {
        var script = @"
                const obj = {
                    get test()
                    {
                        'source';
                        'dummy';
                        return 144;
                    }
                };
                const x = obj.test;
                'target';";

        StepsFromSourceToTarget(script, StepMode.Out).Should().Be(1);
    }

    [Fact]
    public void StepsIntoSetAccessor()
    {
        var script = @"
                const obj = {
                    set test(value)
                    {
                        'target'; // second step
                        this.value = value;
                    }
                };
                'source';
                obj.test = 37; // first step";

        StepsFromSourceToTarget(script, StepMode.Into).Should().Be(2);
    }

    [Fact]
    public void StepsOverSetAccessor()
    {
        var script = @"
                const obj = {
                    set test(value)
                    {
                        this.value = value;
                    }
                };
                'source';
                obj.test = 37;
                'target';";

        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(2);
    }

    [Fact]
    public void StepsOutOfSetAccessor()
    {
        var script = @"
                const obj = {
                    set test(value)
                    {
                        'source';
                        'dummy';
                        this.value = value;
                    }
                };
                obj.test = 37;
                'target';";

        StepsFromSourceToTarget(script, StepMode.Out).Should().Be(1);
    }

    [Fact]
    public void ReturnPointIsAStep()
    {
        var script = @"
                function test()
                {
                    'source';
                }
                test();
                'target';";
        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(2);
    }

    [Fact]
    public void ReturnStatementIsAStep()
    {
        var script = @"
                function test()
                {
                    'source';
                    return 'result';
                }
                test();
                'target';";
        StepsFromSourceToTarget(script, StepMode.Over).Should().Be(3);
    }

    [Fact]
    public void StepOutOnlyStepsOutOneStackLevel()
    {
        var script = @"
                function test()
                {
                    'dummy';
                    test2();
                    'target';
                }

                function test2()
                {
                    'source';
                    'dummy';
                    'dummy';
                }

                test();";

        var engine = new Engine(options => options.DebugMode());
        int step = 0;
        engine.Debugger.Step += (sender, info) =>
        {
            switch (step)
            {
                case 0:
                    if (info.ReachedLiteral("source"))
                    {
                        step++;
                        return StepMode.Out;
                    }
                    break;
                case 1:
                    info.ReachedLiteral("target").Should().BeTrue();
                    step++;
                    break;
            }
            return StepMode.Into;
        };

        engine.Execute(script);
    }

    [Fact]
    public void StepOverDoesSinglestepAfterBreakpoint()
    {
        string script = @"
                test();

                function test()
                {
                    'dummy';
                    debugger;
                    'target';
                }";

        var engine = new Engine(options => options
            .DebugMode()
            .DebuggerStatementHandling(DebuggerStatementHandling.Script));

        bool stepping = false;

        engine.Debugger.Break += (sender, info) =>
        {
            stepping = true;
            return StepMode.Over;
        };
        engine.Debugger.Step += (sender, info) =>
        {
            if (stepping)
            {
                info.ReachedLiteral("target").Should().BeTrue();
            }
            return StepMode.None;
        };

        engine.Execute(script);
    }

    [Fact]
    public void StepNotTriggeredWhenRunning()
    {
        string script = @"
                test();

                function test()
                {
                    'dummy';
                    'dummy';
                }";

        var engine = new Engine(options => options
            .DebugMode()
            .InitialStepMode(StepMode.Into));

        int stepCount = 0;
        engine.Debugger.Step += (sender, info) =>
        {
            stepCount++;
            // Start running after first step
            return StepMode.None;
        };

        engine.Execute(script);

        stepCount.Should().Be(1);
    }

    [Fact]
    public void SkipIsTriggeredWhenRunning()
    {
        string script = @"
                'step';
                'skip';
                'skip';
                debugger;
                'step';
                'step';
                ";

        var engine = new Engine(options => options
            .DebugMode()
            .DebuggerStatementHandling(DebuggerStatementHandling.Script)
            .InitialStepMode(StepMode.Into));

        int stepCount = 0;
        int skipCount = 0;

        engine.Debugger.Step += (sender, info) =>
        {
            TestHelpers.IsLiteral(info.CurrentNode, "step").Should().BeTrue();
            stepCount++;
            // Start running after first step
            return stepCount == 1 ? StepMode.None : StepMode.Into;
        };

        engine.Debugger.Skip += (sender, info) =>
        {
            TestHelpers.IsLiteral(info.CurrentNode, "skip").Should().BeTrue();
            skipCount++;
            return StepMode.None;
        };

        engine.Debugger.Break += (sender, info) =>
        {
            // Back to stepping after debugger statement
            return StepMode.Into;
        };

        engine.Execute(script);

        skipCount.Should().Be(2);
        stepCount.Should().Be(3);
    }
}