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
            Assert.Collection(info.CallStack,
                frame => Assert.Equal("foo", frame.FunctionName),
                frame => Assert.Equal("bar", frame.FunctionName),
                frame => Assert.Equal("(anonymous)", frame.FunctionName)
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
            Assert.Collection(info.CallStack,
                // "debugger;"
                frame => Assert.Equal(Position.From(4, 0), frame.Location.Start),
                // "foo();"
                frame => Assert.Equal(Position.From(9, 0), frame.Location.Start),
                // "bar();"
                frame => Assert.Equal(Position.From(12, 0), frame.Location.Start)
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
            Assert.Collection(info.CallStack,
                // function foo()
                frame => Assert.Equal(Position.From(2, 0), frame.FunctionLocation?.Start),
                // function bar()
                frame => Assert.Equal(Position.From(7, 0), frame.FunctionLocation?.Start),
                // global - no function location
                frame => Assert.Equal(null, frame.FunctionLocation?.Start)
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
                Assert.NotNull(info.CurrentCallFrame.ReturnValue);
                Assert.Equal("result", info.CurrentCallFrame.ReturnValue.AsString());
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

        Assert.True(didCheckReturn);
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
            Assert.Collection(info.CallStack,
                frame => Assert.Equal(engine.Realm.GlobalObject.Get("car"), frame.This),
                frame => Assert.Equal(engine.Realm.GlobalObject, frame.This)
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
            Assert.Equal("regularFunction", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("functionExpression", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("namedFunction", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("arrowFunction", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("anonymous", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("memberFunction", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("(anonymous)", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("get accessor", info.CurrentCallFrame.FunctionName);
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
            Assert.Equal("set accessor", info.CurrentCallFrame.FunctionName);
        });
    }
}