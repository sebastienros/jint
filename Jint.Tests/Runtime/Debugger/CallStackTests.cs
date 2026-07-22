using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class CallStackTests
{
    [Fact]
    public void IncludesFunctionNames()
    {
        var script = @"
            function foo()
            {
                debugger;
            }
            
            function bar()
            {
                foo();
            }

            bar()";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CallStack.Should().SatisfyRespectively(
                frame => frame.FunctionName.Should().Be("foo"),
                frame => frame.FunctionName.Should().Be("bar"),
                frame => frame.FunctionName.Should().Be("(anonymous)")
            );
        });
    }

    [Fact]
    public void IncludesLocations()
    {
        var script = @"
function foo()
{
debugger;
}
            
function bar()
{
foo();
}

bar()";

        TestHelpers.TestAtBreak(script, info =>
        {
            // The line numbers here may mislead - the positions are, as would be expected,
            // at the position before the currently executing line, not the line after.
            // Remember that Esprima (and hence Jint) line numbers are 1-based, not 0-based.
            info.CallStack.Should().SatisfyRespectively(
                // "debugger;"
                frame => frame.Location.Start.Should().Be(Position.From(4, 0)),
                // "foo();"
                frame => frame.Location.Start.Should().Be(Position.From(9, 0)),
                // "bar();"
                frame => frame.Location.Start.Should().Be(Position.From(12, 0))
            );
        });
    }

    [Fact]
    public void IncludesFunctionLocations()
    {
        var script = @"
function foo()
{
debugger;
}
            
function bar()
{
foo();
}

bar()";

        TestHelpers.TestAtBreak(script, info =>
        {
            // Remember that Esprima (and hence Jint) line numbers are 1-based, not 0-based.
            info.CallStack.Should().SatisfyRespectively(
                // function foo()
                frame => (frame.FunctionLocation?.Start).Should().Be(Position.From(2, 0)),
                // function bar()
                frame => (frame.FunctionLocation?.Start).Should().Be(Position.From(7, 0)),
                // global - no function location
                frame => (frame.FunctionLocation?.Start).Should().BeNull()
            );
        });
    }

    [Fact]
    public void HasReturnValue()
    {
        string script = @"
            function foo()
            {
                return 'result';
            }

            foo();";

        var engine = new Engine(options => options.DebugMode().InitialStepMode(StepMode.Into));

        bool atReturn = false;
        bool didCheckReturn = false;

        engine.Debugger.Step += (sender, info) =>
        {
            if (atReturn)
            {
                info.CurrentCallFrame.ReturnValue.Should().NotBeNull();
                info.CurrentCallFrame.ReturnValue.AsString().Should().Be("result");
                didCheckReturn = true;
                atReturn = false;
            }

            if (info.CurrentNode is ReturnStatement)
            {
                // Step one further, and we should have the return value
                atReturn = true;
            }
            return StepMode.Into;
        };

        engine.Execute(script);

        didCheckReturn.Should().BeTrue();
    }

    [Fact]
    public void HasThis()
    {
        string script = @"
function Thing(name)
{
    this.name = name;
}

Thing.prototype.test = function()
{
    debugger;
}

var car = new Thing('car');
car.test();
";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            info.CallStack.Should().SatisfyRespectively(
                frame => frame.This.Should().Be(engine.Realm.GlobalObject.Get("car")),
                frame => frame.This.Should().Be(engine.Realm.GlobalObject)
            );
        });
    }

    [Fact]
    public void NamesRegularFunction()
    {
        string script = @"
            function regularFunction() { debugger; }
            regularFunction();";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("regularFunction");
        });
    }

    [Fact]
    public void NamesFunctionExpression()
    {
        string script = @"
            const functionExpression = function() { debugger; }
            functionExpression()";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("functionExpression");
        });
    }

    [Fact]
    public void NamesNamedFunctionExpression()
    {
        string script = @"
            const functionExpression = function namedFunction() { debugger; }
            functionExpression()";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("namedFunction");
        });
    }

    [Fact]
    public void NamesArrowFunction()
    {
        string script = @"
            const arrowFunction = () => { debugger; }
            arrowFunction()";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("arrowFunction");
        });
    }

    [Fact]
    public void NamesNewFunction()
    {
        string script = @"
            const newFunction = new Function('debugger;');
            newFunction()";

        TestHelpers.TestAtBreak(script, info =>
        {
            // Ideally, this should be "(anonymous)", but FunctionConstructor sets the "anonymous" name.
            info.CurrentCallFrame.FunctionName.Should().Be("anonymous");
        });
    }

    [Fact]
    public void NamesMemberFunction()
    {
        string script = @"
            const obj = { memberFunction() { debugger; } };
            obj.memberFunction()";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("memberFunction");
        });
    }

    [Fact]
    public void NamesAnonymousFunction()
    {
        string script = @"
            (function()
            {
                debugger;
            }());";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("(anonymous)");
        });
    }

    [Fact]
    public void NamesGetAccessor()
    {
        string script = @"
            const obj = {
                get accessor()
                {
                    debugger;
                    return 'test';
                }
            };
            const x = obj.accessor;";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("get accessor");
        });
    }

    [Fact]
    public void NamesSetAccessor()
    {
        string script = @"
            const obj = {
                set accessor(value)
                {
                    debugger;
                    this.value = value;
                }
            };
            obj.accessor = 42;";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentCallFrame.FunctionName.Should().Be("set accessor");
        });
    }
}