#if NET8_0_OR_GREATER
#nullable enable

using Jint;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The Web Locks API seen from outside the assembly: what a host has to write to get it, and the one setting
/// it has — <see cref="Options.WebLocksOptions.Manager"/>, which decides which engines share a lock space.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party.
/// </para>
/// <para>
/// Every engine runs on the test's own thread and is pumped with <c>Tasks.ProcessTasks()</c>, which is the
/// shape a host with its own loop uses, and the shape the contract is stated in: a lock is granted as an
/// event-loop task on the engine that asked, so a release on one engine reaches another only when that other
/// engine is pumped.
/// </para>
/// </remarks>
public class WebApiWebLocksTests
{
    private static Engine LocksEngine() => new(options => options.UseWebApis(WebApiFeatures.WebLocks));

    private static Engine LocksEngine(LockManager manager) =>
        new(options => options.UseWebApis().UseWebLocks(manager));

    // ---------------------------------------------------------------- the opt-in

    [Test]
    public void ADefaultEngineHasNoLocks()
    {
        var engine = new Engine();

        engine.Evaluate("typeof navigator").AsString().Should().Be("undefined");
        engine.Evaluate("'LockManager' in globalThis").AsBoolean().Should().BeFalse();
        engine.Evaluate("'Lock' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void TheFlagInstallsItAndBringsNavigatorWithIt()
    {
        var engine = LocksEngine();

        engine.Evaluate("typeof navigator.locks.request").AsString().Should().Be("function");
        engine.Evaluate("typeof navigator.locks.query").AsString().Should().Be("function");
        engine.Evaluate("navigator.locks instanceof LockManager").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheDefaultSetIncludesIt()
    {
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.WebLocks);

        new Engine(options => options.UseWebApis())
            .Evaluate("typeof navigator.locks").AsString().Should().Be("object");
    }

    [Test]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own LockManager");

        var engine = new Engine(options => options
            .Configure(e => e.SetValue("LockManager", marker))
            .UseWebApis(WebApiFeatures.WebLocks));

        engine.Evaluate("LockManager").Should().BeSameAs(marker);
    }

    // ---------------------------------------------------------------- the host seam

    [Test]
    public void EachEngineGetsAPrivateLockSpaceByDefault()
    {
        var first = new Engine(options => options.UseWebApis());
        var second = new Engine(options => options.UseWebApis());

        first.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");

        second.Execute("var granted = false; navigator.locks.request('r', function () { granted = true; });");
        second.Evaluate("granted").AsBoolean().Should().BeTrue("nothing crosses an engine boundary by default");
    }

    [Test]
    public void OneManagerMakesSeveralEnginesOneAgentCluster()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        holder.Execute("var release; navigator.locks.request('r', function () { return new Promise(function (r) { release = r; }); });");

        waiter.Execute("var granted = false; navigator.locks.request('r', function () { granted = true; });");
        waiter.Evaluate("granted").AsBoolean().Should().BeFalse("the other engine holds it");

        holder.Execute("release();");

        // The grant was queued onto the waiting engine's loop by the releasing engine; nothing but that
        // engine's own pump runs it.
        waiter.Evaluate("granted").AsBoolean().Should().BeFalse();
        waiter.Tasks.ProcessTasks();
        waiter.Evaluate("granted").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TwoEnginesSharingAManagerReportTwoClientIds()
    {
        var manager = new LockManager();
        var first = LocksEngine(manager);
        var second = LocksEngine(manager);

        first.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");
        second.Execute("navigator.locks.request('r', function () {});");

        second.Execute("""
            var answer;
            navigator.locks.query().then(function (s) {
              answer = s.held[0].clientId === s.pending[0].clientId ? 'same' : 'different';
            });
            """);

        second.Evaluate("answer").AsString().Should().Be("different");
    }

    [Test]
    public void ARestoreGivesAnEnginesLocksBackToTheCluster()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        var snapshot = holder.Advanced.CaptureGlobalSnapshot();
        holder.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");

        waiter.Execute("var granted = false; navigator.locks.request('r', function () { granted = true; });");
        waiter.Evaluate("granted").AsBoolean().Should().BeFalse();

        holder.Advanced.RestoreGlobalSnapshot(snapshot);

        waiter.Tasks.ProcessTasks();
        waiter.Evaluate("granted").AsBoolean().Should().BeTrue("a restore terminates that agent's locks");
    }

    [Test]
    public void DisposingAnEngineGivesItsLocksBackToo()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        holder.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");
        waiter.Execute("var granted = false; navigator.locks.request('r', function () { granted = true; });");
        waiter.Evaluate("granted").AsBoolean().Should().BeFalse();

        holder.Dispose();

        waiter.Tasks.ProcessTasks();
        waiter.Evaluate("granted").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheManagerIsReadOnceWhenTheEngineIsBuilt()
    {
        var manager = new LockManager();
        var options = new Options();
        options.UseWebApis().UseWebLocks(manager);

        var engine = new Engine(options);

        // Options are frozen once an engine has read them, which is what "read once" is enforced by.
        Assert.Throws<InvalidOperationException>(() => engine.Options.WebApi.Locks.Manager = new LockManager());
    }
}
#endif
