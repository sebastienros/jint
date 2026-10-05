#if NET8_0_OR_GREATER
using Jint.Native;

namespace Jint.Tests.PublicInterface;

public sealed class HostIndexedDbTests
{
    private static Engine CreateEngine(int registration)
    {
        var engine = registration == 0
            ? new Engine(options => options.UseWebApis(WebApiFeatures.IndexedDb | WebApiFeatures.Messaging))
            : new Engine(options => options.UseWebApis(registration == 1 ? WebApiFeatures.None : WebApiFeatures.Messaging));
        if (registration != 0)
        {
            engine.WebApi.Enable(WebApiFeatures.IndexedDb | WebApiFeatures.Messaging);
        }
        return engine;
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public void HostCallsCheckpointMicrotasksBeforeAndAfterFirstDatabaseUse(int registration, bool call)
    {
        using var engine = CreateEngine(registration);
        engine.Execute("""
            var trace = [];
            function f() { Promise.resolve().then(() => trace.push('micro')).then(() => trace.push('nested')); }
            """);
        var f = engine.GetValue("f");
        for (var pass = 0; pass < 2; pass++)
        {
            engine.Tasks.Post(() => engine.Invoke("eval", "trace.push('task')"));
            if (call) engine.Call(f);
            else engine.Invoke("f");
            // GetValue observes the result without entering a script evaluation that might drain jobs.
            engine.GetValue("trace").AsArray().Length.Should().Be(2);
            engine.Tasks.ProcessTasks();
            engine.Evaluate("trace.join(',')").Should().Be("micro,nested,task");
            engine.Execute("trace = []; indexedDB.open('host-checkpoint').onsuccess = e => e.target.result.close();");
            engine.Tasks.ProcessTasks();
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void MessagingTasksAndListenerCheckpointsHaveStableOrdering(int registration)
    {
        using var engine = CreateEngine(registration);
        engine.Execute("""
            var trace = [];
            const sender = new BroadcastChannel('ordering');
            const receiver = new BroadcastChannel('ordering');
            receiver.addEventListener('message', () => {
                trace.push('listener');
                Promise.resolve().then(() => trace.push('listener-micro'));
            });
            receiver.addEventListener('message', () => trace.push('second-listener'));
            function send() {
                sender.postMessage(1);
                sender.postMessage(2);
                Promise.resolve().then(() => trace.push('micro'));
            }
            """);
        for (var pass = 0; pass < 2; pass++)
        {
            engine.Invoke("send");
            engine.Tasks.ProcessTasks();
            engine.Evaluate("trace.join(',')").Should().Be(
                "micro,listener,listener-micro,second-listener,listener,listener-micro,second-listener");
            engine.Execute("trace = []; indexedDB.open('host-ordering').onsuccess = e => e.target.result.close();");
            engine.Tasks.ProcessTasks();
        }
    }

    [Test]
    public void LiveEnableSeparatesAlreadyQueuedTasksAndMicrotasks()
    {
        using var engine = new Engine();
        engine.Execute("""
            var trace = [];
            function queue() { Promise.resolve().then(() => trace.push('micro')); }
            function noop() {}
            """);
        engine.Tasks.Post(() => engine.Invoke("eval", "trace.push('task')"));
        engine.Invoke("queue");
        engine.GetValue("trace").AsArray().Length.Should().Be(0);
        engine.WebApi.Enable(WebApiFeatures.IndexedDb);
        engine.Invoke("noop");
        engine.GetValue("trace").AsArray().Length.Should().Be(1);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("trace.join(',')").Should().Be("micro,task");
    }

    [Test]
    public void HostsWithoutIndexedDbKeepTheirExistingQueueBehavior()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));
        engine.Execute("""
            var trace = [];
            const sender = new BroadcastChannel('ordering');
            const receiver = new BroadcastChannel('ordering');
            receiver.onmessage = () => trace.push('task');
            function f() {
                sender.postMessage(1);
                Promise.resolve().then(() => trace.push('micro'));
            }
            """);
        engine.Invoke("f");
        engine.GetValue("trace").AsArray().Length.Should().Be(0);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("trace.join(',')").Should().Be("task,micro");
    }

    [Test]
    public void HostCanEnableIndexedDbAndPumpAStoredValue()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.IndexedDb));
        var result = engine.Evaluate("""
            new Promise((resolve,reject) => {
                const r=indexedDB.open('host');
                r.onerror=()=>reject(r.error);
                r.onupgradeneeded=()=>r.result.createObjectStore('items');
                r.onsuccess=()=>{
                    const db=r.result,t=db.transaction('items','readwrite'),s=t.objectStore('items');
                    s.put({answer:42},'key');
                    t.oncomplete=()=>{
                        const read=db.transaction('items').objectStore('items').get('key');
                        read.onsuccess=()=>{resolve(read.result.answer);db.close();};
                        read.onerror=()=>reject(read.error);
                    };
                    t.onabort=()=>reject(t.error);
                };
            })
            """);
        engine.Tasks.ProcessTasks();
        result.UnwrapIfPromise().AsNumber().Should().Be(42);
    }
}
#endif
