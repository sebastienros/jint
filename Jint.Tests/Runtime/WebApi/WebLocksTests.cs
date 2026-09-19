#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The Web Locks API — https://w3c.github.io/web-locks/ — for the three things the vendored
/// <c>web-locks/</c> corpus cannot reach from one engine: the host seam, the rule a worker follows, and the
/// task-versus-microtask ordering a grant has.
/// </summary>
/// <remarks>
/// <para>
/// Everything the corpus <i>does</i> pin is deliberately not repeated here. It runs twelve files against a
/// <see cref="WebApiFeatures.Default"/> engine and covers the request, grant, steal, abort and release
/// algorithms, the option validation, the modes and <c>query()</c>'s shape; what it has no way to express is
/// a second engine, because the driver's lane is one engine per file.
/// </para>
/// <para>
/// A grant is an event-loop <i>task</i>, so every assertion below either lets <c>Execute</c> drain the loop
/// or pumps each engine by hand — which is also the point of several of them.
/// </para>
/// </remarks>
public class WebLocksTests
{
    private static Engine LocksEngine(LockManager? manager = null, WebApiFeatures features = WebApiFeatures.Default)
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(features);
            if (manager is not null)
            {
                options.WebApi.Locks.Manager = manager;
            }
        });

        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    // ---------------------------------------------------------------- installation

    [Test]
    public void IsAbsentUntilTheFeatureIsEnabled()
    {
        var engine = new Engine();

        engine.Evaluate("typeof LockManager").AsString().Should().Be("undefined");
        engine.Evaluate("typeof Lock").AsString().Should().Be("undefined");
    }

    [Test]
    public void IsPartOfTheDefaultFeatureSet()
    {
        (WebApiFeatures.Default & WebApiFeatures.WebLocks).Should().Be(WebApiFeatures.WebLocks);
    }

    [Test]
    public void UsesTheBitTheEnumReserved()
    {
        ((int) WebApiFeatures.WebLocks).Should().Be(1 << 26);
    }

    [Test]
    public void BringsNavigatorAndTheEventsSurfaceWithIt()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.WebLocks));

        // navigator, because `locks` is a member of it; AbortController, because the `signal` option is one.
        engine.Evaluate("typeof navigator.locks").AsString().Should().Be("object");
        engine.Evaluate("typeof AbortController").AsString().Should().Be("function");
        engine.Evaluate("typeof LockManager").AsString().Should().Be("function");
        engine.Evaluate("typeof Lock").AsString().Should().Be("function");
    }

    [Test]
    public void LeavesNavigatorWithoutLocksWhenOnlyTheNavigatorFeatureIsOn()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Navigator));

        // Absent rather than throwing, which is this family's convention and what feature detection reads.
        engine.Evaluate("'locks' in navigator").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.prototype.hasOwnProperty.call(Navigator.prototype, 'locks')").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof LockManager").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// <c>[SameObject] readonly attribute LockManager locks</c>: an enumerable, configurable accessor on the
    /// interface prototype object, answering with one object however often it is read.
    /// </summary>
    [Test]
    public void ExposesLocksAsASameObjectAttributeOfNavigatorPrototype()
    {
        var engine = LocksEngine();

        engine.Evaluate("navigator.locks === navigator.locks").AsBoolean().Should().BeTrue();
        engine.Evaluate("navigator.locks instanceof LockManager").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(navigator.locks) === LockManager.prototype").AsBoolean().Should().BeTrue();

        // The member is the interface's, so the instance has no own keys at all — the same shape `navigator`
        // and `scheduler` have.
        engine.Evaluate("Reflect.ownKeys(navigator.locks).length").AsNumber().Should().Be(0);
        engine.Evaluate("Reflect.ownKeys(navigator).length").AsNumber().Should().Be(0);

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(Navigator.prototype, 'locks')").AsObject();
        descriptor.Get("get").IsCallable().Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue("a WebIDL attribute is enumerable");
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        // Added after CreateProperties_Generated because its presence is conditional — the same thing
        // URL.createObjectURL does — and the shaped prototype has to keep its shared layout through it, which
        // is what Jint.Tests.Browser's NavigatorTests holds the page's own late members to as well.
        engine.Advanced.HasSharedShape(engine.Evaluate("Navigator.prototype").AsObject())
            .Should().BeTrue("a conditional member must not cost the prototype its shared shape");
    }

    [Test]
    public void GivesTheTwoInterfaceObjectsTheAttributesWebIdlAsksFor()
    {
        var engine = LocksEngine();

        foreach (var name in new[] { "LockManager", "Lock" })
        {
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Should().NotBe(PropertyDescriptor.Undefined, name);
            descriptor.Enumerable.Should().BeFalse(name);
            descriptor.Writable.Should().BeTrue(name);
            descriptor.Configurable.Should().BeTrue(name);

            // Neither declares a constructor operation.
            engine.Evaluate($"(function () {{ try {{ new {name}(); return 'constructed'; }} catch (e) {{ return e.name; }} }})()")
                .AsString().Should().Be("TypeError", name);
        }
    }

    /// <summary>
    /// The live door: <c>Engine.WebApi.Enable</c> records the feature before it installs anything, so a
    /// prototype built afterwards carries <c>locks</c>.
    /// </summary>
    /// <remarks>
    /// The other half is the consequence a conditional member has, and it is the one
    /// <c>URL.createObjectURL</c> already has: an engine whose script has <i>already</i> read
    /// <c>navigator</c> built the prototype without the member and keeps it. That is asserted here too, so
    /// the limitation is recorded rather than discovered.
    /// </remarks>
    [Test]
    public void ArrivesOnAnEngineThatEnablesTheFeatureBeforeItsScriptNamesNavigator()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Navigator));
        engine.Evaluate("typeof navigator.locks").AsString().Should().Be("undefined");

        var fresh = new Engine(options => options.UseWebApis(WebApiFeatures.Navigator));
        fresh.WebApi.Enable(WebApiFeatures.WebLocks);
        fresh.Evaluate("typeof navigator.locks").AsString().Should().Be("object");

        // And the engine whose script already built the prototype keeps the one it built.
        engine.WebApi.Enable(WebApiFeatures.WebLocks);
        engine.Evaluate("typeof LockManager").AsString().Should().Be("function", "the globals do arrive");
        engine.Evaluate("typeof navigator.locks").AsString().Should().Be(
            "undefined",
            "a conditional member is decided when the interface prototype is first built");
    }

    // ---------------------------------------------------------------- ordering

    /// <summary>
    /// Both places the specification hands a callback to an event loop say "enqueue the following steps on
    /// callback's relevant settings object's responsible event loop". So a lock is never granted inside the
    /// call that asked for it, however free the resource is — which is what lets an abort signalled on the
    /// next line still abort the request.
    /// </summary>
    [Test]
    public void NeverGrantsInsideTheCallThatAskedForIt()
    {
        var engine = LocksEngine();

        engine.Execute("""
            var granted = false;
            navigator.locks.request('r', function () { granted = true; });
            log.push('after-request:' + granted);
            """);

        Log(engine).Should().Be("after-request:false");
        engine.Evaluate("granted").AsBoolean().Should().BeTrue("Execute drains the loop the grant was queued on");
    }

    /// <summary>
    /// The <c>ifAvailable</c> miss is the second of those two enqueues, and it is deferred for the same
    /// reason: a callback handed <c>null</c> must not run inside the call that asked either.
    /// </summary>
    [Test]
    public void NeverHandsTheIfAvailableMissToTheCallbackInsideTheCallEither()
    {
        var engine = LocksEngine();

        engine.Execute("""
            var called = false;
            navigator.locks.request('r', function () { return new Promise(function () {}); });
            navigator.locks.request('r', { ifAvailable: true }, function (l) { called = true; log.push('miss:' + l); });
            log.push('after-request:' + called);
            """);

        Log(engine).Should().Be("after-request:false,miss:null");
    }

    /// <summary>
    /// It is a <i>task</i> rather than a microtask, which is the part nothing in the corpus can see: the
    /// microtask checkpoint an event listener returns to runs the reaction the listener queued and stops at
    /// the grant behind it.
    /// </summary>
    /// <remarks>
    /// A host dispatch, because that is the one that checkpoints — see
    /// <see cref="ListenerMicrotaskCheckpointTests"/> for why a script's own <c>dispatchEvent</c> does not.
    /// A grant enqueued as a microtask would run between the reaction and the second listener.
    /// </remarks>
    [Test]
    public void ClassifiesAGrantAsATaskSoAListenersCheckpointDoesNotPullItForward()
    {
        var log = new List<string>();
        var engine = new Engine(options => options.UseWebApis());
        engine.SetValue("record", new Action<string>(entry => log.Add(entry)));

        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', function () {
                Promise.resolve().then(function () { record('microtask'); });
                navigator.locks.request('r', function () { record('granted'); });
            });
            target.addEventListener('ping', function () { record('second'); });
            """);

        var dispatchEvent = engine.Evaluate("EventTarget.prototype.dispatchEvent");
        engine.Call(dispatchEvent, engine.GetValue("target"), [engine.GetValue("ev")]);
        engine.Tasks.ProcessTasks();

        log.Should().Equal("microtask", "second", "granted");
    }

    /// <summary>
    /// The queueing itself is synchronous — it happens under the manager's own gate, inside the call — so two
    /// requests made in one turn are granted in the order they were made, and a <c>query()</c> scheduled
    /// after them already reports both.
    /// </summary>
    [Test]
    public void QueuesSynchronouslySoQueryAlreadySeesWhatTheSameTurnAskedFor()
    {
        var engine = LocksEngine();

        engine.Execute("""
            navigator.locks.request('r', function () { return new Promise(function () {}); });
            navigator.locks.request('r', function () { log.push('second'); });
            navigator.locks.query().then(function (s) { log.push(s.held.length + ':' + s.pending.length); });
            """);

        Log(engine).Should().Be("1:1");
    }

    /// <summary>
    /// <c>query()</c>'s rows are the dictionary the specification declares, with the mode strings and the one
    /// client id this engine reports everything under.
    /// </summary>
    [Test]
    public void ReportsNameModeAndClientIdForEveryRow()
    {
        var engine = LocksEngine();

        engine.Execute("""
            navigator.locks.request('a', { mode: 'shared' }, function () { return new Promise(function () {}); });
            navigator.locks.request('a', function () {});
            navigator.locks.query().then(function (s) {
              log.push(s.held[0].name + '/' + s.held[0].mode);
              log.push(s.pending[0].name + '/' + s.pending[0].mode);
              log.push(s.held[0].clientId === s.pending[0].clientId ? 'same-client' : 'different-clients');
              log.push(typeof s.held[0].clientId);
            });
            """);

        Log(engine).Should().Be("a/shared,a/exclusive,same-client,string");
    }

    // ---------------------------------------------------------------- the host seam

    /// <summary>
    /// The whole point of the seam: two engines given one <see cref="LockManager"/> are one agent cluster, so
    /// a name held on either is held against both.
    /// </summary>
    [Test]
    public void TwoEnginesSharingOneManagerContendForOneName()
    {
        var manager = new LockManager();
        var first = LocksEngine(manager);
        var second = LocksEngine(manager);

        // The first engine takes the lock and never gives it back.
        first.Execute("navigator.locks.request('shared-name', function () { return new Promise(function () {}); });");

        second.Execute("navigator.locks.request('shared-name', function () { log.push('granted'); });");
        Log(second).Should().BeEmpty("the other engine is holding it");

        second.Execute("navigator.locks.request('shared-name', { ifAvailable: true }, function (l) { log.push('ifAvailable:' + l); });");
        Log(second).Should().Be("ifAvailable:null");

        // And each engine is its own client.
        first.Execute("navigator.locks.query().then(function (s) { log.push(s.held.length + ':' + s.pending.length); });");
        Log(first).Should().Be("1:1", "one engine's query reports the other engine's request");

        second.Execute("""
            navigator.locks.query().then(function (s) {
              log.push(s.held[0].clientId === s.pending[0].clientId ? 'same-client' : 'different-clients');
            });
            """);
        Log(second).Should().Be("ifAvailable:null,different-clients");
    }

    [Test]
    public void TwoEnginesWithoutASharedManagerDoNotSeeEachOther()
    {
        var first = LocksEngine();
        var second = LocksEngine();

        first.Execute("navigator.locks.request('shared-name', function () { return new Promise(function () {}); });");

        second.Execute("navigator.locks.request('shared-name', function () { log.push('granted'); });");
        Log(second).Should().Be("granted", "a default engine's manager is private to it");

        second.Execute("navigator.locks.query().then(function (s) { log.push(s.held.length + ':' + s.pending.length); });");
        Log(second).Should().Be("granted,0:0");
    }

    /// <summary>
    /// A lock released on one engine grants the request waiting on another, which needs that engine to be
    /// pumped — Jint starts no thread for it, exactly as for a timer or a message.
    /// </summary>
    [Test]
    public void ReleasingOnOneEngineGrantsTheOtherOnItsNextPump()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        holder.Execute("var release; navigator.locks.request('r', function () { return new Promise(function (r) { release = r; }); });");
        waiter.Execute("navigator.locks.request('r', function () { log.push('granted'); });");
        Log(waiter).Should().BeEmpty();

        holder.Execute("release();");

        // The grant was enqueued onto the waiting engine's loop by the releasing engine; nothing runs it but
        // that engine's own pump.
        Log(waiter).Should().BeEmpty("no thread pumps an engine on its behalf");
        waiter.Tasks.ProcessTasks();
        Log(waiter).Should().Be("granted");
    }

    /// <summary>
    /// A manager is documented thread-safe, which is the whole reason it is a class rather than a field on
    /// the engine. Four engines contend for one name from four threads of their own.
    /// </summary>
    /// <remarks>
    /// <see cref="DedicatedThread.RunAsync"/> rather than the pool, because each body blocks for as long as
    /// its engine runs, and <see cref="TestBudgets.WedgeCeiling"/> rather than a number of its own: nothing
    /// here asserts a duration, and a healthy run spends none of it — what it removes is a deadlock hanging
    /// the whole suite instead of failing this test.
    /// </remarks>
    [Test]
    public void AManagerIsSafeToShareBetweenEnginesOnDifferentThreads()
    {
        var manager = new LockManager();
        var engines = new Engine[4];
        for (var i = 0; i < engines.Length; i++)
        {
            engines[i] = LocksEngine(manager);
        }

        var running = new Task[engines.Length];
        for (var i = 0; i < engines.Length; i++)
        {
            var engine = engines[i];
            running[i] = DedicatedThread.RunAsync(() =>
            {
                for (var n = 0; n < 25; n++)
                {
                    engine.Execute("navigator.locks.request('contended', function () { log.push('x'); });");
                }
            });
        }

        Task.WaitAll(running, TestBudgets.WedgeCeiling).Should().BeTrue("the manager must not deadlock");

        // A request queued while another engine held the name is granted by that engine's release, as a task
        // on this one's loop — which only its own pump runs, so the rounds below are the drain the last
        // iteration of each body did not get.
        for (var round = 0; round < 100 && (manager.HeldCount > 0 || manager.PendingCount > 0); round++)
        {
            foreach (var engine in engines)
            {
                engine.Tasks.ProcessTasks();
            }
        }

        // Every lock was taken and given back, so nothing is left behind on the shared manager.
        manager.HeldCount.Should().Be(0);
        manager.PendingCount.Should().Be(0);
        manager.ActiveNameCount.Should().Be(0, "a name whose queue empties is removed outright");
    }

    // ---------------------------------------------------------------- termination

    /// <summary>
    /// https://w3c.github.io/web-locks/#termination-of-locks — an engine that ends its evaluation cycle
    /// gives its locks back, which is what stops one cycle of a pooled engine holding up every other engine
    /// on the host's manager.
    /// </summary>
    [Test]
    public void ARestoreReleasesEveryLockThatEngineHeld()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        var snapshot = holder.Advanced.CaptureGlobalSnapshot();
        holder.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");
        manager.HeldCount.Should().Be(1);

        waiter.Execute("navigator.locks.request('r', function () { log.push('granted'); return new Promise(function () {}); });");
        Log(waiter).Should().BeEmpty();

        holder.Advanced.RestoreGlobalSnapshot(snapshot);

        waiter.Tasks.ProcessTasks();
        Log(waiter).Should().Be("granted");
        manager.HeldCount.Should().Be(1, "the waiting engine now holds it");
    }

    [Test]
    public void ARestoreAlsoAbortsEveryRequestThatEngineHadPending()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        holder.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");

        var snapshot = waiter.Advanced.CaptureGlobalSnapshot();
        waiter.Execute("navigator.locks.request('r', function () {});");
        manager.PendingCount.Should().Be(1);

        waiter.Advanced.RestoreGlobalSnapshot(snapshot);
        manager.PendingCount.Should().Be(0);
        manager.ActiveNameCount.Should().Be(0);
    }

    [Test]
    public void DisposingAnEngineReleasesEveryLockItHeld()
    {
        var manager = new LockManager();
        var holder = LocksEngine(manager);
        var waiter = LocksEngine(manager);

        holder.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");
        waiter.Execute("navigator.locks.request('r', function () { log.push('granted'); });");
        Log(waiter).Should().BeEmpty();

        holder.Dispose();

        waiter.Tasks.ProcessTasks();
        Log(waiter).Should().Be("granted");
    }

    // ---------------------------------------------------------------- the worker rule

    /// <summary>
    /// <b>Restrictions travel; grants never travel by implication.</b> The flag itself travels, because it
    /// grants a worker no reach and no persistence — but the <i>manager</i> does not, exactly as
    /// <c>Options.WebApi.Messaging.Broker</c> does not, so a worker starts in an agent cluster of its own
    /// until a provider deliberately puts it in its parent's.
    /// </summary>
    [Test]
    public void AWorkerInheritsTheFlagAndNeverTheManager()
    {
        var manager = new LockManager();
        var parentOptions = new Options();
        parentOptions.UseWebApis().UseWebLocks(manager);
        var parent = new Engine(parentOptions);

        var workerOptions = WorkerRequestFor(parent).CreateDefaultOptions();

        (workerOptions.WebApi.Features & WebApiFeatures.WebLocks).Should().Be(
            WebApiFeatures.WebLocks,
            "a worker that cannot serialize against itself is a worker missing a non-capability feature");

        workerOptions.WebApi.Locks.Manager.Should().BeNull(
            "a shared lock space is a decision a provider makes in one visible line, not one it inherits");
    }

    [Test]
    public void AWorkerBuiltFromTheDefaultOptionsSerializesOnlyAgainstItself()
    {
        var manager = new LockManager();
        var parentOptions = new Options();
        parentOptions.UseWebApis().UseWebLocks(manager);
        var parent = new Engine(parentOptions);
        parent.Execute("var log = [];");

        var worker = new Engine(WorkerRequestFor(parent).CreateDefaultOptions());
        worker.Execute("var log = [];");

        parent.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");

        worker.Execute("navigator.locks.request('r', function () { log.push('granted'); });");
        Log(worker).Should().Be("granted", "the worker is in an agent cluster of its own");
    }

    /// <summary>
    /// And the other half of the rule: a provider that <i>wants</i> a browser's arrangement — one window and
    /// its workers in one agent cluster — says so, and then the two do contend.
    /// </summary>
    [Test]
    public void AProviderThatNamesTheParentsManagerPutsTheWorkerInTheSameAgentCluster()
    {
        var manager = new LockManager();
        var parentOptions = new Options();
        parentOptions.UseWebApis().UseWebLocks(manager);
        var parent = new Engine(parentOptions);

        var workerOptions = WorkerRequestFor(parent).CreateDefaultOptions();
        workerOptions.WebApi.Locks.Manager = manager;

        var worker = new Engine(workerOptions);
        worker.Execute("var log = [];");

        parent.Execute("navigator.locks.request('r', function () { return new Promise(function () {}); });");

        worker.Execute("navigator.locks.request('r', function () { log.push('granted'); });");
        Log(worker).Should().BeEmpty("the parent is holding the lock and the two now share a manager");
    }

    private static WorkerRequest WorkerRequestFor(Engine parent)
        => new(
            parent,
            specifier: "./worker.js",
            referencingLocation: null,
            WorkerType.Module,
            name: "",
            depth: 0,
            liveWorkerCount: 0,
            terminationToken: default);
}
#endif
