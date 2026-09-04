#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Engine.Diagnostics.GetMemoryReport()</c> counts what one engine is holding. Two properties matter more
/// than any individual figure and are pinned first: the report is derived from state that already exists, so
/// the engine carries no field for it; and reading it materializes nothing, so a host may take it as often as
/// it likes without changing the very numbers it is watching.
/// </summary>
public class EngineMemoryReportTests
{
    [Test]
    public void TwoConsecutiveReportsOnAnIdleEngineAreEqual()
    {
        var engine = new Engine();
        engine.Execute("var total = 0; function add(x) { total += x; } add(1); add(2);");

        var first = engine.Diagnostics.GetMemoryReport();
        var second = engine.Diagnostics.GetMemoryReport();

        // Record equality, so this compares every figure including the nested reports. Taking the report is
        // the only thing that happened between the two, which is exactly what makes it a proof that taking
        // it changes nothing.
        second.Should().Be(first);
    }

    [Test]
    public void AnEngineThatIsReportedOnEndsUpInTheSameStateAsOneThatIsNot()
    {
        const string Script = "var seen = []; function record(x) { seen.push(x); } record(1); record('two'); seen.length;";

        var reported = new Engine();
        var untouched = new Engine();

        _ = reported.Diagnostics.GetMemoryReport();
        reported.Execute(Script);
        _ = reported.Diagnostics.GetMemoryReport();
        reported.Execute(Script);
        _ = reported.Diagnostics.GetMemoryReport();

        untouched.Execute(Script);
        untouched.Execute(Script);

        // The report is derived from collections that already exist and the engine carries no field for it,
        // so an engine that was asked five times and one that was never asked are indistinguishable
        // afterwards. Anything the report recorded on the engine — a cached walk, a visited set, a counter —
        // would show up as a difference here.
        reported.Diagnostics.GetMemoryReport().Should().Be(untouched.Diagnostics.GetMemoryReport());
    }

    [Test]
    public void ReadingTheReportDoesNotMaterializeAnUntouchedBuiltin()
    {
        var engine = new Engine();

        var before = engine.Diagnostics.GetMemoryReport();
        _ = engine.Diagnostics.GetMemoryReport();
        _ = engine.Diagnostics.GetMemoryReport();
        var after = engine.Diagnostics.GetMemoryReport();

        after.MaterializedGlobalPropertyCount.Should().Be(before.MaterializedGlobalPropertyCount);

        // ... and the global surface really is mostly unmaterialized on a fresh engine, so the assertion
        // above is not vacuous.
        before.MaterializedGlobalPropertyCount.Should().BeLessThan(before.GlobalPropertyCount);
    }

    [Test]
    public void ReadingABuiltinMovesItToTheMaterializedSide()
    {
        var engine = new Engine();
        var before = engine.Diagnostics.GetMemoryReport();

        // Straight through the global object rather than through script: an evaluation would materialize
        // whatever else it needed and the delta would stop being one property.
        engine.Global.Get("Array");

        var after = engine.Diagnostics.GetMemoryReport();
        after.MaterializedGlobalPropertyCount.Should().Be(before.MaterializedGlobalPropertyCount + 1);
        after.GlobalPropertyCount.Should().Be(before.GlobalPropertyCount);
    }

    [Test]
    public void ALazyGlobalCountsAsAPropertyUntilItsFactoryRuns()
    {
        var engine = new Engine();
        var invocations = 0;

        var before = engine.Diagnostics.GetMemoryReport();
        engine.AddLazyGlobal("lazy", _ =>
        {
            invocations++;
            return new JsString("resolved");
        });

        var installed = engine.Diagnostics.GetMemoryReport();
        invocations.Should().Be(0, "taking the report must not run a lazy global's factory");
        installed.GlobalPropertyCount.Should().Be(before.GlobalPropertyCount + 1);
        installed.MaterializedGlobalPropertyCount.Should().Be(before.MaterializedGlobalPropertyCount);

        engine.Global.Get("lazy");
        invocations.Should().Be(1);

        var resolved = engine.Diagnostics.GetMemoryReport();
        resolved.GlobalPropertyCount.Should().Be(installed.GlobalPropertyCount);
        resolved.MaterializedGlobalPropertyCount.Should().Be(installed.MaterializedGlobalPropertyCount + 1);
    }

    [Test]
    public void GlobalPropertyCountFollowsHostRegistrations()
    {
        var engine = new Engine();
        var before = engine.Diagnostics.GetMemoryReport();

        engine.SetValue("one", 1);
        engine.SetValue("two", 2);
        engine.SetValue("three", 3);

        engine.Diagnostics.GetMemoryReport().GlobalPropertyCount.Should().Be(before.GlobalPropertyCount + 3);
    }

