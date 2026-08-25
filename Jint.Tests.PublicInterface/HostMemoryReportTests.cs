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
    [Fact]
    public void TheReportIsReachableAndItsFiguresAreConsistent()
    {
        var engine = new Engine();
        engine.Execute("var greeting = 'hello';");

        EngineMemoryReport report = engine.Diagnostics.GetMemoryReport();

        Assert.True(report.GlobalPropertyCount > 0);
        Assert.InRange(report.MaterializedGlobalPropertyCount, 0, report.GlobalPropertyCount);
        Assert.Equal(0, report.EventLoopQueueDepth);
        Assert.Equal(0, report.RegisteredModuleCount);

        var census = report.ObjectCensus;
        Assert.Equal(
            census.ObjectCount,
            census.PlainObjects + census.Arrays + census.Functions + census.HostWrappers + census.OtherObjects);
        Assert.InRange(census.ObjectCount, 1, census.Bound);
    }

    [Fact]
    public void TakingTheReportChangesNothingItReports()
    {
        var engine = new Engine();
        engine.Execute("function twice(x) { return x * 2; } twice(21);");

        var first = engine.Diagnostics.GetMemoryReport();
        var second = engine.Diagnostics.GetMemoryReport();

        // The report types are records, so one comparison covers every figure including the nested ones.
        // This is the property a host relies on when it logs the report on every request.
        Assert.Equal(first, second);
    }

    [Fact]
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
        Assert.Equal(baseline.GlobalPropertyCount, after.GlobalPropertyCount);
        Assert.Equal(0, after.LexicalGlobalBindingCount);

        // What it does not, and what the report is for: the engine has materialized part of its global
        // surface, and that stays materialized. A host watching for growth wants to see this figure settle,
        // not stay at zero.
        Assert.True(after.MaterializedGlobalPropertyCount >= baseline.MaterializedGlobalPropertyCount);
    }

    [Fact]
    public void HandlerTreeCachesShowTheSecondRunOfAScriptBeingCached()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("function total(a, b) { return a + b; } total(1, 2);");

        engine.Execute(prepared);
        var afterFirst = engine.Diagnostics.GetMemoryReport().HandlerTreeCaches;
        Assert.Equal(1, afterFirst.EvaluatedScripts);
        Assert.Equal(0, afterFirst.ScriptStatementLists);

        engine.Execute(prepared);
        var afterSecond = engine.Diagnostics.GetMemoryReport().HandlerTreeCaches;
        Assert.Equal(1, afterSecond.EvaluatedScripts);
        Assert.Equal(1, afterSecond.ScriptStatementLists);
    }

    [Fact]
    public void ModuleRegistryGrowthSurvivesARestore()
    {
        var engine = new Engine();
        engine.Modules.Add("lib", "export const answer = 42;");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Modules.Import("lib");
        Assert.Equal(1, engine.Diagnostics.GetMemoryReport().RegisteredModuleCount);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // Honesty pin: the module registry is deliberately outside what a restore reverts, so on a pooled
        // engine this count only ever grows. Being able to see that is the point.
        Assert.Equal(1, engine.Diagnostics.GetMemoryReport().RegisteredModuleCount);
    }

    [Fact]
    public void TheCensusBoundIsHonoured()
    {
        var engine = new Engine();
        engine.Execute("var chain = { a: { b: { c: { d: {} } } } };");

        var bounded = engine.Diagnostics.GetMemoryReport(objectCensusBound: 3).ObjectCensus;
        Assert.Equal(3, bounded.Bound);
        Assert.Equal(3, bounded.ObjectCount);
        Assert.True(bounded.BoundReached);

        var whole = engine.Diagnostics.GetMemoryReport(objectCensusBound: 100_000).ObjectCensus;
        Assert.False(whole.BoundReached);
        Assert.True(whole.ObjectCount > bounded.ObjectCount);
    }

    [Fact]
    public void ANonPositiveBoundSkipsTheCensusWithoutSkippingTheReport()
    {
        var engine = new Engine();
        engine.Execute("var probe = { nested: {} };");

        var report = engine.Diagnostics.GetMemoryReport(objectCensusBound: 0);

        Assert.Equal(0, report.ObjectCensus.ObjectCount);
        Assert.False(report.ObjectCensus.BoundReached);
        Assert.True(report.GlobalPropertyCount > 0);
    }

    [Fact]
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

        Assert.Equal(0, invocations);
    }

    [Fact]
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
        Assert.Equal(0, invocations);

        // Installed eagerly, resolved lazily: the property is visible to the count, its value is not built.
        Assert.True(installed.GlobalPropertyCount > 0);

        Assert.Equal("built", engine.Evaluate("expensive").AsString());
        Assert.Equal(1, invocations);

        var resolved = engine.Diagnostics.GetMemoryReport();
        Assert.True(resolved.MaterializedGlobalPropertyCount > installed.MaterializedGlobalPropertyCount);
    }
}
