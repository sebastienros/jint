using System.Reflection;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Guards the prototype-method inline cache in <c>JintMemberExpression</c> (resolves <c>obj.method</c>
/// when <c>method</c> lives on the receiver's direct prototype). The cache bypasses the receiver's
/// <c>[[Get]]</c> on a hit, so it must stay consistent with ordinary lookup across the mutations that
/// invalidate it.
/// </summary>
public class PrototypeMethodCacheTests
{
    /// <summary>
    /// Tripwire: the cache short-circuits a receiver's <c>[[Get]]</c> on a hit, so it is only sound for
    /// objects whose <c>[[Get]]</c> is <em>ordinary for string-keyed lookups that miss the own property</em>
    /// — i.e. "no own property ⇒ walk the prototype". Every <see cref="ObjectInstance"/> subclass overriding
    /// <c>Get(JsValue, JsValue)</c> must therefore be one of:
    /// <list type="bullet">
    /// <item>non-ordinary for named keys — must set <c>InternalTypes.ExoticGet</c> in its constructor so the
    /// cache skips it as both receiver and prototype (Proxy traps, TypedArray canonical indices,
    /// IteratorResult inline value/done, interop member resolution, module exports); or</item>
    /// <item>non-ordinary only for keys it also reports from <c>GetOwnProperty</c> (array integer indices and
    /// <c>length</c>) — safe <em>without</em> the flag, because the populate path defers to the full
    /// <c>Get</c> whenever <c>GetOwnProperty</c> is non-undefined. <see cref="ArrayInstance"/> is the one
    /// such case, and must stay unflagged so <c>arr.push</c> / <c>arr.pop</c> stay cacheable. That covers
    /// only the own properties it has <em>at populate time</em>: an array's elements live in its own
    /// dense/sparse storage, which the hot element paths write without moving <c>_propertiesVersion</c>, so
    /// an entry for an <em>index-like</em> name could not be invalidated when an element appeared later. The
    /// cache therefore declines to create one — see <c>VersionWitnessesOwnProperty</c>, and
    /// <see cref="AnArrayElementAddedAfterCachingShadowsThePrototypeIndex"/>.</item>
    /// </list>
    /// When a new override appears this test fails, forcing the author to classify it rather than silently
    /// regressing (or mis-caching) prototype-method reads.
    /// </summary>
    [Test]
    public void EveryGetOverrideIsClassifiedForThePrototypeMethodCache()
    {
        var assembly = typeof(Engine).Assembly;
        var getParameters = new[] { typeof(JsValue), typeof(JsValue) };

        var overriders = assembly.GetTypes()
            .Where(t => typeof(ObjectInstance).IsAssignableFrom(t) && t != typeof(ObjectInstance))
            .Where(t => t.GetMethod(
                "Get",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                binder: null,
                getParameters,
                modifiers: null) is not null)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Non-ordinary for named keys — each sets InternalTypes.ExoticGet in its constructor
        // (ArrayLikeWrapper inherits it from the ObjectWrapper base constructor).
        var exoticGet = new[]
        {
            "Jint.Native.Iterator.IteratorResult",
            "Jint.Native.JsArguments", // virtual read mode: [[Get]] answers from args/bindings without materializing properties
            "Jint.Native.JsError", // virtual message slot: [[Get]] answers 'message' from a field until materialized
            "Jint.Native.JsProxy",
            "Jint.Native.JsTypedArray",
            "Jint.Runtime.Interop.ArrayLikeWrapper",
            "Jint.Runtime.Interop.NamespaceReference",
            "Jint.Runtime.Interop.ObjectWrapper",
            "Jint.Runtime.Modules.ModuleNamespace",
        };

        // Index/length-only exotic — ordinary for named keys, safe (and intentionally) unflagged.
        var safeWithoutFlag = new[]
        {
            "Jint.Native.Array.ArrayInstance",
        };

        // Not exotic at all: the override is a sealed `=> base.Get(...)` whose only purpose is to stop a host
        // subclass from going exotic underneath the guarantees the base class makes for it — for
        // ArrayLikeObject, the lanes that resolve an indexed read without calling Get at all; for
        // NamedPropertyObject, the coherence matrix it derives and seals. The constructor therefore declares
        // PropertyAccessSemantics.Ordinary, which is what CanCacheAgainstVersions keys the receiver-side
        // exemption on: ReadFromNonPlainReceiver is the only lane that reaches the cache with such a receiver,
        // and it re-establishes the own miss — here through the class's TryGetOwnPropertyValue, which consults
        // the live host state — before every consult. So a member appearing behind the engine's back cannot be
        // shadowed by a stale entry, even though the host's own-property set lives outside the engine and moves
        // no version.
        //
        // Receiver only. On the *holder* side VersionWitnessesOwnProperty refuses both, and must keep refusing
        // them: the BuiltinShapeMode carve-out there is sound precisely because such an object keeps its whole
        // own-property set in engine storage and versions it, which is the one thing a live host projection
        // does not do. Neither class can enter that mode (InitializeBuiltinShape is private protected), so the
        // refusal stands by construction — pinned in ArrayLikeObjectLaneTests.
        var ordinaryByConstruction = new[]
        {
            "Jint.Native.Object.ArrayLikeObject",
            "Jint.Native.Object.NamedPropertyObject",
        };

        var expected = exoticGet
            .Concat(safeWithoutFlag)
            .Concat(ordinaryByConstruction)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        overriders.Should().Equal(expected);
    }

    [Test]
    public void ResolvesPrototypeMethodRepeatedly()
    {
        const string script = """
            function C() { this.v = 0; }
            C.prototype.inc = function () { return ++this.v; };
            var c = new C(), last = 0;
            for (var i = 0; i < 50; i++) { last = c.inc(); }
            last;
            """;
        new Engine().Evaluate(script).AsNumber().Should().Be(50);
    }

    [Test]
    public void OwnPropertyAddedAfterCachingShadowsPrototype()
    {
        // First reads hit the prototype method and populate the cache; assigning an own property of the
        // same name must shadow it on every subsequent read (the receiver version bump invalidates).
        const string script = """
            function C() {}
            C.prototype.tag = function () { return 'proto'; };
            var c = new C(), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 3) { c.tag = function () { return 'own'; }; }
                out.push(c.tag());
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("proto,proto,proto,own,own");
    }

    [Test]
    public void PrototypeMethodRedefinedAfterCachingIsReResolved()
    {
        const string script = """
            function C() {}
            C.prototype.tag = function () { return 'v1'; };
            var c = new C(), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { C.prototype.tag = function () { return 'v2'; }; }
                out.push(c.tag());
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("v1,v1,v2,v2");
    }

    [Test]
    public void PrototypeMethodDeletedAfterCachingFallsBack()
    {
        const string script = """
            function C() {}
            C.prototype.tag = function () { return 'v1'; };
            var c = new C(), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { delete C.prototype.tag; }
                out.push(typeof c.tag === 'function' ? c.tag() : 'gone');
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("v1,v1,gone,gone");
    }

    [Test]
    public void PrototypeReassignedAfterCachingIsHonoured()
    {
        const string script = """
            var protoA = { tag: function () { return 'A'; } };
            var protoB = { tag: function () { return 'B'; } };
            var o = Object.create(protoA), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { Object.setPrototypeOf(o, protoB); }
                out.push(o.tag());
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("A,A,B,B");
    }

    /// <summary>
    /// The cached entry stays valid only while the receiver's <c>_propertiesVersion</c> can still prove the
    /// name is not an own property of it. An array's elements are not in the property bag that version
    /// describes, so an index-named entry would never be invalidated by one appearing.
    /// <para>
    /// Note the <em>string</em>-literal index: <c>a['0']</c> is a string-keyed member read and takes this
    /// cache, whereas <c>a[0]</c> and <c>a[k]</c> resolve through the dense-array lanes, which never consult
    /// it. <see cref="ANumericIndexReadIsUnaffectedBecauseItTakesTheDenseLane"/> pins that difference so the
    /// exclusion is not mistaken for something broader than it is.
    /// </para>
    /// </summary>
    [Test]
    public void AnArrayElementAddedAfterCachingShadowsThePrototypeIndex()
    {
        const string script = """
            Array.prototype[0] = 'proto';
            var a = [], out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { a.push('own'); }
                out.push(a['0']);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("proto,proto,own,own");
    }

    [Test]
    public void AnArrayElementAssignedAfterCachingShadowsThePrototypeIndex()
    {
        const string script = """
            Array.prototype[1] = 'proto';
            var a = [], out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { a[1] = 'own'; }
                out.push(a['1']);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("proto,proto,own,own");
    }

    [TestCase("a[0]")]
    [TestCase("a[k]")]
    public void ANumericIndexReadIsUnaffectedBecauseItTakesTheDenseLane(string read)
    {
        // The two spellings that never reach the prototype-method cache: a numeric literal and a computed
        // index both resolve through the dense-array lanes, which re-read the element every time. They were
        // already correct before the exclusion and must stay that way — the scope of the bug is exactly the
        // string-keyed spelling.
        var script = $$"""
            Array.prototype[0] = 'proto';
            var a = [], k = 0, out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { a.push('own'); }
                out.push({{read}});
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("proto,proto,own,own");
    }

    [Test]
    public void AnArrayPrototypeMethodUnderANonIndexNameStaysCorrectWhileElementsChange()
    {
        // The counterpart of the two above: the exclusion is by name, so only index-like names give it up.
        // A method name keeps resolving through the same node while elements come and go — the shape the
        // exclusion had to be written narrowly enough not to disturb (arr.push / arr.pop / arr.slice).
        const string script = """
            Array.prototype.tag = function () { return 'v1:' + this.length; };
            var a = [], out = [];
            for (var i = 0; i < 4; i++) {
                a[a.length] = i;
                if (i === 2) { Array.prototype.tag = function () { return 'v2:' + this.length; }; }
                out[out.length] = a.tag();
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("v1:1,v1:2,v2:3,v2:4");
    }

    /// <summary>
    /// A function's <c>name</c> and <c>length</c> are configurable own properties held in fields rather than in
    /// the property bag, so defining one had to start moving the version itself — otherwise a define following a
    /// delete re-created the own property invisibly. (<c>prototype</c> shares the storage but not the hazard: it
    /// is non-configurable, so it can never leave the own set and come back.)
    /// <para>
    /// The <c>delete</c> is load-bearing and must come first: it is what makes the opening read miss the own
    /// property and cache <c>Function.prototype</c>'s. Defining over a <c>name</c> that is still own is an
    /// own-property hit throughout, caches nothing, and was always correct.
    /// </para>
    /// </summary>
    [Test]
    public void AFunctionOwnPropertyDefinedAfterCachingShadowsThePrototype()
    {
        const string script = """
            function f() {}
            delete f.name;
            var out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { Object.defineProperty(f, 'name', { value: 'own', configurable: true }); }
                out.push(f.name === '' ? 'inherited' : f.name);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("inherited,inherited,own,own");
    }

    [Test]
    public void AFunctionOwnLengthDefinedAfterCachingShadowsThePrototype()
    {
        const string script = """
            function f() {}
            delete f.length;
            var out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { Object.defineProperty(f, 'length', { value: 7, configurable: true }); }
                out.push(f.length);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("0,0,7,7");
    }

    /// <summary>
    /// A constructor's lazily created <c>prototype</c> object holds <c>constructor</c> in a field too, and it
    /// is the holder here rather than the receiver — the same version, read from the other side of the cache.
    /// This one needs no unusual spelling at all: an ordinary constructor, an ordinary instance, an ordinary
    /// <c>delete</c>.
    /// </summary>
    [Test]
    public void ConstructorDeletedFromThePrototypeAfterCachingFallsBack()
    {
        const string script = """
            function C() {}
            function name(ctor) {
                if (ctor === C) { return 'C'; }
                if (ctor === Object) { return 'Object'; }
                return 'other';
            }
            var c = new C(), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { delete C.prototype.constructor; }
                out.push(name(c.constructor));
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("C,C,Object,Object");
    }

    /// <summary>
    /// The control that scopes all of the above. A receiver whose own-property set the engine <em>does</em>
    /// store, and therefore versions, has always shadowed correctly — so the defect was never the
    /// prototype-method cache's design, only the set of receivers whose version was taken to describe them.
    /// <see cref="OwnPropertyAddedAfterCachingShadowsPrototype"/> is the same statement for a constructed
    /// object; this covers the two prototype sources a plain object can have.
    /// </summary>
    [TestCase("var proto = { shared: 'from-proto' }; var o = Object.create(proto);")]
    [TestCase("Object.prototype.shared = 'from-proto'; var o = {};")]
    public void APlainObjectShadowsItsPrototypeOnTheReadAfterTheOwnPropertyAppears(string setup)
    {
        var script = $$"""
            {{setup}}
            var out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { o.shared = 'own'; }
                out.push(o.shared);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("from-proto,from-proto,own,own");
    }

    /// <summary>
    /// A built-in whose own properties live in a shared layout plus a per-realm descriptor array is a valid
    /// holder for this cache: its whole own-property set is engine storage, and every change to that set
    /// moves <c>_propertiesVersion</c> (materializing a slot does not, but materialization does not change
    /// the name set). Such a type reaches the protected <see cref="ObjectInstance"/> constructor and so
    /// carries derived <c>OrdinaryGet</c>, which <c>VersionWitnessesOwnProperty</c> otherwise refuses
    /// outright — that flag stands for "own properties live outside the engine", which is exactly what is
    /// not true here, hence the carve-out.
    /// <para>
    /// These are the #2823 guards restated for that holder family. Each one warms the cache and then makes
    /// the change the version has to witness; a stale answer is the failure.
    /// </para>
    /// </summary>
    [Test]
    public void OwnPropertyAddedAfterCachingShadowsASharedLayoutPrototype()
    {
        const string script = """
            var o = Object.create(Math), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 3) { o.abs = function () { return 'own'; }; }
                out.push(o.abs(-1));
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("1,1,1,own,own");
    }

    [Test]
    public void ASharedLayoutPrototypeMemberRedefinedAfterCachingIsReResolved()
    {
        const string script = """
            var o = Object.create(Math), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 3) { Object.defineProperty(Math, 'abs', { value: function () { return 'patched'; } }); }
                out.push(o.abs(-1));
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("1,1,1,patched,patched");
    }

    [Test]
    public void AMemberDeletedFromASharedLayoutPrototypeAfterCachingFallsBack()
    {
        // Deleting a declared member is what drops the whole object back to the ordinary dictionary, so this
        // covers the cache surviving the representation change as well as the name leaving.
        const string script = """
            var o = Object.create(Math), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 3) { delete Math.abs; }
                out.push(typeof o.abs);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("function,function,function,undefined,undefined");
    }

    [Test]
    public void AMemberAddedToASharedLayoutPrototypeAfterCachingIsSeen()
    {
        // The mirror image: the opening reads miss everywhere and cache nothing, then the name appears on
        // the prototype through the side dictionary a shaped host keeps for post-initialization additions.
        const string script = """
            var o = Object.create(Math), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 3) { Math.brandNew = 'added'; }
                out.push(String(o.brandNew));
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("undefined,undefined,undefined,added,added");
    }

    [Test]
    public void AWarmSharedLayoutPrototypeReadKeepsServingTheSameMember()
    {
        // The other half: with nothing invalidating it, a warm read must keep answering — same value, and
        // the same function object, which is what the per-realm descriptor array guarantees.
        const string script = """
            var o = Object.create(Math), out = [], first = null, stable = true;
            for (var i = 0; i < 50; i++) {
                var f = o.abs;
                if (first === null) { first = f; } else if (f !== first) { stable = false; }
                out.push(f(-2));
            }
            (stable ? 'stable' : 'unstable') + ':' + out[0] + ':' + out[49] + ':' + out.length;
            """;
        new Engine().Evaluate(script).AsString().Should().Be("stable:2:2:50");
    }

    [Test]
    public void AGeneratorInstanceReadsItsPrototypeMethodCorrectlyAcrossInvalidation()
    {
        // GeneratorPrototype is the shared-layout prototype the design named: gen.next is read through a
        // generator instance, so the holder is the newly eligible family and the receiver is an ordinary
        // object that can shadow.
        const string script = """
            function* g() { yield 1; yield 2; yield 3; }
            var it = g(), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { it.next = function () { return { value: 'own', done: false }; }; }
                out.push(it.next().value);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("1,2,own,own");
    }

    [Test]
    public void PrototypeGetterIsReInvokedEachRead()
    {
        // An accessor on the prototype must run its getter on every read, not be frozen as a value.
        const string script = """
            var n = 0;
            var proto = {};
            Object.defineProperty(proto, 'next', { get: function () { return ++n; } });
            var o = Object.create(proto), out = [];
            for (var i = 0; i < 4; i++) { out.push(o.next); }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("1,2,3,4");
    }
}
