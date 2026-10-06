using Jint.Browser;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;

namespace Jint.Tests.Browser.Storage;

public sealed class IndexedDbTests
{
    private const string Helpers = """
        function req(r){return new Promise((resolve,reject)=>{r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});}
        function done(t){return new Promise((resolve,reject)=>{t.oncomplete=resolve;t.onabort=()=>reject(t.error);});}
        async function openDb(){
            const r=indexedDB.open('shared');r.onupgradeneeded=()=>r.result.createObjectStore('items');
            return await req(r);
        }
        """;
    private const string Put = """
        (async()=>{
            const db=await openDb(),t=db.transaction('items','readwrite'),finished=done(t);
            t.objectStore('items').put({value:'kept',buffer:new Uint8Array([7])},1);
            await finished;db.close();return 'kept';
        })()
        """;
    private const string Read = """
        (async()=>{
            const db=await openDb(),v=await req(db.transaction('items').objectStore('items').get(1));
            db.close();return v?.value ?? 'missing';
        })()
        """;

    private static async Task Initialize(Page page) => await page.EvaluateAsync(Helpers);

    [Test]
    public async Task PersistsAcrossNavigationAndSiblingPageThreads()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<title>one</title>").MapHtml("/two", "<title>two</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        await Initialize(fixture.Page);
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Put)).Should().Be("kept");
        await fixture.Page.NavigateAsync(fixture.Url("/two"));
        await Initialize(fixture.Page);
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Read)).Should().Be("kept");
        var sibling = await fixture.NewPageAsync();
        await sibling.NavigateAsync(fixture.Url("/one"));
        await Initialize(sibling);
        (await sibling.EvaluateAndAwaitAsync<string>(Read)).Should().Be("kept");
        fixture.Page.Errors.Should().BeEmpty();
        sibling.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task IsolatesOriginsAndContexts()
    {
        using var other = new LoopbackServer();
        other.MapHtml("/page", "<title>other</title>");
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", "<title>first</title>"),
            options => options.UrlFilter = uri => uri.IsLoopback);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await Initialize(fixture.Page);
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        var isolated = await fixture.NewIsolatedPageAsync();
        await isolated.NavigateAsync(fixture.Url("/page"));
        (await isolated.EvaluateAndAwaitAsync<int>("indexedDB.databases().then(x=>x.length)")).Should().Be(0);
        await fixture.Page.NavigateAsync(other.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<int>("indexedDB.databases().then(x=>x.length)")).Should().Be(0);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await Initialize(fixture.Page);
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Read)).Should().Be("kept");
        fixture.Page.Errors.Should().BeEmpty();
        isolated.Errors.Should().BeEmpty();
    }

    [TestCase("about:blank")]
    [TestCase("data:text/html,opaque")]
    [TestCase("file:///opaque.html")]
    public async Task OpaqueOriginsExposeFactoryButThrowSecurityError(string url)
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync("<title>opaque</title>", url);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            (async()=>{
                const errors=[];
                for(const f of [()=>indexedDB.open('x'),()=>indexedDB.deleteDatabase('x')]){
                    try{f();errors.push('no error');}catch(e){errors.push(e.name);}
                }
                errors.push(await indexedDB.databases().then(()=>'',e=>e.name));
                return [indexedDB instanceof IDBFactory,...errors].join('|');
            })()
            """)).Should().Be("true|SecurityError|SecurityError|SecurityError");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PlainHttpAndCustomPartitionsStillHaveIndexedDb()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureContext: o => o.StoragePartition = new CustomPartition());
        await fixture.Page.SetContentAsync("<title>http</title>", "http://example.test/page");
        await Initialize(fixture.Page);
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Put)).Should().Be("kept");
        var sibling = await fixture.NewPageAsync();
        await sibling.SetContentAsync("<title>same origin</title>", "http://example.test/other");
        await Initialize(sibling);
        (await sibling.EvaluateAndAwaitAsync<string>(Read)).Should().Be("kept");
        fixture.Page.Errors.Should().BeEmpty();
        sibling.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task VersionChangeAndBlockedCrossPageThreads()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync("<title>first</title>", fixture.Url("/page"));
        await Initialize(fixture.Page);
        await fixture.Page.EvaluateAndAwaitAsync("openDb().then(db=>{globalThis.db=db;globalThis.changed=new Promise(resolve=>db.onversionchange=e=>resolve(e.oldVersion+':'+e.newVersion));})");
        var sibling = await fixture.NewPageAsync();
        await sibling.SetContentAsync("<title>second</title>", fixture.Url("/page"));
        await sibling.EvaluateAsync("""
            globalThis.finished=new Promise((resolve,reject)=>{
                const r=indexedDB.open('shared',2);
                globalThis.blocked=new Promise(resolve=>r.onblocked=()=>resolve(true));
                r.onerror=()=>reject(r.error);
                r.onsuccess=()=>{r.result.close();resolve(2);};
            });
            """);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("changed")).Should().Be("1:2");
        (await sibling.EvaluateAndAwaitAsync<bool>("blocked")).Should().BeTrue();
        await fixture.Page.EvaluateAsync("db.close()");
        (await sibling.EvaluateAndAwaitAsync<int>("finished")).Should().Be(2);
        fixture.Page.Errors.Should().BeEmpty();
        sibling.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DedicatedWorkerReadsAndUpdatesSamePartition()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/worker.js", _ =>
            LoopbackResponse.Script(Helpers + """
                onmessage=async()=>{
                    const db=await openDb(),t=db.transaction('items','readwrite'),finished=done(t),s=t.objectStore('items');
                    const value=await req(s.get(1));value.buffer[0]=9;value.value='worker';
                    s.put(value,1);await finished;db.close();postMessage('updated');
                };
                """)));
        await fixture.Page.SetContentAsync("<title>worker</title>", fixture.Url("/page"));
        await Initialize(fixture.Page);
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            new Promise((resolve,reject)=>{
                const w=new Worker('/worker.js',{type:'module'});
                w.onmessage=e=>{w.terminate();resolve(e.data);};
                w.onerror=e=>{w.terminate();reject(e.message);};
                w.postMessage('go');
            })
            """)).Should().Be("updated");
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Read)).Should().Be("worker");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task QuotaAbortDoesNotPublishTheTransaction()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxIndexedDbBytes = 2048);
        await fixture.Page.SetContentAsync("<title>quota</title>", fixture.Url("/page"));
        await Initialize(fixture.Page);
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            (async()=>{
                const db=await openDb(),t=db.transaction('items','readwrite');
                const failed=new Promise(resolve=>t.onabort=()=>resolve(t.error.name));
                t.objectStore('items').put('x'.repeat(4096),2);
                const error=await failed;
                const count=await req(db.transaction('items').objectStore('items').count());
                db.close();await req(indexedDB.deleteDatabase('shared'));
                return error+'|'+count+'|'+(await indexedDB.databases()).length;
            })()
            """)).Should().Be("QuotaExceededError|1|0");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task QuotaBudgetSpansDatabasesAndDeletionReleasesIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxIndexedDbBytes = 2048);
        await fixture.Page.SetContentAsync("<title>shared quota</title>", fixture.Url("/page"));
        await Initialize(fixture.Page);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            (async()=>{
                async function create(name){
                    const r=indexedDB.open(name);
                    r.onupgradeneeded=()=>r.result.createObjectStore('s');
                    return await req(r);
                }
                async function write(db){
                    const t=db.transaction('s','readwrite'),finished=done(t);
                    t.objectStore('s').put('x'.repeat(600),1);
                    try{await finished;return 'complete';}catch(e){return e.name;}
                }
                const a=await create('a'),b=await create('b');
                const first=await write(a),second=await write(b);
                a.close();await req(indexedDB.deleteDatabase('a'));
                const third=await write(b);b.close();
                return [first,second,third].join('|');
            })()
            """)).Should().Be("complete|QuotaExceededError|complete");
        fixture.Page.Errors.Should().BeEmpty();
    }

    private sealed class CustomPartition : StoragePartitionProvider
    {
        public override StorageProvider? GetLocalStorage(string origin) => null;
    }
}
