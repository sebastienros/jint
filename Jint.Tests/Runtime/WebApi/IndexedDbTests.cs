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
