#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime;

/// <summary>
/// The frame the engine was <i>entered</i> at — a host <c>Invoke</c>, a timer callback, a microtask — and
/// what it is called in a stack trace and in the debugger's call stack.
/// </summary>
/// <remarks>
/// <para>
/// Every other frame is created by a call expression, which carries a callee expression the engine can name
/// a frame after. An entry frame has none: nothing in script called it. Until this suite, that showed up as
/// a frame with the empty string for a name, and — for the timer callbacks, which reached the function
/// through <c>ICallable.Call</c> and pushed nothing — as no frame at all.
/// </para>
/// </remarks>
public class CallStackEntryFrameTests
{
    private static Engine WithTimers()
    {
        return new Engine(options => options.UseWebApis(WebApiFeatures.Timers | WebApiFeatures.Console));
    }

    private static Engine WithEvents()
    {
        return new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Console));
    }

    /// <summary>The frames of <c>Error.prototype.stack</c>, one per line, whitespace trimmed.</summary>
    private static string[] StackLines(JsValue error)
    {
        var stack = error.AsObject().Get("stack").AsString();
        return stack.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).ToArray();
    }

    [Test]
    public void AHostInvokedNamedFunctionNamesItsOwnFrame()
    {
        var engine = new Engine();
        engine.Execute("function declared() { return new Error('x'); }", "app.js");

        var error = engine.Invoke("declared");

        // The trailing line is the top-level program frame every rendered trace ends with.
        StackLines(error)[0].Should().StartWith("at declared (app.js:");
    }

    [Test]
    public void AHostInvokedAnonymousFunctionIsAnonymousRatherThanNameless()
    {
        var engine = new Engine();
        var callable = engine.Evaluate("(function () { return new Error('x'); })", "app.js");

        var error = engine.Invoke(callable);

        // The call site gave no name and the function has none of its own, so the frame is anonymous — the
        // same word an immediately invoked function expression already got, rather than an empty one.
        StackLines(error)[0].Should().StartWith("at (anonymous) (app.js:");
    }

    [Test]
    public void ATimerCallbackHasAFrameOfItsOwn()
    {
        var engine = WithTimers();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute(
            """
            setTimeout(function reconcile() {
                report(new Error('x'));
            }, 0);
            """,
            "app.js");

        engine.Tasks.ProcessTasks();

        _reported.Should().NotBeNull();
        StackLines(_reported!).Should().SatisfyRespectively(
            frame => frame.Should().StartWith("at reconcile (app.js:"),
            frame => frame.Should().StartWith("at app.js:"));
    }

    [Test]
    public void AnAnonymousTimerCallbackIsAnonymousRatherThanAbsent()
    {
        var engine = WithTimers();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute("setTimeout(function () { report(new Error('x')); }, 0);", "app.js");

        engine.Tasks.ProcessTasks();

        StackLines(_reported!)[0].Should().StartWith("at (anonymous) (app.js:");
    }

    [Test]
    public void AMicrotaskCallbackHasAFrameOfItsOwn()
    {
        var engine = WithTimers();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute("queueMicrotask(function drain() { report(new Error('x')); });", "app.js");

        engine.Tasks.ProcessTasks();

        StackLines(_reported!)[0].Should().StartWith("at drain (app.js:");
    }

    [Test]
    public void AnEventListenerHasAFrameOfItsOwn()
    {
        var engine = WithEvents();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute(
            """
            var target = new EventTarget();
            target.addEventListener('ping', function handle() {
                report(new Error('x'));
            });
            target.dispatchEvent(new Event('ping'));
            """,
            "app.js");

        StackLines(_reported!)[0].Should().StartWith("at handle (app.js:");
    }

    [Test]
    public void AHandleEventListenerHasAFrameOfItsOwn()
    {
        var engine = WithEvents();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute(
            """
            var target = new EventTarget();
            target.addEventListener('ping', {
                handleEvent: function receive() { report(new Error('x')); }
            });
            target.dispatchEvent(new Event('ping'));
            """,
            "app.js");

        // The callback interface's operation is looked up per invocation, and the function it finds owns a
        // frame just as a directly callable listener does.
        StackLines(_reported!)[0].Should().StartWith("at receive (app.js:");
    }

    [Test]
    public void AnEventHandlerAttributeCallbackHasAFrameOfItsOwn()
    {
        var engine = WithEvents();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute(
            """
            var controller = new AbortController();
            controller.signal.onabort = function reacted() { report(new Error('x')); };
            controller.abort();
            """,
            "app.js");

        // An event handler IDL attribute is a different algorithm from addEventListener's callback, and takes
        // the other branch of the invoke — which pushed nothing either.
        StackLines(_reported!)[0].Should().StartWith("at reacted (app.js:");
    }

    [Test]
    public void AnAnonymousEventListenerIsAnonymousRatherThanAbsent()
    {
        var engine = WithEvents();
        engine.SetValue("report", new Action<JsValue>(value => _reported = value));
        engine.Execute(
            """
            var target = new EventTarget();
            target.addEventListener('ping', function () { report(new Error('x')); });
            target.dispatchEvent(new Event('ping'));
            """,
            "app.js");

        StackLines(_reported!)[0].Should().StartWith("at (anonymous) (app.js:");
    }

    [Test]
    public void TheNameIsTheFunctionsOwnAndIsNotReadThroughAGetter()
    {
        var engine = new Engine();
        var callable = engine.Evaluate(
            """
            (function () {
                var f = function () { return new Error('x'); };
                Object.defineProperty(f, 'name', { get: function () { throw new Error('read'); } });
                return f;
            })()
            """,
            "app.js");

        // Naming a frame must never run script: an accessor `name` leaves the frame anonymous rather than
        // hijacking the stack trace of an error being constructed.
        var error = engine.Invoke(callable);

        StackLines(error)[0].Should().StartWith("at (anonymous) (app.js:");
    }

    [Test]
    public void TheDebuggerNamesAndLocatesTheFrameATimerCallbackEntered()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Timers);
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });

        DebugCallStack? stack = null;
        engine.Debugger.Break += (_, info) =>
        {
            stack = info.CallStack;
            return StepMode.None;
        };

        engine.Execute(
            """
            function inner() {
                debugger;
            }

            setTimeout(function reconcile() {
                inner();
            }, 0);
            """,
            "app.js");

        engine.Tasks.ProcessTasks();

        stack.Should().NotBeNull();
        stack!.Select(frame => frame.FunctionName).Should().Equal("inner", "reconcile", "(anonymous)");

        // The row a front end makes clickable. The callback's declaration is on line 5 of the fixture.
        stack[1].FunctionLocation.Should().NotBeNull();
        stack[1].FunctionLocation!.Value.Start.Line.Should().Be(5);
    }

    [Test]
    public void TheDebuggerNamesAndLocatesTheFrameAHostInvokeEntered()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });

        DebugCallStack? stack = null;
        engine.Debugger.Break += (_, info) =>
        {
            stack = info.CallStack;
            return StepMode.None;
        };

        engine.Execute(
            """
            function entered() {
                debugger;
            }
            """,
            "app.js");

        engine.Invoke("entered");

        stack.Should().NotBeNull();
        stack!.Select(frame => frame.FunctionName).Should().Equal("entered", "(anonymous)");
        stack[0].FunctionLocation.Should().NotBeNull();
        stack[0].FunctionLocation!.Value.Start.Line.Should().Be(1);
    }

    private JsValue? _reported;
}
#endif
