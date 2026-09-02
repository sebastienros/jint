using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class EvaluateTests
{
    [Test]
    public void EvalutesInCurrentContext()
    {
        var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var evaluated = engine.Debugger.Evaluate("x");
            evaluated.Should().BeOfType<JsNumber>();
            evaluated.AsNumber().Should().Be(50);
        });
    }

    [Test]
    public void ThrowsIfNoCurrentContext()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);
        var exception = Invoking(() => engine.Debugger.Evaluate("let x = 1;")).Should().ThrowExactly<DebugEvaluationException>().Which;
        exception.InnerException.Should().BeNull(); // Not a JavaScript or parser exception
    }

    [Test]
    public void ThrowsOnRuntimeError()
    {
        var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var exception = Invoking(() => engine.Debugger.Evaluate("y")).Should().ThrowExactly<DebugEvaluationException>().Which;
            exception.InnerException.Should().BeOfType<JavaScriptException>();
        });
    }

    [Test]
    public void ThrowsOnExecutionError()
    {
        var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var exception = Invoking(() =>
                engine.Debugger.Evaluate("this is a syntax error")).Should().ThrowExactly<DebugEvaluationException>().Which;
            exception.InnerException.Should().BeOfType<Acornima.SyntaxErrorException>();
        });
    }

    [Test]
    public void RestoresStackAfterEvaluation()
    {
        var script = @"
            function throws()
            {
                throw new Error('Take this!');
            }

            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            engine.CallStack.Count.Should().Be(1);
            var frameBefore = engine.CallStack.Stack[0];

            Invoking(() => engine.Debugger.Evaluate("throws()")).Should().ThrowExactly<DebugEvaluationException>();
            engine.CallStack.Count.Should().Be(1);
            var frameAfter = engine.CallStack.Stack[0];
            // Stack frames and some of their properties are structs - can't check reference equality
            // Besides, even if we could, it would be no guarantee. Neither is the following, but it'll do for now.
            frameAfter.CallingExecutionContext.LexicalEnvironment.Should().Be(frameBefore.CallingExecutionContext.LexicalEnvironment);
            frameAfter.Arguments.Should().Be(frameBefore.Arguments);
            frameAfter.Expression.Should().Be(frameBefore.Expression);
            frameAfter.Location.Should().Be(frameBefore.Location);
            frameAfter.Function.Should().Be(frameBefore.Function);
        });
    }

    private const string ShadowingScript = @"
        function outer()
        {
            const shadowed = 'outer';
            let written = 'before';
            inner();
            return written;
        }

        function inner()
        {
            const shadowed = 'inner';
            debugger;
        }

        outer();
        ";

    [Test]
    public void FramesAreIndexedFromTheTop()
    {
        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) =>
        {
            var stack = info.CallStack;
            for (var i = 0; i < stack.Count; i++)
            {
                stack[i].Index.Should().Be(i);
            }

            stack[0].FunctionName.Should().Be("inner");
            stack[1].FunctionName.Should().Be("outer");
        });
    }

    [Test]
    public void EvaluatesInTheEnvironmentOfTheRequestedFrame()
    {
        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) =>
        {
            engine.Debugger.Evaluate("shadowed").AsString().Should().Be("inner");
            engine.Debugger.Evaluate("shadowed", info.CallStack[0]).AsString().Should().Be("inner");
            engine.Debugger.Evaluate("shadowed", info.CallStack[1]).AsString().Should().Be("outer");
        });
    }

    [Test]
    public void WritesBindingsOfTheRequestedFrame()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });

        engine.Debugger.Break += (sender, info) =>
        {
            engine.Debugger.Evaluate("written = 'after'", info.CallStack[1]);
            return StepMode.None;
        };

        engine.Evaluate(ShadowingScript).AsString().Should().Be("after");
    }

    [Test]
    public void EvaluatesThisOfTheRequestedFrame()
    {
        var script = @"
            const host = {
                name: 'host',
                run() { helper(); }
            };

            function helper()
            {
                debugger;
            }

            host.run();
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            // helper is called as a plain function, so its own `this` is undefined in strict mode and the
            // global object otherwise - either way it is not the object whose method is one frame down.
            engine.Debugger.Evaluate("this.name", info.CallStack[1]).AsString().Should().Be("host");
        });
    }

    [Test]
    public void EvaluatesArgumentsOfTheRequestedFrame()
    {
        var script = @"
            function outer(a, b)
            {
                inner();
            }

            function inner()
            {
                debugger;
            }

            outer(11, 22);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            engine.Debugger.Evaluate("arguments.length", info.CallStack[1]).AsNumber().Should().Be(2);
            engine.Debugger.Evaluate("arguments[1]", info.CallStack[1]).AsNumber().Should().Be(22);
        });
    }

    [Test]
    public void EvaluatesInTheGlobalFrame()
    {
        var script = @"
            var globalBinding = 'global';

            function outer()
            {
                const shadowed = 'outer';
                inner();
            }

            function inner()
            {
                const shadowed = 'inner';
                debugger;
            }

            outer();
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var globalFrame = info.CallStack[info.CallStack.Count - 1];
            globalFrame.FunctionName.Should().Be("(anonymous)");
            engine.Debugger.Evaluate("globalBinding", globalFrame).AsString().Should().Be("global");
            engine.Debugger.Evaluate("typeof shadowed", globalFrame).AsString().Should().Be("undefined");
        });
    }

    [Test]
    public void AcceptsAPreparedScriptForAFrame()
    {
        var prepared = Engine.PrepareScript("shadowed");

        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) =>
        {
            engine.Debugger.Evaluate(prepared, info.CallStack[1]).AsString().Should().Be("outer");
        });
    }

    [Test]
    public void FrameEvaluationErrorLeavesThePauseIntact()
    {
        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) =>
        {
            var callStackSize = engine.CallStack.Count;

            var exception = Invoking(() => engine.Debugger.Evaluate("undefinedFunction()", info.CallStack[1]))
                .Should().ThrowExactly<DebugEvaluationException>().Which;
            exception.InnerException.Should().BeOfType<JavaScriptException>();

            engine.CallStack.Count.Should().Be(callStackSize);
            engine.Debugger.Evaluate("shadowed", info.CallStack[1]).AsString().Should().Be("outer");
            engine.Debugger.Evaluate("shadowed").AsString().Should().Be("inner");
        });
    }

    [Test]
    public void RefusesAFrameFromAnEarlierPause()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });

        CallFrame staleFrame = null;
        var breaks = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            breaks++;
            if (staleFrame is null)
            {
                staleFrame = info.CallStack[1];
                // The frame is usable while the pause it came from is still running.
                engine.Debugger.Evaluate("1", staleFrame).AsNumber().Should().Be(1);
            }
            else
            {
                Invoking(() => engine.Debugger.Evaluate("1", staleFrame))
                    .Should().Throw<InvalidOperationException>()
                    .WithMessage("*already left*");
            }

            return StepMode.None;
        };

        engine.Execute(@"
            function inner() { debugger; }
            function outer() { inner(); }
            outer();
            outer();
        ");

        breaks.Should().Be(2);
    }

    [Test]
    public void RefusesAFrameFromAnotherEngine()
    {
        CallFrame foreignFrame = null;
        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) => foreignFrame = info.CallStack[1]);

        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) =>
        {
            Invoking(() => engine.Debugger.Evaluate("1", foreignFrame))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*different engine*");
        });
    }

    [Test]
    public void RefusesANullFrame()
    {
        TestHelpers.TestAtBreak(ShadowingScript, (engine, info) =>
        {
            Invoking(() => engine.Debugger.Evaluate("1", (CallFrame) null))
                .Should().Throw<ArgumentNullException>();
        });
    }

    [Test]
    public void EvaluatesInAFrameSuspendedAtAnAwait()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });
        var gate = engine.Tasks.RegisterPromise();
        engine.SetValue("gate", gate.Promise);

        var breaks = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            breaks++;
            engine.Debugger.Evaluate("captured").AsString().Should().Be("from-helper");
            engine.Debugger.Evaluate("captured", info.CallStack[1]).AsString().Should().Be("from-async");
            return StepMode.None;
        };

        engine.Execute(@"
            async function run(gate) {
                const captured = 'from-async';
                await gate;
                helper();
            }

            function helper() {
                const captured = 'from-helper';
                debugger;
            }

            run(gate);
        ");

        breaks.Should().Be(0, "execution is still suspended at the await");

        gate.Resolve(JsValue.Undefined);

        breaks.Should().Be(1);
    }
}
