using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

/// <summary>
/// Debugging across an event-loop drain — the window in which the engine is running script with no
/// host entry on the stack, because the host's call has already returned and a promise settled
/// later. The debugger used to be blind here: it asked whether an evaluation context happened to be
/// installed on the engine, and async and generator resumes ran script without installing one, so
/// <see cref="DebugHandler.Evaluate(string, ScriptParsingOptions)"/> threw. For a watch expression
/// that surfaced as an exception; for a conditional breakpoint it was worse, because
/// <c>BreakPointCollection.FindMatch</c> catches <see cref="DebugEvaluationException"/> and reports
/// "condition false" — so the breakpoint was silently skipped with no diagnostic at all.
/// <para>
/// Note the shape every test here shares: the drain must be driven by <em>settling a promise the
/// host holds</em>, not by <c>engine.Execute</c>. A drain that happens on the way out of Execute is
/// still inside the host entry and always worked.
/// </para>
/// </summary>
public class AsyncDebuggerTests
{
    // 1 async function run(gate) {
    // 2 var before = 1;
    // 3 await gate;
    // 4 var after = before + 1;
    // 5 return after;
    // 6 }
    // 7 run(gate);
    private const string AwaitScript = @"async function run(gate) {
var before = 1;
await gate;
var after = before + 1;
return after;
}
run(gate);";

    private static Engine CreateEngine(out ManualPromise gate)
    {
        var engine = new Engine(options => options.DebugMode());
        gate = engine.Advanced.RegisterPromise();
        engine.SetValue("gate", gate.Promise);
        return engine;
    }

    [Fact]
    public void ConditionalBreakPointFiresAfterAnAwaitResumedFromTheHost()
    {
        var engine = CreateEngine(out var gate);

        var hits = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            info.Location.Start.Line.Should().Be(4);
            hits++;
            return StepMode.None;
        };
        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 0, "before === 1"));

        engine.Execute(AwaitScript);
        hits.Should().Be(0, "execution is still suspended at the await");

        gate.Resolve(JsValue.Undefined);
        hits.Should().Be(1, "the resumed frame's breakpoint condition must be evaluated, not swallowed");
    }

    [Fact]
    public void ConditionalBreakPointAfterAnAwaitStillHonoursAFalseCondition()
    {
        var engine = CreateEngine(out var gate);

        var hits = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            hits++;
            return StepMode.None;
        };
        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 0, "before === 999"));

        engine.Execute(AwaitScript);
        gate.Resolve(JsValue.Undefined);

        hits.Should().Be(0, "a condition that evaluates to false must not break");
    }

    [Fact]
    public void WatchExpressionResolvesInAFrameResumedFromTheHost()
    {
        var engine = CreateEngine(out var gate);

        JsValue observed = null;
        engine.Debugger.Break += (sender, info) =>
        {
            observed = engine.Debugger.Evaluate("before");
            return StepMode.None;
        };
        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 0));

        engine.Execute(AwaitScript);
        gate.Resolve(JsValue.Undefined);

        observed.Should().NotBeNull("the Break handler must have run after the await");
        observed.AsNumber().Should().Be(1, "the watch expression resolves against the resumed frame");
    }

    [Fact]
    public void BreakPointFiresInsideAThenCallbackReachedFromADrain()
    {
        // 1 var value = 41;
        // 2 gate.then(function (x) {
        // 3 value = value + 1;
        // 4 });
        var script = @"var value = 41;
gate.then(function (x) {
value = value + 1;
});";

        var engine = CreateEngine(out var gate);

        var hits = 0;
        engine.Debugger.Break += (sender, info) =>
        {
            engine.Debugger.Evaluate("value").AsNumber().Should().Be(41);
            hits++;
            return StepMode.None;
        };
        engine.Debugger.BreakPoints.Set(new BreakPoint(3, 0, "value === 41"));

        engine.Execute(script);
        hits.Should().Be(0);

        gate.Resolve(JsValue.Undefined);
        hits.Should().Be(1);
    }

    [Fact]
    public void AsyncGeneratorResumeFromTheHostCanEvaluateAWatchExpression()
    {
        // 1 var seen = 0;
        // 2 async function* produce(gate) {
        // 3 var local = 7;
        // 4 await gate;
        // 5 seen = local;
        // 6 yield local;
        // 7 }
        // 8 var it = produce(gate);
        // 9 it.next();
        var script = @"var seen = 0;
async function* produce(gate) {
var local = 7;
await gate;
seen = local;
yield local;
}
var it = produce(gate);
it.next();";

        var engine = CreateEngine(out var gate);

        JsValue observed = null;
        engine.Debugger.Break += (sender, info) =>
        {
            observed = engine.Debugger.Evaluate("local");
            return StepMode.None;
        };
        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 0));

        engine.Execute(script);
        gate.Resolve(JsValue.Undefined);

        observed.Should().NotBeNull("the async generator body must have resumed past the await");
        observed.AsNumber().Should().Be(7);
    }

    [Fact]
    public void AnIdleEngineStillRefusesToEvaluate()
    {
        var engine = new Engine(options => options.DebugMode());

        Invoking(() => engine.Debugger.Evaluate("1 + 1"))
            .Should().ThrowExactly<DebugEvaluationException>()
            .Which.InnerException.Should().BeNull();

        // ...and still refuses once an evaluation has run to completion and unwound.
        engine.Execute("var x = 1;");

        Invoking(() => engine.Debugger.Evaluate("x"))
            .Should().ThrowExactly<DebugEvaluationException>()
            .Which.InnerException.Should().BeNull();
    }
}
