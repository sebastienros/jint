#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Threading;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Modules;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>Worker</c> constructor, the two façades, the message paths, and the two ways a connection ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The worker engine is pumped by the test's own thread</b> wherever determinism matters, which is nearly
/// everywhere: <see cref="TestWorkerHost"/> builds the engine and hands it back, and the test calls
/// <c>ProcessTasks</c> on the two engines in turn. Nothing here waits on a clock. The three pins that are
/// genuinely about threads say so and use a real one, with an event-driven wait and a ceiling generous enough
/// that a slow machine makes them slower rather than redder.
/// </para>
/// <para>
/// The sibling file <c>WorkerTests</c> holds the host-facing foundation — <c>CreateDefaultOptions</c> and the
/// connection object — which needs no constructor to exercise.
/// </para>
/// </remarks>
public class WorkerMechanismTests
{
    private const string Specifier = "./worker.js";

    /// <summary>
    /// Long enough that a loaded machine is slow rather than red. Every use is a <c>Wait</c> or a
    /// <c>Join</c> on a thread this class started, and nothing asserts how long either took — the assertions
    /// are that the handshake happened and that the thread left — so this is a wedge ceiling and
    /// <see cref="TestBudgets.WedgeCeiling"/> is what the suite calls one. Ten seconds was that number sized
    /// for an idle box (#3379).
    /// </summary>
    private static readonly TimeSpan Ceiling = TestBudgets.WedgeCeiling;

    // -------------------------------------------------------------------------------------------------------
    // Construction and refusals
    // -------------------------------------------------------------------------------------------------------

    [Test]
    public void NoProviderMeansNoWorkerGlobal()
    {
        var withFlagOnly = new Engine(options => options.UseWebApis(WebApiFeatures.Default | WebApiFeatures.Workers));
        withFlagOnly.Evaluate("typeof Worker").AsString().Should().Be(
            "undefined",
            "a worker needs a thread and a pump, and with no provider there is neither");

        var withProviderOnly = new Engine(options =>
        {
            options.UseWebApis();
            options.WebApi.Workers.Provider = new TestWorkerHost();
        });
        withProviderOnly.Evaluate("typeof Worker").AsString().Should().Be("undefined", "the flag is still the grant");

        var withBoth = new Engine(options => options.UseWebApis().UseWorkers(new TestWorkerHost()));
        withBoth.Evaluate("typeof Worker").AsString().Should().Be("function");
    }

