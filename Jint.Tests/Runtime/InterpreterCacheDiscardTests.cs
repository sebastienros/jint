#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Advanced.DiscardInterpreterCaches</c>: what a pooled engine's warmed handler trees keep alive between
/// evaluations, and that discarding them lets it go without changing what the next evaluation computes.
/// Every retention scenario runs twice, once without the discard as a control: the control is what proves the
/// scenario really pins the host object, without which the discarding assertion would pass vacuously.
/// </summary>
[NonParallelizable]
public class InterpreterCacheDiscardTests
{
    private static int ScriptStatementListCount(Engine engine)
        => engine.Diagnostics.GetMemoryReport().HandlerTreeCaches.ScriptStatementLists;

    private static int FunctionDefinitionCount(Engine engine)
        => engine.Diagnostics.GetMemoryReport().HandlerTreeCaches.FunctionDefinitions;

    // A request's services, as far as the engine can tell: an object nothing but this request references.
    private sealed class RequestServices
    {
        public string Name => "services";

        public string Label { get; set; } = "";
    }

    /// <summary>
    /// The reported shape: a global method built per request as a delegate closing over that request's
    /// services, called from a cached prepared script. The warmed call site keeps the delegate as its last
    /// callee, and the delegate keeps the services.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AWarmedCallSiteReleasesItsHostCallee(bool discard)
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("describe('x')");

        RunRequestCallingADelegate(engine, snapshot, script);
        var services = RunRequestCallingADelegate(engine, snapshot, script);
        ScriptStatementListCount(engine).Should().Be(1, "the second evaluation is the one that caches and warms the tree");

        AssertReleasedOnlyWhenDiscarded(engine, services, discard);

