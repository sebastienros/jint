using Jint.Browser.Runtime;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Browser.Navigation;

/// <summary>https://cookiestore.spec.whatwg.org/ - Window operations, conversion and shared-jar observation.</summary>
public sealed class CookieStoreTests
{
    private static async Task<LoopbackPage> OpenAsync()
    {
        var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/dir/page", "<title>cookies</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/dir/page"));
        return fixture;
    }

    [Test]
    public async Task InterfacesAndWindowAttributeFollowWebIdl()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              let error;
              try { new CookieStore(); } catch (e) { error = e.name; }
              const d = Object.getOwnPropertyDescriptor(window, 'cookieStore');
              return [error, cookieStore === window.cookieStore, cookieStore instanceof CookieStore,
                cookieStore instanceof EventTarget, Object.prototype.toString.call(cookieStore),
                Object.getPrototypeOf(CookieStore) === EventTarget,
                Object.getPrototypeOf(CookieStore.prototype) === EventTarget.prototype,
                Object.getPrototypeOf(CookieChangeEvent.prototype) === Event.prototype,
                d.enumerable, d.configurable, d.set === undefined,
                CookieStore.prototype.get.length, CookieStore.prototype.getAll.length,
                CookieStore.prototype.set.length, CookieStore.prototype.delete.length].join('|');
            })()
            """)).Should().Be("TypeError|true|true|true|[object CookieStore]|true|true|true|true|true|true|0|0|1|1");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RoundTripsShareDocumentCookieAndReturnOnlyTheSpecifiedMembers()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              await cookieStore.set('first', 'one');
              const written = document.cookie;
              document.cookie = 'second=two; Path=/';
              const one = await cookieStore.get({name:'first'});
              const all = await cookieStore.getAll();
              await cookieStore.delete('first');
              await cookieStore.delete({name:'second'});
              return JSON.stringify([written, one, all, await cookieStore.get('first'), await cookieStore.getAll()]);
            })()
            """)).Should().Be("""["first=one",{"name":"first","value":"one"},[{"name":"first","value":"one"},{"name":"second","value":"two"}],null,[]]""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OptionsNormalizeAndScopeCookiesAndExpireThem()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              await cookieStore.set({name:' x\t', value:'\tvalue ', path:'/dir', sameSite:'lax'});
              await cookieStore.set({name:'x', value:'root', domain:location.hostname, sameSite:'none', partitioned:true});
              await cookieStore.set({name:'hidden', value:'v', path:'/other'});
              await cookieStore.set({name:'defaultPath', value:'v', path:''});
              const values = (await cookieStore.getAll(' x ')).map(x => x.value);
              const keys = (await cookieStore.getAll()).map(x => x.name).sort();
              await cookieStore.delete({name:'x', path:'/dir'});
              await cookieStore.set({name:'x', value:'gone', domain:location.hostname, expires:1});
              await cookieStore.set({name:'defaultPath', value:'gone', path:'', maxAge:0});
              return JSON.stringify([values, keys, await cookieStore.getAll()]);
            })()
            """)).Should().Be("""[["value","root"],["defaultPath","x","x"],[]]""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ValidPrefixesAlwaysStoreSecureCookies()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              await cookieStore.set('__Host-x', 'one');
              await cookieStore.set('__Secure-y', 'two');
              return (await cookieStore.getAll()).map(x=>x.name).sort().join(',');
            })()
            """)).Should().Be("__Host-x,__Secure-y");
        var jar = (CookieContainerCookieJar) fixture.Context.CookieJar;
        jar.Container.GetAllCookies().Cast<System.Net.Cookie>().Should().OnlyContain(c => c.Secure);
    }

    [Test]
    public async Task ByteLimitsAreInclusiveAndMaxAgeUsesWebIdlLongLongConversion()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              await cookieStore.set('a','x'.repeat(4095));
              const length = (await cookieStore.get('a')).value.length;
              await cookieStore.set({name:'a',value:'expires',maxAge:0.9});
              const deleted = await cookieStore.get('a');
              await cookieStore.set({name:'forever',value:'v',expires:Number.MAX_VALUE});
              await cookieStore.set({name:'long',value:'v',maxAge:1e9});
              await cookieStore.set({name:'past',value:'v',expires:-Number.MAX_VALUE});
              return JSON.stringify([length,deleted,(await cookieStore.getAll()).map(c=>c.name).sort()]);
            })()
            """)).Should().Be("""[4095,null,["forever","long"]]""");
    }

    [Test]
    public async Task PrefixesAreAsciiOnlyAndExpiresUsesTheNearestCookieDateSecond()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              await cookieStore.set({name:'__ho\u017ft-x',value:'v',path:'/dir'});
              await cookieStore.set({name:'dated',value:'v',expires:4102444800501});
              return 'fulfilled';
            })()
            """)).Should().Be("fulfilled");
        var jar = (CookieContainerCookieJar) fixture.Context.CookieJar;
        jar.GetScriptCookies(new Uri(fixture.Url("/dir/page"))).Single(c => c.Name == "dated")
            .Expires!.Value.ToUnixTimeMilliseconds().Should().Be(4102444801000);
    }

    [TestCase("cookieStore.get()")]
    [TestCase("cookieStore.get({})")]
    [TestCase("cookieStore.get(null)")]
    [TestCase("cookieStore.get(Symbol())")]
    [TestCase("cookieStore.get({url:'/other'})")]
    [TestCase("cookieStore.getAll({url:'https://elsewhere.invalid/'})")]
    [TestCase("cookieStore.getAll({url:'http://['})")]
    [TestCase("cookieStore.set()")]
    [TestCase("cookieStore.set('onlyName')")]
    [TestCase("cookieStore.set({name:'a'})")]
    [TestCase("cookieStore.set(null)")]
    [TestCase("cookieStore.set({name:'a',value:Symbol()})")]
    [TestCase("cookieStore.set('a=b','c')")]
    [TestCase("cookieStore.set('a;b','c')")]
    [TestCase("cookieStore.set('a','b;c')")]
    [TestCase("cookieStore.set('a','b\\nc')")]
    [TestCase("cookieStore.set('a','b\\x7fc')")]
    [TestCase("cookieStore.set('','')")]
    [TestCase("cookieStore.set('','a=b')")]
    [TestCase("cookieStore.set('','__sEcUrE-x')")]
    [TestCase("cookieStore.set('__hTtP-x','v')")]
    [TestCase("cookieStore.set('__Host-Http-x','v')")]
    [TestCase("cookieStore.set({name:'__hOsT-x',value:'v',path:'/dir'})")]
    [TestCase("cookieStore.set({name:'__Host-x',value:'v',domain:location.hostname})")]
    [TestCase("cookieStore.set({name:'a',value:'v',domain:'.' + location.hostname})")]
    [TestCase("cookieStore.set({name:'a',value:'v',domain:'invalid.example'})")]
    [TestCase("cookieStore.set({name:'a',value:'v',path:'relative'})")]
    [TestCase("cookieStore.set({name:'a',value:'v',path:'/'+'x'.repeat(1024)})")]
    [TestCase("cookieStore.set({name:'a',value:'v',sameSite:'Strict'})")]
    [TestCase("cookieStore.set({name:'a',value:'v',expires:Infinity})")]
    [TestCase("cookieStore.set({name:'a',value:'v',expires:NaN})")]
    [TestCase("cookieStore.set({name:'a',value:'v',expires:1n})")]
    [TestCase("cookieStore.set({name:'a',value:'v',maxAge:Symbol()})")]
    [TestCase("cookieStore.set({name:'a',value:'v',maxAge:1n})")]
    [TestCase("cookieStore.set({name:'a',value:'v',expires:1,maxAge:0})")]
    [TestCase("cookieStore.set('a','x'.repeat(4096))")]
    [TestCase("cookieStore.set('a','\\u00e9'.repeat(2048))")]
    [TestCase("cookieStore.delete()")]
    [TestCase("cookieStore.delete({})")]
    [TestCase("cookieStore.delete(Symbol())")]
    [TestCase("CookieStore.prototype.get.call(undefined, 'a')")]
    [TestCase("CookieStore.prototype.getAll.call({}, 'a')")]
    public async Task ConversionAndValidationErrorsRejectRatherThanThrow(string expression)
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            $$"""
            (() => {
              try {
                const p = {{expression}};
                return p.then(() => 'fulfilled', e => (p instanceof Promise) + ':' + e.name);
              } catch(e) { return 'threw:' + e.name; }
            })()
            """)).Should().Be("true:TypeError");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OverloadsAndDictionaryReadsFollowWebIdl()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const seen = [];
              const options = new Proxy({name:'x',value:'one'}, {get(t,k) {seen.push(k); return t[k];}});
              await cookieStore.set(options);
              await cookieStore.set(123, false);
              const numeric = await cookieStore.get(123);
              const empty = await cookieStore.getAll(null);
              const boxed = await cookieStore.getAll(new String('x'));
              const sentinel = {};
              const sameError = await cookieStore.getAll({get name(){throw sentinel}}).catch(e=>e===sentinel);
              await cookieStore.set({toString(){return 'objectName'}}, 'value');
              return JSON.stringify([seen,numeric,empty.length,boxed.length,sameError,(await cookieStore.get('objectName')).value]);
            })()
            """)).Should().Be("""[["domain","expires","maxAge","name","partitioned","path","sameSite","value"],{"name":"123","value":"false"},2,2,true,"value"]""");
    }

    [Test]
    public async Task QueriesUseCreationUrlEvenAfterHistoryChangesAndHonorTheApiBaseUrl()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              document.cookie = 'scoped=original; Path=/dir';
              const initial = location.href;
              history.replaceState({}, '', '/elsewhere');
              document.head.innerHTML = '<base href="/dir/">';
              const cookie = await cookieStore.get({url:'page#ignored'});
              const changedUrl = await cookieStore.getAll({url:location.href}).catch(e=>e.name);
              return JSON.stringify([cookie, (await cookieStore.getAll({url:initial})).length, changedUrl]);
            })()
            """)).Should().Be("""[{"name":"scoped","value":"original"},1,"TypeError"]""");
    }

    [Test]
    public async Task ResponseCookiesAreVisibleExceptHttpOnlyWhichNeitherScriptApiCanOverwrite()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/dir/page", _ => LoopbackResponse.Html("<title>x</title>")
                .With("Set-Cookie", "secret=server; Path=/; HttpOnly")
                .With("Set-Cookie", "hidden=server; Path=/other; HttpOnly")
                .With("Set-Cookie", "nav=one; Path=/"))
            .Map("/fetch", _ => LoopbackResponse.Html("done").With("Set-Cookie", "fetched=two; Path=/"))
            .MapHtml("/other/check", "check"));
        await fixture.Page.NavigateAsync(fixture.Url("/dir/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              await cookieStore.set('secret', 'stolen');
              await cookieStore.delete('secret');
              await cookieStore.set({name:'hidden',value:'stolen',path:'/other'});
              document.cookie = 'hidden=stolen; Path=/other';
              await fetch('/fetch').then(r=>r.text());
              return JSON.stringify([await cookieStore.get('secret'), await cookieStore.getAll()]);
            })()
            """)).Should().Be("""[null,[{"name":"nav","value":"one"},{"name":"fetched","value":"two"}]]""");
        await fixture.Page.NavigateAsync(fixture.Url("/other/check"));
        var header = fixture.Server.Received.Single(r => r.Path == "/other/check").Header("Cookie");
        header.Should().Contain("secret=server").And.Contain("hidden=server").And.NotContain("stolen");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SetAndDocumentWritesCoalesceAndDeleteOmitsTheValue()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const events = [];
              const first = new Promise(resolve => cookieStore.onchange = e => {
                events.push([e instanceof CookieChangeEvent,e.isTrusted,e.bubbles,e.cancelable,
                  e.target===cookieStore,e.changed===e.changed,Object.isFrozen(e.changed),e.changed,e.deleted]);
                resolve();
              });
              const writes = [cookieStore.set('a','1'), cookieStore.set('a','2')];
              document.cookie = 'b=3; Path=/';
              await Promise.all([...writes, first]);
              await new Promise(resolve => {
                cookieStore.onchange = e => { events.push([e.changed,e.deleted,Object.keys(e.deleted[0])]); resolve(); };
                cookieStore.delete('a');
              });
              cookieStore.onchange = null;
              return JSON.stringify(events);
            })()
            """)).Should().Be("""[[true,true,false,false,true,true,true,[{"name":"a","value":"2"},{"name":"b","value":"3"}],[]],[[],[{"name":"a"}],["name"]]]""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFetchSetCookieFiresChangeButHttpOnlyDoesNotLeak()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", "x")
            .Map("/fetch", _ => LoopbackResponse.Html("done")
                .With("Set-Cookie", "remote=yes; Path=/")
                .With("Set-Cookie", "secret=hidden; Path=/; HttpOnly")));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const event = new Promise(resolve => cookieStore.addEventListener('change', resolve, {once:true}));
              const [e] = await Promise.all([event, fetch('/fetch').then(r=>r.text())]);
              return JSON.stringify([e.changed,e.deleted]);
            })()
            """)).Should().Be("""[[{"name":"remote","value":"yes"}],[]]""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task HostWritesAndClearsAreObservedWithoutAnyScriptTask()
    {
        await using var fixture = await OpenAsync();
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.Page.RunOnLoopAsync(engine =>
        {
            engine.SetValue("notifyHost", (Action) (() => notified.TrySetResult()));
            return 0;
        });
        await fixture.Page.EvaluateAsync("cookieStore.onchange = () => notifyHost()");
        await fixture.Page.EvaluateAsync("globalThis.nextChange = new Promise(r => cookieStore.addEventListener('change',r,{once:true}))");
        fixture.Context.CookieJar.StoreResponseCookies(new Uri(fixture.Url("/")), ["host=one; Path=/"]);
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(10));
        (await fixture.Page.EvaluateAndAwaitAsync<string>("nextChange.then(e=>JSON.stringify(e.changed))"))
            .Should().Be("""[{"name":"host","value":"one"}]""");
        await fixture.Page.EvaluateAsync("globalThis.nextChange = new Promise(r => cookieStore.addEventListener('change',r,{once:true}))");
        var jar = (CookieContainerCookieJar) fixture.Context.CookieJar;
        // CDP's clearBrowserCookies expires cookies in this same container.
        foreach (System.Net.Cookie cookie in jar.Container.GetAllCookies()) cookie.Expired = true;
        (await fixture.Page.EvaluateAndAwaitAsync<string>("nextChange.then(e=>JSON.stringify(e.deleted))"))
            .Should().Be("""[{"name":"host"}]""");
    }

    [Test]
    public async Task WindowsHaveIndependentSingletonsAndEventRealmsOverTheSharedJar()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/parent", "<iframe src='/child'></iframe>")
            .MapHtml("/child", "<title>child</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/parent"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const child = document.querySelector('iframe').contentWindow;
              const store = child.cookieStore;
              const event = new Promise(r=>store.addEventListener('change',r,{once:true}));
              await cookieStore.set('shared','value');
              const e = await event;
              return [store===child.cookieStore,store!==cookieStore,store instanceof child.CookieStore,
                !(store instanceof CookieStore),e instanceof child.CookieChangeEvent,
                (await store.get('shared')).value].join(',');
            })()
            """)).Should().Be("true,true,true,true,true,value");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CookieChangeEventConvertsDictionariesCopiesAndFreezesOnlyTheArrays()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              const item = {name:123, value:false, path:'/ignored'};
              const e = new CookieChangeEvent('x', {changed:new Set([item, null, {name:'deleted'}]),
                deleted:[{value:'v'}], bubbles:true});
              item.name = 'mutated';
              e.changed[0].value = 'mutable';
              const empty = new CookieChangeEvent('x');
              return JSON.stringify([e.type,e.bubbles,e.isTrusted,e.changed===e.changed,e.deleted===e.deleted,
                Object.isFrozen(e.changed),Object.isFrozen(e.deleted),Object.isFrozen(e.changed[0]),
                e.changed,e.deleted,empty.changed,empty.deleted]);
            })()
            """)).Should().Be("""["x",true,false,true,true,true,true,false,[{"name":"123","value":"mutable"},{},{"name":"deleted"}],[{"value":"v"}],[],[]]""");
    }

    [TestCase("null")]
    [TestCase("'abc'")]
    [TestCase("{}")]
    [TestCase("[42]")]
    [TestCase("[{name:Symbol()}]")]
    public async Task InvalidEventSequencesThrowTypeError(string sequence)
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAsync<string>(
            $$$"""(() => {try {new CookieChangeEvent('x',{changed:{{{sequence}}}}); return 'accepted';}catch(e){return e.name}})()"""))
            .Should().Be("TypeError");
    }

    [TestCase("({get name(){throw sentinel}})", "e===sentinel")]
    [TestCase("({name:Symbol()})", "e instanceof TypeError")]
    public async Task EventDictionaryConversionClosesTheIteratorOnFailure(string item, string check)
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAsync<string>(
            $$$"""
            (() => {
              let closed = false;
              const sentinel = {};
              function* items() {try {yield {{{item}}};}finally{closed=true;}}
              try {new CookieChangeEvent('x',{changed:items()});}catch(e){return [closed,{{{check}}}].join(',');}
            })()
            """)).Should().Be("true,true");
    }

    [TestCase("about:blank")]
    [TestCase("data:text/html,<title>opaque</title>")]
    public async Task CookieAverseDocumentsRejectWithSecurityError(string url)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.NavigateAsync(url);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            Promise.all([
              cookieStore.get('a'), cookieStore.getAll(), cookieStore.set('a','b'), cookieStore.delete('a')
            ].map(p=>p.catch(e=>e.name))).then(x=>x.join(','))
            """)).Should().Be("SecurityError,SecurityError,SecurityError,SecurityError");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("cookieStore.set('a','b')")]
    [TestCase("cookieStore.get('a')")]
    [TestCase("cookieStore.getAll()")]
    [TestCase("cookieStore.delete('a')")]
    public async Task ResolutionWaitsForATaskRatherThanTheCurrentMicrotaskCheckpoint(string expression)
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            $$"""
            (async () => {
              const order = [];
              const p = {{expression}}.then(()=>order.push('cookie'));
              order.push('sync');
              await Promise.resolve();
              order.push('microtask');
              await p;
              return order.join(',');
            })()
            """)).Should().Be("sync,microtask,cookie");
    }

    [Test]
    public async Task ParallelValidationRejectsInATaskButWebIdlConversionRejectsImmediately()
    {
        await using var fixture = await OpenAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const order = [];
              const validation = cookieStore.set('a=b','c').catch(()=>order.push('validation'));
              const conversion = cookieStore.set({name:'a'}).catch(()=>order.push('conversion'));
              await Promise.resolve();
              order.push('microtask');
              await Promise.all([validation,conversion]);
              return order.join(',');
            })()
            """)).Should().Be("conversion,microtask,validation");
    }

    [Test]
    public async Task NoSnapshotsWithoutChangeListenersAndAbortStopsObservationImmediately()
    {
        var jar = new CountingJar();
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "x"), options => options.CookieJar = jar);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        var reads = jar.Reads;
        await ProcessAsync();
        jar.Reads.Should().Be(reads);
        await fixture.Page.EvaluateAsync("void cookieStore; cookieStore.addEventListener('unrelated',()=>{});");
        await ProcessAsync();
        jar.Reads.Should().Be(reads);
        await fixture.Page.EvaluateAsync(
            "globalThis.controller = new AbortController(); cookieStore.addEventListener('change',()=>{}, {signal:controller.signal});");
        jar.Reads.Should().BeGreaterThan(reads);
        await fixture.Page.EvaluateAsync("controller.abort()");
        reads = jar.Reads;
        await ProcessAsync();
        jar.Reads.Should().Be(reads);

        await fixture.Page.EvaluateAsync("cookieStore.onchange = ()=>{}");
        jar.Reads.Should().BeGreaterThan(reads);
        await fixture.Page.EvaluateAsync("cookieStore.onchange = null");
        reads = jar.Reads;
        await ProcessAsync();
        jar.Reads.Should().Be(reads);

        await fixture.Page.EvaluateAsync(
            "cookieStore.addEventListener('change',()=>{},{once:true}); cookieStore.dispatchEvent(new CookieChangeEvent('change'));");
        reads = jar.Reads;
        await ProcessAsync();
        jar.Reads.Should().Be(reads);

        async Task ProcessAsync() => await fixture.Page.RunOnLoopAsync(engine =>
        {
            for (var i = 0; i < 10; i++) PageRuntime.Find(engine)!.UpdateRendering();
            return 0;
        });
    }

    private sealed class CountingJar : CookieJar
    {
        private int _reads;
        internal int Reads => Volatile.Read(ref _reads);
        public override string? GetCookieHeader(Uri url)
        {
            Interlocked.Increment(ref _reads);
            return null;
        }
        public override void StoreResponseCookies(Uri url, IReadOnlyList<string> setCookieHeaders)
        {
        }
    }
}
