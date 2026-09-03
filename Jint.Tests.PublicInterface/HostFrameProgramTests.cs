#nullable enable

using System.Collections.Generic;
using System.Linq;
using Jint.Runtime.Debugger;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The program a debugger frame and a profile frame belong to, which is the identity a tooling protocol
/// answers "which script is this?" with.
/// </summary>
/// <remarks>
/// A location carries a source <em>name</em>, and every <c>Execute</c> given no source is
/// <c>&lt;anonymous&gt;</c>, so a host matching positions against names collapses every such script into one.
/// The reference on the frame is the one <c>DebugHandler.BeforeEvaluate</c> hands over, which is what makes
/// the two answerable apart.
/// </remarks>
public class HostFrameProgramTests
{
    private const string Script = """
        function work() {
            debugger;
            return 1;
        }
        work();
        """;

    private static Engine CreateDebuggerEngine() => new(options =>
    {
        options.Debugger.Enabled = true;
        options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
    });

    /// <summary>
    /// The case a name cannot answer: two programs parsed under one name, at the very same positions.
    /// </summary>
    [Test]
    public void TwoScriptsParsedUnderOneNameAreToldApartByTheirFramesProgram()
    {
        var engine = CreateDebuggerEngine();

        var parsed = new List<Program>();
        engine.Debugger.BeforeEvaluate += (sender, ast) => parsed.Add(ast);

        var paused = new List<Program?>();
        engine.Debugger.Break += (sender, info) =>
        {
            paused.Add(info.CallStack[0].Program);
            return StepMode.None;
        };

        engine.Execute(Script);
        engine.Execute(Script);

        parsed.Should().HaveCount(2);
        parsed[0].Should().NotBeSameAs(parsed[1]);

        paused.Should().HaveCount(2);
        paused[0].Should().BeSameAs(parsed[0]);
        paused[1].Should().BeSameAs(parsed[1]);
    }

    /// <summary>
    /// Every frame of the stack answers, the top-level one included — it is the frame a sourceless
    /// <c>Execute</c> spends most of its time in.
    /// </summary>
    [Test]
    public void EveryFrameOfThePauseNamesTheProgramItIsRunning()
    {
        var engine = CreateDebuggerEngine();

        var prepared = Engine.PrepareScript(Script, "host.js");

        DebugCallStack? stack = null;
        engine.Debugger.Break += (sender, info) =>
        {
            stack = info.CallStack;
            return StepMode.None;
        };

        engine.Execute(prepared);

        stack.Should().NotBeNull();
        stack!.Should().HaveCount(2);
        stack.Should().AllSatisfy(frame => frame.Program.Should().BeSameAs(prepared.Program));
    }

    /// <summary>
    /// A shared <c>Prepared&lt;Script&gt;</c> is one program however often it runs, which is what makes the
    /// identity worth keying a script table on.
    /// </summary>
    [Test]
    public void RunningOnePreparedScriptTwiceNamesTheOneProgram()
    {
        var engine = CreateDebuggerEngine();
        var prepared = Engine.PrepareScript(Script, "host.js");

        var paused = new List<Program?>();
        engine.Debugger.Break += (sender, info) =>
        {
            paused.Add(info.CallStack[0].Program);
            return StepMode.None;
        };

        engine.Execute(prepared);
        engine.Execute(prepared);

        paused.Should().HaveCount(2);
        paused.Should().AllSatisfy(program => program.Should().BeSameAs(prepared.Program));
    }

    /// <summary>
    /// Code the engine reached through <c>eval</c> is a program no execution context names, so the frame
    /// answers with none rather than with the script that ran the <c>eval</c>.
    /// </summary>
    [Test]
    public void AFrameInsideEvalNamesNoProgram()
    {
        var engine = CreateDebuggerEngine();

        var paused = new List<Program?>();
        engine.Debugger.Break += (sender, info) =>
        {
            paused.Add(info.CallStack[0].Program);
            return StepMode.None;
        };

        engine.Execute("eval('debugger;');", "host.js");

        paused.Should().ContainSingle();
        paused[0].Should().BeNull();
    }