        // whatever was discarded, the next request computes the same thing and warms the tree again
        RunRequestCallingADelegate(engine, snapshot, script);
        ScriptStatementListCount(engine).Should().Be(1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequestCallingADelegate(Engine engine, GlobalSnapshot snapshot, Prepared<Script> script)
    {
        var services = new RequestServices();
        engine.SetValue("describe", new Func<string, string>(value => value + ":" + services.Name));
        engine.Evaluate(script).AsString().Should().Be("x:services");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    /// <summary>
    /// A member read on a wrapped CLR object: the site's wrapper cache keeps the wrapper, and the wrapper
    /// keeps the object.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AWarmedMemberReadReleasesItsHostReceiver(bool discard)
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("ctx.Name + '!'");

        RunRequestReadingAMember(engine, snapshot, script);
        var services = RunRequestReadingAMember(engine, snapshot, script);

        AssertReleasedOnlyWhenDiscarded(engine, services, discard);

        RunRequestReadingAMember(engine, snapshot, script);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequestReadingAMember(Engine engine, GlobalSnapshot snapshot, Prepared<Script> script)
    {
        var services = new RequestServices();
        engine.SetValue("ctx", services);
        engine.Evaluate(script).AsString().Should().Be("services!");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    /// <summary>
    /// Helpers the host defined before capturing, so the snapshot itself keeps the functions — and with them
    /// the bodies they warmed on their first call. A class method's definition is in the engine's cache; a
    /// top-level function or arrow of a script that ran once is reached only through the global surface.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AHelperDefinedBeforeTheCaptureReleasesWhatItsBodyCached(bool discard)
    {
        var engine = new Engine();
        engine.Execute("""
            function read(o) { return o.Name + suffix(); }
            function readThis() { return this.Name; }
            const readArrow = o => o.Name;
            class Reader { read(o) { return o.Name; } }
            var reader = new Reader();
            """);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("read(ctx) + readArrow(ctx) + reader.read(ctx) + readThis.call(ctx)");

        RunRequestThroughAHelper(engine, snapshot, script);
        var services = RunRequestThroughAHelper(engine, snapshot, script);

        AssertReleasedOnlyWhenDiscarded(engine, services, discard);

        RunRequestThroughAHelper(engine, snapshot, script);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequestThroughAHelper(Engine engine, GlobalSnapshot snapshot, Prepared<Script> script)
    {
        var services = new RequestServices();
        engine.SetValue("ctx", services);
        engine.SetValue("suffix", new Func<string>(() => "/" + services.Name));
        engine.Evaluate(script).AsString().Should().Be("services/servicesservicesservicesservices");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    /// <summary>
    /// Functions the evaluated script itself declared, nested ones included: their definitions are the ones
    /// the engine caches by AST node, and a nested declaration's is cached from its very first instantiation.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void FunctionsTheScriptDeclaredReleaseWhatTheirBodiesCached(bool discard)
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("function outer(o) { function inner(p) { return p.Name; } return inner(o) + describe('y'); } outer(ctx)");

        RunRequestThroughDeclaredFunctions(engine, snapshot, script);
        var services = RunRequestThroughDeclaredFunctions(engine, snapshot, script);

        AssertReleasedOnlyWhenDiscarded(engine, services, discard);

        RunRequestThroughDeclaredFunctions(engine, snapshot, script);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequestThroughDeclaredFunctions(Engine engine, GlobalSnapshot snapshot, Prepared<Script> script)
    {
        var services = new RequestServices();
        engine.SetValue("ctx", services);
        engine.SetValue("describe", new Func<string, string>(value => value + ":" + services.Name));
        engine.Evaluate(script).AsString().Should().Be("servicesy:services");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    /// <summary>
    /// <c>new Function</c> with a source seen before is served from the realm's compilation cache, whose
    /// definition carries the warmed body and the parked call environment of the last instance.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void ACachedFunctionConstructorBodyReleasesWhatItCached(bool discard)
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("new Function('o', 'return o.Name + this.Name').call(ctx, ctx)");

        RunRequestThroughAFunctionConstructor(engine, snapshot, script);
        RunRequestThroughAFunctionConstructor(engine, snapshot, script);
        var services = RunRequestThroughAFunctionConstructor(engine, snapshot, script);

        AssertReleasedOnlyWhenDiscarded(engine, services, discard);

        RunRequestThroughAFunctionConstructor(engine, snapshot, script);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequestThroughAFunctionConstructor(Engine engine, GlobalSnapshot snapshot, Prepared<Script> script)
    {
        var services = new RequestServices();
        engine.SetValue("ctx", services);
        engine.Evaluate(script).AsString().Should().Be("servicesservices");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    /// <summary>
    /// Not a handler tree at all: the engine pools <c>Reference</c> objects, and a returned one keeps the base
    /// it last resolved against — here the wrapped object a member assignment wrote to.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void APooledReferenceReleasesTheBaseItLastResolved(bool discard)
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var services = RunRequestAssigningAMember(engine, snapshot);

        AssertReleasedOnlyWhenDiscarded(engine, services, discard);

        RunRequestAssigningAMember(engine, snapshot);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequestAssigningAMember(Engine engine, GlobalSnapshot snapshot)
    {
        var services = new RequestServices();
        engine.SetValue("ctx", services);

        // parsed afresh on every call, so no handler tree is cached and only the pool can keep anything; the
        // write itself is refused by the default interop configuration, the Reference is resolved regardless
        engine.Evaluate("ctx.Label = 'unused'; ctx.Name").AsString().Should().Be("services");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    private static void AssertReleasedOnlyWhenDiscarded(Engine engine, WeakReference services, bool discard)
    {
        if (discard)
        {
            engine.Advanced.DiscardInterpreterCaches();
        }

        CollectEverything();

        if (discard)
        {
            services.IsAlive.Should().BeFalse("discarding the interpreter caches must leave nothing in the engine that reaches a finished request's objects");
        }
        else
        {
            services.IsAlive.Should().BeTrue("without a discard the warmed site keeps it, which is what makes this scenario a test of the discard at all");
        }

        GC.KeepAlive(engine);
    }

    private static void CollectEverything()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    /// <summary>
    /// The discard keeps the marker that a script has run here, so the very next evaluation builds the tree
    /// into the cache again rather than starting over from the uncached first-evaluation path.
    /// </summary>
    [Test]
    public void TheNextEvaluationCachesTheTreeAgainAtOnce()
    {
        var engine = new Engine();
        var script = Engine.PrepareScript("function f(x) { return x * 2; } f(21)");

        engine.Evaluate(script).AsNumber().Should().Be(42);
        engine.Evaluate(script).AsNumber().Should().Be(42);
        ScriptStatementListCount(engine).Should().Be(1);
        FunctionDefinitionCount(engine).Should().BeGreaterThan(0);

        engine.Advanced.DiscardInterpreterCaches();
        ScriptStatementListCount(engine).Should().Be(0);
        FunctionDefinitionCount(engine).Should().Be(0);

        engine.Evaluate(script).AsNumber().Should().Be(42);
        ScriptStatementListCount(engine).Should().Be(1);
    }

    /// <summary>
    /// A generator holds the body tree it started with, so dropping its definition's tree must not disturb a
    /// resume — the replay finds its parked position on the very handlers that parked it.
    /// </summary>
    [Test]
    public void ASuspendedGeneratorResumesAcrossADiscard()
    {
        var engine = new Engine();
        engine.Execute("var log = []; function* g() { log.push(1); yield 1; log.push(2); yield 2; } var it = g(); it.next();");

        engine.Advanced.DiscardInterpreterCaches();

        engine.Evaluate("it.next().value").AsNumber().Should().Be(2);
        engine.Evaluate("log.join()").AsString().Should().Be("1,2");
        engine.Evaluate("it.next().done").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// An async arrow with an expression body resumes by re-reading its body handler, so a discard landing
    /// while one is suspended must leave the resume a handler to read.
    /// </summary>
    [Test]
    public void ASuspendedAsyncArrowResumesAcrossADiscard()
    {
        var engine = new Engine();
        var pending = engine.Tasks.RegisterPromise();
        engine.SetValue("pending", pending.Promise);
        engine.Execute("var result; const add = async x => (await x) + 1; add(pending).then(v => result = v);");

        engine.Advanced.DiscardInterpreterCaches();

        pending.Resolve(41);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("result").AsNumber().Should().Be(42);
    }

    [Test]
    public void DiscardingFromInsideAnEvaluationIsRefused()
    {
        var engine = new Engine();
        engine.SetValue("discard", new Action(() => engine.Advanced.DiscardInterpreterCaches()));

        var thrown = Caught.Exception(() => engine.Execute("discard()"));

        thrown.Should().BeOfType<InvalidOperationException>();
    }
}
