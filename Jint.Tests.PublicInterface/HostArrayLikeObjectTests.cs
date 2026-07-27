#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="ArrayLikeObject"/> is the supported way to project a <b>live</b> host collection — a DOM
/// <c>NodeList</c>, a result window, any lazily computed indexed view — into script. A subclass declares only
/// <see cref="ArrayLikeObject.Length"/> and <see cref="ArrayLikeObject.TryGetIndex"/>; the base class derives the
/// entire JS-visible property model from those two, and the engine keys two fast lanes on the type: the
/// <c>ArrayOperations</c> dispatcher (so <c>Array.prototype</c> generics, the array iterator, spread,
/// <c>Array.from</c> and destructuring cost one virtual call per element) and the interpreter's computed-read
/// branch (so <c>list[i]</c> resolves with no <c>Reference</c>, no key object and no descriptor).
///
/// <para>
/// These tests live in the project <b>without</b> <c>InternalsVisibleTo</c>, so the fact that the collections
/// below compile at all is the reachability proof: everything the lanes need is expressible from the public
/// surface. Nothing here reaches into engine internals, and the hosts deliberately use only members a third
/// party has.
/// </para>
///
/// <para>
/// A Debug build of Jint verifies, on every read, that the class's <c>TryGetOwnPropertyValue</c> and
/// <c>ProbeOwnProperty</c> agree with its <c>GetOwnProperty</c> and that its declared ordinary <c>Get</c>
/// semantics hold. Running this suite against a Debug Jint is therefore the coherence checker for the whole
/// matrix, which is why every assertion below is written to hold in both configurations.
/// </para>
/// </summary>
public class HostArrayLikeObjectTests
{
    /// <summary>
    /// A live view over a <see cref="List{T}"/> the test can mutate between reads. Restricted to the public
    /// surface on purpose: the only Jint members it touches are <see cref="ArrayLikeObject"/>'s two abstract
    /// members, <see cref="ObjectInstance.Prototype"/> and <c>Engine.Intrinsics</c>. A <see langword="null"/>
    /// entry models a hole — an index below <c>length</c> that the collection nevertheless does not own.
    /// </summary>
    private sealed class HostList : ArrayLikeObject
    {
        private readonly List<string?> _items;

        public HostList(Engine engine, bool attachArrayPrototype, params string?[] items) : base(engine)
        {
            _items = new List<string?>(items);
            if (attachArrayPrototype)
            {
                // What a WHATWG-shaped host does for a NodeList: inherit the array generics and the original
                // array iterator, without claiming to be an Array.
                Prototype = engine.Intrinsics.Array.PrototypeObject;
            }
        }

        public List<string?> Items => _items;

        /// <summary>Number of <see cref="TryGetIndex"/> calls since <see cref="ResetCounters"/>.</summary>
        public int IndexReads { get; private set; }

        /// <summary>Number of <see cref="Length"/> reads since <see cref="ResetCounters"/>.</summary>
        public int LengthReads { get; private set; }

        public void ResetCounters()
        {
            IndexReads = 0;
            LengthReads = 0;
        }

        public override uint Length
        {
            get
            {
                LengthReads++;
                return (uint) _items.Count;
            }
        }

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            IndexReads++;

            if (index < (uint) _items.Count && _items[(int) index] is { } item)
            {
                value = item;
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }

    /// <summary>
    /// The documented extension point: a subclass adding a <b>live named getter</b> by overriding
    /// <see cref="ObjectInstance.GetOwnProperty"/> and delegating everything it does not own — crucially every
    /// array-index key and <c>length</c> — to <c>base</c>. Its named key is advertised by extending
    /// <see cref="ArrayLikeObject.GetOwnPropertyKeys"/> the same way.
    /// </summary>
    private sealed class NamedGetterHostList : ArrayLikeObject
    {
        private readonly List<string> _items;

        public NamedGetterHostList(Engine engine, params string[] items) : base(engine)
        {
            _items = new List<string>(items);
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public List<string> Items => _items;

        public override uint Length => (uint) _items.Count;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count)
            {
                value = _items[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property is JsString name && string.Equals(name.ToString(), "last", StringComparison.Ordinal))
            {
                return _items.Count == 0
                    ? PropertyDescriptor.Undefined
                    : new PropertyDescriptor(_items[_items.Count - 1], writable: false, enumerable: true, configurable: true);
            }

            return base.GetOwnProperty(property);
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
        {
            var keys = base.GetOwnPropertyKeys(types);
            if ((types & Types.String) != Types.Empty && _items.Count > 0)
            {
                keys.Add("last");
            }

            return keys;
        }
    }