    [Test]
    public void LexicalDeclarationsCountApartFromGlobalProperties()
    {
        var engine = new Engine();
        var before = engine.Diagnostics.GetMemoryReport();
        before.LexicalGlobalBindingCount.Should().Be(0);

        engine.Execute("let a = 1; const b = 2; class C {}");

        var after = engine.Diagnostics.GetMemoryReport();

        // let/const/class are bindings of the global environment record, not properties of the global object
        // — which is precisely why the report reports the two separately.
        after.LexicalGlobalBindingCount.Should().Be(3);

        engine.Execute("var d = 4;");
        var withVar = engine.Diagnostics.GetMemoryReport();
        withVar.LexicalGlobalBindingCount.Should().Be(3);
        withVar.GlobalPropertyCount.Should().Be(after.GlobalPropertyCount + 1);
    }

    [Test]
    public void HandlerTreeCachesEngageOnlyOnTheSecondEvaluation()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("function greet(name) { return 'hi ' + name; } greet('world');");

        var fresh = engine.Diagnostics.GetMemoryReport();
        fresh.HandlerTreeCaches.EvaluatedScripts.Should().Be(0);
        fresh.HandlerTreeCaches.ScriptStatementLists.Should().Be(0);

        engine.Execute(prepared);

        var afterFirst = engine.Diagnostics.GetMemoryReport();
        afterFirst.HandlerTreeCaches.EvaluatedScripts.Should().Be(1);

        // The first run of a script on an engine builds a tree and caches nothing: that is the
        // fresh-engine-per-operation shape, and it stays byte-identical to what it always was.
        afterFirst.HandlerTreeCaches.ScriptStatementLists.Should().Be(0);

        engine.Execute(prepared);

        var afterSecond = engine.Diagnostics.GetMemoryReport();
        afterSecond.HandlerTreeCaches.EvaluatedScripts.Should().Be(1);
        afterSecond.HandlerTreeCaches.ScriptStatementLists.Should().Be(1);

