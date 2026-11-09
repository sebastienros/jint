#nullable enable

using AwesomeAssertions.Execution;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A module whose only <c>await</c> is an <c>await using</c> declaration is an asynchronous module.
/// <see href="https://tc39.es/ecma262/#sec-parsemodule">ParseModule</see> sets [[HasTLA]] to <c>body Contains await</c>,
/// and an <c>await using</c> declaration contains the <c>await</c> token wherever it sits outside a function, in a
/// block, an <c>if</c>, a <c>for</c> header or a <c>for</c>-<c>of</c> head, whether or not it is ever evaluated.
/// Such a module is evaluated by
/// <see href="https://tc39.es/ecma262/#sec-execute-async-module">ExecuteAsyncModule</see>, so the disposal that ends
/// its body is awaited as an <c>await</c> expression is, an importer runs only once that disposal has settled, a
/// sibling import is evaluated while it is pending, and a disposal that throws or rejects rejects the evaluation.
/// <para>
/// Jint looked for an <c>await</c> expression or a <c>for await</c> only, so such a module was evaluated
/// synchronously and disposed with a blocking wait (sebastienros/jint#4367). Each log is the module's own entries
/// interleaved with a ladder of microtasks <c>t1</c>, <c>t2</c>, … the awaiting module starts; every expected log is
/// Node 26's.
/// </para>
/// </summary>
public class ModuleAwaitUsingTests
{
    private const string Setup = """
        globalThis.log = [];
        globalThis.ladder = function (n) {
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          return p;
        };
        globalThis.show = function (e) {
          return e instanceof SuppressedError ? 'SuppressedError(' + e.error + ',' + e.suppressed + ')' : String(e);
        };
        """;

    /// <summary>
    /// A module body standing in for <c>m</c>, imported after <c>setup</c> by a root that logs <c>root</c>.
    /// </summary>
    private static readonly Dictionary<string, string> Bodies = new()
    {
        // The reference: a module whose only await is an await expression, at the end of its body.
        ["AnAwaitExpression"] =
            "log.push('m body'); ladder(8); log.push('m end'); await undefined;",
        ["AnAwaitUsing"] =
            "log.push('m body'); ladder(8); await using r = { [Symbol.asyncDispose]() { log.push('m dispose'); } }; log.push('m end');",
        // DisposeResources awaits undefined for a null resource (step 3), so this one is the reference's twin.
        ["AnAwaitUsingOfNull"] =
            "log.push('m body'); ladder(8); await using r = null; log.push('m end');",
        ["AnAwaitUsingInABlock"] =
            "log.push('m body'); ladder(8); { await using r = { [Symbol.asyncDispose]() { log.push('m dispose'); } }; log.push('m block'); } log.push('m end');",
        ["AnAwaitUsingInAForOfHead"] =
            "log.push('m body'); ladder(8); for (await using r of [{ [Symbol.asyncDispose]() { log.push('m dispose'); } }]) { log.push('m iteration'); } log.push('m end');",
        ["AnAwaitUsingInAForHeader"] =
            "log.push('m body'); ladder(8); for (await using r = { [Symbol.asyncDispose]() { log.push('m dispose'); } }; ;) { log.push('m iteration'); break; } log.push('m end');",
        // [[HasTLA]] is static: a declaration that is never evaluated still makes the module asynchronous, so its
        // importer runs a microtask later than a synchronous module's would.
        ["AnAwaitUsingNeverEvaluated"] =
            "log.push('m body'); ladder(8); if (globalThis.never) { await using r = null; } log.push('m end');",
        // Neither of these is an await of the module's: a using declaration disposes synchronously, and an await using
        // inside a function belongs to that function.
        ["AUsing"] =
            "log.push('m body'); ladder(8); using r = { [Symbol.dispose]() { log.push('m dispose'); } }; log.push('m end');",
        ["AnAwaitUsingInAFunction"] =
            "log.push('m body'); ladder(8); async function f() { await using r = null; } const g = async () => { await using r = null; }; log.push('m end');",
    };