    private static Engine CreateEngine(out HostList list, bool attachArrayPrototype = true, params string?[] items)
    {
        var engine = new Engine();
        list = new HostList(engine, attachArrayPrototype, items.Length > 0 ? items : ["a", "b", "c"]);
        engine.SetValue("list", list);
        return engine;
    }

    // ---------------------------------------------------------------------------------------------------
    // Indexed reads
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void IndexedReadsResolveAgainstTheHost()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("list[0]").Should().Be("a");
        engine.Evaluate("list[2]").Should().Be("c");
        engine.Evaluate("list.length").Should().Be(3);

        // canonical numeric string keys address the same elements
        engine.Evaluate("list['1']").Should().Be("b");

        // out of range: no own element, nothing on the prototype chain either
        engine.Evaluate("list[3]").Should().Be(JsValue.Undefined);
        engine.Evaluate("list[-1]").Should().Be(JsValue.Undefined);

        // non-canonical index-looking keys are ordinary names, not elements
        engine.Evaluate("list['01']").Should().Be(JsValue.Undefined);
        engine.Evaluate("list['1.0']").Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void AWarmIndexedLoopCostsExactlyOneHostReadPerElement()
    {
        var engine = CreateEngine(out var list, attachArrayPrototype: true, "a", "b", "c", "d", "e");

        // warm the handler tree first: the second evaluation of a script on an engine is when the
        // per-node caches engage, and the count must not depend on that.
        engine.Evaluate("var s = ''; for (var i = 0; i < list.length; i++) { s += list[i]; } s;");

        list.ResetCounters();
        engine.Evaluate("var s = ''; for (var i = 0; i < list.length; i++) { s += list[i]; } s;")
            .Should().Be("abcde");

        // The computed-read lane answers straight from TryGetIndex — no descriptor, no Reference, and no
        // second probe. `list.length` never reaches TryGetIndex at all.
        list.IndexReads.Should().Be(5);
    }

    [Fact]
    public void AnIndexTheHostDoesNotOwnStillResolvesOnThePrototypeChain()
    {
        var engine = CreateEngine(out _);

        // A `false` from TryGetIndex is an authoritative own miss, not "stop looking": the read continues up
        // the prototype chain exactly as an ordinary [[Get]] would.
        engine.Execute("Array.prototype[7] = 'from-prototype';");
        engine.Evaluate("list[7]").Should().Be("from-prototype");
        engine.Evaluate("list.hasOwnProperty(7)").Should().Be(false);
        engine.Evaluate("7 in list").Should().Be(true);
    }

    [Fact]
    public void AHoleIsAnAbsentOwnPropertyNotAnUndefinedElement()
    {
        var engine = CreateEngine(out _, attachArrayPrototype: true, "a", null, "c");

        engine.Evaluate("list[1]").Should().Be(JsValue.Undefined);
        engine.Evaluate("list.hasOwnProperty(1)").Should().Be(false);
        engine.Evaluate("1 in list").Should().Be(false);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,2");
        engine.Evaluate("list.length").Should().Be(3);

        // a hole reads through to the prototype, like an array hole does
        engine.Execute("Array.prototype[1] = 'filled';");
        engine.Evaluate("list[1]").Should().Be("filled");
    }

    // ---------------------------------------------------------------------------------------------------
    // Iteration, spread, Array.from, destructuring
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ForOfSpreadArrayFromAndDestructuringAllIterateTheHost()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("var s = []; for (var x of list) { s.push(x); } s.join('-');").Should().Be("a-b-c");
        engine.Evaluate("[...list].join('-')").Should().Be("a-b-c");
        engine.Evaluate("Array.from(list).join('-')").Should().Be("a-b-c");
        engine.Evaluate("var [p, q] = list; p + q;").Should().Be("ab");
        engine.Evaluate("var [, , r, s] = list; r + ':' + s;").Should().Be("c:undefined");
        engine.Evaluate("(function () { return arguments.length + ':' + arguments[1]; })(...list)").Should().Be("3:b");
    }

    [Fact]
    public void ForOfCostsOneHostReadPerElement()
    {
        var engine = CreateEngine(out var list, attachArrayPrototype: true, "a", "b", "c", "d");

        engine.Evaluate("var s = ''; for (var x of list) { s += x; } s;");

        list.ResetCounters();
        engine.Evaluate("var s = ''; for (var x of list) { s += x; } s;").Should().Be("abcd");

        list.IndexReads.Should().Be(4);
    }

