#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System.Collections.Generic;
using System.Threading;
using Jint.Constraints;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers <see cref="Engine.AdvancedOperations.GetInteropConversionDiagnostics"/>, the answer to a question a
/// host cannot otherwise ask: did any CLR array cross into script during this run, and under which semantics?
/// <para>
/// <see cref="ArrayConversionMode.Copy"/> is the default and <see cref="ArrayConversionMode.LiveView"/> remains
/// an explicit performance opt-in. The modes differ in behavior a script can observe — a live view reads through
/// to the CLR array and writes through only when CLR writes are separately enabled, while a copy does neither.
/// A host that does not own all of its Jint consumers therefore has a real audit to perform and, before these
/// counters, no way to perform it except by reading every transitive caller. These tests live outside the Jint
/// assembly on purpose: the project has no internals access, so the audit written here is one a third party can
/// write.
/// </para>
/// </summary>
public class HostArrayConversionDiagnosticsTests
{
    public static bool MemoryAccountingAvailable => MemoryLimitConstraint.Accuracy != MemoryLimitAccuracy.Unavailable;

    private static Engine CreateEngine(ArrayConversionMode? mode = null) => new(options =>
    {
        options.AllowClr(typeof(HostWithArrays).Assembly);
        options.AllowClrWrite();
        if (mode is not null)
        {
            options.Interop.ArrayConversion = mode.Value;
        }
    });

    // ---- reachability ----

    [Fact]
    public void TheCountersAreReadableFromOutsideTheJintAssemblyAndStartAtZero()
    {
        // This project has no InternalsVisibleTo, so the call below compiling at all is the guarantee.
        var diagnostics = new Engine().Advanced.GetInteropConversionDiagnostics();

        diagnostics.ArrayLiveViewConversions.Should().Be(0);
        diagnostics.ArrayCopyConversions.Should().Be(0);
    }

    // ---- the audit ----

    [Fact]
    public void CopyIsTheDefaultAndLiveViewRemainsAnExplicitOptIn()
    {
        new Options().Interop.ArrayConversion.Should().Be(ArrayConversionMode.Copy);

        var copied = new HostWithArrays();
        var defaultEngine = CreateEngine();
        defaultEngine.SetValue("host", copied);
        defaultEngine.Evaluate("Array.isArray(host.Values)").Should().BeTrue();
        defaultEngine.Execute("host.Values[0] = 99;");
        copied.Values[0].Should().Be(1);

        var shared = new HostWithArrays();
        var liveViewEngine = CreateEngine(ArrayConversionMode.LiveView);
        liveViewEngine.SetValue("host", shared);
        liveViewEngine.Evaluate("Array.isArray(host.Values)").Should().BeFalse();
        liveViewEngine.Execute("host.Values[0] = 99;");
        shared.Values[0].Should().Be(99);
    }

    [Fact]
    public void CopyMutationRemainsAllowedWhileLiveViewWriteThroughRequiresBothOptIns()
    {
        var copied = new[] { 1, 2 };
        var copyEngine = new Engine();
        copyEngine.SetValue("values", copied);
        copyEngine.Execute("values[0] = 9; values.push(3);");
        copied.Should().Equal(1, 2);
        copyEngine.Evaluate("values.join(',')").Should().Be("9,2,3");

        var readOnlyLive = new[] { 1, 2 };
        var readOnlyEngine = new Engine(options =>
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView);
        readOnlyEngine.SetValue("values", readOnlyLive);
        readOnlyLive[0] = 7;
        readOnlyEngine.Evaluate("values[0]").Should().Be(7);
        Invoking(() => readOnlyEngine.Execute("'use strict'; values[0] = 9;"))
            .Should().Throw<JavaScriptException>();
        readOnlyLive[0].Should().Be(7);

        var writableLive = new[] { 1, 2 };
        var writableEngine = new Engine(options =>
        {
            options.AllowClrWrite();
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });
        writableEngine.SetValue("values", writableLive);
        writableEngine.Execute("values[0] = 9;");
        writableLive[0].Should().Be(9);
    }