    public static TestCases<string, string> Forms => new()
    {
        { "AnAwaitExpression", "m body, m end, t1, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsing", "m body, m end, m dispose, t1, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsingOfNull", "m body, m end, t1, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsingInABlock", "m body, m block, m dispose, t1, m end, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsingInAForOfHead", "m body, m iteration, m dispose, t1, m end, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsingInAForHeader", "m body, m iteration, m dispose, t1, m end, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsingNeverEvaluated", "m body, m end, t1, root, t2, t3, t4, t5, t6, t7, t8" },
        { "AUsing", "m body, m end, m dispose, root, t1, t2, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitUsingInAFunction", "m body, m end, root, t1, t2, t3, t4, t5, t6, t7, t8" },
    };

    [TestCaseSource(nameof(Forms)), CancelAfter(10000)]
    public void AnImporterRunsOnceTheModuleHasSettled(string form, string expected)
    {
        var engine = NewEngine();
        engine.Modules.Add("m", Bodies[form]);
        engine.Modules.Add("root", "import 'setup'; import 'm'; log.push('root');");

        engine.Modules.Import("root");

        Log(engine).Should().Be(expected, form);
    }

    /// <summary>
    /// The answer [[HasTLA]] is computed from, for each form above that has one, and for the forms that do not.
    /// </summary>
    [TestCase("await using r = null;", true)]
    [TestCase("{ await using r = null; }", true)]
    [TestCase("if (x) { await using r = null; }", true)]
    [TestCase("for (await using r of []) {}", true)]
    [TestCase("for (await using r = null; ;) { break; }", true)]
    [TestCase("label: { await using r = null; }", true)]
    [TestCase("try { await using r = null; } finally {}", true)]
    [TestCase("switch (x) { case 1: { await using r = null; } }", true)]
    [TestCase("using r = null;", false)]
    [TestCase("for (using r of []) {}", false)]
    [TestCase("async function f() { await using r = null; }", false)]
    [TestCase("const f = async () => { await using r = null; };", false)]
    [TestCase("const o = { async m() { await using r = null; } };", false)]
    [TestCase("class C { async m() { await using r = null; } }", false)]
    public void AnAwaitUsingOutsideAFunctionIsATopLevelAwait(string source, bool expected)
    {
        var module = Engine.PrepareModule(source, "m");

        HoistingScope.HasTopLevelAwait(module.Program!).Should().Be(expected, source);
    }

    /// <summary>
    /// A module that awaits its disposal is evaluated by ExecuteAsyncModule, so its sibling import is evaluated
    /// while the disposal is pending (https://tc39.es/ecma262/#sec-innermoduleevaluation, step 12), and the
    /// importer only once the disposal has settled
    /// (https://tc39.es/ecma262/#sec-async-module-execution-fulfilled).
    /// </summary>
    [Test, CancelAfter(10000)]
    public void ASiblingIsEvaluatedWhileTheDisposalIsPending()
    {
        var engine = NewEngine();
        engine.Modules.Add("m", """
            log.push('m body'); ladder(8);
            await using r = { async [Symbol.asyncDispose]() { log.push('m dispose'); await 0; await 0; log.push('m disposed'); } };
            export const value = 'm';
            """);
        engine.Modules.Add("s", "log.push('s body');");
        engine.Modules.Add("root", "import 'setup'; import { value } from 'm'; import 's'; log.push('root ' + value);");

        engine.Modules.Import("root");

        Log(engine).Should().Be("m body, m dispose, s body, t1, t2, m disposed, t3, t4, root m, t5, t6, t7, t8");
    }

    /// <summary>
    /// A disposal that rejects or throws is the module's evaluation error: its importer never runs and the import
    /// rejects with it, a SuppressedError when the body threw too
    /// (https://tc39.es/ecma262/#sec-disposeresources, step 2.e.iii). The sibling is evaluated first all the same,
    /// because the module's evaluation only ends once its body has.
    /// </summary>
    [TestCase("return Promise.reject('R');", "", "rejected R")]
    [TestCase("throw 'T';", "", "rejected T")]
    [TestCase("return Promise.reject('R');", "throw 'B';", "rejected SuppressedError(R,B)")]
    [CancelAfter(10000)]
    public void ADisposalThatFailsRejectsTheModule(string dispose, string tail, string expectedOutcome)
    {
        var engine = NewEngine();
        engine.Modules.Add("m", "log.push('m body'); ladder(6); await using r = { [Symbol.asyncDispose]() { log.push('m dispose'); "
            + dispose + " } }; log.push('m end'); " + tail);
        engine.Modules.Add("s", "log.push('s body');");
        engine.Modules.Add("root", "import 'setup'; import 'm'; import 's'; log.push('root body');");

        string outcome;
        try
        {
            engine.Modules.Import("root");
            outcome = "resolved";
        }
        catch (JavaScriptException e)
        {
            outcome = "rejected " + engine.Invoke("show", e.Error).AsString();
        }

        using var _ = new AssertionScope();
        Log(engine).Should().Be("m body, m end, m dispose, s body, t1, t2, t3, t4, t5, t6");
        outcome.Should().Be(expectedOutcome);
    }