    [Fact]
    public void IterationObservesTheCollectionLive()
    {
        var engine = CreateEngine(out var list, attachArrayPrototype: true, "a", "b", "c");
        engine.SetValue("shrink", () => list.Items.RemoveAt(list.Items.Count - 1));
        engine.SetValue("grow", () => list.Items.Add("z"));

        // Length is re-read on every step, so an element removed mid-iteration is never yielded.
        engine.Evaluate("var s = []; for (var x of list) { s.push(x); shrink(); } s.join('-');")
            .Should().Be("a-b");

        list.Items.Clear();
        list.Items.AddRange(["a"]);

        // and one appended mid-iteration is.
        engine.Evaluate("var s = []; var n = 0; for (var x of list) { s.push(x); if (++n < 3) grow(); } s.join('-');")
            .Should().Be("a-z-z");
    }

    // ---------------------------------------------------------------------------------------------------
    // Array.prototype generics
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ArrayPrototypeGenericsWorkOnAHostCollection()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("list.join('-')").Should().Be("a-b-c");
        engine.Evaluate("list.indexOf('b')").Should().Be(1);
        engine.Evaluate("list.indexOf('zzz')").Should().Be(-1);
        engine.Evaluate("list.includes('c')").Should().Be(true);
        engine.Evaluate("list.slice(1).join('-')").Should().Be("b-c");
        engine.Evaluate("list.map(function (x) { return x.toUpperCase(); }).join('-')").Should().Be("A-B-C");
        engine.Evaluate("list.filter(function (x) { return x !== 'b'; }).join('-')").Should().Be("a-c");
        engine.Evaluate("var s = ''; list.forEach(function (x) { s += x; }); s;").Should().Be("abc");
        engine.Evaluate("list.reduce(function (a, b) { return a + b; }, '')").Should().Be("abc");
        engine.Evaluate("list.every(function (x) { return x.length === 1; })").Should().Be(true);
        engine.Evaluate("list.some(function (x) { return x === 'c'; })").Should().Be(true);
        engine.Evaluate("list.lastIndexOf('a')").Should().Be(0);
        engine.Evaluate("list.find(function (x) { return x > 'a'; })").Should().Be("b");
    }

    [Fact]
    public void ArrayPrototypeGenericsAlsoWorkCalledOnAHostCollectionWithoutTheArrayPrototype()
    {
        // A host that keeps its own prototype (the DOM shape: NodeList.prototype) is still a valid `this` for
        // the generics — nothing about the lanes depends on which prototype is attached.
        var engine = CreateEngine(out _, attachArrayPrototype: false);

        engine.Evaluate("Array.prototype.join.call(list, '-')").Should().Be("a-b-c");
        engine.Evaluate("Array.prototype.map.call(list, function (x) { return x + '!'; }).join('-')").Should().Be("a!-b!-c!");
        engine.Evaluate("Array.prototype.filter.call(list, function (x) { return x !== 'a'; }).join('-')").Should().Be("b-c");
        engine.Evaluate("Array.prototype.indexOf.call(list, 'c')").Should().Be(2);
        engine.Evaluate("Array.prototype.slice.call(list, 0, 2).join('-')").Should().Be("a-b");
        engine.Evaluate("Array.from(list, function (x) { return x + '?'; }).join('-')").Should().Be("a?-b?-c?");
    }

    [Fact]
    public void AMutatingGenericFailsTheWayANonWritablePropertyDoes()
    {
        var engine = CreateEngine(out _);

        // The read lane is read-only by design; mutating generics fall through to ordinary [[Set]] against a
        // non-writable own property, which is a spec-shaped TypeError rather than a CLR exception.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("list.sort();"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("list.reverse();"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("list.fill('x');"));
    }

