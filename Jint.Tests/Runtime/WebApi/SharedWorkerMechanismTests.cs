#if NET8_0_OR_GREATER
#nullable enable
using Jint.Native;
using Jint.WebApi;
using Jint.WebApi.Messaging;
using Jint.WebApi.Workers;

namespace Jint.Tests.Runtime.WebApi;

public sealed class SharedWorkerMechanismTests
{
    [Test]
    public void SharedGlobalHasItsOwnBrandAndOnlyItsOwnMessagingInterface()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Default | WebApiFeatures.GlobalEvents));
        var connection = new WorkerConnection(engine, engine, "shared", null, default);
        WorkerGlobalScope.InstallShared(connection, () => { }, (_, _) => JsValue.Undefined);
        engine.Evaluate(
            """
            self instanceof SharedWorkerGlobalScope && self instanceof WorkerGlobalScope &&
            typeof DedicatedWorkerGlobalScope === 'undefined' && typeof postMessage === 'undefined' &&
            typeof SharedWorker === 'undefined' && typeof onmessage === 'undefined' &&
            onconnect === null && onerror === null && name === 'shared'
            """).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AConnectEventCarriesTheSameRealmPortAsItsSource()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Default | WebApiFeatures.GlobalEvents));
        var connection = new WorkerConnection(engine, engine, "", null, default);
        WorkerGlobalScope.InstallShared(connection, () => { }, (_, _) => JsValue.Undefined);
        engine.Execute(
            "globalThis.connected = false; onconnect = e => { connected = e.data === '' &&"
            + "e.source === e.ports[0] && e.ports[0] instanceof MessagePort && Object.isFrozen(e.ports); };");
        var port = new JsMessagePort(engine, engine.Realm);
        engine._webApi!.GlobalEventTarget.DispatchEvent(engine.Realm.Intrinsics.MessageEvent.CreateTrustedConnectEvent(port));
        engine.Evaluate("connected").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RestoreReleasesOwnersAndEndsASharedWorker()
    {
        using var engine = new Engine(o => o.UseWebApis());
        var released = 0;
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine._webApi!.ReleaseSharedWorkerOwners = () => released++;
        var connection = new WorkerConnection(engine, engine, "", null, default);
        engine._webApi.OwningSharedWorker = connection;
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        released.Should().Be(1);
        connection.EndReason.Should().Be(WorkerEndReason.WorkerRestored);
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        released.Should().Be(1);
    }

    [Test]
    public void DisposeReleasesOwnersAndEndsASharedWorker()
    {
        var engine = new Engine(o => o.UseWebApis());
        var released = false;
        engine._webApi!.ReleaseSharedWorkerOwners = () => released = true;
        var connection = new WorkerConnection(engine, engine, "", null, default);
        engine._webApi.OwningSharedWorker = connection;
        engine.Dispose();
        released.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.WorkerDisposed);
    }

    [Test]
    public void AWorkerQueueBoundTravelsWithATransferredPort()
    {
        using var sender = new Engine(o => o.UseWebApis());
        using var receiver = new Engine(o => o.UseWebApis());
        var first = new MessagePortEndpoint { MaxQueuedMessages = 1 };
        var second = new MessagePortEndpoint { MaxQueuedMessages = 1 };
        MessagePortEndpoint.Entangle(first, second);
        var port = new JsMessagePort(sender, sender.Realm, first);
        var receivingPort = new JsMessagePort(sender, sender.Realm, second);
        var transferred = new JsMessagePort(receiver, receiver.Realm, receivingPort.DetachForTransfer());
        sender.SetValue("port", port);
        sender.Execute("port.postMessage('one')");
        sender.Evaluate("try { port.postMessage('two'); 'missed' } catch(e) { e.name }")
            .AsString().Should().Be("QuotaExceededError");
        receiver.SetValue("port", transferred);
        receiver.Execute("globalThis.result = ''; port.onmessage = e => result = e.data;");
        receiver.Tasks.ProcessTasks();
        receiver.Evaluate("result").AsString().Should().Be("one");
    }
}
#endif