    /// <summary>
    /// In a cycle the module that awaits its disposal holds back the module that imports it, whichever of the two
    /// the cycle is entered at (https://tc39.es/ecma262/#sec-innermoduleevaluation, step 11.c.v).
    /// </summary>
    [Test, CancelAfter(10000)]
    public void ACycleWaitsForTheDisposalOfItsAwaitingMember()
    {
        var engine = NewEngine();
        engine.Modules.Add("a", "import 'setup'; import { b } from 'b'; log.push('a body ' + b); export function a() { return 'A'; }");
        engine.Modules.Add("b", """
            import { a } from 'a';
            log.push('b body'); ladder(8);
            await using r = { async [Symbol.asyncDispose]() { log.push('b dispose ' + a()); await 0; log.push('b disposed'); } };
            export const b = 'B';
            log.push('b end');
            """);

        engine.Modules.Import("a");

        Log(engine).Should().Be("b body, b end, b dispose A, t1, b disposed, t2, t3, a body B, t4, t5, t6, t7, t8");
    }

    [Test, CancelAfter(10000)]
    public void ACycleRootThatAwaitsItsDisposalHoldsBackItsImporter()
    {
        var engine = NewEngine();
        engine.Modules.Add("c", """
            import 'setup';
            import { d } from 'd';
            log.push('c body ' + d()); ladder(8);
            await using r = { async [Symbol.asyncDispose]() { log.push('c dispose'); await 0; log.push('c disposed'); } };
            export const c = 'C';
            """);
        engine.Modules.Add("d", "import { c } from 'c'; log.push('d body'); export function d() { return 'D'; }");
        engine.Modules.Add("e", "import 'c'; log.push('e body');");

        engine.Modules.Import("e");

        Log(engine).Should().Be("d body, c body D, c dispose, t1, c disposed, t2, t3, e body, t4, t5, t6, t7, t8");
    }

    /// <summary>
    /// <c>import defer</c> evaluates an asynchronous module up front, since its namespace could never be evaluated
    /// synchronously on first access (https://tc39.es/proposal-defer-import-eval/#sec-GatherAsynchronousTransitiveDependencies),
    /// and a module whose only await is an <c>await using</c> is one. A <c>using</c> declaration defers as any other
    /// module does. Node 26's are with <c>--js-defer-import-eval</c>.
    /// </summary>
    [TestCase("await using r = { [Symbol.asyncDispose]() { log.push('m dispose'); } };", "m body, m dispose, root body, value m")]
    [TestCase("using r = { [Symbol.dispose]() { log.push('m dispose'); } };", "root body, m body, m dispose, value m")]
    [CancelAfter(10000)]
    public void ImportDeferEvaluatesTheModuleUpFront(string declaration, string expected)
    {
        var engine = NewEngine();
        engine.Modules.Add("m", "log.push('m body'); " + declaration + " export const value = 'm';");
        engine.Modules.Add("root", "import 'setup'; import defer * as ns from 'm'; log.push('root body'); log.push('value ' + ns.value);");

        engine.Modules.Import("root");

        Log(engine).Should().Be(expected);
    }

    private static Engine NewEngine()
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Modules.Add("setup", Setup);
        return engine;
    }

    private static string Log(Engine engine)
    {
        // The ladder may still have steps queued once the import has settled; a script run drains them.
        engine.Evaluate("0");
        return engine.Evaluate("log.join(', ')").AsString();
    }
}