    // ---------------------------------------------------------------------------------------------------
    // Enumeration and reflection
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void OwnKeysAreTheIndicesAscendingThenLengthThenTheNamedBag()
    {
        var engine = CreateEngine(out _);
        engine.Execute("list.named = 'expando';");

        // `length` is non-enumerable, so Object.keys excludes it while getOwnPropertyNames lists it — in the
        // ordinary [[OwnPropertyKeys]] position, after the indices and before the string bag.
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1,2,named");
        engine.Evaluate("Object.getOwnPropertyNames(list).join(',')").Should().Be("0,1,2,length,named");
        engine.Evaluate("Object.values(list).join(',')").Should().Be("a,b,c,expando");
        engine.Evaluate("Object.entries(list).map(function (e) { return e[0] + '=' + e[1]; }).join(',')")
            .Should().Be("0=a,1=b,2=c,named=expando");

        engine.Evaluate("var k = []; for (var n in list) { k.push(n); } k.join(',');").Should().Be("0,1,2,named");
        engine.Evaluate("JSON.stringify(Object.assign({}, list))").Should().Be("""{"0":"a","1":"b","2":"c","named":"expando"}""");
        engine.Evaluate("JSON.stringify({ ...list })").Should().Be("""{"0":"a","1":"b","2":"c","named":"expando"}""");
    }

    [Fact]
    public void ExistenceChecksAgreeWithTheProjection()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("0 in list").Should().Be(true);
        engine.Evaluate("2 in list").Should().Be(true);
        engine.Evaluate("3 in list").Should().Be(false);
        engine.Evaluate("'length' in list").Should().Be(true);

        engine.Evaluate("list.hasOwnProperty(0)").Should().Be(true);
        engine.Evaluate("list.hasOwnProperty('0')").Should().Be(true);
        engine.Evaluate("list.hasOwnProperty(3)").Should().Be(false);
        engine.Evaluate("list.hasOwnProperty('length')").Should().Be(true);

