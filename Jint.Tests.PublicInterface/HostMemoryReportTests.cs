#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System;
using Jint.Diagnostics;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>engine.Diagnostics.GetMemoryReport()</c> from a third party's side. This project has no internals access,
/// so everything exercised here is genuinely reachable by an embedder: the report types, the counts, and the
/// bound on the object census.
///
/// <para>
/// The case the API exists for is the pooled engine. An engine reused across requests keeps things a fresh
/// one never had — warmed handler trees, a module registry a snapshot restore deliberately does not revert,
/// globals a script installed — and each of those is documented but was not <em>observable</em>. What is
/// pinned here is the shape a host actually uses: take a report, do a request, take another, compare.
/// </para>
/// </summary>
public class HostMemoryReportTests
{
    [Test]
    public void TheReportIsReachableAndItsFiguresAreConsistent()
    {
        var engine = new Engine();
        engine.Execute("var greeting = 'hello';");

        EngineMemoryReport report = engine.Diagnostics.GetMemoryReport();

        Assert.That(report.GlobalPropertyCount > 0);
        Assert.That(report.MaterializedGlobalPropertyCount, Is.InRange(0, report.GlobalPropertyCount));
        Assert.That(report.EventLoopQueueDepth, Is.EqualTo(0));
        Assert.That(report.RegisteredModuleCount, Is.EqualTo(0));

        var census = report.ObjectCensus;
        Assert.That(
            census.PlainObjects + census.Arrays + census.Functions + census.HostWrappers + census.OtherObjects,
            Is.EqualTo(census.ObjectCount));
        Assert.That(census.ObjectCount, Is.InRange(1, census.Bound));
    }

    [Test]
    public void TakingTheReportChangesNothingItReports()
    {
        var engine = new Engine();
        engine.Execute("function twice(x) { return x * 2; } twice(21);");

        var first = engine.Diagnostics.GetMemoryReport();
        var second = engine.Diagnostics.GetMemoryReport();

        // The report types are records, so one comparison covers every figure including the nested ones.
        // This is the property a host relies on when it logs the report on every request.
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void APooledEngineShowsWhatItIsStillHoldingAfterARestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var baseline = engine.Diagnostics.GetMemoryReport();

        for (var request = 0; request < 5; request++)
        {
            engine.Execute("var perRequest = { id: 1 }; let scoped = 2;");
            engine.Advanced.RestoreGlobalSnapshot(snapshot);
        }

        var after = engine.Diagnostics.GetMemoryReport();

        // What the restore does revert: the globals and the lexical declarations each request added.
        Assert.That(after.GlobalPropertyCount, Is.EqualTo(baseline.GlobalPropertyCount));
        Assert.That(after.LexicalGlobalBindingCount, Is.EqualTo(0));

        // What it does not, and what the report is for: the engine has materialized part of its global
        // surface, and that stays materialized. A host watching for growth wants to see this figure settle,
        // not stay at zero.
        Assert.That(after.MaterializedGlobalPropertyCount >= baseline.MaterializedGlobalPropertyCount);
    }

    [Test]
    public void HandlerTreeCachesShowTheSecondRunOfAScriptBeingCached()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("function total(a, b) { return a + b; } total(1, 2);");

        engine.Execute(prepared);
        var afterFirst = engine.Diagnostics.GetMemoryReport().HandlerTreeCaches;
        Assert.That(afterFirst.EvaluatedScripts, Is.EqualTo(1));
        Assert.That(afterFirst.ScriptStatementLists, Is.EqualTo(0));

        engine.Execute(prepared);
        var afterSecond = engine.Diagnostics.GetMemoryReport().HandlerTreeCaches;
        Assert.That(afterSecond.EvaluatedScripts, Is.EqualTo(1));
        Assert.That(afterSecond.ScriptStatementLists, Is.EqualTo(1));
    }

    [Test]
    public void ModuleRegistryGrowthSurvivesARestore()
    {
        var engine = new Engine();
        engine.Modules.Add("lib", "export const answer = 42;");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Modules.Import("lib");
        Assert.That(engine.Diagnostics.GetMemoryReport().RegisteredModuleCount, Is.EqualTo(1));

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // Honesty pin: the module registry is deliberately outside what a restore reverts, so on a pooled
        // engine this count only ever grows. Being able to see that is the point.
        Assert.That(engine.Diagnostics.GetMemoryReport().RegisteredModuleCount, Is.EqualTo(1));
    }

    [Test]
    public void TheCensusBoundIsHonoured()
    {
        var engine = new Engine();
        engine.Execute("var chain = { a: { b: { c: { d: {} } } } };");

        var bounded = engine.Diagnostics.GetMemoryReport(objectCensusBound: 3).ObjectCensus;
        Assert.That(bounded.Bound, Is.EqualTo(3));
        Assert.That(bounded.ObjectCount, Is.EqualTo(3));
        Assert.That(bounded.BoundReached);

        var whole = engine.Diagnostics.GetMemoryReport(objectCensusBound: 100_000).ObjectCensus;
        Assert.That(whole.BoundReached, Is.False);
        Assert.That(whole.ObjectCount > bounded.ObjectCount);
    }

    [Test]
    public void ANonPositiveBoundSkipsTheCensusWithoutSkippingTheReport()
    {
        var engine = new Engine();
        engine.Execute("var probe = { nested: {} };");

        var report = engine.Diagnostics.GetMemoryReport(objectCensusBound: 0);

        Assert.That(report.ObjectCensus.ObjectCount, Is.EqualTo(0));
        Assert.That(report.ObjectCensus.BoundReached, Is.False);
        Assert.That(report.GlobalPropertyCount > 0);
    }

    [Test]
    public void TheCensusNeverInvokesAHostAccessor()
    {
        var invocations = 0;
        var engine = new Engine();
        engine.SetValue("readCounter", new Func<JsValue>(() =>
        {
            invocations++;
            return JsValue.Undefined;
        }));

        engine.Execute("globalThis.probe = { get live() { return readCounter(); } };");
        invocations = 0;

        engine.Diagnostics.GetMemoryReport();

        Assert.That(invocations, Is.EqualTo(0));
    }

    [Test]
    public void TheCensusNeverRunsALazyGlobalFactory()
    {
        var invocations = 0;
        var engine = new Engine();
        engine.AddLazyGlobal("expensive", _ =>
        {
            invocations++;
            return new JsString("built");
        });

        var installed = engine.Diagnostics.GetMemoryReport();
        Assert.That(invocations, Is.EqualTo(0));

        // Installed eagerly, resolved lazily: the property is visible to the count, its value is not built.
        Assert.That(installed.GlobalPropertyCount > 0);

        Assert.That(engine.Evaluate("expensive").AsString(), Is.EqualTo("built"));
        Assert.That(invocations, Is.EqualTo(1));

        var resolved = engine.Diagnostics.GetMemoryReport();
        Assert.That(resolved.MaterializedGlobalPropertyCount > installed.MaterializedGlobalPropertyCount);
    }
}
