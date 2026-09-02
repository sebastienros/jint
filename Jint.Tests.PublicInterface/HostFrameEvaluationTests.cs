#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>DebugHandler.Evaluate(…, CallFrame)</c> from the outside: running a watch expression, or a console
/// input, in the environment of a call frame other than the innermost one.
/// </summary>
/// <remarks>
/// This is what a tooling protocol answers <c>Debugger.evaluateOnCallFrame</c> with, and a debugger front
/// end asks for it every time a user selects a frame in the call-stack pane. A host reaches it through the
/// <see cref="CallFrame"/> instances of the pause it is handling, and through nothing else: a frame kept
/// past its pause names environments the engine has left.
/// </remarks>
public class HostFrameEvaluationTests
{
    private const string Script = """
        function outer(tag)
        {
            const scoped = 'outer';
            let written = 'before';
            inner();
            return written + '/' + scoped + '/' + tag;
        }

        function inner()
        {
            const scoped = 'inner';
            debugger;
        }

        outer('tag');
        """;

    private static Engine CreateEngine() => new(options =>
    {
        options.Debugger.Enabled = true;
        options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
    });

    /// <summary>
    /// The frame's own scope chain answers, so a binding the innermost frame shadows is read as the chosen
    /// frame sees it — the whole point of selecting a frame.
    /// </summary>
    [Test]
    public void AFrameEvaluationReadsThatFramesBindings()
    {
        var engine = CreateEngine();
        var seen = new List<string>();

        engine.Debugger.Break += (sender, info) =>
        {
            seen.Add(engine.Debugger.Evaluate("scoped").AsString());
            seen.Add(engine.Debugger.Evaluate("scoped", info.CallStack[0]).AsString());
            seen.Add(engine.Debugger.Evaluate("scoped", info.CallStack[1]).AsString());
            return StepMode.None;
        };

        engine.Execute(Script);

        seen.Should().Equal("inner", "inner", "outer");
    }

    /// <summary>
    /// Writing is the half a watch pane alone would not need and a console input does: the assignment lands
    /// in the frame's binding, and the script sees it when it resumes.
    /// </summary>
    [Test]
    public void AFrameEvaluationWritesThatFramesBindings()
    {
        var engine = CreateEngine();

        engine.Debugger.Break += (sender, info) =>
        {
            engine.Debugger.Evaluate("written = 'after'", info.CallStack[1]);
            return StepMode.None;
        };

        engine.Evaluate(Script).AsString().Should().Be("after/outer/tag");
    }

    /// <summary>
    /// The frame supplies <c>this</c> and <c>arguments</c> too, because both are resolved out of the same
    /// environment chain rather than passed alongside it.
    /// </summary>
    [Test]
    public void AFrameEvaluationSeesThatFramesThisAndArguments()
    {
        var engine = CreateEngine();
        JsValue? name = null;
        JsValue? argument = null;

        engine.Debugger.Break += (sender, info) =>
        {
            name = engine.Debugger.Evaluate("this.name", info.CallStack[1]);
            argument = engine.Debugger.Evaluate("arguments[0]", info.CallStack[1]);
            return StepMode.None;
        };

        engine.Execute("""
            const host = { name: 'host', run(value) { inner(); } };
            function inner() { debugger; }
            host.run(42);
            """);

        name!.AsString().Should().Be("host");
        argument!.AsNumber().Should().Be(42);
    }

    /// <summary>
    /// The last frame is the global one, and evaluating there is what a protocol's plain
    /// <c>Runtime.evaluate</c> does while a page is paused.
    /// </summary>
    [Test]
    public void TheLastFrameIsTheGlobalScope()
    {
        var engine = CreateEngine();
        var results = new List<string>();

        engine.Debugger.Break += (sender, info) =>
        {
            var global = info.CallStack[info.CallStack.Count - 1];
            global.Index.Should().Be(info.CallStack.Count - 1);
            results.Add(engine.Debugger.Evaluate("globalBinding", global).AsString());
            results.Add(engine.Debugger.Evaluate("typeof scoped", global).AsString());
            return StepMode.None;
        };

        engine.Execute("var globalBinding = 'global';\n" + Script);

        results.Should().Equal("global", "undefined");
    }

    /// <summary>
    /// A throw inside the evaluated expression is reported as a failed evaluation and nothing else: the
    /// pause is still running afterwards and every frame still answers.
    /// </summary>
    [Test]
    public void AThrowInAFrameEvaluationLeavesThePauseUsable()
    {
        var engine = CreateEngine();
        var recovered = false;

        engine.Debugger.Break += (sender, info) =>
        {
            var thrown = Caught.Exception(() => engine.Debugger.Evaluate("missingFunction()", info.CallStack[1]));
            thrown.Should().BeOfType<DebugEvaluationException>();
            thrown!.InnerException.Should().BeOfType<JavaScriptException>();

            recovered = engine.Debugger.Evaluate("scoped", info.CallStack[1]).AsString() == "outer";
            return StepMode.None;
        };

        engine.Execute(Script);

        recovered.Should().BeTrue();
    }

    /// <summary>
    /// A frame is only good for the pause it came from. Keeping one and using it later would evaluate
    /// against environments the engine has left, so it is refused instead.
    /// </summary>
    [Test]
    public void AFrameKeptPastItsPauseIsRefused()
    {
        var engine = CreateEngine();
        CallFrame? kept = null;
        Exception? refusal = null;

        engine.Debugger.Break += (sender, info) =>
        {
            kept ??= info.CallStack[1];
            return StepMode.None;
        };

        engine.Execute(Script);

        kept.Should().NotBeNull();
        refusal = Caught.Exception(() => engine.Debugger.Evaluate("scoped", kept!));
        refusal.Should().BeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// Frames carry their engine, and a value never crosses engines in Jint, so a frame taken from one is
    /// refused by another rather than resolved against the wrong realm.
    /// </summary>
    [Test]
    public void AFrameFromAnotherEngineIsRefused()
    {
        CallFrame? foreign = null;
        var source = CreateEngine();
        source.Debugger.Break += (sender, info) =>
        {
            foreign ??= info.CallStack[1];
            return StepMode.None;
        };
        source.Execute(Script);

        var engine = CreateEngine();
        Exception? refusal = null;
        engine.Debugger.Break += (sender, info) =>
        {
            refusal = Caught.Exception(() => engine.Debugger.Evaluate("scoped", foreign!));
            return StepMode.None;
        };

        engine.Execute(Script);

        refusal.Should().BeOfType<InvalidOperationException>();
        refusal!.Message.Should().Contain("different engine");
    }

    /// <summary>
    /// A prepared script is accepted for a frame too, which is what a front end caches when the same watch
    /// expression is re-evaluated at every stop.
    /// </summary>
    [Test]
    public void APreparedScriptEvaluatesInAFrame()
    {
        var engine = CreateEngine();
        var prepared = Engine.PrepareScript("scoped");
        JsValue? result = null;

        engine.Debugger.Break += (sender, info) =>
        {
            result = engine.Debugger.Evaluate(prepared, info.CallStack[1]);
            return StepMode.None;
        };

        engine.Execute(Script);

        result!.AsString().Should().Be("outer");
    }
}