        // The tree the second run cached is what holds this engine's warmed call and member-read sites, and
        // through them the last receiver and callee each of them served. The report surfaces the roots; see
        // HandlerTreeCacheReport for why it deliberately does not enumerate the sites inside them.
        afterSecond.HandlerTreeCaches.FunctionDefinitions.Should().BeGreaterThan(0);
    }

    [Test]
    public void AComputedPropertyKeyRegistersInThePropertyKeyExpressionCache()
    {
        var engine = new Engine();

        engine.Diagnostics.GetMemoryReport().HandlerTreeCaches.PropertyKeyExpressions.Should().Be(0);

        engine.Evaluate("({ ['k' + 1]: 1 })");

        // One evaluated computed key, one cached handler — the stable identity a suspension inside the key
        // relies on to find its replay state again, and a retention root like the other three counts here.
        engine.Diagnostics.GetMemoryReport().HandlerTreeCaches.PropertyKeyExpressions.Should().Be(1);
    }

    [Test]
    public void EachDistinctScriptAddsOneEvaluatedScriptEntry()
    {
        var engine = new Engine();

        engine.Execute(Engine.PrepareScript("1;"));
        engine.Execute(Engine.PrepareScript("2;"));
        engine.Execute(Engine.PrepareScript("3;"));

        engine.Diagnostics.GetMemoryReport().HandlerTreeCaches.EvaluatedScripts.Should().Be(3);
    }

    [Test]
    public void ModuleRegistryCountsWhatWasActuallyImported()
    {
        var engine = new Engine();
        engine.Modules.Add("lib", "export const answer = 42;");

        // A registration nobody imported has produced no module record.
        engine.Diagnostics.GetMemoryReport().RegisteredModuleCount.Should().Be(0);

        engine.Modules.Import("lib");

        var after = engine.Diagnostics.GetMemoryReport();
        after.RegisteredModuleCount.Should().Be(1);
        after.PendingModuleLoadCount.Should().Be(0);
    }

    [Test]
    public void EventLoopQueueDepthSeesQueuedWorkAndItsDrain()
    {
        var engine = new Engine();
        engine.Diagnostics.GetMemoryReport().EventLoopQueueDepth.Should().Be(0);

        engine.AddToEventLoop(static () => { }, EventLoopJobKind.Task);
        engine.AddToEventLoop(static () => { }, EventLoopJobKind.Task);

        engine.Diagnostics.GetMemoryReport().EventLoopQueueDepth.Should().Be(2);

        engine.Tasks.ProcessTasks();

        engine.Diagnostics.GetMemoryReport().EventLoopQueueDepth.Should().Be(0);
    }

    [Test]
    public void PendingAtomicsWaiterCountSeesAFiniteTimeoutWait()
    {
        var engine = new Engine();
        engine.Diagnostics.GetMemoryReport().PendingAtomicsWaiterCount.Should().Be(0);

        // int.MaxValue milliseconds is where the engine clamps a wait's deadline, so this one cannot come due
        // while the test runs and the assertion is structural rather than a race against a clock.
        engine.Execute("""
            var sab = new SharedArrayBuffer(8);
            var ta = new Int32Array(sab);
            globalThis.wait = Atomics.waitAsync(ta, 0, 0, 2147483647);
            """);

        engine.Evaluate("wait.async").AsBoolean().Should().BeTrue();
        engine.Diagnostics.GetMemoryReport().PendingAtomicsWaiterCount.Should().Be(1);
    }

    [Test]
    public void PoolCountsFollowARentAndReturn()
    {
        var engine = new Engine();
        var before = engine.Diagnostics.GetMemoryReport();

        var reference = engine._referencePool.Rent(JsValue.Undefined, JsString.Empty, strict: false, thisValue: null);
        engine.Diagnostics.GetMemoryReport().Pools.PooledReferences.Should().Be(before.Pools.PooledReferences);

        engine._referencePool.Return(reference);
        engine.Diagnostics.GetMemoryReport().Pools.PooledReferences.Should().Be(before.Pools.PooledReferences + 1);
    }

    [Test]
    public void PooledArraySlotsAreTheSumOfTheArrayLengths()
    {
        var engine = new Engine();
        var before = engine.Diagnostics.GetMemoryReport().Pools;

        engine._jsValueArrayPool.ReturnArray(new JsValue[3]);

        var after = engine.Diagnostics.GetMemoryReport().Pools;
        after.PooledJsValueArrays.Should().Be(before.PooledJsValueArrays + 1);
        after.PooledJsValueArraySlots.Should().Be(before.PooledJsValueArraySlots + 3);
    }

    [Test]
    public void InteropCachesCountThisEnginesTypeReferences()
    {
        var engine = new Engine(options => options.AllowClr(typeof(EngineMemoryReportTests).Assembly));
        var before = engine.Diagnostics.GetMemoryReport();

        engine.SetValue("probe", typeof(Probe));

        engine.Diagnostics.GetMemoryReport().InteropCaches.TypeReferenceCount
            .Should().Be(before.InteropCaches.TypeReferenceCount + 1);
    }

    [Test]
    public void CensusCountsTheObjectsAHostRegistrationBringsWithIt()
    {
        var engine = new Engine();

        // Built before the baseline so that constructing it cannot be mistaken for what attaching it costs.
        var graph = engine.Evaluate("({ a: {}, b: {} })");
        var before = engine.Diagnostics.GetMemoryReport();

        engine.SetValue("graph", graph);

        var after = engine.Diagnostics.GetMemoryReport();
        after.ObjectCensus.ObjectCount.Should().Be(before.ObjectCensus.ObjectCount + 3);
        after.ObjectCensus.PlainObjects.Should().Be(before.ObjectCensus.PlainObjects + 3);
        after.ObjectCensus.BoundReached.Should().BeFalse();
    }

    [Test]
    public void CensusSeparatesArraysFunctionsAndHostWrappers()
    {
        var engine = new Engine();
        var array = engine.Evaluate("[1, 2, 3]");
        var callback = engine.Evaluate("(function () { return 1; })");

        var before = engine.Diagnostics.GetMemoryReport().ObjectCensus;

        engine.SetValue("array", array);
        var withArray = engine.Diagnostics.GetMemoryReport().ObjectCensus;
        withArray.Arrays.Should().Be(before.Arrays + 1);

        engine.SetValue("callback", callback);
        var withCallback = engine.Diagnostics.GetMemoryReport().ObjectCensus;
        withCallback.Functions.Should().Be(withArray.Functions + 1);

        engine.SetValue("host", new Probe());
        var withHost = engine.Diagnostics.GetMemoryReport().ObjectCensus;
        withHost.HostWrappers.Should().Be(withCallback.HostWrappers + 1);
    }

    [Test]
    public void CensusStopsAtItsBoundAndSaysSo()
    {
        var engine = new Engine();
        engine.Execute("var deep = { a: { b: { c: { d: {} } } } };");

        var bounded = engine.Diagnostics.GetMemoryReport(objectCensusBound: 4).ObjectCensus;
        bounded.Bound.Should().Be(4);
        bounded.ObjectCount.Should().Be(4);
        bounded.BoundReached.Should().BeTrue();

        var whole = engine.Diagnostics.GetMemoryReport(objectCensusBound: 1_000_000).ObjectCensus;
        whole.BoundReached.Should().BeFalse();
        whole.ObjectCount.Should().BeGreaterThan(4);

        // The bound is the only thing that changed, and it changed nothing else: a census that had
        // materialized anything on the way would make these two disagree.
        engine.Diagnostics.GetMemoryReport(objectCensusBound: 4).ObjectCensus.Should().Be(bounded);
    }

    [Test]
    public void CensusRespectsItsBoundInsideALargeArray()
    {
        var engine = new Engine();
        engine.Execute("var big = []; for (var i = 0; i < 20000; i++) { big.push({}); }");

        // The array is walked element by element straight out of its backing store, and the bound ends the
        // walk in the middle of it — nothing collects the elements into a list first, which for a dense
        // array of ten million would be the largest allocation in the process.
        var census = engine.Diagnostics.GetMemoryReport(objectCensusBound: 100).ObjectCensus;

        census.ObjectCount.Should().Be(100);
        census.BoundReached.Should().BeTrue();
    }

    [Test]
    public void ANonPositiveBoundSkipsTheCensusEntirely()
    {
        var engine = new Engine();
        engine.Execute("var probe = { a: {} };");

        var skipped = engine.Diagnostics.GetMemoryReport(objectCensusBound: 0).ObjectCensus;
        skipped.Bound.Should().Be(0);
        skipped.BoundReached.Should().BeFalse();
        skipped.ObjectCount.Should().Be(0);

        // The counts the census does not need are still there, so this really is "skip the walk" and not
        // "skip the report".
        engine.Diagnostics.GetMemoryReport(objectCensusBound: -1).GlobalPropertyCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void CensusNeverInvokesAnAccessor()
    {
        var engine = new Engine();
        engine.Execute("globalThis.probe = { get boom() { throw new Error('the census invoked an accessor'); } };");

        // Reading descriptor.Value instead of testing the descriptor's flags is all it would take to break
        // this, and it would surface as a JavaScriptException out of a diagnostic call.
        var report = engine.Diagnostics.GetMemoryReport();

        report.ObjectCensus.ObjectCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void CensusNeverInvokesAHostBackedGetter()
    {
        var engine = new Engine();
        var probe = new Probe();
        engine.SetValue("host", probe);

        // Read the member once so the wrapper caches the descriptor that reads it. Without this the wrapper
        // holds no descriptor at all and the assertion below would be true for the wrong reason.
        engine.Evaluate("host.Value");
        probe.ReadCount.Should().Be(1);
        probe.Reset();

        // That cached descriptor produces its value by invoking the CLR getter on every read, which is host
        // code — a diagnostic must not run it behind the host's back.
        var report = engine.Diagnostics.GetMemoryReport();

        probe.ReadCount.Should().Be(0);
        report.ObjectCensus.HostWrappers.Should().BeGreaterThan(0);
    }

    [Test]
    public void CensusDoesNotFireProxyTraps()
    {
        var engine = new Engine();
        engine.Execute("""
            globalThis.trapped = new Proxy({}, {
                get() { throw new Error('get trap fired'); },
                ownKeys() { throw new Error('ownKeys trap fired'); },
                getOwnPropertyDescriptor() { throw new Error('getOwnPropertyDescriptor trap fired'); }
            });
            """);

        var report = engine.Diagnostics.GetMemoryReport();

        report.ObjectCensus.ObjectCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void CensusTerminatesOnACyclicGraph()
    {
        var engine = new Engine();
        engine.Execute("var a = {}; var b = { a: a }; a.b = b; a.self = a;");

        var census = engine.Diagnostics.GetMemoryReport().ObjectCensus;

        census.BoundReached.Should().BeFalse();
        census.ObjectCount.Should().BeGreaterThan(1);
    }

    [Test]
    public void CensusWalksArrayElementsWithoutMaterializingKeys()
    {
        var engine = new Engine();
        var array = engine.Evaluate("[{}, {}, {}]");
        var before = engine.Diagnostics.GetMemoryReport().ObjectCensus;

        engine.SetValue("array", array);

        var after = engine.Diagnostics.GetMemoryReport().ObjectCensus;
        after.Arrays.Should().Be(before.Arrays + 1);
        after.PlainObjects.Should().Be(before.PlainObjects + 3);
    }

    [Test]
    public void PendingTimerCountIsZeroWithoutTheTimerGlobals()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");

        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(0);
    }

    /// <summary>
    /// A CLR object with one readable member, so that "the census must not run a host getter" has something
    /// to be true of.
    /// </summary>
    private sealed class Probe
    {
        public int ReadCount { get; private set; }

        public string Value
        {
            get
            {
                ReadCount++;
                return "read";
            }
        }

        public void Reset() => ReadCount = 0;
    }
}