    [TestCase("new Worker('./worker.js')", "type: 'module'")]
    [TestCase("new Worker('./worker.js', { type: 'classic' })", "type: 'module'")]
    [TestCase("new Worker('./worker.js', { type: 'nope' })", "WorkerType")]
    public void AClassicWorkerRequestIsATypeError(string source, string expectedInMessage)
    {
        var host = new TestWorkerHost();
        var parent = Parent(host);

        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute(source))!;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        exception.Message.Should().Contain(expectedInMessage);
        host.Requests.Should().Be(0, "WebIDL runs before the host is ever consulted");
    }

    [Test]
    public void AProviderRefusalIsASecurityError()
    {
        var host = new TestWorkerHost { Refuse = true };
        var parent = Parent(host);

        var exception = Assert.Throws<JavaScriptException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"))!;

        exception.Error.Get("name").AsString().Should().Be(
            "SecurityError",
            "a refusal is a policy decision, not a fetch failure");
        host.Requests.Should().Be(1);
        host.Started.Should().BeEmpty();
    }

    [Test]
    public void AProviderExceptionPropagatesUnchanged()
    {
        var failure = new InvalidTimeZoneException("the tenant has no worker budget");
        var host = new TestWorkerHost { Throw = failure };
        var parent = Parent(host);

        var thrown = Assert.Throws<InvalidTimeZoneException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"))!;

        thrown.Should().BeSameAs(failure, "nothing a provider throws is translated");
    }

    [Test]
    public void MoreThanMaxWorkersIsAQuotaExceededError()
    {
        var host = new TestWorkerHost(Module("")) ;
        var parent = Parent(host, maxWorkers: 2);

        parent.Execute("var a = new Worker('./worker.js', { type: 'module' });");
        parent.Execute("var b = new Worker('./worker.js', { type: 'module' });");

        var exception = Assert.Throws<JavaScriptException>(
            () => parent.Execute("var c = new Worker('./worker.js', { type: 'module' });"))!;

        exception.Error.Get("name").AsString().Should().Be("QuotaExceededError");
        exception.Error.Get("quota").AsNumber().Should().Be(2);
        exception.Error.Get("requested").AsNumber().Should().Be(3);
        host.Requests.Should().Be(2, "the cap is checked before the host is consulted");

        // And the slot comes back when a connection ends.
        parent.Execute("a.terminate();");
        parent.Execute("var d = new Worker('./worker.js', { type: 'module' });");
    }

    [Test]
    public void AnEngineWithoutTheTerminationTokenIsRefused()
    {
        var host = new TestWorkerHost
        {
            Build = _ => new Engine(options => options.UseWebApis(WebApiFeatures.Messaging | WebApiFeatures.GlobalEvents)),
        };
        var parent = Parent(host);

        var exception = Assert.Throws<InvalidOperationException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"))!;

        exception.Message.Should().Contain("request.TerminationToken");
        exception.Message.Should().Contain("options.ObserveCancellation", "the message names the one-line fix");
    }

    [Test]
    public void AnEngineWithoutMessagingIsRefused()
    {
        var host = new TestWorkerHost
        {
            Build = request => new Engine(options => options.ObserveCancellation(request.TerminationToken)),
        };
        var parent = Parent(host);

        var exception = Assert.Throws<InvalidOperationException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"))!;

        exception.Message.Should().Contain("WebApiFeatures.Messaging");
    }

    [Test]
    public void TheParentEngineItselfIsRefused()
    {
        var host = new TestWorkerHost();
        Engine? parent = null;
        host.Build = _ => parent!;
        parent = Parent(host);

        var exception = Assert.Throws<InvalidOperationException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"))!;

        exception.Message.Should().Contain("returned the parent engine");
    }

    /// <summary>
    /// A pre-warmed engine another thread is already inside is the bug this validates against, and the answer
    /// is the engine's own admission error at <c>new Worker()</c> rather than silent corruption later.
    /// </summary>
    [Test]
    public void AnUnQuiescentWorkerEngineIsRefused()
    {
        using var owned = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var busy = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging | WebApiFeatures.GlobalEvents));
        var host = new TestWorkerHost { Build = _ => busy };
        var parent = Parent(host);

        var holder = new Thread(() =>
        {
            using (busy.EnterHostCall())
            {
                owned.Set();
                release.Wait(Ceiling);
            }
        })
        {
            IsBackground = true,
            Name = "worker engine holder",
        };

        holder.Start();
        owned.Wait(Ceiling).Should().BeTrue("the holder thread has to have the engine before the constructor runs");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => parent.Execute("new Worker('./worker.js', { type: 'module' })"))!;

            exception.Message.Should().Contain("already in use by another thread");
        }
        finally
        {
            release.Set();
            holder.Join(Ceiling).Should().BeTrue();
        }
    }

    /// <summary>
    /// Nothing of the worker's script runs during <c>new Worker()</c>, and what runs it is whichever thread
    /// pumps the worker engine — never the parent's.
    /// </summary>
    [Test]
    public void TheWorkerScriptDoesNotRunOnTheParentsThread()
    {
        using var evaluated = new ManualResetEventSlim(false);

        var recorded = 0;
        var host = new TestWorkerHost(Module("record('evaluated');"));
        host.Configure = engine => engine.SetValue("noteThread", new Action(() =>
        {
            Volatile.Write(ref recorded, Environment.CurrentManagedThreadId);
            evaluated.Set();
        }));
        host.Modules[Specifier] = "noteThread();";

        var parent = Parent(host);
        var parentThread = Environment.CurrentManagedThreadId;

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");

        evaluated.IsSet.Should().BeFalse("new Worker() runs none of the worker's script");
        Volatile.Read(ref recorded).Should().Be(0);

        var connection = host.Connection;
        var pump = new Thread(() =>
        {
            for (var i = 0; i < 200 && !evaluated.IsSet; i++)
            {
                connection.Worker.Tasks.ProcessTasks();
            }
        })
        {
            IsBackground = true,
            Name = "worker pump",
        };

        pump.Start();
        evaluated.Wait(Ceiling).Should().BeTrue("the worker's module runs on the thread that pumps it");
        pump.Join(Ceiling).Should().BeTrue();

        Volatile.Read(ref recorded).Should().NotBe(parentThread);
    }

    // -------------------------------------------------------------------------------------------------------
    // Messaging and ordering
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The worker's own queue is not enabled until its module has evaluated, so a message posted before then
    /// waits rather than being dispatched at a global that has not registered a listener yet.
    /// </summary>
    /// <remarks>
    /// The worker's module suspends on a timer, which is what makes the pin bite: with the inner queue enabled
    /// at construction the delivery job would run during that suspension and the first message would be
    /// dropped on the floor.
    /// </remarks>
    [Test]
    public void AMessagePostedBeforeTheWorkerEvaluatesIsBuffered()
    {
        var host = new TestWorkerHost(Module("""
            record('evaluating');
            await new Promise(resolve => setTimeout(resolve, 0));
            addEventListener('message', e => record('msg:' + e.data));
            record('ready');
            """));

        var parent = Parent(host);
        parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            w.postMessage(1);
            w.postMessage(2);
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be("evaluating,ready,msg:1,msg:2");
    }

    [Test]
    public void MessagesArriveInPostOrder()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host);

        parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            for (var i = 0; i < 8; i++) { w.postMessage('m' + i); }
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be("m0,m1,m2,m3,m4,m5,m6,m7");
    }

    /// <summary>
    /// A message is a task, so everything a listener queues runs before the next message is even looked at —
    /// the timers' rule, inherited whole.
    /// </summary>
    [Test]
    public void EachMessageGetsItsOwnMicrotaskCheckpoint()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', e => {
                record('m' + e.data);
                Promise.resolve().then(() => record('p' + e.data));
            });
            """));

        var parent = Parent(host);
        parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            w.postMessage(1);
            w.postMessage(2);
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be("m1,p1,m2,p2", "not m1,m2,p1,p2");
    }

    /// <summary>
    /// The parent half is enabled by the engine at construction, so a message the worker posts during its own
    /// evaluation waits on the parent's queue until the parent is pumped — whenever the handler was assigned.
    /// </summary>
    [Test]
    public void AMessageSentBeforeTheParentAssignsOnmessageIsNotLost()
    {
        var host = new TestWorkerHost(Module("postMessage('hello');"));
        var parent = Parent(host);

        parent.Execute("var got = []; var w = new Worker('./worker.js', { type: 'module' });");

        // The worker runs and posts while the parent has no handler at all.
        DrainWorker(host.Connection);

        parent.Execute("w.onmessage = e => got.push(e.data);");
        Drain(parent, host.Connection);

        parent.Evaluate("got.join(',')").AsString().Should().Be("hello");
    }

    /// <summary>
    /// <c>addEventListener('message', …)</c> alone receives on both façades. The <c>onmessage</c>-implies-
    /// <c>start()</c> rule is scoped to the <c>MessagePort</c> interface and must not be reused here.
    /// </summary>
    [Test]
    public void AWorkerDeliversToAddEventListenerWithoutStart()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', e => { record('in:' + e.data); postMessage('out:' + e.data); });
            """));

        var parent = Parent(host);
        parent.Execute("""
            var got = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.addEventListener('message', e => got.push(e.data));
            w.postMessage('x');
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be("in:x");
        parent.Evaluate("got.join(',')").AsString().Should().Be("out:x");
    }

    [Test]
    public void ATransferredArrayBufferIsMovedNotCopied()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', e => record('bytes:' + new Uint8Array(e.data).join('-')));
            """));

        var parent = Parent(host);
        parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            var view = new Uint8Array([7, 8, 9]);
            var detachedBefore = view.buffer.byteLength;
            w.postMessage(view.buffer, [view.buffer]);
            var detachedAfter = view.buffer.byteLength;
            """);

        parent.Evaluate("detachedBefore").AsNumber().Should().Be(3);
        parent.Evaluate("detachedAfter").AsNumber().Should().Be(0, "the sender's buffer is detached, not copied");

        Drain(parent, host.Connection);

        host.Log.Should().Be("bytes:7-8-9");
    }

    [Test]
    public void ASharedArrayBufferIsADataCloneError()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            w.postMessage(new SharedArrayBuffer(8));
            """))!;

        exception.Error.Get("name").AsString().Should().Be("DataCloneError");
    }

    [Test]
    public void APortTransferredToAWorkerIsReEntangledOnTheWorkerSide()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', e => {
                const port = e.ports[0];
                port.onmessage = m => record('private:' + m.data);
                port.postMessage('pong');
            });
            """));

        var parent = Parent(host);
        parent.Execute("""
            var got = [];
            var w = new Worker('./worker.js', { type: 'module' });
            var channel = new MessageChannel();
            channel.port1.onmessage = e => { got.push(e.data); channel.port1.postMessage('ping'); };
            w.postMessage('take it', [channel.port2]);
            """);

        Drain(parent, host.Connection);

        parent.Evaluate("got.join(',')").AsString().Should().Be("pong");
        host.Log.Should().Be("private:ping");
    }

    [Test]
    public void MoreThanMaxQueuedMessagesIsAQuotaExceededError()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host, maxQueuedMessages: 2);

        // The worker is never pumped, so nothing leaves the queue.
        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            w.postMessage('a');
            w.postMessage('b');
            w.postMessage('c');
            """))!;

        exception.Error.Get("name").AsString().Should().Be("QuotaExceededError");
        exception.Error.Get("quota").AsNumber().Should().Be(2);
        exception.Error.Get("requested").AsNumber().Should().Be(3);

        // Draining the queue makes room again: the cap is a backstop against a stuck receiver, not a budget.
        Drain(parent, host.Connection);
        host.Log.Should().Be("a,b");

        parent.Execute("w.postMessage('d');");
        Drain(parent, host.Connection);
        host.Log.Should().Be("a,b,d");
    }

    // -------------------------------------------------------------------------------------------------------
    // terminate()
    // -------------------------------------------------------------------------------------------------------

    [Test]
    public void APostMessageAfterTerminateIsNeverDelivered()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.postMessage('before');");
        Drain(parent, host.Connection);
        host.Log.Should().Be("before");

        parent.Execute("w.terminate(); w.postMessage('after');");
        Drain(parent, host.Connection);

        host.Log.Should().Be("before", "terminate is an immediate parent-to-worker fence");
    }

    /// <summary>
    /// "Silent no-op" is true only for serializable values: HTML's step 5 runs before step 6, so the message is
    /// serialized — and any transfer performed — before there turns out to be nowhere to deliver it.
    /// </summary>
    [Test]
    public void APostMessageAfterTerminateStillThrowsDataCloneError()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.terminate();");

        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute("w.postMessage(function () {})"))!;
        exception.Error.Get("name").AsString().Should().Be("DataCloneError");

        parent.Execute("""
            var view = new Uint8Array([1, 2, 3]);
            w.postMessage(view.buffer, [view.buffer]);
            var lengthAfter = view.buffer.byteLength;
            """);

        parent.Evaluate("lengthAfter").AsNumber().Should().Be(0, "a transfer-listed buffer is still detached");
    }

    [Test]
    public void TerminateIsIdempotent()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            w.terminate();
            w.terminate();
            w.terminate();
            """);

        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.Terminated);
        host.Connection.IsEnded.Should().BeTrue();
        host.Connection.EndReason.Should().Be(WorkerEndReason.Terminated);
        host.Connection.IsFaulted.Should().BeFalse();

        // The host's own terminate is the same thing, and is equally idempotent.
        host.Connection.End();
        host.Ended.Should().ContainSingle();
    }

    /// <summary>
    /// Termination is cooperative and bounded by the engine's amortized check interval on the <i>worker's</i>
    /// thread: a running worker stops, and the cancellation erupts from whatever is pumping it.
    /// </summary>
    [Test]
    public void TerminateStopsARunningWorkerWithinTheAmortizedInterval()
    {
        using var running = new ManualResetEventSlim(false);

        var host = new TestWorkerHost();
        host.Modules[Specifier] = "started(); while (true) { }";
        host.Configure = engine => engine.SetValue("started", new Action(() => running.Set()));

        var parent = Parent(host);
        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");

        var connection = host.Connection;
        Exception? escaped = null;
        var pump = new Thread(() =>
        {
            try
            {
                connection.Worker.Tasks.ProcessTasks();
            }
            catch (Exception ex)
            {
                escaped = ex;
            }
        })
        {
            IsBackground = true,
            Name = "worker pump",
        };

        pump.Start();
        running.Wait(Ceiling).Should().BeTrue("the worker has to be inside its loop before terminate() means anything");

        parent.Execute("w.terminate();");

        pump.Join(Ceiling).Should().BeTrue("a terminated worker stops rather than spinning forever");
        escaped.Should().BeOfType<ExecutionCanceledException>(
            "the worker's budget is the host's to observe, not the parent script's");
    }

    /// <summary>
    /// Terminate's fourth step — "empty the port message queue of the port entangled with the worker's" — is
    /// the specification's own licence for discarding what the worker had already posted.
    /// </summary>
    [Test]
    public void TerminateDiscardsAMessageTheWorkerAlreadyPosted()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', () => postMessage('from-worker'));"));
        var parent = Parent(host);

        parent.Execute("""
            var got = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onmessage = e => got.push(e.data);
            w.postMessage('go');
            """);

        // The worker answers; the parent has not been pumped, so the answer is sitting on its queue.
        DrainWorker(host.Connection);

        parent.Execute("w.terminate();");
        Drain(parent, host.Connection);

        parent.Evaluate("got.join(',')").AsString().Should().BeEmpty("terminate empties the parent-side queue");
    }

    // -------------------------------------------------------------------------------------------------------
    // close()
    // -------------------------------------------------------------------------------------------------------

    [Test]
    public void CloseFromTheWorkerEndsTheConnection()
    {
        var host = new TestWorkerHost(Module("record('before'); close(); record('after');"));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        host.Connection.IsEnded.Should().BeFalse();

        Drain(parent, host.Connection);

        host.Connection.IsEnded.Should().BeTrue();
        host.Connection.EndReason.Should().Be(WorkerEndReason.ClosedByWorker);
        host.Connection.IsFaulted.Should().BeFalse();
        host.Connection.TerminationToken.IsCancellationRequested.Should().BeFalse(
            "close() lets the current turn finish; only terminate cancels");
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.ClosedByWorker);
    }

    /// <summary>
    /// <i>Close a worker</i> discards the worker's queued tasks and sets the closing flag. It does not abort
    /// the running script, which is what makes <c>close(); flushMetrics();</c> work.
    /// </summary>
    [Test]
    public void CloseDoesNotAbortTheCurrentlyRunningScript()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', () => {
                close();
                var total = 0;
                for (var i = 0; i < 500; i++) { total += i; }
                record('after-close:' + total);
            });
            """));

        var parent = Parent(host);
        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.postMessage('go');");

        Drain(parent, host.Connection);

        host.Log.Should().Be("after-close:124750", "five hundred statements is far past the 64-statement check interval");
        host.Connection.EndReason.Should().Be(WorkerEndReason.ClosedByWorker);
    }

    /// <summary>
    /// The parent-side queue drains rather than being discarded, so <c>postMessage(result); close();</c> — the
    /// commonest <c>close()</c> idiom there is — reaches the parent whether or not it had pumped in between.
    /// </summary>
    [Test]
    public void AFinalPostMessageBeforeCloseIsDelivered()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', () => { postMessage('result'); close(); });
            """));

        var parent = Parent(host);
        parent.Execute("""
            var got = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onmessage = e => got.push(e.data);
            w.postMessage('go');
            """);

        // The worker answers and closes with the parent never having been pumped.
        DrainWorker(host.Connection);
        host.Connection.IsEnded.Should().BeTrue();

        Drain(parent, host.Connection);

        parent.Evaluate("got.join(',')").AsString().Should().Be("result");
    }

    // -------------------------------------------------------------------------------------------------------
    // Startup failure
    // -------------------------------------------------------------------------------------------------------

    [Test]
    public void ALoadFailureEndsTheConnectionAsStartupFailedWithACLRError()
    {
        var host = new TestWorkerHost();
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./missing.js', { type: 'module' });");
        Drain(parent, host.Connection);

        var connection = host.Connection;
        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(
            WorkerEndReason.StartupFailed,
            "'the specifier was wrong' reported as 'somebody called terminate()' is the worst log line this could ship");
        connection.IsFaulted.Should().BeTrue();
        connection.Error.Should().BeOfType<ModuleResolutionException>(
            "the connection surface is a CLR channel, never a worker-realm JsValue");
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.StartupFailed);
    }

    [Test]
    public void AModuleThatThrowsEndsTheConnectionAsStartupFailedWithASummaryException()
    {
        var host = new TestWorkerHost(Module("throw new RangeError('boom');"));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        Drain(parent, host.Connection);

        var connection = host.Connection;
        connection.EndReason.Should().Be(WorkerEndReason.StartupFailed);
        connection.IsFaulted.Should().BeTrue();
        connection.Error.Should().NotBeNull();
        connection.Error!.Message.Should().Contain("boom");
    }

    // -------------------------------------------------------------------------------------------------------
    // The error channels
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A load or parse failure fires <b>a plain <c>Event</c></b> at the <c>Worker</c> object. The standard's
    /// step names no interface, and libraries branch on exactly that to tell "the script never loaded" from
    /// "the script threw".
    /// </summary>
    /// <remarks>
    /// Firing an <c>ErrorEvent</c> here instead would make every assertion below but the first still pass,
    /// which is why the shape is asserted rather than the arrival.
    /// </remarks>
    [Test]
    public void ALoadFailureFiresAPlainEventNotAnErrorEvent()
    {
        var host = new TestWorkerHost();
        var parent = Parent(host);

        parent.Execute("""
            var seen = [];
            var w = new Worker('./missing.js', { type: 'module' });
            w.onerror = e => seen.push([
                e.type,
                e instanceof ErrorEvent,
                e.isTrusted,
                'message' in e,
                e.target === w].join('|'));
            """);

        Drain(parent, host.Connection);

        parent.Evaluate("seen.join(',')").AsString().Should().Be(
            "error|false|true|false|true",
            "the onComplete step for a script that failed to fetch names no interface, so it is an Event");
    }

    /// <summary>
    /// An uncaught runtime error inside a worker reaches the parent as an <c>ErrorEvent</c> carrying the
    /// message and the location — and <c>error: null</c>, which is the specification's own value and not a
    /// limitation dressed up as one.
    /// </summary>
    [Test]
    public void AWorkerErrorReachesTheParentWithNullError()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', () => { throw new TypeError('boom'); });
            """));

        var parent = Parent(host);
        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = e => seen.push([
                e.type,
                e instanceof ErrorEvent,
                e.isTrusted,
                e.error === null,
                e.message,
                e.target === w].join('|'));
            w.postMessage('go');
            """);

        Drain(parent, host.Connection);

        parent.Evaluate("seen.length").AsNumber().Should().Be(1);
        parent.Evaluate("seen[0]").AsString().Should().StartWith("error|true|true|true|boom|");
        parent.Evaluate("seen[0]").AsString().Should().EndWith("|true");

        host.Connection.IsEnded.Should().BeFalse("a runtime error is not the end of the connection");
    }

    /// <summary>
    /// Step 5.2.3: the <c>ErrorEvent</c> at the <c>Worker</c> object was not cancelled either, so the failure is
    /// reported one level further up — at the parent's own global scope, and to the parent's sink.
    /// </summary>
    [Test]
    public void AnUnhandledWorkerErrorReachesTheParentsGlobalErrorEvent()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', () => { throw new TypeError('boom'); });
            """));

        var sink = new RecordingSink();
        var parent = Parent(host, parentSink: sink);
        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            addEventListener('error', e => seen.push(e.message + '|' + (e.error === null)));
            w.postMessage('go');
            """);

        Drain(parent, host.Connection);

        parent.Evaluate("seen.join(',')").AsString().Should().Be(
            "boom|true",
            "nothing handled it on the worker's side or on the Worker object's, so it is reported here");

        sink.Reports.Should().ContainSingle().Which.Should().Be($"{DiagnosticEventKind.WorkerError}:boom");
    }

    /// <summary>
    /// The relay is gated on HTML's <i>notHandled</i>, which is what a worker-side <c>preventDefault()</c> sets
    /// to false — and it is deliberately not the <see cref="DiagnosticsSink"/>, whose contract is that a script
    /// may not switch it off.
    /// </summary>
    [Test]
    public void AWorkerSidePreventDefaultStopsPropagationToTheParent()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('error', e => { record('worker-saw:' + e.message); e.preventDefault(); });
            addEventListener('message', () => { throw new TypeError('boom'); });
            """));

        var workerSink = new RecordingSink();
        var parentSink = new RecordingSink();
        var parent = Parent(host, parentSink: parentSink);
        host.Tune = options => options.WebApi.Diagnostics.Sink = workerSink;

        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = () => seen.push('parent-saw');
            addEventListener('error', () => seen.push('parent-global'));
            w.postMessage('go');
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be("worker-saw:boom");
        parent.Evaluate("seen.join(',')").AsString().Should().BeEmpty("preventDefault() on the worker's side is HTML's gate");

        workerSink.Reports.Should().ContainSingle().Which.Should().StartWith(DiagnosticEventKind.UncaughtCallbackError.ToString());
        parentSink.Reports.Should().BeEmpty("the parent was never told, so its sink has nothing to say either");
    }

    /// <summary>
    /// The worker global's <c>onerror</c> is HTML's legacy shape — «message, filename, lineno, colno, error»,
    /// and returning <see langword="true"/> cancels. That is what decides <i>notHandled</i> on the worker side.
    /// </summary>
    [Test]
    public void TheWorkerGlobalOnErrorTakesFiveArgumentsAndCancelsByReturningTrue()
    {
        var host = new TestWorkerHost(Module("""
            onerror = function (message, filename, lineno, colno, error) {
                record('args:' + arguments.length);
                record('message:' + message);
                record('lineno>0:' + (lineno > 0));
                record('error:' + (error instanceof TypeError));
                return true;
            };
            addEventListener('message', () => { throw new TypeError('boom'); });
            """));

        var parent = Parent(host);
        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = () => seen.push('parent-saw');
            w.postMessage('go');
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be(
            "args:5,message:boom,lineno>0:true,error:true",
            "an ErrorEvent named error at a global scope gets the five-argument invocation");
        parent.Evaluate("seen.join(',')").AsString().Should().BeEmpty("returning true is the worker cancelling it");
    }

    /// <summary>
    /// <c>Worker.onerror</c> is <c>AbstractWorker</c>'s <i>plain</i> <c>EventHandler</c> instead: invoked with
    /// the event, and cancelled by <c>preventDefault()</c> or by returning <see langword="false"/> — the
    /// opposite boolean from the global's.
    /// </summary>
    [Test]
    public void TheWorkerObjectOnErrorTakesTheEventAndCancelsByReturningFalse()
    {
        var host = new TestWorkerHost(Module("""
            addEventListener('message', () => { throw new TypeError('boom'); });
            """));

        var sink = new RecordingSink();
        var parent = Parent(host, parentSink: sink);
        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = function () { seen.push('args:' + arguments.length + ':' + arguments[0].message); return false; };
            addEventListener('error', () => seen.push('parent-global'));
            w.postMessage('go');
            """);

        Drain(parent, host.Connection);

        parent.Evaluate("seen.join(',')").AsString().Should().Be(
            "args:1:boom",
            "the plain shape takes the event, and returning false is what cancels it");
        sink.Reports.Should().BeEmpty("a cancelled ErrorEvent is not reported one level up");
    }

    /// <summary>
    /// An unhandled rejection fires at the worker's own global and reaches the parent through nothing at all.
    /// The sink wiring makes the opposite easy to fall into, which is why it is pinned.
    /// </summary>
    [Test]
    public void AnUnhandledRejectionInAWorkerDoesNotReachTheParent()
    {
        var host = new TestWorkerHost(Module("""
            onunhandledrejection = e => record('worker-rejection:' + e.reason.message);
            addEventListener('message', () => { Promise.reject(new TypeError('nope')); });
            """));

        var sink = new RecordingSink();
        var parent = Parent(host, parentSink: sink);
        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = () => seen.push('parent-error');
            addEventListener('error', () => seen.push('parent-global'));
            addEventListener('unhandledrejection', () => seen.push('parent-rejection'));
            w.postMessage('go');
            """);

        Drain(parent, host.Connection);

        host.Log.Should().Be("worker-rejection:nope", "the event really did fire, on the worker's own global");
        parent.Evaluate("seen.join(',')").AsString().Should().BeEmpty("the relay carries error reports and nothing else");
        sink.Reports.Should().BeEmpty();
    }

    /// <summary>
    /// A constraint failure is never an error event. It is a <c>JintException</c> and not a
    /// <c>JavaScriptException</c>, so nothing catches it: the worker's budget is the <b>host's</b> to observe,
    /// on whichever thread was pumping, and the parent's script never learns of it.
    /// </summary>
    /// <remarks>
    /// The constraint is a tripwire rather than a clock or a statement count, so the pin turns on the
    /// classification and not on how much work a module happens to do. It is registered as a <i>factory</i> on
    /// the parent, which is what the request replays — each engine gets its own instance over one shared
    /// switch.
    /// </remarks>
    [Test]
    public void AWorkerConstraintFailureEruptsFromTheHostsPump()
    {
        var tripwire = new Tripwire();
        var host = new TestWorkerHost(Module("""
            addEventListener('error', () => record('worker-error-event'));
            addEventListener('message', () => { var n = 0; for (var i = 0; i < 5000; i++) { n += i; } record('finished'); });
            """));

        var parent = Parent(host, tune: options => options.AddConstraint(() => new TripwireConstraint(tripwire)));
        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = () => seen.push('parent-error');
            """);

        // The module evaluates and the message is queued with the tripwire still down, so what trips is the
        // listener — the very callback a sink would otherwise turn into a report.
        Drain(parent, host.Connection);
        parent.Execute("w.postMessage('go');");
        host.Connection.IsEnded.Should().BeFalse();

        tripwire.Tripped = true;

        var escaped = Caught.Exception(() => DrainWorker(host.Connection));

        escaped.Should().BeOfType<MemoryLimitExceededException>("a constraint bounds the host's pump, it does not become an event");
        host.Log.Should().NotContain("worker-error-event");
        host.Log.Should().NotContain("finished");

        tripwire.Tripped = false;
        parent.Tasks.ProcessTasks();
        parent.Evaluate("seen.join(',')").AsString().Should().BeEmpty("the parent's script never learns of a budget");
    }

    // -------------------------------------------------------------------------------------------------------
    // The worker global scope
    // -------------------------------------------------------------------------------------------------------

    [Test]
    public void ImportScriptsThrowsTypeError()
    {
        var host = new TestWorkerHost(Module("""
            record('typeof:' + typeof importScripts);
            try { importScripts('./other.js'); record('no-throw'); }
            catch (e) { record('caught:' + (e instanceof TypeError)); }
            """));

        var parent = Parent(host);
        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        Drain(parent, host.Connection);

        host.Log.Should().Be(
            "typeof:function,caught:true",
            "the specification's own step 1 for a module worker prescribes the throw, so the function is present");
    }

    [Test]
    public void TheWorkerGlobalCarriesTheNamesHtmlGivesIt()
    {
        var host = new TestWorkerHost(Module("""
            record('WorkerGlobalScope:' + typeof WorkerGlobalScope);
            record('DedicatedWorkerGlobalScope:' + typeof DedicatedWorkerGlobalScope);
            record('self:' + (self === globalThis));
            record('postMessage:' + typeof postMessage);
            record('close:' + typeof close);
            record('name:' + name);
            record('onmessage:' + onmessage);
            """));

        var parent = Parent(host, workerName: "crunch");
        parent.Execute("var w = new Worker('./worker.js', { type: 'module', name: 'crunch' });");
        Drain(parent, host.Connection);

        host.Log.Should().Be(
            "WorkerGlobalScope:function,DedicatedWorkerGlobalScope:function,self:true,postMessage:function,close:function,name:crunch,onmessage:null");
    }

    /// <summary>
    /// <c>self</c> is a plain <c>readonly attribute WorkerGlobalScope self</c> here —
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self — where Window's is
    /// <c>[Replaceable] readonly attribute WindowProxy self</c>
    /// (https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-self), so the writable data
    /// property <c>WebApiRegistration</c> installs for a top-level global is the wrong one for this one.
    /// </summary>
    /// <remarks>
    /// <b>What "read-only" means is decided by the assigning code's own mode</b>, not by the descriptor:
    /// ignored in sloppy mode, a <c>TypeError</c> in strict. Both are pinned, because a fix that satisfied
    /// only one would be half a fix — the module body is strict, and an indirect <c>eval</c> is global code
    /// with no directive prologue, which is the only sloppy code a module worker can reach at all.
    /// </remarks>
    [Test]
    public void TheWorkerGlobalsSelfIsAReadOnlyAttribute()
    {
        var host = new TestWorkerHost(Module("""
            const d = Object.getOwnPropertyDescriptor(globalThis, 'self');
            record('writable:' + d.writable);
            record('enumerable:' + d.enumerable);
            record('configurable:' + d.configurable);
            record('value:' + (d.value === globalThis));

            // Sloppy first, so that what it reports does not stand on the strict case's outcome. An
            // indirect eval is global code with no directive prologue, which is the only sloppy code a
            // module worker can reach at all.
            try { (0, eval)("self = 'SLOPPY'"); record('sloppy:' + (self === globalThis)); }
            catch (e) { record('sloppy:' + e.constructor.name); }

            // And the module body itself is strict, where the same assignment is a TypeError.
            try { self = 'STRICT'; record('strict:assigned'); }
            catch (e) { record('strict:' + e.constructor.name); }

            record('same:' + (self === globalThis));
            record('brand:' + (self instanceof DedicatedWorkerGlobalScope));
            """));

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");
        Drain(parent, host.Connection);

        host.Log.Should().Be(
            "writable:false,enumerable:true,configurable:true,value:true," +
            "sloppy:true,strict:TypeError,same:true,brand:true");
    }

    /// <summary>
    /// The sibling attribute, and the contrast that makes the one above a per-interface decision rather than
    /// a blanket one: <c>DedicatedWorkerGlobalScope.name</c> is
    /// <c>[Replaceable] readonly attribute DOMString name</c> —
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-name — so the
    /// writable data property that simplification installs is right for it and wrong for <c>self</c>.
    /// </summary>
    [Test]
    public void TheWorkerGlobalsNameIsReplaceable()
    {
        var host = new TestWorkerHost(Module("""
            record('writable:' + Object.getOwnPropertyDescriptor(globalThis, 'name').writable);
            name = 'renamed';
            record('name:' + name);
            """));

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module', name: 'crunch' })");
        Drain(parent, host.Connection);

        host.Log.Should().Be("writable:true,name:renamed");
    }

    /// <summary>
    /// The read-only <c>self</c> is a <i>replacement</i> of the descriptor <c>WebApiRegistration</c>
    /// installed, so it can only be non-clobbering by identity — and that is the test. A <c>self</c> the host
    /// owns is never that descriptor and is left exactly as the host left it, with the host's own
    /// <c>[Replaceable]</c>-style writability intact.
    /// </summary>
    /// <remarks>
    /// The materialization count is the second half: the replacement compares references and reads nothing,
    /// so a host's own <i>lazy</i> global is not forced into existence merely by our looking at it — the rule
    /// every install in <c>WebApiRegistration</c> and beside it follows.
    /// </remarks>
    [Test]
    public void AHostThatInstalledItsOwnSelfKeepsIt()
    {
        var factoryCalls = 0;

        var host = new TestWorkerHost(Module("""
            record('writable:' + Object.getOwnPropertyDescriptor(globalThis, 'self').writable);
            record('host:' + (self.marker === 'host'));
            self = 'replaced';
            record('assigned:' + self);
            """))
        {
            Tune = options => options.AddLazyGlobal("self", engine =>
            {
                factoryCalls++;
                var value = new JsObject(engine);
                value.DefineOwnDataPropertyUnchecked("marker", new JsString("host"));
                return value;
            }),
        };

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");

        factoryCalls.Should().Be(0, "nothing about installing the worker scope may materialize a host's lazy global");

        Drain(parent, host.Connection);

        host.Log.Should().Be("writable:true,host:true,assigned:replaced");
        factoryCalls.Should().Be(1, "the worker's own first read is what materializes it");
    }

    [Test]
    public void TheWorkerObjectCarriesTheEventHandlerAttributes()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");

        parent.Evaluate("typeof w.postMessage").AsString().Should().Be("function");
        parent.Evaluate("typeof w.terminate").AsString().Should().Be("function");
        parent.Evaluate("w.onmessage").IsNull().Should().BeTrue();
        parent.Evaluate("w.onmessageerror").IsNull().Should().BeTrue();
        parent.Evaluate("w.onerror").IsNull().Should().BeTrue();
        parent.Evaluate("w instanceof EventTarget").AsBoolean().Should().BeTrue();
        parent.Evaluate("Object.prototype.toString.call(w)").AsString().Should().Be("[object Worker]");
    }

    /// <summary>
    /// And they are the same three rules every other event handler IDL attribute obeys
    /// (https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes), read on the one
    /// façade that needs a thread to exist at all: the handler is a single entry of the worker object's own
    /// listener list that keeps the position it first took, a non-object clears it, and an object that is not
    /// callable is stored and read back but never invoked.
    /// </summary>
    /// <remarks>
    /// The event is dispatched by hand rather than posted from the worker, because what is under test is the
    /// listener list the attribute writes to and not the arrival — <c>EventHandlerAttributeTests</c> reads the
    /// same three rules on every interface that can be built without a thread.
    /// </remarks>
    [Test]
    public void TheWorkerObjectsHandlerAttributeKeepsItsPositionAndTakesANonObjectAsNull()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        parent.Execute("""
            var log = [];
            var w = new Worker('./worker.js', { type: 'module' });
            var fire = () => w.dispatchEvent(new Event('message'));

            w.onmessage = () => log.push('handler');
            w.addEventListener('message', () => log.push('listener'));
            w.onmessage = () => log.push('replaced');
            fire();
            """);

        parent.Evaluate("log.join('|')").AsString().Should().Be("replaced|listener");

        parent.Execute("w.onmessage = 42; fire();");
        parent.Evaluate("w.onmessage").IsNull().Should().BeTrue();
        parent.Evaluate("log.join('|')").AsString().Should().Be("replaced|listener|listener");

        parent.Execute("var bag = {}; w.onmessage = bag; fire();");
        parent.Evaluate("w.onmessage === bag").AsBoolean().Should().BeTrue();
        parent.Evaluate("log.join('|')").AsString().Should().Be("replaced|listener|listener|listener");

        parent.Execute("w.terminate();");
    }

    // -------------------------------------------------------------------------------------------------------
    // Threading and the transferable-stream interaction
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The host ending a connection while the worker's own thread is <b>provably inside</b> the worker engine
    /// is the ordinary thread-per-worker shape, and it must touch nothing but endpoints, a token and
    /// interlocked bookkeeping.
    /// </summary>
    /// <remarks>
    /// The rendezvous is the point, and is what makes the pin deterministic rather than lucky: the worker's
    /// listener parks in a host callback, so its thread owns the engine for as long as the test wants. Anything
    /// in the end sequence that reached into the worker engine — a <c>ProcessTasks</c>, a <c>Dispose</c>, a
    /// <c>Constraints.Reset</c> — is then the engine's own concurrent-use exception, thrown out of
    /// <c>End()</c> on the caller's thread. No wall-clock assertion anywhere; the ceilings only stop a
    /// deadlock from hanging the run.
    /// </remarks>
    [Test]
    public void EndingTheConnectionFromTheParentWhileTheWorkerPumpsDoesNotThrow()
    {
        using var inside = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var host = new TestWorkerHost(Module("addEventListener('message', () => hold());"));
        host.Configure = engine => engine.SetValue("hold", new Action(() =>
        {
            inside.Set();
            release.Wait(Ceiling);
        }));

        var parent = Parent(host);
        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");

        var connection = host.Connection;
        Exception? escaped = null;
        var pump = new Thread(() =>
        {
            try
            {
                while (!connection.IsEnded)
                {
                    connection.Worker.Tasks.ProcessTasks();
                }
            }
            catch (ExecutionCanceledException)
            {
                // The cooperative half of terminate; the loop is leaving anyway.
            }
            catch (Exception ex)
            {
                escaped = ex;
            }
        })
        {
            IsBackground = true,
            Name = "worker pump",
        };

        pump.Start();

        // The message that parks the worker's thread inside the engine. Posted from the parent's thread, which
        // is allowed: a port's queue is the one part of another engine any thread may touch.
        parent.Execute("w.postMessage('park');");

        inside.Wait(Ceiling).Should().BeTrue("the worker's thread has to own its engine before End() means anything");

        // From the parent's thread, with the worker's thread demonstrably inside the worker engine.
        var end = Caught.Exception(connection.End);
        end.Should().BeNull("ending a connection touches only endpoints, a token and interlocked bookkeeping");

        release.Set();
        pump.Join(Ceiling).Should().BeTrue("the pump loop observes IsEnded and leaves");
        escaped.Should().BeNull();

        connection.EndReason.Should().Be(WorkerEndReason.Terminated);
        host.Ended.Should().ContainSingle();
    }

    [Test]
    public void TerminateIsObservableOnTheConnection()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");

        var connection = host.Connection;
        connection.IsEnded.Should().BeFalse();
        connection.EndReason.Should().BeNull();
        connection.TerminationToken.IsCancellationRequested.Should().BeFalse();

        parent.Execute("w.terminate();");

        connection.IsEnded.Should().BeTrue("the flag a pump loop keys on");
        connection.EndReason.Should().Be(WorkerEndReason.Terminated);
        connection.IsFaulted.Should().BeFalse();
        connection.TerminationToken.IsCancellationRequested.Should().BeTrue(
            "the token is what wakes a pump thread parked on it, from whichever thread ended the connection");
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.Terminated);
    }

    // -------------------------------------------------------------------------------------------------------
    // Restore and dispose
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A <c>RestoreGlobalSnapshot</c> on the parent ends every connection it is a party to — both endpoints,
    /// the token, and the host told with a reason of its own.
    /// </summary>
    [Test]
    public void AParentRestoreEndsTheConnection()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host);

        var snapshot = parent.Advanced.CaptureGlobalSnapshot();
        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        Drain(parent, host.Connection);

        var connection = host.Connection;
        connection.IsEnded.Should().BeFalse();

        parent.Advanced.RestoreGlobalSnapshot(snapshot);

        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.ParentRestored);
        connection.IsFaulted.Should().BeFalse();
        connection.TerminationToken.IsCancellationRequested.Should().BeTrue();
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.ParentRestored);
        parent.Evaluate("typeof w").AsString().Should().Be("undefined", "the binding went back with the globals");
    }

    /// <summary>
    /// The <c>Worker</c> object outlives the restore and is <b>inert</b>: <c>postMessage</c> is a no-op after
    /// the serialization the standard's step order still prescribes, and <c>terminate()</c> does nothing.
    /// </summary>
    /// <remarks>
    /// The snapshot is captured <i>after</i> the worker exists, which is the pooled-engine shape — the setup a
    /// host wants every cycle to start from — and is also what keeps the binding pointing at the same
    /// <c>Worker</c> object afterwards, so the pin can ask it questions.
    /// </remarks>
    [Test]
    public void APostMessageAfterAParentRestoreIsASilentNoOp()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        var snapshot = parent.Advanced.CaptureGlobalSnapshot();
        Drain(parent, host.Connection);

        parent.Advanced.RestoreGlobalSnapshot(snapshot);

        parent.Execute("w.postMessage('after'); w.terminate(); w.terminate();");
        Drain(parent, host.Connection);

        host.Log.Should().BeEmpty("both endpoints closed with the connection");
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(
            WorkerEndReason.ParentRestored,
            "terminate() on an ended connection is idempotent, not a second end");

        // Still post-serialization, which is HTML's own step order: step 5 runs before step 6.
        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute("w.postMessage(function () {})"))!;
        exception.Error.Get("name").AsString().Should().Be("DataCloneError");
    }

    /// <summary>
    /// An error event queued on the parent's loop before a restore is dropped by the generation fence rather
    /// than fired at listeners that are closures over globals the restore has just replaced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The listener records through a CLR delegate it closed over, not through a global, so the assertion
    /// survives the very restore it is about — which is what makes the pin about the mechanism rather than
    /// about the binding.
    /// </para>
    /// <para>
    /// Two things cover this and the pin holds them together. The parent-side event is a <b>job on the
    /// parent's loop</b> rather than a dispatch on the worker's thread — the first assertion, which is what a
    /// synchronous fire would break — and it carries the <b>generation</b> of the cycle that queued it, which
    /// is what covers a failure landing after the loop has already been cleared.
    /// </para>
    /// </remarks>
    [Test]
    public void AParentRestoreDropsAQueuedErrorEvent()
    {
        var fired = 0;
        var host = new TestWorkerHost();
        var parent = Parent(host);
        parent.SetValue("noteError", new Action(() => fired++));

        var snapshot = parent.Advanced.CaptureGlobalSnapshot();
        parent.Execute("""
            var w = new Worker('./missing.js', { type: 'module' });
            (function () { var note = noteError; w.onerror = function () { note(); }; })();
            """);

        // The worker's module fails to resolve, which queues the plain `error` event on the parent's loop. The
        // parent is deliberately not pumped.
        DrainWorker(host.Connection);
        host.Connection.EndReason.Should().Be(WorkerEndReason.StartupFailed);
        fired.Should().Be(0, "nothing has pumped the parent yet");

        parent.Advanced.RestoreGlobalSnapshot(snapshot);
        parent.Tasks.ProcessTasks();

        fired.Should().Be(0, "the job carries the generation of the cycle that queued it");
    }

    /// <summary>
    /// A <c>RestoreGlobalSnapshot</c> on the <i>worker</i> ends the connection too, and from the other side:
    /// the reason says which engine it was.
    /// </summary>
    [Test]
    public void AWorkerRestoreEndsTheConnection()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.postMessage('before');");
        Drain(parent, host.Connection);
        host.Log.Should().Be("before");

        var connection = host.Connection;
        var snapshot = connection.Worker.Advanced.CaptureGlobalSnapshot();
        connection.Worker.Advanced.RestoreGlobalSnapshot(snapshot);

        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.WorkerRestored);
        connection.TerminationToken.IsCancellationRequested.Should().BeTrue();
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.WorkerRestored);

        parent.Execute("w.postMessage('after');");
        Drain(parent, host.Connection);
        host.Log.Should().Be("before", "the parent's half closed with the connection");
    }

    [Test]
    public void ParentDisposeEndsEveryConnection()
    {
        var host = new TestWorkerHost(new Dictionary<string, string> { ["./a.js"] = "", ["./b.js"] = "" });
        var parent = Parent(host);

        parent.Execute("""
            var a = new Worker('./a.js', { type: 'module' });
            var b = new Worker('./b.js', { type: 'module' });
            """);

        host.Started.Should().HaveCount(2);

        parent.Dispose();

        host.Ended.Should().HaveCount(2);
        host.Ended.Should().AllSatisfy(entry => entry.Reason.Should().Be(WorkerEndReason.ParentDisposed));
        host.Started.Should().AllSatisfy(connection =>
        {
            connection.IsEnded.Should().BeTrue();
            connection.TerminationToken.IsCancellationRequested.Should().BeTrue();
        });
    }

    /// <summary>
    /// The host disposing the engine it built ends the connection from the worker's side, so the far side does
    /// not stay a <c>Worker</c> object that looks alive while every <c>postMessage</c> pays a full
    /// serialization into a queue nothing will ever drain.
    /// </summary>
    [Test]
    public void DisposingTheWorkerEngineEndsTheConnectionAsWorkerDisposed()
    {
        var host = new TestWorkerHost(Module("addEventListener('message', e => record(e.data));"));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.postMessage('before');");
        Drain(parent, host.Connection);
        host.Log.Should().Be("before");

        var connection = host.Connection;
        connection.Worker.Dispose();

        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.WorkerDisposed);
        connection.TerminationToken.IsCancellationRequested.Should().BeTrue();
        host.Ended.Should().ContainSingle().Which.Reason.Should().Be(WorkerEndReason.WorkerDisposed);

        parent.Execute("w.postMessage('after');");
        parent.Tasks.ProcessTasks();
        host.Log.Should().Be("before");
    }

    /// <summary>
    /// The drain-then-close endpoint state and the transferable-stream predicate meet here: a stream the worker
    /// transferred, and then <c>close()</c>d behind, still reaches the parent and still ends cleanly.
    /// </summary>
    /// <remarks>
    /// Closing the parent-side endpoint outright — terminate's treatment — strands the transferred stream's own
    /// channel side in the discarded record, and the parent's <c>read()</c> then never completes.
    /// </remarks>
    [Test]
    public void ATransferredStreamStillClosesCleanlyAcrossAWorkerClose()
    {
        var host = new TestWorkerHost(Module("""
            const stream = new ReadableStream({
                start(controller) {
                    controller.enqueue('a');
                    controller.enqueue('b');
                    controller.close();
                }
            });
            postMessage(stream, [stream]);
            close();
            """));

        var parent = Parent(host);
        parent.Execute("""
            var chunks = [];
            var done = false;
            var failed = null;
            var w = new Worker('./worker.js', { type: 'module' });
            w.onmessage = async e => {
                try {
                    const reader = e.data.getReader();
                    for (;;) {
                        const r = await reader.read();
                        if (r.done) { break; }
                        chunks.push(r.value);
                    }
                    done = true;
                } catch (err) { failed = String(err); }
            };
            """);

        // The worker posts the stream and closes with the parent never having been pumped, which is exactly the
        // case drain-then-close exists for.
        DrainWorker(host.Connection);
        host.Connection.EndReason.Should().Be(WorkerEndReason.ClosedByWorker);

        Drain(parent, host.Connection, rounds: 200);

        parent.Evaluate("failed").IsNull().Should().BeTrue();
        parent.Evaluate("chunks.join(',')").AsString().Should().Be("a,b");
        parent.Evaluate("done").AsBoolean().Should().BeTrue("the stream ends rather than hanging");
    }

    // -------------------------------------------------------------------------------------------------------
    // The worker global scope's interface objects
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The canonical worker feature-detect,
    /// <c>'DedicatedWorkerGlobalScope' in self &amp;&amp; self instanceof DedicatedWorkerGlobalScope</c>, which
    /// every fixture of wpt's <c>workers/modules/</c> corpus opens with.
    /// </summary>
    [Test]
    public void TheCanonicalWorkerSniffTakesTheDedicatedBranch()
    {
        var host = new TestWorkerHost(Module("""
            if ('DedicatedWorkerGlobalScope' in self && self instanceof DedicatedWorkerGlobalScope) {
              record('dedicated');
            } else if ('SharedWorkerGlobalScope' in self && self instanceof SharedWorkerGlobalScope) {
              record('shared');
            } else {
              record('neither');
            }
            """));

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");
        Drain(parent, host.Connection);

        host.Log.Should().Be("dedicated");
    }

    /// <summary>
    /// <c>instanceof</c> answers from a real prototype chain rather than from a <c>Symbol.hasInstance</c> shim:
    /// the worker's global object genuinely inherits from
    /// <c>DedicatedWorkerGlobalScope.prototype</c>, which inherits from <c>WorkerGlobalScope.prototype</c>.
    /// </summary>
    [Test]
    public void TheWorkerGlobalHasARealPrototypeChain()
    {
        var host = new TestWorkerHost(Module("""
            record('self:' + (self instanceof DedicatedWorkerGlobalScope));
            record('base:' + (self instanceof WorkerGlobalScope));
            record('proto:' + (Object.getPrototypeOf(self) === DedicatedWorkerGlobalScope.prototype));
            record('chain:' + (Object.getPrototypeOf(DedicatedWorkerGlobalScope.prototype) === WorkerGlobalScope.prototype));
            record('root:' + (Object.getPrototypeOf(WorkerGlobalScope.prototype) === Object.prototype));
            record('inherit:' + (Object.getPrototypeOf(DedicatedWorkerGlobalScope) === WorkerGlobalScope));
            record('ctor:' + (DedicatedWorkerGlobalScope.prototype.constructor === DedicatedWorkerGlobalScope));
            record('tag:' + Object.prototype.toString.call(self));
            record('hasSymbol:' + [DedicatedWorkerGlobalScope, WorkerGlobalScope].some(
                c => Object.prototype.hasOwnProperty.call(c, Symbol.hasInstance)));
            """));

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");
        Drain(parent, host.Connection);

        // `hasSymbol:false` is the load-bearing one: neither interface object owns a Symbol.hasInstance, so
        // every `instanceof` above was answered by walking the prototype chain and by nothing else.
        host.Log.Should().Be(
            "self:true,base:true,proto:true,chain:true,root:true,inherit:true,ctor:true," +
            "tag:[object DedicatedWorkerGlobalScope],hasSymbol:false");
    }

    /// <summary>
    /// Neither interface declares a constructor operation, so both refuse <c>new</c> —
    /// https://webidl.spec.whatwg.org/#es-interface-call.
    /// </summary>
    [Test]
    public void NeitherWorkerScopeInterfaceIsConstructible()
    {
        var host = new TestWorkerHost(Module("""
            for (const name of ['WorkerGlobalScope', 'DedicatedWorkerGlobalScope']) {
              record(name + ':' + typeof globalThis[name]);
              try { new globalThis[name](); record(name + ':no throw'); }
              catch (e) { record(name + ':' + e.constructor.name); }
            }
            """));

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");
        Drain(parent, host.Connection);

        host.Log.Should().Be(
            "WorkerGlobalScope:function,WorkerGlobalScope:TypeError," +
            "DedicatedWorkerGlobalScope:function,DedicatedWorkerGlobalScope:TypeError");
    }

    /// <summary>
    /// Both interfaces are <c>[Exposed=Worker]</c> / <c>[Exposed=DedicatedWorker]</c>, so the engine that
    /// <i>created</i> the worker carries neither — a parent is not a worker global however many it makes.
    /// </summary>
    [Test]
    public void TheParentCarriesNeitherWorkerScopeInterface()
    {
        var parent = Parent(new TestWorkerHost());

        parent.Evaluate("typeof WorkerGlobalScope").AsString().Should().Be("undefined");
        parent.Evaluate("typeof DedicatedWorkerGlobalScope").AsString().Should().Be("undefined");
        parent.Evaluate("Object.getPrototypeOf(globalThis) === Object.prototype").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The chain is inserted <b>non-clobbering</b>, exactly as every name beside it is: a provider whose
    /// worker engine has a global object with a <c>[[Prototype]]</c> of its own keeps it, and
    /// <c>self instanceof DedicatedWorkerGlobalScope</c> then answers <see langword="false"/> — the truth
    /// about that object rather than a claim planted on it.
    /// </summary>
    [Test]
    public void AHostThatGaveItsGlobalAPrototypeKeepsIt()
    {
        var host = new TestWorkerHost(Module("""
            record('self:' + (self instanceof DedicatedWorkerGlobalScope));
            record('named:' + ('DedicatedWorkerGlobalScope' in self));
            record('proto:' + (Object.getPrototypeOf(self).marker === 'host'));
            """))
        {
            Tune = options => options.UseHostFactory(_ => new PrototypeSettingHost()),
        };

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");
        Drain(parent, host.Connection);

        // The names are still installed — they are non-clobbering against the *global's own properties*, and
        // the host took no such name — but the brand they describe is not claimed of an object that is not one.
        host.Log.Should().Be("self:false,named:true,proto:true");
    }

    /// <summary>A host whose global object carries a prototype of its own.</summary>
    private sealed class PrototypeSettingHost : Jint.Runtime.Host
    {
        protected override ObjectInstance CreateGlobalObject(Realm realm)
        {
            var global = base.CreateGlobalObject(realm);
            var marker = new JsObject(Engine);
            marker.DefineOwnDataPropertyUnchecked("marker", new JsString("host"));
            global.Prototype = marker;
            return global;
        }
    }

    /// <summary>
    /// The worker global's own names are unaffected: they stay own properties of the global object, which is
    /// what makes them per-connection state rather than a realm intrinsic's.
    /// </summary>
    [Test]
    public void TheWorkerNamesRemainOwnPropertiesOfTheGlobal()
    {
        var host = new TestWorkerHost(Module("""
            for (const name of ['postMessage', 'close', 'importScripts', 'name', 'onmessage']) {
              record(name + ':' + Object.prototype.hasOwnProperty.call(globalThis, name));
            }
            """));

        var parent = Parent(host);
        parent.Execute("new Worker('./worker.js', { type: 'module' })");
        Drain(parent, host.Connection);

        host.Log.Should().Be("postMessage:true,close:true,importScripts:true,name:true,onmessage:true");
    }

    // -------------------------------------------------------------------------------------------------------
    // Harness
    // -------------------------------------------------------------------------------------------------------

    private static Dictionary<string, string> Module(string source) => new() { [Specifier] = source };

    private static Engine Parent(
        TestWorkerHost host,
        int maxWorkers = 16,
        int maxQueuedMessages = 16384,
        string workerName = "",
        DiagnosticsSink? parentSink = null,
        Action<Options>? tune = null)
    {
        _ = workerName;

        return new Engine(options =>
        {
            options.UseWebApis().UseWorkers(host);
            options.WebApi.Workers.MaxWorkers = maxWorkers;
            options.WebApi.Workers.MaxQueuedMessages = maxQueuedMessages;

            if (parentSink is not null)
            {
                options.WebApi.Diagnostics.Sink = parentSink;
            }

            tune?.Invoke(options);
        });
    }

    /// <summary>
    /// A sink that keeps what it was told as strings, so an assertion can name the kind and the message without
    /// holding a <c>JsValue</c> past the call.
    /// </summary>
    private sealed class RecordingSink : DiagnosticsSink
    {
        private readonly ConcurrentQueue<string> _reports = new();

        public List<string> Reports => _reports.ToList();

        public override void Report(DiagnosticEvent report)
            => _reports.Enqueue($"{report.Kind}:{report.Exception?.Message ?? report.Value.ToString()}");
    }

    /// <summary>A switch two engines' constraint instances share, so a pin can decide exactly when one fails.</summary>
    private sealed class Tripwire
    {
        private volatile bool _tripped;

        public bool Tripped
        {
            get => _tripped;
            set => _tripped = value;
        }
    }

    /// <summary>
    /// A constraint that observes external state and nothing else — the shape the request replays as a factory,
    /// and one whose failure is a <c>JintException</c> that is not a <c>JavaScriptException</c>.
    /// </summary>
    private sealed class TripwireConstraint : Constraint
    {
        private readonly Tripwire _tripwire;

        public TripwireConstraint(Tripwire tripwire) => _tripwire = tripwire;

        public override void Check()
        {
            if (_tripwire.Tripped)
            {
                // A JintException that is not a JavaScriptException, which is exactly the class every built-in
                // budget throws and exactly the discrimination the two report sites make.
                throw new MemoryLimitExceededException("the tripwire is up");
            }
        }

        public override void Reset()
        {
        }
    }

    /// <summary>
    /// Alternates the two engines' pumps a fixed number of times. Deliberately a count rather than a clock: a
    /// slow machine makes nothing here flaky, and a mechanism that stopped working fails on the assertion
    /// rather than on a timeout.
    /// </summary>
    private static void Drain(Engine parent, WorkerConnection connection, int rounds = 50)
    {
        for (var i = 0; i < rounds; i++)
        {
            PumpWorker(connection);
            parent.Tasks.ProcessTasks();
        }
    }

    private static void DrainWorker(WorkerConnection connection, int rounds = 50)
    {
        for (var i = 0; i < rounds; i++)
        {
            PumpWorker(connection);
        }
    }

    private static void PumpWorker(WorkerConnection connection)
    {
        try
        {
            connection.Worker.Tasks.ProcessTasks();
        }
        catch (ExecutionCanceledException)
        {
            // A terminated worker's cooperative stop erupts from whatever is pumping, which here is the test.
        }
    }

    /// <summary>
    /// A worker host the test drives itself: it builds the worker engine over an in-memory module map and does
    /// not start a thread, so the test decides exactly when the worker gets a turn.
    /// </summary>
    private sealed class TestWorkerHost : WorkerProvider
    {
        private readonly ConcurrentQueue<string> _log = new();

        public TestWorkerHost() : this(new Dictionary<string, string>())
        {
        }

        public TestWorkerHost(Dictionary<string, string> modules) => Modules = modules;

        public Dictionary<string, string> Modules { get; }

        /// <summary>Returns null from <c>CreateWorkerEngine</c>, which is the host's policy refusal.</summary>
        public bool Refuse { get; set; }

        /// <summary>Thrown from <c>CreateWorkerEngine</c>, to pin that nothing translates it.</summary>
        public Exception? Throw { get; set; }

        /// <summary>Replaces the whole engine-building step, for the validation pins.</summary>
        public Func<WorkerRequest, Engine>? Build { get; set; }

        /// <summary>Runs on the freshly built engine, before it is handed back.</summary>
        public Action<Engine>? Configure { get; set; }

        /// <summary>Runs on the worker's options, after <c>CreateDefaultOptions</c> and before the engine.</summary>
        public Action<Options>? Tune { get; set; }

        public int Requests { get; private set; }

        public List<WorkerConnection> Started { get; } = [];

        public List<(WorkerConnection Connection, WorkerEndReason Reason)> Ended { get; } = [];

        public WorkerConnection Connection => Started[^1];

        public string Log => string.Join(",", _log);

        public override Engine? CreateWorkerEngine(WorkerRequest request)
        {
            Requests++;

            if (Throw is { } failure)
            {
                throw failure;
            }

            if (Refuse)
            {
                return null;
            }

            if (Build is { } build)
            {
                return build(request);
            }

            var options = request.CreateDefaultOptions();
            options.Modules.ModuleLoader = new MapModuleLoader(Modules);
            Tune?.Invoke(options);

            var engine = new Engine(options);
            engine.SetValue("record", new Action<string>(_log.Enqueue));
            Configure?.Invoke(engine);
            return engine;
        }

        public override void OnWorkerStarted(WorkerConnection connection) => Started.Add(connection);

        public override void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason)
            => Ended.Add((connection, reason));
    }

    /// <summary>
    /// A module loader over a dictionary, so a test can say exactly what a specifier resolves to — and what it
    /// does not, which is what the startup-failure pin needs.
    /// </summary>
    private sealed class MapModuleLoader : IModuleLoader
    {
        private readonly Dictionary<string, string> _sources;

        internal MapModuleLoader(Dictionary<string, string> sources) => _sources = sources;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (!_sources.ContainsKey(moduleRequest.Specifier))
            {
                throw new ModuleResolutionException("Module not found", moduleRequest.Specifier, referencingModuleLocation, filePath: null);
            }

            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.RelativeOrAbsolute);
        }

        public Jint.Runtime.Modules.ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, resolved, _sources[resolved.Key]);
    }
}
#endif
