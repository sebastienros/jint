#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public sealed class HostIndexedDbTests
{
    private const string StorageHelpers = """
        function request(r) {
            return new Promise((resolve,reject)=>{
                r.onsuccess=()=>resolve(r.result);
                r.onerror=()=>reject(r.error);
            });
        }
        async function write(name, size) {
            let db;
            try {
                const r=indexedDB.open(name);
                r.onupgradeneeded=()=>r.result.createObjectStore('items');
                db=await request(r);
                const tx=db.transaction('items','readwrite');
                const done=new Promise((resolve,reject)=>{
                    tx.oncomplete=()=>resolve('ok');
                    tx.onabort=()=>reject(tx.error);
                });
                tx.objectStore('items').put('x'.repeat(size),'key');
                return await done;
            } catch(e) { return e.name; }
            finally { if(db) db.close(); }
        }
        """;

    private static JsValue StorageResult(Engine engine, string script)
    {
        var result = engine.Evaluate(script);
        engine.Tasks.ProcessTasks();
        return result.UnwrapIfPromise();
    }

    [TestCase(0L, "AbortError")]
    [TestCase(1024L, "QuotaExceededError")]
    [TestCase(long.MaxValue, "ok")]
    public void HostControlsRetainedQuota(long maxBytes, string expected)
    {
        using var engine = new Engine(options =>
        {
            options.WebApi.IndexedDb.MaxBytes = maxBytes;
            options.UseWebApis(WebApiFeatures.IndexedDb);
        });
        engine.Execute(StorageHelpers);
        StorageResult(engine, "write('bounded',1000)").Should().Be(expected);
        StorageResult(engine, "indexedDB.databases().then(x=>x.length)").Should().Be(maxBytes == 0 ? 0 : 1);
        if (maxBytes == 1024)
        {
            StorageResult(engine, """
                request(indexedDB.open('bounded')).then(async db=>{
                    const count=await request(db.transaction('items').objectStore('items').count());
                    db.close();return count;
                })
                """).Should().Be(0);
        }
    }

    [Test]
    public void QuotaSpansDatabasesAndSnapshotRestoresAndDeletionReleasesIt()
    {
        using var engine = new Engine(options =>
        {
            options.WebApi.IndexedDb.MaxBytes = 1024;
            options.UseWebApis(WebApiFeatures.IndexedDb);
        });
        engine.Execute(StorageHelpers);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        StorageResult(engine, "write('first',200)").Should().Be("ok");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        StorageResult(engine, "write('second',200)").Should().Be("QuotaExceededError");
        StorageResult(engine, "request(indexedDB.deleteDatabase('first')).then(()=>true)").Should().Be(true);
        StorageResult(engine, "write('second',200)").Should().Be("ok");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IndexedDbOptionsFreezeEvenWhenMaterializedAfterConstruction(bool materialize)
    {
        var options = new Options();
        if (materialize) options.WebApi.IndexedDb.MaxBytes = 1024;
        using var engine = new Engine(options);
        engine.Options.WebApi.IndexedDb.MaxBytes.Should().Be(materialize ? 1024 : 50 * 1024 * 1024);
        Invoking(() => engine.Options.WebApi.IndexedDb.MaxBytes = 2048).Should().Throw<InvalidOperationException>();
        Invoking(() => new Options().WebApi.IndexedDb.MaxBytes = -1).Should().Throw<ArgumentOutOfRangeException>();
        engine.Evaluate("typeof indexedDB").Should().Be("undefined");
    }

    [Test]
    public void LiveConfigurationClonesQuotaAndSharedOptionsDoNotShareStorage()
    {
        var options = new Options();
        options.WebApi.IndexedDb.MaxBytes = 1024;
        options.UseWebApis(WebApiFeatures.None);
        using var first = new Engine(options);
        using var second = new Engine(options);
        first.WebApi.Enable(WebApiFeatures.IndexedDb, web => web.IndexedDb.MaxBytes = 0);
        second.WebApi.Enable(WebApiFeatures.IndexedDb);
        second.Options.WebApi.IndexedDb.MaxBytes.Should().Be(1024);
        first.Execute(StorageHelpers);
        second.Execute(StorageHelpers);
        StorageResult(first, "write('same',10)").Should().Be("AbortError");
        StorageResult(second, "write('same',10)").Should().Be("ok");
        using var third = new Engine(options);
        third.WebApi.Enable(WebApiFeatures.IndexedDb);
        StorageResult(third, "indexedDB.databases().then(x=>x.length)").Should().Be(0);
    }

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
    public void HostCanResumePumpingAfterAThrowingUpgradeListener()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.IndexedDb));
        engine.Execute("""
            var db;
            const initial=indexedDB.open('existing');
            initial.onupgradeneeded=()=>initial.result.createObjectStore('items');
            initial.onsuccess=()=>db=initial.result;
            function start() {
                globalThis.openError='';globalThis.opened=false;
                const failed=indexedDB.open('failed');
                failed.onupgradeneeded=()=>{throw new Error('upgrade listener failed');};
                failed.onerror=()=>openError=failed.error.name;
                const queued=indexedDB.open('queued');
                queued.onsuccess=()=>{opened=true;queued.result.close();};
            }
            """);
        // Invoke checkpoints reactions but leaves the database tasks to the explicit host pump.
        engine.Invoke("start");
        Invoking(() => engine.Tasks.ProcessTasks()).Should().Throw<JavaScriptException>()
            .WithMessage("*upgrade listener failed*");
        engine.Tasks.ProcessTasks();
        engine.GetValue("openError").Should().Be("AbortError");
        engine.GetValue("opened").Should().Be(true);
        var result = engine.Evaluate("""
            new Promise(resolve=>{
                const r=db.transaction('items').objectStore('items').count();
                r.onsuccess=()=>resolve(r.result);
            })
            """);
        engine.Tasks.ProcessTasks();
        result.UnwrapIfPromise().Should().Be(0);
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
