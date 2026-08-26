#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>localStorage</c> and <c>sessionStorage</c>: the six members of the <c>Storage</c> interface, and the
/// named property access that makes <c>storage.foo = 'bar'</c> mean <c>setItem</c>.
/// </summary>
public class StorageTests
{
    private static Engine StorageEngine() => new(options => options.UseStorage());

    private static Engine StorageEngine(StorageProvider local, StorageProvider? session = null) =>
        new(options => options.UseStorage(local, session));

    [Test]
    public void RoundTripsThroughTheInterfaceMethods()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                localStorage.setItem('a', '1');
                localStorage.setItem('b', '2');
                const before = [localStorage.length, localStorage.getItem('a'), String(localStorage.getItem('missing'))];
                localStorage.removeItem('a');
                const after = [localStorage.length, String(localStorage.getItem('a'))];
                localStorage.clear();
                return [...before, ...after, localStorage.length].join('|');
            })()
            """).AsString().Should().Be("2|1|null|1|null|0");
    }

    [Test]
    public void CoercesKeysAndValuesToStrings()
    {
        var engine = StorageEngine();

        // Both parameters are DOMStrings, so everything is stringified on the way in.
        engine.Evaluate("""
            (() => {
                localStorage.setItem(1, 2);
                localStorage.setItem(null, undefined);
                localStorage.setItem({ toString: () => 'obj' }, [1, 2]);
                return [
                    typeof localStorage.getItem('1'), localStorage.getItem('1'),
                    localStorage.getItem('null'), localStorage.getItem('obj'),
                ].join('|');
            })()
            """).AsString().Should().Be("string|2|undefined|1,2");
    }

    [Test]
    public void KeyAnswersTheStoreOrderAndNullBeyondIt()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                localStorage.setItem('first', '1');
                localStorage.setItem('second', '2');
                // Overwriting does not move a key, and removing one closes the gap.
                localStorage.setItem('first', 'again');
                return [
                    localStorage.key(0), localStorage.key(1), String(localStorage.key(2)),
                    // unsigned long, neither [EnforceRange] nor [Clamp]: -1 wraps to 4294967295.
                    String(localStorage.key(-1)), localStorage.key(NaN), localStorage.key(1.9),
                ].join('|');
            })()
            """).AsString().Should().Be("first|second|null|null|first|second");
    }

    [Test]
    public void ChecksArityBeforeConvertingAnything()
    {
        var engine = StorageEngine();

        // WebIDL converts arguments only after the arity check, which is observable: the missing second
        // argument is reported without the first one's toString ever running.
        engine.Evaluate("""
            (() => {
                const seen = [];
                let converted = false;
                const loud = { toString() { converted = true; return 'x'; } };
                try { localStorage.setItem(loud); } catch (e) { seen.push(e.constructor.name); }
                try { localStorage.getItem(); } catch (e) { seen.push(e.constructor.name); }
                try { localStorage.key(); } catch (e) { seen.push(e.constructor.name); }
                return seen.join(',') + '/' + converted;
            })()
            """).AsString().Should().Be("TypeError,TypeError,TypeError/false");
    }

    [Test]
    public void ReadsAndWritesStoredKeysAsProperties()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                localStorage.foo = 'bar';
                const asProperty = localStorage.foo;
                const asMethod = localStorage.getItem('foo');
                localStorage.setItem('baz', 'qux');
                const both = localStorage.baz;
                delete localStorage.foo;
                return [asProperty, asMethod, both, String(localStorage.foo), String(localStorage.getItem('foo'))].join('|');
            })()
            """).AsString().Should().Be("bar|bar|qux|undefined|null");
    }

    [Test]
    public void AnswersExistenceQuestionsFromTheStore()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                localStorage.setItem('here', 'yes');
                return [
                    'here' in localStorage,
                    'gone' in localStorage,
                    localStorage.hasOwnProperty('here'),
                    localStorage.hasOwnProperty('gone'),
                    localStorage.propertyIsEnumerable('here'),
                ].join('|');
            })()
            """).AsString().Should().Be("true|false|true|false|true");
    }

    [Test]
    public void EnumeratesTheStoreInItsOwnOrder()
    {
        var engine = StorageEngine();

        // The named properties are appended in the store's order, and deliberately NOT sorted the way
        // OrdinaryOwnPropertyKeys sorts array indices — "b" stays ahead of "1" everywhere the storage
        // itself is enumerated. The spread is the exception that proves it: the keys leave in that order
        // and are then re-sorted by the ordinary object they are copied into.
        engine.Evaluate("""
            (() => {
                localStorage.setItem('b', '1');
                localStorage.setItem('1', 'x');
                localStorage.setItem('a', '2');
                const spread = { ...localStorage };
                return [
                    Object.keys(localStorage).join(','),
                    Object.values(localStorage).join(','),
                    Object.entries(localStorage).map(e => e.join('=')).join(','),
                    Object.keys(spread).join(','),
                    JSON.stringify(localStorage),
                    Object.getOwnPropertyNames(localStorage).join(','),
                ].join('|');
            })()
            """).AsString().Should().Be("b,1,a|1,x,2|b=1,1=x,a=2|1,b,a|{\"b\":\"1\",\"1\":\"x\",\"a\":\"2\"}|b,1,a");
    }

    [Test]
    public void ForInVisitsTheStoredKeysAndTheEnumerableAttribute()
    {
        var engine = StorageEngine();

        // for-in walks the prototype chain, and both WebIDL member kinds on Storage.prototype are
        // enumerable — the `length` attribute and the five operations — so all six are visited, exactly as
        // they are in a browser and in Node. Object.keys is the way to ask for the stored keys alone.
        //
        // Node 24 visits the same six names in a different order (`length` first, because its prototype is
        // built in IDL declaration order, where Jint's generator sorts a host's members by JS name).
        // Membership is what WebIDL specifies here, and it matches.
        engine.Evaluate("""
            (() => {
                localStorage.setItem('one', '1');
                localStorage.setItem('two', '2');
                const seen = [];
                for (const key in localStorage) { seen.push(key); }
                const own = [];
                for (const key in localStorage) { if (localStorage.hasOwnProperty(key)) own.push(key); }
                return seen.join(',') + '/' + own.join(',');
            })()
            """).AsString().Should().Be("one,two,clear,getItem,key,removeItem,setItem,length/one,two");
    }

    [Test]
    public void LetsTheInterfaceWinOverAStoredKeyOfTheSameName()
    {
        var engine = StorageEngine();

        // The named property visibility algorithm: Storage is not [LegacyOverrideBuiltIns], so anything the
        // prototype chain carries hides the stored key of that name — while the key itself is stored, read
        // back through getItem, and left out of the enumerations.
        engine.Evaluate("""
            (() => {
                localStorage.setItem('getItem', 'shadowed');
                localStorage.setItem('length', 'also shadowed');
                localStorage.setItem('toString', 'inherited from Object.prototype');
                return [
                    typeof localStorage.getItem,
                    localStorage.getItem('getItem'),
                    localStorage.length,
                    typeof localStorage.toString,
                    localStorage.hasOwnProperty('getItem'),
                    'getItem' in localStorage,
                    Object.keys(localStorage).join(','),
                ].join('|');
            })()
            """).AsString().Should().Be("function|shadowed|3|function|false|true|");
    }

    [Test]
    public void AssigningOverAMethodNameStoresTheKeyAndKeepsTheMethod()
    {
        var engine = StorageEngine();

        // [[Set]] invokes the named property setter whenever the receiver is the storage and the key is a
        // string — before any own-property or prototype lookup happens at all.
        engine.Evaluate("""
            (() => {
                localStorage.getItem = 'not a function';
                return [typeof localStorage.getItem, localStorage.getItem('getItem')].join('|');
            })()
            """).AsString().Should().Be("function|not a function");
    }

    [Test]
    public void FollowsThePrototypeChainAsItChanges()
    {
        var engine = StorageEngine();

        // The visibility answer is recomputed on every read, so a name that becomes a prototype property
        // starts hiding the stored key, and stops again when it is deleted. A warmed member-read inline
        // cache must not outlive either transition.
        engine.Evaluate("""
            (() => {
                localStorage.setItem('later', 'from the store');
                const read = () => localStorage.later;
                const before = [read(), read()];
                Storage.prototype.later = 'from the prototype';
                const during = [read(), read(), Object.keys(localStorage).length];
                delete Storage.prototype.later;
                const after = [read(), read()];
                return [...before, ...during, ...after].join('|');
            })()
            """).AsString().Should().Be("from the store|from the store|from the prototype|from the prototype|0|from the store|from the store");
    }

    [Test]
    public void IsALegacyPlatformObjectForDefineAndFreeze()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                const seen = [];
                Object.defineProperty(localStorage, 'defined', { value: 42 });
                seen.push(localStorage.getItem('defined'));

                const descriptor = Object.getOwnPropertyDescriptor(localStorage, 'defined');
                seen.push([descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable].join(''));

                // A non-data descriptor is refused, which DefinePropertyOrThrow turns into a TypeError.
                try { Object.defineProperty(localStorage, 'x', { get: () => 1 }); } catch (e) { seen.push(e.constructor.name); }
                try { Object.defineProperty(localStorage, 'x', {}); } catch (e) { seen.push(e.constructor.name); }

                // [[PreventExtensions]] returns false for a legacy platform object.
                seen.push(Object.isExtensible(localStorage));
                try { Object.freeze(localStorage); } catch (e) { seen.push(e.constructor.name); }
                try { Object.preventExtensions(localStorage); } catch (e) { seen.push(e.constructor.name); }

                return seen.join('|');
            })()
            """).AsString().Should().Be("42|42truetruetrue|TypeError|TypeError|true|TypeError|TypeError");
    }

    [Test]
    public void DeletingAnAbsentOrInheritedNameSucceedsVacuously()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                localStorage.setItem('getItem', 'stored');
                return [
                    delete localStorage.absent,
                    delete localStorage.getItem,
                    typeof localStorage.getItem,
                    localStorage.getItem('getItem'),
                ].join('|');
            })()
            """).AsString().Should().Be("true|true|function|stored");
    }

    [Test]
    public void CarriesTheInterfaceObjectAndItsPrototype()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                const seen = [];
                seen.push(localStorage instanceof Storage);
                seen.push(Object.getPrototypeOf(localStorage) === Storage.prototype);
                seen.push(Object.prototype.toString.call(localStorage));
                try { new Storage(); } catch (e) { seen.push(e.constructor.name); }
                try { Storage(); } catch (e) { seen.push(e.constructor.name); }
                // Every member brand-checks its receiver.
                try { Storage.prototype.getItem.call({}, 'a'); } catch (e) { seen.push(e.constructor.name); }
                try { Storage.prototype.length; } catch (e) { seen.push(e.constructor.name); }
                return seen.join('|');
            })()
            """).AsString().Should().Be("true|true|[object Storage]|TypeError|TypeError|TypeError|TypeError");
    }

    [Test]
    public void KeepsLocalAndSessionApart()
    {
        var engine = StorageEngine();

        engine.Evaluate("""
            (() => {
                localStorage.setItem('shared?', 'no');
                sessionStorage.setItem('shared?', 'definitely not');
                return [
                    localStorage === sessionStorage,
                    localStorage === localStorage,
                    localStorage.getItem('shared?'),
                    sessionStorage.getItem('shared?'),
                    sessionStorage instanceof Storage,
                ].join('|');
            })()
            """).AsString().Should().Be("false|true|no|definitely not|true");
    }

    [Test]
    public void TurnsAProvidersQuotaRefusalIntoACatchableQuotaExceededError()
    {
        var engine = new Engine(options => options
            .UseStorage()
            .WebApi.Storage.MaxTotalBytes = 32);

        engine.Evaluate("""
            (() => {
                const seen = [];
                localStorage.setItem('k', 'small');           // 2 * (1 + 5) = 12 bytes
                try {
                    localStorage.setItem('big', 'x'.repeat(64));
                } catch (e) {
                    // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface, carrying the budget
                    // the in-box provider enforces and the size the refused write would have taken it to:
                    // 12 bytes already stored plus 2 * (3 + 64) for the new pair.
                    seen.push(e.name, e instanceof QuotaExceededError, e.code, e.quota, e.requested, String(localStorage.getItem('big')));
                }
                // The store is exactly as it was, and still usable.
                localStorage.setItem('k', 'tiny');
                seen.push(localStorage.getItem('k'), localStorage.length);
                return seen.join('|');
            })()
            """).AsString().Should().Be("QuotaExceededError|true|22|32|146|null|tiny|1");
    }

    [Test]
    public void QuotaAlsoGuardsTheNamedSetter()
    {
        var engine = new Engine(options => options
            .UseStorage()
            .WebApi.Storage.MaxTotalBytes = 8);

        engine.Evaluate("""
            (() => {
                try { localStorage.tooBig = 'well over the quota'; } catch (e) { return e.name; }
                return 'no throw';
            })()
            """).AsString().Should().Be("QuotaExceededError");
    }

    [Test]
    public void DoesNotTouchTheProviderWhenTheValueIsUnchanged()
    {
        // "If this's map[key] exists: ... if oldValue is value, return" — the whole operation is a no-op,
        // which a recording provider can see and a quota cannot refuse.
        var provider = new RecordingStorageProvider();
        var engine = StorageEngine(provider);

        engine.Execute("""
            localStorage.setItem('a', '1');
            localStorage.setItem('a', '1');
            localStorage.setItem('a', '2');
            localStorage.a = '2';
            """);

        provider.Log.Should().Be("set(a,1)|set(a,2)");
    }

    [Test]
    public void DrivesTheHostsProviderForEveryOperation()
    {
        var provider = new RecordingStorageProvider();
        var engine = StorageEngine(provider);

        engine.Execute("""
            localStorage.setItem('a', '1');
            localStorage.b = '2';
            localStorage.getItem('a');
            delete localStorage.b;
            localStorage.removeItem('a');
            localStorage.clear();
            """);

        // Named access goes through the same three operations the interface's methods do.
        provider.Log.Should().Be("set(a,1)|set(b,2)|remove(b)|remove(a)|clear");
    }

    [Test]
    public void ReadsWhateverTheProviderSaysAtThatMoment()
    {
        var provider = new InMemoryStorageProvider();
        var engine = StorageEngine(provider);

        // A host writing behind the engine's back is seen immediately: the store is read live.
        provider.SetItem("fromHost", "hello");
        engine.Evaluate("localStorage.fromHost").AsString().Should().Be("hello");

        engine.Execute("localStorage.setItem('fromScript', 'hi')");
        provider.GetItem("fromScript").Should().Be("hi");
        provider.Count.Should().Be(2);
    }

    [Test]
    public void GivesTwoEnginesBuiltFromOneOptionsInstanceTheirOwnStores()
    {
        var options = new Options().UseStorage();

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("localStorage.setItem('who', 'first')");
        second.Execute("localStorage.setItem('who', 'second')");

        first.Evaluate("localStorage.getItem('who')").AsString().Should().Be("first");
        second.Evaluate("localStorage.getItem('who')").AsString().Should().Be("second");
        second.Evaluate("localStorage.length").AsNumber().Should().Be(1);
    }

    [Test]
    public void SharesAStoreWhenTheProviderIsShared()
    {
        var shared = new InMemoryStorageProvider();
        var options = new Options().UseStorage(shared, shared);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("localStorage.setItem('who', 'first')");

        // One provider, one store — for both engines and for both globals.
        second.Evaluate("localStorage.getItem('who')").AsString().Should().Be("first");
        second.Evaluate("sessionStorage.getItem('who')").AsString().Should().Be("first");
        first.Evaluate("localStorage === sessionStorage").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void IsNotPartOfTheDefaultFeatureSet()
    {
        // Storage is state a host may not expect, so it is never inherited from "give me the web APIs".
        new Options().UseWebApis().WebApi.Features.Should().NotHaveFlag(WebApiFeatures.Storage);

        var web = new Engine(options => options.UseWebApis());
        web.Evaluate("typeof localStorage").AsString().Should().Be("undefined");
        web.Evaluate("typeof sessionStorage").AsString().Should().Be("undefined");
        web.Evaluate("typeof Storage").AsString().Should().Be("undefined");

        var plain = new Engine();
        plain.Evaluate("typeof localStorage").AsString().Should().Be("undefined");
        plain.Evaluate("'localStorage' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void GivesEachGlobalTheAttributesWebIdlAsksFor()
    {
        var engine = StorageEngine();

        // The interface object is writable and configurable but not enumerable.
        var storage = engine.Realm.GlobalObject.GetOwnProperty("Storage");
        storage.Writable.Should().BeTrue();
        storage.Configurable.Should().BeTrue();
        storage.Enumerable.Should().BeFalse();

        foreach (var name in new[] { "localStorage", "sessionStorage" })
        {
            // ... and the two instances are ordinary enumerable data properties, the documented
            // simplification of the [Replaceable] accessor pair.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();
            descriptor.Enumerable.Should().BeTrue();
        }
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = StorageEngine();

        foreach (var name in new[] { "localStorage", "sessionStorage", "Storage" })
        {
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }

        engine.Evaluate("typeof localStorage").AsString().Should().Be("object");
    }

    [Test]
    public void LeavesAGlobalTheHostAlreadyOwns()
    {
        var marker = new JsString("the host's own");
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("localStorage", marker))
            .UseStorage());

        engine.Evaluate("localStorage").Should().BeSameAs(marker);
        engine.Evaluate("typeof sessionStorage").AsString().Should().Be("object");
    }

    [Test]
    public void SurvivesAGlobalSnapshotRestoreWithItsContents()
    {
        var engine = StorageEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var before = engine.Evaluate("localStorage");
        engine.Execute("localStorage.setItem('written', 'in the first cycle'); var x = 1;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The binding is back to its unmaterialized lazy descriptor, so the next read runs the factory
        // again — and the factory reads the realm's memoized intrinsic, so it hands back the same object.
        // Two independent reasons the contents survive, then: the object is not rebuilt, and the map behind
        // it is host state a restore deliberately does not revert (like the module registry).
        engine.Evaluate("typeof x").AsString().Should().Be("undefined");
        engine.Evaluate("localStorage").Should().BeSameAs(before);
        engine.Evaluate("localStorage.getItem('written')").AsString().Should().Be("in the first cycle");
    }

    [Test]
    public void SurvivesAKeyThatIsAlsoASymbol()
    {
        var engine = StorageEngine();

        // Symbols never reach the named property machinery: they define, read and delete ordinarily.
        engine.Evaluate("""
            (() => {
                const s = Symbol('tag');
                localStorage[s] = 'not stored';
                localStorage.setItem('tag', 'stored');
                return [
                    localStorage[s],
                    localStorage.getItem('tag'),
                    Object.getOwnPropertySymbols(localStorage).length,
                    Object.keys(localStorage).join(','),
                    delete localStorage[s],
                    String(localStorage[s]),
                ].join('|');
            })()
            """).AsString().Should().Be("not stored|stored|1|tag|true|undefined");
    }

    [Test]
    public void ThrowsThroughToTheHostForAnythingButAQuotaRefusal()
    {
        var engine = StorageEngine(new ThrowingStorageProvider());

        // Only StorageQuotaExceededException is translated; a host's own failure is not flattened into a
        // JavaScript error the script could swallow.
        var exception = Assert.Throws<InvalidOperationException>(() => engine.Execute("localStorage.setItem('a', '1')"))!;
        exception.Message.Should().Be("the database is on fire");
    }

    private sealed class RecordingStorageProvider : StorageProvider
    {
        private readonly InMemoryStorageProvider _inner = new();
        private readonly List<string> _log = [];

        internal string Log => string.Join("|", _log);

        public override IReadOnlyList<string> Keys => _inner.Keys;

        public override string? GetItem(string key) => _inner.GetItem(key);

        public override void SetItem(string key, string value)
        {
            _log.Add($"set({key},{value})");
            _inner.SetItem(key, value);
        }

        public override void RemoveItem(string key)
        {
            _log.Add($"remove({key})");
            _inner.RemoveItem(key);
        }

        public override void Clear()
        {
            _log.Add("clear");
            _inner.Clear();
        }
    }

    private sealed class ThrowingStorageProvider : StorageProvider
    {
        public override IReadOnlyList<string> Keys => [];

        public override string? GetItem(string key) => null;

        public override void SetItem(string key, string value) => throw new InvalidOperationException("the database is on fire");

        public override void RemoveItem(string key)
        {
        }

        public override void Clear()
        {
        }
    }
}
#endif