    [Fact(Skip = "Managed allocation accounting is unavailable on this runtime.", SkipUnless = nameof(MemoryAccountingAvailable))]
    public void FailedMemoryBoundCopyPublishesNothingAndLaterCopyCanRetry()
    {
        var engine = new Engine(options => options.LimitMemory(256_000));
        var oversized = new object[100_000];

        Invoking(() => engine.SetValue("oversized", oversized))
            .Should().ThrowExactly<MemoryLimitExceededException>();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(0);

        engine.SetValue("small", new[] { 1, 2, 3 });
        engine.Evaluate("small.join(',')").Should().Be("1,2,3");
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void FailedDeadlineBoundCopyPublishesNothingAndCanRetryAfterDisarm()
    {
        var deadline = new OperationDeadlineConstraint();
        var engine = new Engine(options => options.Constraint(deadline));
        deadline.Begin(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);

        Invoking(() => engine.SetValue("timedOut", new object[100_000]))
            .Should().ThrowExactly<TimeoutException>();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(0);

        deadline.End();
        engine.SetValue("retry", new[] { 1, 2, 3 });
        engine.Evaluate("retry.join(',')").Should().Be("1,2,3");
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void AnAuditOfARunThatCrossesNoArrayAnswersZero()
    {
        // The negative result is the one the audit usually wants, and the one that has to be trustworthy: a
        // busy run over host objects, strings, numbers, a delegate, a dictionary and a list — everything an
        // embedder normally projects except an array — must leave both counters untouched. Anything that
        // counted an ordinary wrapper would make the audit useless by always answering "yes".
        var engine = CreateEngine();
        engine.SetValue("host", new HostWithArrays());
        engine.SetValue("callback", new Func<int, int>(x => x * 2));

        engine.Execute(
            """
            var total = 0;
            for (var i = 0; i < 5; i++) {
                total += host.Number + host.Text.length + callback(i);
                total += host.Names.length;
                total += host.Lookup.Count;
                total += host.Nested.Number;
            }
            """);

        var diagnostics = engine.Advanced.GetInteropConversionDiagnostics();
        diagnostics.ArrayLiveViewConversions.Should().Be(0);
        diagnostics.ArrayCopyConversions.Should().Be(0);
    }

    [Fact]
    public void AnAuditOfARunThatCrossesAnArrayAnswersHowItCrossed()
    {
        // ...and the positive result names the semantics, which is the half that decides whether the host
        // cares. The same script under the two modes gives the host two different facts about its own data.
        var liveViewEngine = CreateEngine(ArrayConversionMode.LiveView);
        liveViewEngine.SetValue("host", new HostWithArrays());
        liveViewEngine.Evaluate("host.Values[0]").Should().Be(1);

        var live = liveViewEngine.Advanced.GetInteropConversionDiagnostics();
        live.ArrayLiveViewConversions.Should().Be(1);
        live.ArrayCopyConversions.Should().Be(0);

        var copyEngine = CreateEngine(ArrayConversionMode.Copy);
        copyEngine.SetValue("host", new HostWithArrays());
        copyEngine.Evaluate("host.Values[0]").Should().Be(1);

        var copy = copyEngine.Advanced.GetInteropConversionDiagnostics();
        copy.ArrayLiveViewConversions.Should().Be(0);
        copy.ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void TheCountedSemanticsAreTheOnesTheScriptObserves()
    {
        // The counter is only worth reading if it names what actually happened, so pin it against the
        // behavioural difference itself: a live view writes through to the CLR array, a copy does not.
        var host = new HostWithArrays();
        var liveViewEngine = CreateEngine(ArrayConversionMode.LiveView);
        liveViewEngine.SetValue("host", host);
        liveViewEngine.Execute("host.Values[0] = 99;");

        host.Values[0].Should().Be(99);
        liveViewEngine.Advanced.GetInteropConversionDiagnostics().ArrayLiveViewConversions.Should().Be(1);

        var copied = new HostWithArrays();
        var copyEngine = CreateEngine(ArrayConversionMode.Copy);
        copyEngine.SetValue("host", copied);
        copyEngine.Execute("host.Values[0] = 99;");

        copied.Values[0].Should().Be(1);
        copyEngine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);
    }

    // ---- what is and is not counted ----

    [Fact]
    public void ARecrossingServedFromTheIdentityCacheIsNotCountedAgain()
    {
        // A cache hit performs no conversion, so counting it would be counting nothing. It also cannot hide a
        // crossing: a hit implies an earlier miss that was counted, which is exactly what "did arrays cross"
        // needs. Documented as part of the counted set, so pinned here.
        var engine = CreateEngine();
        engine.SetValue("host", new HostWithArrays());

        engine.Execute("for (var i = 0; i < 10; i++) { host.Values[0]; }");

        var diagnostics = engine.Advanced.GetInteropConversionDiagnostics();
        diagnostics.ArrayLiveViewConversions.Should().Be(0);
        diagnostics.ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void DistinctArraysAreCountedSeparately()
    {
        var engine = CreateEngine();
        engine.SetValue("host", new HostWithArrays());

        engine.Execute("host.Values[0]; host.Others[0];");

        var diagnostics = engine.Advanced.GetInteropConversionDiagnostics();
        diagnostics.ArrayLiveViewConversions.Should().Be(0);
        diagnostics.ArrayCopyConversions.Should().Be(2);
    }

    [Fact]
    public void AnArrayNoViewCanBeBuiltOverIsCountedAsTheCopyItBecomes()
    {
        // LiveView is a preference, not a guarantee: a wrap handler declining the value drops the conversion
        // to the copy lane. The counter follows what happened rather than what was configured, which is the
        // whole reason a host reads it instead of reading its own options.
        var engine = new Engine(options =>
        {
            options.AllowClr(typeof(HostWithArrays).Assembly);
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            options.Interop.WrapObjectHandler = static (e, target, type) =>
                target is Array ? null : ObjectWrapper.Create(e, target, type);
        });
        engine.SetValue("host", new HostWithArrays());

        engine.Evaluate("host.Values[0]").Should().Be(1);

        var diagnostics = engine.Advanced.GetInteropConversionDiagnostics();
        diagnostics.ArrayLiveViewConversions.Should().Be(0);
        diagnostics.ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void ADeclaredCollectionTypeDecidesWhetherTheArrayLaneIsTakenAtAll()
    {
        // The documented scope boundary. Under LiveView an array reaching script under a non-array declared
        // type honors that declared contract through the ordinary wrapper lane — no array conversion happens,
        // so none is counted. Under Copy the same value still takes the copy lane, and is counted there.
        var liveViewEngine = CreateEngine(ArrayConversionMode.LiveView);
        liveViewEngine.SetValue("host", new HostWithArrays());
        liveViewEngine.Evaluate("host.Declared[0]").Should().Be(1);

        var live = liveViewEngine.Advanced.GetInteropConversionDiagnostics();
        live.ArrayLiveViewConversions.Should().Be(0);
        live.ArrayCopyConversions.Should().Be(0);

        var copyEngine = CreateEngine(ArrayConversionMode.Copy);
        copyEngine.SetValue("host", new HostWithArrays());
        copyEngine.Evaluate("host.Declared[0]").Should().Be(1);

        var copy = copyEngine.Advanced.GetInteropConversionDiagnostics();
        copy.ArrayLiveViewConversions.Should().Be(0);
        copy.ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void EachEngineCountsItsOwnConversions()
    {
        // A host pooling engines audits per engine, so the counters must not be static.
        var first = CreateEngine();
        var second = CreateEngine();
        first.SetValue("host", new HostWithArrays());
        second.SetValue("host", new HostWithArrays());

        first.Execute("host.Values[0]; host.Others[0];");
        second.Execute("host.Values[0];");

        first.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(2);
        second.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void TheCountersAreCumulativeAcrossEvaluations()
    {
        var engine = CreateEngine();
        engine.SetValue("host", new HostWithArrays());

        engine.Execute("host.Values[0];");
        var afterFirst = engine.Advanced.GetInteropConversionDiagnostics();

        // A fresh array each time, so the identity caches cannot answer.
        engine.SetValue("host", new HostWithArrays());
        engine.Execute("host.Values[0];");
        var afterSecond = engine.Advanced.GetInteropConversionDiagnostics();

        afterFirst.ArrayCopyConversions.Should().Be(1);
        afterSecond.ArrayCopyConversions.Should().Be(2);
    }

    [Fact]
    public void TheResultIsAValueSnapshotNotALiveHandle()
    {
        // Reading the counters must not hand back something that keeps changing underneath the host, or an
        // audit could not compare a before against an after.
        var engine = CreateEngine();
        engine.SetValue("host", new HostWithArrays());

        var before = engine.Advanced.GetInteropConversionDiagnostics();
        engine.Execute("host.Values[0];");
        var after = engine.Advanced.GetInteropConversionDiagnostics();

        before.ArrayCopyConversions.Should().Be(0);
        after.ArrayCopyConversions.Should().Be(1);
        before.Should().NotBe(after);
        engine.Advanced.GetInteropConversionDiagnostics().Should().Be(after);
    }

    /// <summary>
    /// A plain CLR host with the members an embedder normally projects, plus the array shapes the counters
    /// discriminate between.
    /// </summary>
    public sealed class HostWithArrays
    {
        public int Number => 7;
        public string Text => "text";
        public List<string> Names { get; } = ["a", "b"];
        public Dictionary<string, int> Lookup { get; } = new() { ["a"] = 1 };
        public HostLeaf Nested { get; } = new();

        public int[] Values { get; } = [1, 2, 3];
        public int[] Others { get; } = [4, 5];

        /// <summary>An array reaching script under a declared type that is not an array type.</summary>
        public IReadOnlyList<int> Declared { get; } = new[] { 1, 2, 3 };
    }

    public sealed class HostLeaf
    {
        public int Number => 3;
    }
}
