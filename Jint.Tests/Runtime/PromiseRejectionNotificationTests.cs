#nullable enable

using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Tests.Runtime;

/// <summary>
/// When the engine reports an unhandled promise rejection, which is HTML's cadence rather than
/// <c>HostPromiseRejectionTracker</c>'s.
/// </summary>
public class PromiseRejectionNotificationTests
{
    private sealed record Report(PromiseRejectionOperation Operation, JsValue Promise);

    private static (Engine Engine, List<Report> Reports) Tracked(Action<Options>? configure = null)
    {
        var reports = new List<Report>();
        var engine = new Engine(options => configure?.Invoke(options));
        engine.Tasks.PromiseRejectionTracker += (_, args) => reports.Add(new Report(args.Operation, args.Promise));
        return (engine, reports);
    }

    [Test]
    public void ARejectionCaughtSynchronouslyIsNeverReported()
    {
        var (engine, reports) = Tracked();

        engine.Execute("Promise.reject(new Error('boom')).catch(function () {});");

        reports.Should().BeEmpty();
    }

    [Test]
    public void ARejectionCaughtInTheSameCheckpointIsNeverReported()
    {
        var (engine, reports) = Tracked();

        engine.Execute("""
            var p = Promise.reject(new Error('boom'));
            Promise.resolve().then(function () { p.catch(function () {}); });
            """);

        reports.Should().BeEmpty();
    }

    [Test]
    public void ARejectionNothingHandlesIsReportedOnce()
    {
        var (engine, reports) = Tracked();

        engine.Execute("var p = Promise.reject(new Error('boom'));");

        reports.Should().ContainSingle().Which.Operation.Should().Be(PromiseRejectionOperation.Reject);
    }

    [Test]
    public void AHandlerAttachedInALaterTaskIsReportedAsHandled()
    {
        var (engine, reports) = Tracked();

        engine.Execute("var p = Promise.reject(new Error('boom'));");
        engine.Execute("p.catch(function () {});");

        reports.Select(r => r.Operation).Should().Equal(
            PromiseRejectionOperation.Reject,
            PromiseRejectionOperation.Handle);
    }

    [Test]
    public void AnAwaitedAsyncFunctionThatThrowsIsNeverReported()
    {
        var (engine, reports) = Tracked();

        engine.Execute("""
            async function inner() { throw new Error('boom'); }
            async function outer() { try { await inner(); } catch (e) { } }
            outer();
            """);

        reports.Should().BeEmpty();
    }

    [Test]
    public void ARejectionInsideAJobIsReportedAtThatTurnsCheckpoint()
    {
        var (engine, reports) = Tracked();

        // The rejection happens in a queued job rather than in the script, so the checkpoint that reads it is
        // the one after that job — which is the same drain, one pass later.
        engine.Execute("Promise.resolve().then(function () { Promise.reject(new Error('boom')); });");

        reports.Should().ContainSingle().Which.Operation.Should().Be(PromiseRejectionOperation.Reject);
    }

    [Test]
    public void APumpOnlyEngineIsToldAtTheTurnThatRejected()
    {
        var (engine, reports) = Tracked();

        var (_, _, reject) = engine.Tasks.RegisterPromise();
        reject(new InvalidOperationException("boom"));

        // The settle goes on the event loop, so a host that never evaluates anything still gets its report:
        // the pump reaches the checkpoint like any other drain.
        engine.Tasks.ProcessTasks();

        reports.Should().ContainSingle().Which.Operation.Should().Be(PromiseRejectionOperation.Reject);
    }

    [Test]
    public void ARejectionDuringAHostInvokeIsReportedWhenTheEntryEnds()
    {
        var (engine, reports) = Tracked();

        engine.Execute("function reject() { Promise.reject(new Error('boom')); }");
        engine.Invoke("reject");

        reports.Should().ContainSingle().Which.Operation.Should().Be(PromiseRejectionOperation.Reject);
    }

    [Test]
    public void ARejectionDuringAHostInvokeIsReportedUnderAMemoryLimitToo()
    {
        // The memory-limit path is a second copy of the host-entry bracket, and a checkpoint that exists in
        // only one of them would report for an engine the host bounded and not for the one beside it.
        var (engine, reports) = Tracked(options => options.LimitMemory(4_000_000));

        engine.Execute("function reject() { Promise.reject(new Error('boom')); }");
        engine.Invoke("reject");

        reports.Should().ContainSingle().Which.Operation.Should().Be(PromiseRejectionOperation.Reject);
    }
}