    /// <summary>
    /// The profiler's frame table answers the same question for a function, so a profile's frames map onto
    /// the same script table a pause does.
    /// </summary>
    [Test]
    public void AProfileFrameNamesTheProgramItsFunctionWasParsedFrom()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);

        var first = Engine.PrepareScript("function alpha() { return 1; } alpha();");
        var second = Engine.PrepareScript("function beta() { return 2; } beta();");

        engine.Diagnostics.StartProfiling();
        engine.Execute(first);
        engine.Execute(second);
        var profile = engine.Diagnostics.StopProfiling();

        profile.Frames.Single(f => f.Name == "alpha").Program.Should().BeSameAs(first.Program);
        profile.Frames.Single(f => f.Name == "beta").Program.Should().BeSameAs(second.Program);

        // both were parsed under the one name every sourceless script gets, which is the collision
        profile.Frames.Select(f => f.File).Distinct().Should().ContainSingle();
    }

    /// <summary>
    /// A function <em>value</em> answers the same question, which is what makes <c>[[FunctionLocation]]</c>
    /// resolvable by identity: two parses of one text declare two functions under one source name.
    /// </summary>
    [Test]
    public void TwoFunctionsFromTwoParsesOfOneTextNameTheirOwnProgram()
    {
        const string Declaration = "globalThis.fns = globalThis.fns || []; fns.push(function work() { return 1; });";

        var engine = CreateDebuggerEngine();

        var parsed = new List<Program>();
        engine.Debugger.BeforeEvaluate += (sender, ast) => parsed.Add(ast);

        engine.Execute(Declaration);
        engine.Execute(Declaration);

        parsed.Should().HaveCount(2);
        FunctionOf(engine, "fns[0]").Program.Should().BeSameAs(parsed[0]);
        FunctionOf(engine, "fns[1]").Program.Should().BeSameAs(parsed[1]);
    }

    /// <summary>
    /// The contract <c>TryGetSourceText</c> keeps: one <c>Prepared&lt;Script&gt;</c> shared by several
    /// engines answers the same program on every one of them.
    /// </summary>
    [Test]
    public void AFunctionBuiltOnEitherOfTwoEnginesNamesTheOnePreparedProgram()
    {
        var prepared = Engine.PrepareScript("function work() { return 1; }", "shared.js");

        var first = new Engine();
        var second = new Engine();
        first.Execute(prepared);
        second.Execute(prepared);

        FunctionOf(first, "work").Program.Should().BeSameAs(prepared.Program);
        FunctionOf(second, "work").Program.Should().BeSameAs(prepared.Program);
    }

    /// <summary>
    /// A built-in, a host callable and a bound function have no declaration at all, so they name no program
    /// rather than the script that happened to be running when they were created.
    /// </summary>
    [Test]
    public void AFunctionValueWithNoDeclarationNamesNoProgram()
    {
        var engine = new Engine();
        engine.SetValue("host", new System.Func<int>(() => 1));
        engine.Execute("function work() { return 1; } globalThis.bound = work.bind(null);", "host.js");

        FunctionOf(engine, "Math.max").Program.Should().BeNull();
        FunctionOf(engine, "host").Program.Should().BeNull();
        FunctionOf(engine, "bound").Program.Should().BeNull();
    }

    /// <summary>
    /// A body the engine reached another way is a program no execution context names, so it is declined
    /// rather than attributed to the script that ran it.
    /// </summary>
    [Test]
    public void AFunctionDeclaredInsideEvalOrTheFunctionConstructorNamesNoProgram()
    {
        var engine = new Engine();
        engine.Execute(
            """
            globalThis.evaluated = eval('(function fromEval() { return 1; })');
            globalThis.constructed = new Function('return 2;');
            function declared() { return 3; }
            """,
            "host.js");

        FunctionOf(engine, "evaluated").Program.Should().BeNull();
        FunctionOf(engine, "constructed").Program.Should().BeNull();
        FunctionOf(engine, "declared").Program.Should().NotBeNull();
    }

    private static Jint.Native.Function.Function FunctionOf(Engine engine, string expression)
        => (Jint.Native.Function.Function) engine.Evaluate(expression);

    /// <summary>
    /// A built-in and a host callable have no source, so they name no program either — the answer is
    /// <see langword="null"/> rather than the script that happened to be running when they were created.
    /// </summary>
    [Test]
    public void AFunctionWithNoSourceNamesNoProgram()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.SetValue("host", new System.Func<int>(() => 1));

        engine.Diagnostics.StartProfiling();
        engine.Execute("[3, 1, 2].sort(function (a, b) { return a - b; }); host();", "host.js");
        var profile = engine.Diagnostics.StopProfiling();

        profile.Frames.Should().NotBeEmpty();
        profile.Frames.Should().OnlyContain(f => f.File != null || f.Program == null);
    }
}
