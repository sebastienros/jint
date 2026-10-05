#if NET8_0_OR_GREATER
#nullable enable
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;
using Jint.WebApi.IndexedDb;

namespace Jint.Tests.Runtime.WebApi;

public sealed class IndexedDbTests
{
    private const string Helpers = """
        function request(r) {
            return new Promise((resolve, reject) => {
                r.onsuccess = () => resolve(r.result);
                r.onerror = () => reject(r.error);
            });
        }
        function completed(t) {
            return new Promise((resolve, reject) => {
                t.oncomplete = () => resolve();
                t.onabort = () => reject(t.error);
            });
        }
        function open(name = 'db', version = 1, upgrade = db => db.createObjectStore('s')) {
            const r = indexedDB.open(name, version);
            r.onupgradeneeded = () => upgrade(r.result, r.transaction);
            return request(r);
        }
        function errorName(f) { try { f(); return 'no error'; } catch(e) { return e.name; } }
        """;

    private static Engine Create(IndexedDbStore? store = null)
    {
        var engine = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb | WebApiFeatures.Timers | WebApiFeatures.Files));
        if (store is not null) engine._webApi!.IndexedDb.Configure(store, opaqueOrigin: false);
        engine.Execute(Helpers);
        return engine;
    }

    private static JsValue Run(Engine engine, string script)
        => engine.Evaluate("(async () => {" + script + "})()").UnwrapIfPromise();

    [Test]
    public void OptInHasItsDependenciesButIsNotDefault()
    {
        using var defaults = new Engine(o => o.UseWebApis());
        defaults.Evaluate("typeof indexedDB").Should().Be("undefined");
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb));
        engine.Evaluate("""
            indexedDB instanceof IDBFactory && typeof Event === 'function'
                && typeof DOMException === 'function' && typeof structuredClone === 'function'
                && typeof fetch === 'undefined' && Reflect.ownKeys(indexedDB).length === 0
            """).Should().Be(true);
    }

    [TestCase("-Infinity", "0", -1)]
    [TestCase("-0", "0", 0)]
    [TestCase("Infinity", "new Date(-8640000000000000)", -1)]
    [TestCase("new Date(0)", "''", -1)]
    [TestCase("'z'", "new Uint8Array([])", -1)]
    [TestCase("new Uint8Array([1,255])", "new Uint8Array([2])", -1)]
    [TestCase("new Uint8Array([1]).buffer", "[]", -1)]
    [TestCase("[1,'a']", "[1,'b']", -1)]
    [TestCase("[1]", "[1,0]", -1)]
    [TestCase("'\\uD800'", "'\\uE000'", -1)]
    public void KeyOrdering(string left, string right, int expected)
    {
        using var engine = Create();
        engine.Evaluate($"indexedDB.cmp({left}, {right})").Should().Be(expected);
    }

    [TestCase("undefined")]
    [TestCase("null")]
    [TestCase("NaN")]
    [TestCase("true")]
    [TestCase("1n")]
    [TestCase("({})")]
    [TestCase("new Date(NaN)")]
    [TestCase("[,]")]
    [TestCase("(()=>{const a=[];a[0]=a;return a})()")]
    [TestCase("new Number(1)")]
    [TestCase("new Array(2147483647)")]
    public void InvalidKeysAreDataErrors(string key)
    {
        using var engine = Create();
        engine.Evaluate($"errorName(() => indexedDB.cmp({key}, 1))").Should().Be("DataError");
    }

    [Test]
    public void KeyRangesCopyKeysAndEnforceBounds()
    {
        using var engine = Create();
        engine.Evaluate("""
            (() => {
                const a=[1], r=IDBKeyRange.bound(a,[3],true,true); a[0]=8;
                const key=r.lower; key[0]=9;
                return [r.includes([1]),r.includes([2]),r.includes([3]),r.lower===key,
                    IDBKeyRange.lowerBound(1).upperOpen, IDBKeyRange.upperBound(2).lowerOpen,
                    errorName(()=>IDBKeyRange.bound(1,1,true)),
                    errorName(()=>IDBKeyRange.only(NaN))].join('|');
            })()
            """).Should().Be("false|true|false|true|true|true|DataError|DataError");
    }

    [Test]
    public void SchemaRequestsAndStructuredValuesRoundTrip()
    {
        using var engine = Create();
        Run(engine, """
            let input={nested:{}, bytes:new Uint8Array([4,5]), map:new Map([[1,'one']])};
            input.self=input;
            const db=await open('db',1,db=>{
                const s=db.createObjectStore('s',{keyPath:'nested.id',autoIncrement:true});
                s.createIndex('byName','name');
            });
            const t=db.transaction('s','readwrite',{durability:'relaxed'}), done=completed(t), s=t.objectStore('s');
            const r=s.put(input); input.bytes[0]=99;
            const pending=errorName(()=>r.result);
            const key=await request(r);
            const copy=await request(s.get(key));
            copy.bytes[0]=7;
            const fresh=await request(s.get(key));
            await done;
            return [key,input.nested.id,copy.self===copy,fresh.bytes[0],fresh.map.get(1),
                pending,r.readyState,r.source===s,r.transaction===t,r.error,
                db.objectStoreNames instanceof DOMStringList,s.indexNames.item(0),t.mode,t.durability].join('|');
            """).Should().Be("1||true|4|one|InvalidStateError|done|true|true||true|byName|readwrite|relaxed");
    }

    [Test]
    public void IndexQueriesAndRangeDeletion()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>{
                const s=db.createObjectStore('s',{keyPath:'id'});
                s.createIndex('tags','tags',{multiEntry:true});
                s.createIndex('compound',['group','id']);
            });
            let t=db.transaction('s','readwrite'), done=completed(t), s=t.objectStore('s');
            s.put({id:2,group:'a',tags:['red','red',null,'blue']});
            s.put({id:1,group:'a',tags:['red']});
            s.put({id:3,group:'b',tags:['blue']});
            await done;
            t=db.transaction('s','readwrite'); done=completed(t); s=t.objectStore('s');
            const index=s.index('tags');
            const keys=await request(index.getAllKeys('red'));
            const all=await request(index.getAll('blue',1));
            const compound=await request(s.index('compound').getKey(['a',2]));
            const n=await request(index.count());
            await request(s.delete(IDBKeyRange.bound(2,3)));
            const left=await request(s.getAllKeys());
            await done;
            return [keys.join(),all[0].id,compound,n,left.join()].join('|');
            """).Should().Be("1,2|2|2|4|1");
    }

    [Test]
    public void ErrorBubblesAndPreventDefaultAllowsTransactionToContinue()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>{
                const s=db.createObjectStore('s'); s.createIndex('u','v',{unique:true});
            });
            const t=db.transaction('s','readwrite'), done=completed(t), s=t.objectStore('s'), events=[];
            db.addEventListener('error',e=>events.push('db:'+e.target.error.name));
            t.onerror=e=>events.push('tx:'+e.currentTarget.mode);
            s.add({v:'same'},1);
            const duplicate=s.add({v:'same'},2);
            duplicate.onerror=e=>{events.push('request');e.preventDefault();};
            const value=await request(s.get(1));
            await done;
            return events.join('|')+'|'+value.v;
            """).Should().Be("request|tx:readwrite|db:ConstraintError|same");
    }

    [Test]
    public void AbortRollsBackRecordsAndGeneratorAndErrorsPendingRequests()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s',{autoIncrement:true}));
            const t=db.transaction('s','readwrite'), s=t.objectStore('s'), errors=[];
            const aborted=new Promise(resolve=>t.onabort=resolve);
            s.add('first').onsuccess=()=>{
                const pending=s.add('second'); pending.onerror=e=>errors.push(e.target.error.name);
                t.abort();
            };
            await aborted;
            const next=db.transaction('s','readwrite'), done=completed(next), store=next.objectStore('s');
            const count=await request(store.count()), key=await request(store.add('after'));
            await done;
            return [count,key,errors.join(),t.error===null].join('|');
            """).Should().Be("0|1|AbortError|true");
    }

    [Test]
    public void TransactionActivityIncludesMicrotasksButNotTheNextTask()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open();
            const t=db.transaction('s','readwrite'), done=completed(t), s=t.objectStore('s');
            await Promise.resolve();
            const a=await request(s.put('a',1));
            const b=await request(s.put('b',2));
            await new Promise(resolve=>setTimeout(resolve,0));
            const error=errorName(()=>s.put('c',3));
            await done;
            return [a,b,error].join('|');
            """).Should().Be("1|2|TransactionInactiveError");
    }

    [Test]
    public void ExplicitCommitRefusesNewRequestsAndDoesNotAllowErrorCancellation()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open();
            const t=db.transaction('s','readwrite'), s=t.objectStore('s');
            const aborted=new Promise(resolve=>t.onabort=()=>resolve(t.error.name));
            s.add('a',1); const duplicate=s.add('b',1); duplicate.onerror=e=>e.preventDefault();
            t.commit();
            const inactive=errorName(()=>s.get(1));
            return inactive+'|'+await aborted;
            """).Should().Be("TransactionInactiveError|ConstraintError");
    }

    [Test]
    public void CursorsAdvanceContinueAndModifyLiveRecords()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open();
            let t=db.transaction('s','readwrite'), done=completed(t), s=t.objectStore('s');
            for(let i=1;i<=5;i++)s.put({n:i},i); await done;
            t=db.transaction('s','readwrite');done=completed(t);s=t.objectStore('s');
            const seen=[], r=s.openCursor();
            await new Promise((resolve,reject)=>{
                r.onerror=()=>reject(r.error);
                r.onsuccess=()=>{
                    const c=r.result;if(!c){resolve();return;}
                    seen.push(c.key+':'+c.value.n+':'+(c.request===r));
                    if(c.key===1){c.update({n:10});c.advance(2);}
                    else if(c.key===3){c.delete();c.continue(5);}
                    else c.continue();
                };
            });await done;
            const q=db.transaction('s').objectStore('s');
            return seen.join(',')+'|'+(await request(q.getAll())).map(x=>x.n).join();
            """).Should().Be("1:1:true,3:3:true,5:5:true|10,2,4,5");
    }

    [TestCase("next", "a:1,a:2,b:3")]
    [TestCase("nextunique", "a:1,b:3")]
    [TestCase("prev", "b:3,a:2,a:1")]
    [TestCase("prevunique", "b:3,a:1")]
    public void IndexCursorDirections(string direction, string expected)
    {
        using var engine = Create();
        engine.SetValue("direction", direction);
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s').createIndex('i','v'));
            const t=db.transaction('s','readwrite'),done=completed(t),s=t.objectStore('s');
            s.put({v:'a'},1);s.put({v:'a'},2);s.put({v:'b'},3);await done;
            const r=db.transaction('s').objectStore('s').index('i').openKeyCursor(null,direction),seen=[];
            await new Promise(resolve=>r.onsuccess=()=>{const c=r.result;if(!c){resolve();return;}seen.push(c.key+':'+c.primaryKey);c.continue();});
            return seen.join();
            """).Should().Be(expected);
    }

    [Test]
    public void UpgradeRenameRollbackAndDeleteDatabase()
    {
        using var engine = Create();
        Run(engine, """
            let db=await open();db.close();
            let r=indexedDB.open('db',2);
            r.onupgradeneeded=()=>{
                const s=r.transaction.objectStore('s');s.name='renamed';s.createIndex('i','v').name='renamedIndex';
                r.transaction.abort();
            };
            await new Promise(resolve=>r.onerror=resolve);
            db=await open('db',1);const names=[...db.objectStoreNames].join();db.close();
            const databases=await indexedDB.databases();
            const deletion=indexedDB.deleteDatabase('db');
            const old=await new Promise(resolve=>deletion.onsuccess=e=>resolve(e.oldVersion));
            return [names,databases[0].version,old,(await indexedDB.databases()).length].join('|');
            """).Should().Be("s|1|1|0");
    }

    [Test]
    public void CloneFailuresAndKeyPathErrorsAreSynchronous()
    {
        using var engine = Create();
        Run(engine, """
            const errors=[];
            const db=await open('db',1,db=>{
                errors.push(errorName(()=>db.createObjectStore('bad',{keyPath:'a..b'})));
                errors.push(errorName(()=>db.createObjectStore('bad',{keyPath:[],autoIncrement:true})));
                db.createObjectStore('s',{keyPath:'a.b',autoIncrement:true});
            });
            const t=db.transaction('s','readwrite'), done=completed(t), s=t.objectStore('s');
            errors.push(errorName(()=>s.put(()=>1)));
            errors.push(errorName(()=>s.put(1)));
            errors.push(errorName(()=>s.put({},1)));
            errors.push(errorName(()=>s.put({a:{b:NaN}})));
            const key=await request(s.put({}));
            await done;
            return errors.join('|')+'|'+key;
            """).Should().Be("SyntaxError|SyntaxError|DataCloneError|DataError|DataError|DataError|1");
    }

    [Test]
    public void QuotaAbortDoesNotPublishPartialData()
    {
        using var engine = Create(new IndexedDbStore(2048));
        Run(engine, """
            const db=await open();
            const t=db.transaction('s','readwrite'),done=new Promise(resolve=>t.onabort=()=>resolve(t.error.name));
            t.objectStore('s').put('x'.repeat(4096),1);
            const error=await done;
            return error+'|'+await request(db.transaction('s').objectStore('s').count());
            """).Should().Be("QuotaExceededError|0");
    }

    [Test]
    public void SharedStoreSerializesEnginesAndPostsVersionChanges()
    {
        var store = new IndexedDbStore();
        using var first = Create(store);
        using var second = Create(store);
        first.Execute("var db; open().then(x=>db=x);");
        second.Execute("var db; open().then(x=>db=x);");
        using (first.EventLoop.DeferTaskDrain())
        {
            first.Execute("var t=db.transaction('s','readwrite'); t.objectStore('s').put('first',1);");
        }
        using (second.EventLoop.DeferTaskDrain())
        {
            second.Execute("var observed;var t=db.transaction('s','readwrite');request(t.objectStore('s').get(1)).then(x=>{observed=x;t.objectStore('s').put('second',1)});");
        }
        second.Tasks.ProcessTasks();
        second.GetValue("observed").IsUndefined().Should().BeTrue();
        first.Tasks.ProcessTasks();
        second.Tasks.ProcessTasks();
        second.GetValue("observed").Should().Be("first");
        Run(first, "return await request(db.transaction('s').objectStore('s').get(1));").Should().Be("second");

        second.Execute("db.close();var blocked=false,upgraded=false;var r=indexedDB.open('db',2);r.onblocked=()=>blocked=true;r.onsuccess=()=>{upgraded=true;r.result.close()};");
        first.Execute("var change='';db.onversionchange=e=>change=e.oldVersion+':'+e.newVersion;");
        first.Tasks.ProcessTasks();
        second.Tasks.ProcessTasks();
        first.GetValue("change").Should().Be("1:2");
        second.GetValue("blocked").Should().Be(true);
        second.GetValue("upgraded").Should().Be(false);
        first.Execute("db.close()");
        second.Tasks.ProcessTasks();
        second.GetValue("upgraded").Should().Be(true);
    }

    [Test]
    public void DisposalReleasesAnUnpumpedWriter()
    {
        var store = new IndexedDbStore();
        using var first = Create(store);
        using var second = Create(store);
        first.Execute("var db;open().then(x=>db=x)");
        second.Execute("var db;open().then(x=>db=x)");
        using (first.EventLoop.DeferTaskDrain())
        {
            first.Execute("db.transaction('s','readwrite').objectStore('s').put('abandoned',1)");
        }
        second.Execute("var done=false;var t=db.transaction('s','readwrite');t.objectStore('s').put('kept',1);t.oncomplete=()=>done=true");
        second.GetValue("done").Should().Be(false);
        first.Dispose();
        second.Tasks.ProcessTasks();
        second.GetValue("done").Should().Be(true);
    }

    [Test]
    public void DeepKeysDoNotUseTheNativeStack()
    {
        using var engine = Create();
        engine.Evaluate("""
            (()=>{let a=1,b=1;for(let i=0;i<20000;i++){a=[a];b=[b];}
            const r=IDBKeyRange.only(a);return indexedDB.cmp(r.lower,b)===0;})()
            """).Should().Be(true);
    }

    [TestCase(false, "AbortError")]
    [TestCase(true, "complete")]
    public void ListenerExceptionsAbortUnlessAlreadyCommitting(bool commit, string expected)
    {
        var reports = new List<DiagnosticEvent>();
        using var engine = new Engine(o =>
        {
            o.UseWebApis(WebApiFeatures.IndexedDb);
            o.WebApi.Diagnostics.Sink = new RecordingSink(reports);
        });
        engine.Execute(Helpers);
        engine.SetValue("commitExplicitly", commit);
        Run(engine, """
            const db=await open(),t=db.transaction('s','readwrite'),s=t.objectStore('s');
            const outcome=new Promise(resolve=>{t.oncomplete=()=>resolve('complete');t.onabort=()=>resolve(t.error.name);});
            s.put('x',1).onsuccess=()=>{if(commitExplicitly)t.commit();throw new Error('listener failed');};
            return await outcome;
            """).Should().Be(expected);
        reports.Should().ContainSingle();
    }

    [Test]
    public void StoredBuffersAreCopiedForEveryEngineAndRead()
    {
        var store = new IndexedDbStore();
        using var first = Create(store);
        using var second = Create(store);
        Run(first, """
            const db=await open(),t=db.transaction('s','readwrite'),d=completed(t);
            t.objectStore('s').put(new Uint8Array([1,2]),1);await d;db.close();
            """);
        Run(second, """
            const db=await open(),t=db.transaction('s'),s=t.objectStore('s');
            const a=await request(s.get(1));a[0]=99;
            const b=await request(s.get(1));db.close();return b[0];
            """).Should().Be(1);
        Run(first, """
            const db=await open(),value=await request(db.transaction('s').objectStore('s').get(1));
            db.close();return value[0];
            """).Should().Be(1);
    }

    [Test]
    public void DisjointWritersCommitWithoutOverwritingEachOther()
    {
        var store = new IndexedDbStore();
        using var first = Create(store);
        using var second = Create(store);
        first.Execute("var db;open('db',1,db=>{db.createObjectStore('a');db.createObjectStore('b')}).then(x=>db=x)");
        second.Execute("var db;open().then(x=>db=x)");
        using (first.EventLoop.DeferTaskDrain()) first.Execute("db.transaction('a','readwrite').objectStore('a').put('A',1)");
        using (second.EventLoop.DeferTaskDrain()) second.Execute("db.transaction('b','readwrite').objectStore('b').put('B',1)");
        second.Tasks.ProcessTasks();
        first.Tasks.ProcessTasks();
        Run(first, """
            const t=db.transaction(['a','b']);
            const a=request(t.objectStore('a').get(1)),b=request(t.objectStore('b').get(1));
            return (await a)+(await b);
            """).Should().Be("AB");
    }

    [Test]
    public void GeneratorExhaustionDoesNotReuseTheLastKey()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s',{autoIncrement:true}));
            const t=db.transaction('s','readwrite'),d=completed(t),s=t.objectStore('s');
            await request(s.put('explicit',9007199254740991));
            const last=await request(s.put('last'));
            const failed=s.put('exhausted');
            const error=await new Promise(resolve=>failed.onerror=e=>{e.preventDefault();resolve(failed.error.name);});
            await d;return last+'|'+error;
            """).Should().Be("9007199254740992|ConstraintError");
    }

    [TestCase(false, "request")]
    [TestCase(true, "request")]
    [TestCase(true, "transaction")]
    [TestCase(true, "database")]
    public void SinklessRequestListenerFailureAbortsOnlyItsTransaction(bool requestFails, string listenerTarget)
    {
        using var engine = Create();
        engine.Execute("var db,other;open().then(x=>db=x);open('other').then(x=>other=x);");
        engine.SetValue("requestFails", requestFails);
        engine.SetValue("listenerTarget", listenerTarget);
        using (engine.EventLoop.DeferTaskDrain())
        {
            engine.Execute("""
                var t=db.transaction('s','readwrite'),s=t.objectStore('s'),aborted='',pendingError='',otherDone=false,opened=false;
                t.onabort=()=>aborted=t.error.name;
                if(requestFails)s.add('first',1);
                var r=requestFails?s.add('duplicate',1):s.put('first',1);
                var target=listenerTarget==='request'?r:listenerTarget==='transaction'?t:db;
                target.addEventListener(requestFails?'error':'success',e=>{
                    e.preventDefault();throw new Error('listener failed');
                },{once:true});
                var pending=s.put('pending',2);pending.onerror=()=>pendingError=pending.error.name;
                var unrelated=other.transaction('s','readwrite');
                unrelated.objectStore('s').put('kept',1);unrelated.oncomplete=()=>otherDone=true;
                var opening=indexedDB.open('queued');
                opening.onupgradeneeded=()=>opening.result.createObjectStore('s');
                opening.onsuccess=()=>{opened=true;opening.result.close();};
                """);
        }
        Invoking(() => engine.Tasks.ProcessTasks()).Should().Throw<JavaScriptException>().WithMessage("*listener failed*");
        engine.Evaluate("t.error.name").Should().Be("AbortError");
        engine.Tasks.ProcessTasks();
        engine.Evaluate("[aborted,pendingError,otherDone,opened].join('|')").Should().Be("AbortError|AbortError|true|true");
        Run(engine, """
            const a=request(db.transaction('s').objectStore('s').count());
            const b=request(other.transaction('s').objectStore('s').get(1));
            return (await a)+'|'+await b;
            """).Should().Be("0|kept");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SinklessListenerFailureDoesNotAbortACommittingOrAbortedTransaction(bool abort)
    {
        using var engine = Create();
        engine.Execute("var db;open().then(x=>db=x);");
        engine.SetValue("abortExplicitly", abort);
        using (engine.EventLoop.DeferTaskDrain())
        {
            engine.Execute("""
                var t=db.transaction('s','readwrite'),outcome='';
                t.oncomplete=()=>outcome='complete';t.onabort=()=>outcome='abort';
                t.objectStore('s').put('x',1).onsuccess=()=>{
                    if(abortExplicitly)t.abort();else t.commit();
                    throw new Error('listener failed');
                };
                """);
        }
        Invoking(() => engine.Tasks.ProcessTasks()).Should().Throw<JavaScriptException>().WithMessage("*listener failed*");
        engine.Tasks.ProcessTasks();
        engine.GetValue("outcome").Should().Be(abort ? "abort" : "complete");
        Run(engine, "return await request(db.transaction('s').objectStore('s').count());").Should().Be(abort ? 0 : 1);
    }

    [Test]
    public void SinklessUpgradeListenerFailureSettlesOpenAndAllowsTheNextOpen()
    {
        using var engine = Create();
        engine.Execute("var other;open('other').then(x=>other=x);");
        using (engine.EventLoop.DeferTaskDrain())
        {
            engine.Execute("""
                var failed=indexedDB.open('upgrade',2),openError='',pendingError='',reopened=false,version=0;
                failed.onupgradeneeded=()=>{
                    const s=failed.result.createObjectStore('rolledback');
                    s.put('pending',1).onerror=e=>pendingError=e.target.error.name;
                    failed.transaction.onabort=()=>{throw new Error('abort listener failed');};
                    throw new Error('upgrade listener failed');
                };
                failed.onerror=()=>openError=failed.error.name;
                var next=indexedDB.open('upgrade',1);
                next.onupgradeneeded=()=>next.result.createObjectStore('s');
                next.onsuccess=()=>{reopened=true;version=next.result.version;next.result.close();};
                """);
        }
        Invoking(() => engine.Tasks.ProcessTasks()).Should().Throw<JavaScriptException>().WithMessage("*upgrade listener failed*");
        Invoking(() => engine.Tasks.ProcessTasks()).Should().Throw<JavaScriptException>().WithMessage("*abort listener failed*");
        engine.Tasks.ProcessTasks();
        engine.Evaluate("[openError,pendingError,reopened,version,failed.transaction].join('|')")
            .Should().Be("AbortError|AbortError|true|1|");
        Run(engine, "return await request(other.transaction('s').objectStore('s').count());").Should().Be(0);
    }

    [TestCase("complete")]
    [TestCase("abort")]
    [TestCase("success")]
    [TestCase("blocked")]
    [TestCase("versionchange")]
    public void SinklessNonRequestListenerFailureKeepsOtherConnectionsAndQueuedOpens(string eventType)
    {
        using var engine = Create();
        engine.Execute("var db,other;open().then(x=>db=x);open('other').then(x=>other=x);");
        engine.SetValue("eventType", eventType);
        using (engine.EventLoop.DeferTaskDrain())
        {
            engine.Execute("""
                var opened=false;
                const throwing=()=>{throw new Error('listener failed');};
                if(eventType==='success')indexedDB.open('db').onsuccess=throwing;
                else if(eventType==='blocked'||eventType==='versionchange'){
                    const r=indexedDB.open('db',2);
                    if(eventType==='blocked')r.onblocked=throwing;
                    else db.onversionchange=throwing;
                    r.onupgradeneeded=()=>{};
                    r.onsuccess=()=>r.result.close();
                }else{
                    const t=db.transaction('s','readwrite');
                    if(eventType==='abort'){t.onabort=throwing;t.abort();}
                    else {t.oncomplete=throwing;t.objectStore('s').put('kept',1);}
                }
                const queued=indexedDB.open('queued');
                queued.onsuccess=()=>{opened=true;queued.result.close();};
                """);
        }
        Invoking(() => engine.Tasks.ProcessTasks()).Should().Throw<JavaScriptException>().WithMessage("*listener failed*");
        engine.Tasks.ProcessTasks();
        engine.GetValue("opened").Should().Be(true);
        Run(engine, "return await request(other.transaction('s').objectStore('s').count());").Should().Be(0);
        engine.Execute("db.close()");
        engine.Tasks.ProcessTasks();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RequestListenerConstraintFailuresPropagateAndReleaseConnections(bool diagnostics)
    {
        var tripwire = new TripwireConstraint();
        var reports = new List<DiagnosticEvent>();
        using var engine = new Engine(o =>
        {
            o.UseWebApis(WebApiFeatures.IndexedDb).AddConstraint(tripwire);
            if (diagnostics) o.WebApi.Diagnostics.Sink = new RecordingSink(reports);
        });
        engine.Execute(Helpers);
        engine.Execute("var db;open().then(x=>db=x);");
        engine.SetValue("arm", (Action) (() => tripwire.Armed = true));
        using (engine.EventLoop.DeferTaskDrain())
        {
            engine.Execute("""
                var aborted=false;
                const t=db.transaction('s','readwrite');
                t.onabort=()=>aborted=true;
                t.objectStore('s').put('x',1).onsuccess=()=>{arm();while(true){}};
                """);
        }
        Invoking(() => engine.Tasks.ProcessTasks()).Should().ThrowExactly<TimeoutException>();
        tripwire.Checks.Should().Be(20);
        tripwire.Armed = false;
        reports.Should().BeEmpty();
        engine.GetValue("aborted").Should().Be(false);
        engine.Evaluate("errorName(()=>db.transaction('s'))").Should().Be("InvalidStateError");
    }

    private sealed class RecordingSink(List<DiagnosticEvent> reports) : DiagnosticsSink
    {
        public override void Report(DiagnosticEvent report) => reports.Add(report);
    }

    [Test]
    public void UpgradeCompletionCanStartAnOrdinaryTransaction()
    {
        using var engine = Create();
        Run(engine, """
            let state;
            const db=await open('db',1,(db,t)=>{
                db.createObjectStore('s');
                t.oncomplete=()=>{state=db.transaction('s').mode;};
            });
            return state;
            """).Should().Be("readonly");
    }

    [Test]
    public void PendingRequestsSurviveSchemaHandleDeletion()
    {
        using var engine = Create();
        Run(engine, """
            let written,read;
            const db=await open('db',1,(db,t)=>{
                const s=db.createObjectStore('s'),i=s.createIndex('i','x');
                s.put({x:3},1).onsuccess=e=>written=e.target.result;
                i.getKey(3).onsuccess=e=>read=e.target.result;
                s.deleteIndex('i');
                db.deleteObjectStore('s');
            });
            return written+'|'+read+'|'+db.objectStoreNames.length;
            """).Should().Be("1|1|0");
    }

    [Test]
    public void NewUniqueIndexFailureIsNotAnEarlierPutFailure()
    {
        using var engine = Create();
        Run(engine, """
            let writes=0,errors=0;
            const r=indexedDB.open('db',1);
            r.onupgradeneeded=()=>{
                const s=r.result.createObjectStore('s');
                for(let k=1;k<=2;k++){
                    const p=s.put({x:1},k);p.onsuccess=()=>writes++;
                    p.onerror=e=>{errors++;e.preventDefault();};
                }
                s.createIndex('i','x',{unique:true});
            };
            try{await request(r);return 'unexpected success';}
            catch(e){return writes+'|'+errors+'|'+e.name;}
            """).Should().Be("2|0|AbortError");
    }

    [Test]
    public void MultiEntryConversionSharesItsSeenArrays()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s').createIndex('i','x',{multiEntry:true}));
            const t=db.transaction('s','readwrite'),s=t.objectStore('s'),a=[1],x=[a,[a,2]];
            x.push(x);await request(s.put({x},1));
            return await request(s.index('i').count());
            """).Should().Be(1);
    }

    [TestCase("s.put({x:20000,tags:[20000,200001]},20000)")]
    [TestCase("s.put({x:500,tags:[500,200001]},500)")]
    [TestCase("s.delete(500)")]
    [TestCase("s.delete(IDBKeyRange.bound(500,500))")]
    [TestCase("s.delete(20000)")]
    public void SingleRecordWritesDoNotPollUnrelatedRecords(string operation)
    {
        int? smallerStoreChecks = null;
        foreach (var recordCount in new[] { 1024, 4096 })
        {
            var counter = new RecordAccessConstraint();
            using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb).AddConstraint(counter));
            engine.Execute(Helpers);
            engine.SetValue("recordCount", recordCount);
            engine.SetValue("beginMeasure", (Action) (() => { counter.Checks = 0; counter.Armed = true; }));
            engine.SetValue("endMeasure", (Action) (() => counter.Armed = false));
            Run(engine, """
                const db=await open('db',1,db=>{
                    const s=db.createObjectStore('s');
                    s.createIndex('u','x',{unique:true});
                    s.createIndex('tags','tags',{multiEntry:true});
                });
                const t=db.transaction('s','readwrite'),done=completed(t),s=t.objectStore('s');
                for(let i=0;i<recordCount;i++)s.put({x:i,tags:[i,i+1]},i);
                await request(s.get(0));
                beginMeasure();
                """ + "await request(" + operation + ");" + """
                endMeasure();
                await done;
                return true;
                """).Should().Be(true);
            // Excludes copying and population, and catches even scans polling every 256 entries.
            counter.Checks.Should().BeGreaterThan(0).And.BeLessThan(128);
            if (smallerStoreChecks is { } expected) counter.Checks.Should().Be(expected);
            smallerStoreChecks = counter.Checks;
        }
    }

    [TestCase("s.put({x:20000,tags:[20000,200001]},20000)")]
    [TestCase("s.put({x:500,tags:[500,200001]},500)")]
    [TestCase("s.delete(500)")]
    public void OneWriteTransactionsDoNotPollUnrelatedRecords(string operation)
    {
        int? smallerStoreChecks = null;
        foreach (var recordCount in new[] { 1024, 8192 })
        {
            var counter = new RecordAccessConstraint();
            using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb).AddConstraint(counter));
            engine.Execute(Helpers);
            engine.SetValue("recordCount", recordCount);
            engine.SetValue("beginMeasure", (Action) (() => { counter.Checks = 0; counter.Armed = true; }));
            engine.SetValue("endMeasure", (Action) (() => counter.Armed = false));
            Run(engine, """
                const db=await open('db',1,db=>{
                    const s=db.createObjectStore('s');
                    s.createIndex('u','x',{unique:true});
                    s.createIndex('tags','tags',{multiEntry:true});
                });
                let t=db.transaction('s','readwrite'),done=completed(t),s=t.objectStore('s');
                for(let i=0;i<recordCount;i++)s.put({x:i,tags:[i,i+1]},i);
                await done;
                beginMeasure();
                for(let i=0;i<8;i++) {
                    t=db.transaction('s','readwrite');done=completed(t);s=t.objectStore('s');
                """ + "await request(" + operation + ");" + """
                    await done;
                }
                endMeasure();
                return true;
                """).Should().Be(true);
            // Includes startup, first mutation and publication for each separate transaction.
            counter.Checks.Should().BeGreaterThan(0);
            if (smallerStoreChecks is { } expected) counter.Checks.Should().Be(expected);
            smallerStoreChecks = counter.Checks;
        }
    }

    [Test]
    public void StoreForksShareTreesAndSingleWritesAllocateOnlyChangedPaths()
    {
        long? smallerStoreBytes = null;
        foreach (var count in new[] { 1024, 8192 })
        {
            var original = new ObjectStoreData("s", null, true);
            var index = new IndexData("i", new IndexedDbKeyPath(["x"], false), false, false);
            original.Indexes.Add("i", index);
            var record = new StoredRecord(default, 1);
            for (var i = 0; i < count; i++)
            {
                var key = IndexedDbKey.Numeric(i);
                original.Records = original.Records.Add(key, record);
                index.Entries = index.Entries.Add(new IndexEntry(key, key));
            }
            var fork = original.Copy(static () => { });
            fork.Records.Should().BeSameAs(original.Records);
            fork.Indexes["i"].Entries.Should().BeSameAs(index.Entries);
            // Warm the exact path before measuring allocations, never wall-clock time.
            Write(fork, count);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var current = original;
            for (var i = 0; i < 64; i++)
            {
                current = current.Copy(static () => { });
                Write(current, count + i);
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (smallerStoreBytes is { } small) bytes.Should().BeLessThan(small * 2);
            smallerStoreBytes = bytes;
            original.Records.Count.Should().Be(count);
            index.Entries.Count.Should().Be(count);
            original.Generator.Should().Be(1);
            current.Records.Count.Should().Be(count + 64);
            current.Indexes["i"].Entries.Count.Should().Be(count + 64);
        }

        static void Write(ObjectStoreData data, int number)
        {
            var key = IndexedDbKey.Numeric(number);
            data.Records = data.Records.SetItem(key, new StoredRecord(default, 1));
            var index = data.Indexes["i"];
            index.Entries = index.Entries.Add(new IndexEntry(key, key));
            data.Generator = number + 1;
        }
    }

    [Test]
    public void UniqueMultiEntryReplacementDeletionAndRollbackPreserveEntries()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>{
                const s=db.createObjectStore('s');
                s.createIndex('u','tags',{unique:true,multiEntry:true});
                s.createIndex('n','tags',{multiEntry:true});
            });
            let t=db.transaction('s','readwrite'),done=completed(t),s=t.objectStore('s');
            await request(s.put({tags:['a','a','b']},1));
            await request(s.put({tags:['c']},2));
            await request(s.put({tags:['b','d','d']},1));
            const failed=s.put({tags:['c','e']},1);
            const failure=new Promise(resolve=>failed.onerror=e=>{e.preventDefault();resolve(failed.error.name);});
            const error=await failure;
            const before=await request(s.index('u').getAllKeys());
            await request(s.delete(IDBKeyRange.only(1)));
            const after=await request(s.index('u').getAllKeys());
            await request(s.put({tags:['b','d']},3));
            await done;
            t=db.transaction('s','readwrite');s=t.objectStore('s');
            const aborted=new Promise(resolve=>t.onabort=resolve);
            await request(s.delete(2));await request(s.put({tags:['c']},4));t.abort();await aborted;
            t=db.transaction('s','readwrite');done=completed(t);s=t.objectStore('s');
            const rolledBack=await request(s.index('u').getAllKeys());
            await request(s.clear());await request(s.put({tags:['c','c']},5));await done;
            t=db.transaction('s');s=t.objectStore('s');
            return error+'|'+before+'|'+after+'|'+rolledBack+'|'
                +await request(s.index('u').getAllKeys())+'|'+await request(s.index('n').getAllKeys());
            """).Should().Be("ConstraintError|1,2,1|2|3,2,3|5|5");
    }

    [Test]
    public void QueuedReplacementAndDeleteUseTheCapturedIndexSchema()
    {
        using var engine = Create();
        Run(engine, """
            let keys;
            const db=await open('db',1,(db,t)=>{
                const s=db.createObjectStore('s');
                s.createIndex('i','old',{unique:true});
                s.put({old:'a',new:'z'},1);
                s.put({old:'b',new:'y'},1);
                s.delete(1);
                s.put({old:'a',new:'x'},2);
                s.deleteIndex('i');
                const i=s.createIndex('i','new',{unique:true});
                i.getAllKeys().onsuccess=e=>keys=e.target.result;
            });
            return keys+'|'+await request(db.transaction('s').objectStore('s').index('i').getKey('x'));
            """).Should().Be("2|2");
    }

    [TestCase("s.delete(1)")]
    [TestCase("s.put({tags:[999]},1)")]
    public void RemovingManyMultiEntryKeysStillPropagatesConstraints(string operation)
    {
        var counter = new RecordAccessConstraint { Limit = 100 };
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb).AddConstraint(counter));
        engine.Execute(Helpers);
        engine.SetValue("arm", (Action) (() => counter.Armed = true));
        Run(engine, """
            globalThis.db=await open('db',1,db=>db.createObjectStore('s').createIndex('i','tags',{multiEntry:true}));
            const t=db.transaction('s','readwrite'),done=completed(t);
            t.objectStore('s').put({tags:Array.from({length:256},(_,i)=>i)},1);await done;
            """);
        using (engine.EventLoop.DeferTaskDrain())
        {
            engine.Execute("""
                var t=db.transaction('s','readwrite'),s=t.objectStore('s');
                s.get(1).onsuccess=()=>{
                """ + operation + ";arm();};");
        }
        Invoking(() => engine.Tasks.ProcessTasks()).Should().ThrowExactly<TimeoutException>();
        counter.Checks.Should().Be(100);
        counter.Armed = false;
        Run(engine, """
            const reopened=await open();
            const s=reopened.transaction('s').objectStore('s');
            return (await request(s.get(1))).tags.length+'|'+await request(s.index('i').count());
            """).Should().Be("256|256");
    }

    [TestCase("-0", "0")]
    [TestCase("new Date(0)", "new Date(0)")]
    [TestCase("new Uint8Array([1,2])", "new Uint8Array([1,2]).buffer")]
    [TestCase("[1,'x',[]]", "[1,'x',[]]")]
    public void UniqueIndexLookupUsesIndexedDbKeyEquality(string first, string equivalent)
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s').createIndex('i','x',{unique:true}));
            const t=db.transaction('s','readwrite'),done=completed(t),s=t.objectStore('s');
            """ + "await request(s.put({x:" + first + "},1));await request(s.put({x:" + equivalent + "},1));"
            + "const failed=s.put({x:" + equivalent + "},2);" + """
            const error=await new Promise(resolve=>failed.onerror=e=>{e.preventDefault();resolve(failed.error.name);});
            await request(s.delete(1));
            """ + "await request(s.put({x:" + equivalent + "},2));" + """
            await done;
            const r=db.transaction('s').objectStore('s').index('i').openKeyCursor(null,'prev');
            const key=await new Promise(resolve=>r.onsuccess=()=>resolve(r.result.primaryKey));
            return error+'|'+key;
            """).Should().Be("ConstraintError|2");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void IndexCursorUpdateAndDeleteKeepTheRemainingOrder(bool unique)
    {
        using var engine = Create();
        engine.SetValue("unique", unique);
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s').createIndex('i','v',{unique}));
            const t=db.transaction('s','readwrite'),done=completed(t),s=t.objectStore('s');
            s.put({v:'a'},1);s.put({v:'b'},2);s.put({v:'c'},3);
            const r=s.index('i').openCursor(),seen=[];
            await new Promise((resolve,reject)=>{
                r.onerror=()=>reject(r.error);
                r.onsuccess=()=>{
                    const c=r.result;if(!c){resolve();return;}
                    seen.push(c.key+':'+c.primaryKey);
                    if(c.key==='a')c.update({v:'d'});
                    if(c.key==='b')c.delete();
                    c.continue();
                };
            });await done;
            const q=db.transaction('s').objectStore('s');
            return seen.join()+'|'+await request(q.index('i').getAllKeys())+'|'+await request(q.getAllKeys());
            """).Should().Be("a:1,b:2,c:3,d:1|3,1|1,3");
    }

    private sealed class RecordAccessConstraint : Constraint
    {
        internal bool Armed;
        internal int Checks;
        public override void Reset() { }
        internal int Limit = int.MaxValue;
        public override void Check()
        {
            if (Armed && ++Checks == Limit) throw new TimeoutException("IndexedDB record access budget");
        }
    }

    [Test]
    public void FirstDatabaseUseInsideAMicrotaskDrainsItsNewTaskLane()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb));
        engine.Execute("""
            var done=false;
            Promise.resolve().then(()=>{
                const r=indexedDB.open('db');
                r.onsuccess=()=>{done=true;r.result.close();};
            });
            """);
        engine.GetValue("done").Should().Be(true);
    }

    [Test]
    public void SnapshotRestoreReleasesConnectionsAndPreservesCommittedData()
    {
        var store = new IndexedDbStore();
        using var first = Create(store);
        using var second = Create(store);
        var snapshot = first.Advanced.CaptureGlobalSnapshot();
        Run(first, """
            const db=await open(),t=db.transaction('s','readwrite'),done=completed(t);
            t.objectStore('s').put('kept',1);await done;
            """);
        second.Execute("var done=false;open('db',2,()=>{}).then(db=>{done=true;db.close();})");
        second.GetValue("done").Should().Be(false);
        first.Advanced.RestoreGlobalSnapshot(snapshot);
        second.Tasks.ProcessTasks();
        second.GetValue("done").Should().Be(true);
        Run(first, """
            const db=await open('db',2);
            return await request(db.transaction('s').objectStore('s').get(1));
            """).Should().Be("kept");
    }

    [Test]
    public void ConstraintFailureDuringGetAllReleasesAWaitingWriter()
    {
        var store = new IndexedDbStore();
        var tripwire = new TripwireConstraint();
        using var first = new Engine(o => o.UseWebApis(WebApiFeatures.IndexedDb).AddConstraint(tripwire));
        first._webApi!.IndexedDb.Configure(store, opaqueOrigin: false);
        first.Execute(Helpers);
        using var second = Create(store);
        Run(first, """
            const db=await open();globalThis.db=db;
            const t=db.transaction('s','readwrite'),done=completed(t);
            for(let i=0;i<256;i++)t.objectStore('s').put(i,i);
            await done;
            """);
        second.Execute("var db,done=false;open().then(x=>db=x)");
        using (first.EventLoop.DeferTaskDrain())
        {
            first.Execute("var delivered=false;db.transaction('s').objectStore('s').getAll().onerror=()=>delivered=true");
        }
        second.Execute("var t=db.transaction('s','readwrite');t.objectStore('s').put('after',999);t.oncomplete=()=>done=true");
        second.GetValue("done").Should().Be(false);
        tripwire.Armed = true;
        Invoking(() => first.Tasks.ProcessTasks()).Should().ThrowExactly<TimeoutException>();
        tripwire.Checks.Should().Be(20);
        tripwire.Armed = false;
        first.GetValue("delivered").Should().Be(false);
        second.Tasks.ProcessTasks();
        second.GetValue("done").Should().Be(true);
        Run(second, "return await request(db.transaction('s').objectStore('s').get(999));").Should().Be("after");
    }

    private sealed class TripwireConstraint : Constraint
    {
        internal bool Armed;
        internal int Checks;
        public override void Reset() { }
        public override void Check()
        {
            if (Armed && ++Checks == 20) throw new TimeoutException("IndexedDB scan tripwire");
        }
    }

    [Test]
    public void DeletionDoesNotRetainAnEmptyDatabasePartition()
    {
        var store = new IndexedDbStore();
        var first = store.Open("absent", null, delete: true, _ => { });
        store.CompleteOpen(first);
        var second = store.Open("absent", null, delete: true, _ => { });
        second.Database.Should().NotBeSameAs(first.Database);
        store.CompleteOpen(first);
        store.CompleteOpen(second);
    }

    [Test]
    public void KeyPathArraysHaveStableIdentityWithoutMutatingTheSchema()
    {
        using var engine = Create();
        Run(engine, """
            const db=await open('db',1,db=>db.createObjectStore('s',{keyPath:['a','b']}).createIndex('i',['a','b']));
            const t=db.transaction('s','readwrite'),s=t.objectStore('s'),i=s.index('i'),p=s.keyPath,q=i.keyPath;
            p[0]='changed';q[0]='changed';
            await request(s.put({a:1,b:2}));
            return [s.keyPath===p,i.keyPath===q,await request(i.count([1,2]))].join('|');
            """).Should().Be("true|true|1");
    }

    [Test]
    public void ClosedConnectionSchemaDoesNotFollowLaterUpgrades()
    {
        using var engine = Create();
        Run(engine, """
            const first=await open(),names=first.objectStoreNames;first.close();
            const second=await open('db',2,db=>db.createObjectStore('new'));
            return [Array.from(names),Array.from(first.objectStoreNames),Array.from(second.objectStoreNames)].join('|');
            """).Should().Be("s|s|new,s");
    }
}
#endif