        engine.Evaluate("list.propertyIsEnumerable(0)").Should().Be(true);
        engine.Evaluate("list.propertyIsEnumerable('length')").Should().Be(false);
        engine.Evaluate("list.propertyIsEnumerable(3)").Should().Be(false);
    }

    [Fact]
    public void ReflectAgreesWithTheMemberLane()
    {
        // Reflect.* never reaches the interpreter's member lane, so these read through ObjectInstance.Get and
        // the class's own-value hook instead — the two must not be able to disagree.
        var engine = CreateEngine(out _);

        engine.Evaluate("Reflect.get(list, 0)").Should().Be("a");
        engine.Evaluate("Reflect.get(list, '2')").Should().Be("c");
        engine.Evaluate("Reflect.get(list, 5)").Should().Be(JsValue.Undefined);
        engine.Evaluate("Reflect.get(list, 'length')").Should().Be(3);
        engine.Evaluate("Reflect.has(list, 1)").Should().Be(true);
        engine.Evaluate("Reflect.has(list, 9)").Should().Be(false);
        engine.Evaluate("Reflect.ownKeys(list).join(',')").Should().Be("0,1,2,length");
        engine.Evaluate("Reflect.set(list, 0, 'X')").Should().Be(false);
        engine.Evaluate("Reflect.deleteProperty(list, 0)").Should().Be(false);
        engine.Evaluate("Reflect.defineProperty(list, 0, { value: 'X' })").Should().Be(false);
        engine.Evaluate("list[0]").Should().Be("a");
    }

    [Fact]
    public void DescriptorsReportTheWebIdlPlatformShape()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(list, 0))")
            .Should().Be("""{"value":"a","writable":false,"enumerable":true,"configurable":true}""");

        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(list, 'length'))")
            .Should().Be("""{"value":3,"writable":false,"enumerable":false,"configurable":true}""");

        engine.Evaluate("Object.getOwnPropertyDescriptor(list, 3)").Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void JsonStringifySerializesTheCollectionAsAnArray()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a","b","c"]""");
        engine.Evaluate("JSON.stringify({ items: list })").Should().Be("""{"items":["a","b","c"]}""");
        engine.Evaluate("JSON.stringify(list, null, 2)").Should().Be("[\n  \"a\",\n  \"b\",\n  \"c\"\n]");
    }

    [Fact]
    public void JsonStringifyEmitsNullForAHole()
    {
        var engine = CreateEngine(out _, attachArrayPrototype: true, "a", null, "c");

        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a",null,"c"]""");
    }

    // ---------------------------------------------------------------------------------------------------
    // It is array-LIKE, not an Array
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCollectionDoesNotClaimToBeAnArray()
    {
        var engine = CreateEngine(out _);

        // Browsers answer false for a NodeList and so does this: IsArray is true exactly for Array exotic
        // objects and proxies over them, and Jint's interop layer hard-casts behind that check.
        engine.Evaluate("Array.isArray(list)").Should().Be(false);
        engine.Evaluate("Object.prototype.toString.call(list)").Should().Be("[object Object]");

        // both opt-ins are the ordinary per-object well-known symbols
        engine.Execute("list[Symbol.toStringTag] = 'HostList';");
        engine.Evaluate("Object.prototype.toString.call(list)").Should().Be("[object HostList]");
    }

    [Fact]
    public void ConcatAppendsUntilSymbolIsConcatSpreadableSaysOtherwise()
    {
        var engine = CreateEngine(out _);

        // not spreadable by default — the collection is appended as a single element
        engine.Evaluate("[].concat(list).length").Should().Be(1);
        engine.Evaluate("[].concat(list)[0] === list").Should().Be(true);

        engine.Execute("list[Symbol.isConcatSpreadable] = true;");
        engine.Evaluate("[].concat(list).join('-')").Should().Be("a-b-c");
        engine.Evaluate("['x'].concat(list).length").Should().Be(4);
    }

    // ---------------------------------------------------------------------------------------------------
    // Write / delete / defineProperty refusals
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void WritesToIndicesAndLengthAreIgnoredInSloppyModeAndThrowInStrictMode()
    {
        var engine = CreateEngine(out var list);

        engine.Execute("list[0] = 'X'; list.length = 0; list[9] = 'Y';");
        engine.Evaluate("list[0]").Should().Be("a");
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("list[9]").Should().Be(JsValue.Undefined);
        list.Items.Should().Equal("a", "b", "c");

        Assert.Throws<JavaScriptException>(() => engine.Execute("'use strict'; list[0] = 'X';"));
        Assert.Throws<JavaScriptException>(() => engine.Execute("'use strict'; list.length = 0;"));
        Assert.Throws<JavaScriptException>(() => engine.Execute("'use strict'; list[9] = 'Y';"));
    }

    [Fact]
    public void NamedPropertiesRemainOrdinary()
    {
        var engine = CreateEngine(out _);

        engine.Execute("'use strict'; list.named = 1; list.named = 2;");
        engine.Evaluate("list.named").Should().Be(2);
        engine.Evaluate("delete list.named").Should().Be(true);
        engine.Evaluate("'named' in list").Should().Be(false);

        engine.Execute("Object.defineProperty(list, 'computed', { get: function () { return 'via-getter'; } });");
        engine.Evaluate("list.computed").Should().Be("via-getter");
    }

    [Fact]
    public void DeleteRefusesIndicesTheCollectionOwnsAndLength()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("delete list[0]").Should().Be(false);
        engine.Evaluate("delete list.length").Should().Be(false);
        engine.Evaluate("list[0]").Should().Be("a");

        // an index the collection does not own deletes vacuously, like any absent property
        engine.Evaluate("delete list[99]").Should().Be(true);

        Assert.Throws<JavaScriptException>(() => engine.Execute("'use strict'; delete list[0];"));
        Assert.Throws<JavaScriptException>(() => engine.Execute("'use strict'; delete list.length;"));
        engine.Execute("'use strict'; delete list[99];");
    }

    [Fact]
    public void DefinePropertyIsRefusedForEveryIndexKeyAndForLength()
    {
        var engine = CreateEngine(out _);

        Assert.Throws<JavaScriptException>(() => engine.Execute("Object.defineProperty(list, '0', { value: 'X' });"));
        Assert.Throws<JavaScriptException>(() => engine.Execute("Object.defineProperty(list, 'length', { value: 0 });"));

        // stricter than WebIDL, which allows an expando at an index >= length: refusing those too keeps the
        // projection and the property bag from disagreeing once the live collection grows.
        Assert.Throws<JavaScriptException>(() => engine.Execute("Object.defineProperty(list, '99', { value: 'X' });"));

        // named keys define ordinarily
        engine.Execute("Object.defineProperty(list, 'named', { value: 'ok', enumerable: true });");
        engine.Evaluate("list.named").Should().Be("ok");
    }

    [Fact]
    public void FreezingTheCollectionFailsTheWayFreezingAPlatformObjectDoes()
    {
        var engine = CreateEngine(out _);

        // SetIntegrityLevel has to redefine every own key as non-configurable, which the index refusal denies.
        Assert.Throws<JavaScriptException>(() => engine.Execute("'use strict'; Object.freeze(list);"));
        engine.Evaluate("Object.isFrozen(list)").Should().Be(false);
        engine.Evaluate("list[0]").Should().Be("a");
    }

    // ---------------------------------------------------------------------------------------------------
    // Live collection
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCollectionIsReadLiveWithNoStaleCacheServingARemovedIndex()
    {
        var engine = CreateEngine(out var list, attachArrayPrototype: true, "a", "b", "c");

        // warm every read lane on the current contents first
        engine.Evaluate("list[0] + list[1] + list[2] + list.length + list.join('-') + JSON.stringify(list)");
        engine.Evaluate("list[0] + list[1] + list[2] + list.length + list.join('-') + JSON.stringify(list)");

        list.Items.RemoveAt(2);

        engine.Evaluate("list.length").Should().Be(2);
        engine.Evaluate("list[2]").Should().Be(JsValue.Undefined);
        engine.Evaluate("list.hasOwnProperty(2)").Should().Be(false);
        engine.Evaluate("2 in list").Should().Be(false);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1");
        engine.Evaluate("list.join('-')").Should().Be("a-b");
        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a","b"]""");
        engine.Evaluate("[...list].join('-')").Should().Be("a-b");

        list.Items.Add("c");
        list.Items.Add("d");

        engine.Evaluate("list.length").Should().Be(4);
        engine.Evaluate("list[3]").Should().Be("d");
        engine.Evaluate("list.hasOwnProperty(3)").Should().Be(true);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1,2,3");
        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a","b","c","d"]""");

        list.Items.Clear();

        engine.Evaluate("list.length").Should().Be(0);
        engine.Evaluate("list[0]").Should().Be(JsValue.Undefined);
        engine.Evaluate("Object.keys(list).length").Should().Be(0);
        engine.Evaluate("JSON.stringify(list)").Should().Be("[]");
        engine.Evaluate("[...list].length").Should().Be(0);
    }

    [Fact]
    public void AnIndexedReadInAWarmLoopFollowsTheCollectionAsItChanges()
    {
        var engine = CreateEngine(out var list, attachArrayPrototype: true, "a", "b", "c");
        engine.SetValue("swap", (int i, string v) => list.Items[i] = v);

        const string Script = "var s = ''; for (var i = 0; i < list.length; i++) { s += list[i]; } s;";

        engine.Evaluate(Script).Should().Be("abc");
        engine.Evaluate(Script).Should().Be("abc");

        list.Items[1] = "B";
        engine.Evaluate(Script).Should().Be("aBc");

        engine.Evaluate("swap(0, 'A'); " + Script).Should().Be("ABc");
    }

    [Fact]
    public void AnElementAppearingAfterAPrototypeReadWasCachedShadowsItFromTheVeryNextRead()
    {
        // The string-keyed spelling is the one that reaches the prototype-method inline cache. A host keeps its
        // own-property set outside the engine, so nothing the engine versions moves when an element appears —
        // the cache stays honest only because the own miss is re-established (through the class's own-value
        // hook, i.e. against the live collection) before every consult.
        var engine = CreateEngine(out var list, attachArrayPrototype: true, []);
        list.Items.Clear();
        engine.Execute("Array.prototype['0'] = 'from-prototype';");

        var script = "var out = []; for (var i = 0; i < 4; i++) { if (i === 2) { grow(); } out.push(list['0']); } out.join(',');";
        engine.SetValue("grow", () => list.Items.Insert(0, "own"));

        engine.Evaluate(script).Should().Be("from-prototype,from-prototype,own,own");
    }

    [Fact]
    public void APrototypeMethodStaysResolvableWhileTheCollectionChangesUnderIt()
    {
        var engine = CreateEngine(out var list, attachArrayPrototype: true, "a");
        engine.SetValue("grow", () => list.Items.Add("b"));

        // The counterpart: a non-index name keeps resolving through the same node while elements come and go.
        engine.Evaluate("var out = []; for (var i = 0; i < 3; i++) { out.push(list.join('')); grow(); } out.join(',');")
            .Should().Be("a,ab,abb");
    }

    // ---------------------------------------------------------------------------------------------------
    // Named-getter subclass
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ASubclassMayAddALiveNamedGetterByDelegatingToBase()
    {
        var engine = new Engine();
        var list = new NamedGetterHostList(engine, "a", "b", "c");
        engine.SetValue("list", list);

        engine.Evaluate("list.last").Should().Be("c");
        engine.Evaluate("list.hasOwnProperty('last')").Should().Be(true);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1,2,last");

        // the index and length answers still come from the base class
        engine.Evaluate("list[1]").Should().Be("b");
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("list.join('-')").Should().Be("a-b-c");
        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a","b","c"]""");

        list.Items.Add("d");
        engine.Evaluate("list.last").Should().Be("d");
        engine.Evaluate("list.length").Should().Be(4);
    }
}
