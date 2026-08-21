#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Threading;
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

    /// <summary>Ten seconds: long enough that a loaded machine is slow rather than red.</summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(10);

    // -------------------------------------------------------------------------------------------------------
    // Construction and refusals
    // -------------------------------------------------------------------------------------------------------

    [Fact]
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

    [Theory]
    [InlineData("new Worker('./worker.js')", "type: 'module'")]
    [InlineData("new Worker('./worker.js', { type: 'classic' })", "type: 'module'")]
    [InlineData("new Worker('./worker.js', { type: 'nope' })", "WorkerType")]
    public void AClassicWorkerRequestIsATypeError(string source, string expectedInMessage)
    {
        var host = new TestWorkerHost();
        var parent = Parent(host);

        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute(source));

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        exception.Message.Should().Contain(expectedInMessage);
        host.Requests.Should().Be(0, "WebIDL runs before the host is ever consulted");
    }

    [Fact]
    public void AProviderRefusalIsASecurityError()
    {
        var host = new TestWorkerHost { Refuse = true };
        var parent = Parent(host);

        var exception = Assert.Throws<JavaScriptException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"));

        exception.Error.Get("name").AsString().Should().Be(
            "SecurityError",
            "a refusal is a policy decision, not a fetch failure");
        host.Requests.Should().Be(1);
        host.Started.Should().BeEmpty();
    }

    [Fact]
    public void AProviderExceptionPropagatesUnchanged()
    {
        var failure = new InvalidTimeZoneException("the tenant has no worker budget");
        var host = new TestWorkerHost { Throw = failure };
        var parent = Parent(host);

        var thrown = Assert.Throws<InvalidTimeZoneException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"));

        thrown.Should().BeSameAs(failure, "nothing a provider throws is translated");
    }

    [Fact]
    public void MoreThanMaxWorkersIsAQuotaExceededError()
    {
        var host = new TestWorkerHost(Module("")) ;
        var parent = Parent(host, maxWorkers: 2);

        parent.Execute("var a = new Worker('./worker.js', { type: 'module' });");
        parent.Execute("var b = new Worker('./worker.js', { type: 'module' });");

        var exception = Assert.Throws<JavaScriptException>(
            () => parent.Execute("var c = new Worker('./worker.js', { type: 'module' });"));

        exception.Error.Get("name").AsString().Should().Be("QuotaExceededError");
        exception.Error.Get("quota").AsNumber().Should().Be(2);
        exception.Error.Get("requested").AsNumber().Should().Be(3);
        host.Requests.Should().Be(2, "the cap is checked before the host is consulted");

        // And the slot comes back when a connection ends.
        parent.Execute("a.terminate();");
        parent.Execute("var d = new Worker('./worker.js', { type: 'module' });");
    }

    [Fact]
    public void AnEngineWithoutTheTerminationTokenIsRefused()
    {
        var host = new TestWorkerHost
        {
            Build = _ => new Engine(options => options.UseWebApis(WebApiFeatures.Messaging | WebApiFeatures.GlobalEvents)),
        };
        var parent = Parent(host);

        var exception = Assert.Throws<InvalidOperationException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"));

        exception.Message.Should().Contain("request.TerminationToken");
        exception.Message.Should().Contain("options.CancellationToken", "the message names the one-line fix");
    }

    [Fact]
    public void AnEngineWithoutMessagingIsRefused()
    {
        var host = new TestWorkerHost
        {
            Build = request => new Engine(options => options.CancellationToken(request.TerminationToken)),
        };
        var parent = Parent(host);

        var exception = Assert.Throws<InvalidOperationException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"));

        exception.Message.Should().Contain("WebApiFeatures.Messaging");
    }

    [Fact]
    public void TheParentEngineItselfIsRefused()
    {
        var host = new TestWorkerHost();
        Engine? parent = null;
        host.Build = _ => parent!;
        parent = Parent(host);

        var exception = Assert.Throws<InvalidOperationException>(
            () => parent.Execute("new Worker('./worker.js', { type: 'module' })"));

        exception.Message.Should().Contain("returned the parent engine");
    }

    /// <summary>
    /// A pre-warmed engine another thread is already inside is the bug this validates against, and the answer
    /// is the engine's own admission error at <c>new Worker()</c> rather than silent corruption later.
    /// </summary>
    [Fact]
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
                () => parent.Execute("new Worker('./worker.js', { type: 'module' })"));

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
    [Fact]
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
                connection.Worker.Advanced.ProcessTasks();
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
    [Fact]
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

    [Fact]
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
    [Fact]
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
    [Fact]
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
    [Fact]
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

    [Fact]
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

    [Fact]
    public void ASharedArrayBufferIsADataCloneError()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute("""
            var w = new Worker('./worker.js', { type: 'module' });
            w.postMessage(new SharedArrayBuffer(8));
            """));

        exception.Error.Get("name").AsString().Should().Be("DataCloneError");
    }

    [Fact]
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

    [Fact]
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
            """));

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

    [Fact]
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
    [Fact]
    public void APostMessageAfterTerminateStillThrowsDataCloneError()
    {
        var host = new TestWorkerHost(Module(""));
        var parent = Parent(host);

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.terminate();");

        var exception = Assert.Throws<JavaScriptException>(() => parent.Execute("w.postMessage(function () {})"));
        exception.Error.Get("name").AsString().Should().Be("DataCloneError");

        parent.Execute("""
            var view = new Uint8Array([1, 2, 3]);
            w.postMessage(view.buffer, [view.buffer]);
            var lengthAfter = view.buffer.byteLength;
            """);

        parent.Evaluate("lengthAfter").AsNumber().Should().Be(0, "a transfer-listed buffer is still detached");
    }

    [Fact]
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
    [Fact]
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
                connection.Worker.Advanced.ProcessTasks();
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
    [Fact]
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

    [Fact]
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
    [Fact]
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
    [Fact]
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

    [Fact]
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

    [Fact]
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
    // The worker global scope
    // -------------------------------------------------------------------------------------------------------

    [Fact]
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

    [Fact]
    public void TheWorkerGlobalHasNoWorkerGlobalScopeInterfaceObject()
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
            "WorkerGlobalScope:undefined,DedicatedWorkerGlobalScope:undefined,self:true,postMessage:function,close:function,name:crunch,onmessage:null");
    }

    [Fact]
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
    [Fact]
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
                    connection.Worker.Advanced.ProcessTasks();
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
        var end = Record.Exception(connection.End);
        end.Should().BeNull("ending a connection touches only endpoints, a token and interlocked bookkeeping");

        release.Set();
        pump.Join(Ceiling).Should().BeTrue("the pump loop observes IsEnded and leaves");
        escaped.Should().BeNull();

        connection.EndReason.Should().Be(WorkerEndReason.Terminated);
        host.Ended.Should().ContainSingle();
    }

    /// <summary>
    /// The drain-then-close endpoint state and the transferable-stream predicate meet here: a stream the worker
    /// transferred, and then <c>close()</c>d behind, still reaches the parent and still ends cleanly.
    /// </summary>
    /// <remarks>
    /// Closing the parent-side endpoint outright — terminate's treatment — strands the transferred stream's own
    /// channel side in the discarded record, and the parent's <c>read()</c> then never completes.
    /// </remarks>
    [Fact]
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
    // Harness
    // -------------------------------------------------------------------------------------------------------

    private static Dictionary<string, string> Module(string source) => new() { [Specifier] = source };

    private static Engine Parent(
        TestWorkerHost host,
        int maxWorkers = 16,
        int maxQueuedMessages = 16384,
        string workerName = "")
    {
        _ = workerName;

        return new Engine(options =>
        {
            options.UseWebApis().UseWorkers(host);
            options.WebApi.Workers.MaxWorkers = maxWorkers;
            options.WebApi.Workers.MaxQueuedMessages = maxQueuedMessages;
        });
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
            parent.Advanced.ProcessTasks();
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
            connection.Worker.Advanced.ProcessTasks();
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

        public Jint.Runtime.Modules.Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, resolved, _sources[resolved.Key]);
    }
}
#endif
