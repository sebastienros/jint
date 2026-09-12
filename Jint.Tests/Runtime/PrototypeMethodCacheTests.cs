using System.Reflection;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Guards the prototype-member inline cache in <c>JintMemberExpression</c> (resolves <c>obj.member</c>
/// when <c>member</c> lives anywhere on the receiver's prototype chain). The cache bypasses the receiver's
/// <c>[[Get]]</c> on a hit, so it must stay consistent with ordinary lookup across the mutations that
/// invalidate it.
/// <para>
/// An entry that spans more than one link carries a second obligation the direct-prototype form never had:
/// it must keep proving that <b>no object between the receiver and the holder</b> has gained an own property
/// of that name, and that the chain still runs through exactly the objects it was recorded against. The
/// deep-chain tests at the end of this file are that obligation, mutation by mutation.
/// </para>
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
        // PropertyAccessSemantics.Ordinary, which is what CanCacheAgainstReceiverVersion keys the receiver-side
        // exemption on: ReadFromNonPlainReceiver is the only lane that reaches the cache with such a receiver,
        // and it re-establishes the own miss — here through the class's TryGetOwnPropertyValue, which consults
        // the live host state — before every consult. So a member appearing behind the engine's back cannot be
        // shadowed by a stale entry, even though the host's own-property set lives outside the engine and moves
        // no version.
        //
        // Receiver only. On the *holder* side — and, since an entry may span several links, on every
        // INTERMEDIATE link too — VersionWitnessesOwnProperty refuses both, and must keep refusing them: the
        // BuiltinShapeMode carve-out there is sound precisely because such an object keeps its whole
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
    // ---------------------------------------------------------------------------------------------------
    // Chains deeper than the direct prototype. An entry may now span intermediate links, and each of them is
    // a fact the entry has to keep proving: that the link is still the object that occupied its position, and
    // that it still has no own property of this name. Everything below mutates exactly one of those.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ResolvesAMemberDeclaredSeveralLinksUpRepeatedly()
    {
        const string script = """
            class Base { get tag() { return 'base'; } run() { return 'ran'; } }
            class Middle extends Base { }
            class Derived extends Middle { }
            class Leaf extends Derived { }
            var leaf = new Leaf(), out = [];
            for (var i = 0; i < 50; i++) { out.push(leaf.tag + ':' + leaf.run()); }
            out[0] + '|' + out[49] + '|' + out.length;
            """;
        new Engine().Evaluate(script).AsString().Should().Be("base:ran|base:ran|50");
    }

    /// <summary>
    /// The new obligation, stated at its sharpest: the name appears on a link <em>between</em> the receiver
    /// and the holder. Neither the receiver's version nor the holder's moves, so an entry that recorded only
    /// those two would go on serving the root's member after the middle level started declaring its own.
    /// </summary>
    [TestCase(1, "Middle")]
    [TestCase(2, "Derived")]
    [TestCase(3, "Leaf")]
    public void AnIntermediateLinkThatGainsTheNameAfterCachingShadowsTheDeepHolder(int level, string shadowedBy)
    {
        var script = $$"""
            class Base { }
            class Middle extends Base { }
            class Derived extends Middle { }
            class Leaf extends Derived { }
            Base.prototype.tag = 'base';
            var levels = [null, Middle.prototype, Derived.prototype, Leaf.prototype];
            var leaf = new Leaf(), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { levels[{{level}}].tag = '{{shadowedBy}}'; }
                out.push(leaf.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be($"base,base,{shadowedBy},{shadowedBy},{shadowedBy}");
    }

    /// <summary>
    /// The same fact on the way out: a name that shadowed from an intermediate link and is then deleted must
    /// stop shadowing. The entry recorded while the shadow stood is for the intermediate as holder, so this
    /// exercises the opposite transition from
    /// <see cref="AnIntermediateLinkThatGainsTheNameAfterCachingShadowsTheDeepHolder"/>.
    /// </summary>
    [Test]
    public void ANameDeletedFromAnIntermediateLinkAfterCachingUncoversTheDeepHolder()
    {
        const string script = """
            class Base { }
            class Middle extends Base { }
            class Derived extends Middle { }
            Base.prototype.tag = 'base';
            Derived.prototype.tag = 'middle';
            var d = new Derived(), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { delete Derived.prototype.tag; }
                out.push(d.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("middle,middle,base,base,base");
    }

    /// <summary>
    /// <c>[[SetPrototypeOf]]</c> on a link in the middle. Nothing about the receiver, the holder or any
    /// version moves — only the link between them stops pointing where it pointed.
    /// </summary>
    [Test]
    public void AnIntermediateLinkReassignedAfterCachingIsHonoured()
    {
        const string script = """
            var rootA = { tag: 'A' };
            var rootB = { tag: 'B' };
            var middle = Object.create(rootA);
            var o = Object.create(middle), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { Object.setPrototypeOf(middle, rootB); }
                out.push(o.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("A,A,B,B,B");
    }

    /// <summary>
    /// A brand-new object spliced in between the receiver and the chain it was cached against. The recorded
    /// links are all still there, still unversioned and still linked to one another — the chain simply no
    /// longer starts where it started.
    /// </summary>
    [Test]
    public void ALinkInsertedInFrontOfACachedChainIsHonoured()
    {
        const string script = """
            var root = { tag: 'root' };
            var middle = Object.create(root);
            var o = Object.create(middle), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { Object.setPrototypeOf(o, Object.create(middle, { tag: { value: 'spliced' } })); }
                out.push(o.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("root,root,spliced,spliced,spliced");
    }

    /// <summary>The holder's own half of the contract, at a distance: redefined, then removed.</summary>
    [Test]
    public void ADeepHolderMemberRedefinedOrDeletedAfterCachingIsReResolved()
    {
        const string script = """
            var root = { tag: 'v1' };
            var o = Object.create(Object.create(Object.create(root))), out = [];
            for (var i = 0; i < 6; i++) {
                if (i === 2) { root.tag = 'v2'; }
                if (i === 4) { delete root.tag; }
                out.push(o.tag === undefined ? 'gone' : o.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("v1,v1,v2,v2,gone,gone");
    }

    /// <summary>
    /// A getter three links up receives the <em>receiver</em> as <c>this</c>, never the holder — and a second
    /// receiver read through the same warmed site gets its own answer rather than the first one's. The site is
    /// pinned on receiver identity, so this is what stops an entry leaking one object's state to another.
    /// </summary>
    [Test]
    public void ADeepGetterIsInvokedWithTheReceiverAndNotWithTheHolder()
    {
        const string script = """
            class Base { get who() { return this.tag; } }
            class Middle extends Base { }
            class Leaf extends Middle { constructor(tag) { super(); this.tag = tag; } }
            var a = new Leaf('a'), b = new Leaf('b'), out = [];
            for (var i = 0; i < 4; i++) { out.push(a.who); out.push(b.who); }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("a,b,a,b,a,b,a,b");
    }

    /// <summary>
    /// An exotic link resolves the rest of the read itself. A <c>Proxy</c> makes that observable: its
    /// <c>get</c> trap is user code with a side effect, and it must run once per read for as long as the read
    /// passes through it.
    /// </summary>
    [Test]
    public void AProxyInTheMiddleOfTheChainRunsItsTrapOnEveryRead()
    {
        const string script = """
            var calls = 0;
            var proxied = new Proxy({}, { get: function (t, k, r) { return k === 'tag' ? 'trap' + (++calls) : t[k]; } });
            var middle = Object.create(proxied);
            var o = Object.create(middle), out = [];
            for (var i = 0; i < 4; i++) { out.push(o.tag); }
            out.join(',') + '|' + calls;
            """;
        new Engine().Evaluate(script).AsString().Should().Be("trap1,trap2,trap3,trap4|4");
    }

    /// <summary>
    /// The array exclusion, applied to an intermediate link. An array's elements are not in the property bag
    /// its <c>_propertiesVersion</c> describes, so no entry may be recorded that would have to prove an
    /// index-like name is <em>absent</em> from one.
    /// <para>
    /// The index is a string literal on purpose: <c>o['0']</c> is a string-keyed member read and takes this
    /// lane, where <c>o[0]</c> takes the dense-element lanes — the same distinction
    /// <see cref="ANumericIndexReadIsUnaffectedBecauseItTakesTheDenseLane"/> pins for a receiver.
    /// </para>
    /// </summary>
    [Test]
    public void AnArrayElementAppearingOnAnIntermediateLinkShadowsTheDeepHolder()
    {
        const string script = """
            var root = { '0': 'root' };
            var middle = [];
            Object.setPrototypeOf(middle, root);
            var o = Object.create(middle), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { middle[0] = 'element'; }
                out.push(o['0']);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("root,root,element,element,element");
    }

    /// <summary>
    /// Past the depth an entry may record, the read still resolves — the walk simply hands the rest of the
    /// chain back to the ordinary path — and it stays correct across a mutation of the holder, which is what
    /// proves nothing was quietly cached on the way there.
    /// </summary>
    [Test]
    public void AChainDeeperThanTheCacheBoundStillResolvesAndStaysCorrect()
    {
        const string script = """
            var root = { tag: 'v1' };
            var o = root;
            for (var d = 0; d < 14; d++) { o = Object.create(o); }
            var out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { root.tag = 'v2'; }
                out.push(o.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("v1,v1,v2,v2,v2");
    }

    /// <summary>
    /// A name absent from the whole chain answers <c>undefined</c> every time, and starts resolving the
    /// moment something on the chain declares it. The read is served by the same walk that looks for an
    /// entry, so this is the path where an absent name must not be mistaken for a cacheable outcome.
    /// </summary>
    [Test]
    public void ANameAbsentFromAWholeChainAnswersUndefinedUntilTheChainDeclaresIt()
    {
        const string script = """
            class Base { }
            class Middle extends Base { }
            class Leaf extends Middle { }
            var leaf = new Leaf(), out = [];
            for (var i = 0; i < 6; i++) {
                if (i === 2) { Base.prototype.tag = 'base'; }
                if (i === 4) { Middle.prototype.tag = 'middle'; }
                out.push(leaf.tag === undefined ? 'absent' : leaf.tag);
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("absent,absent,base,base,middle,middle");
    }

    /// <summary>
    /// A prototype-less chain: the walk ends on a null <c>[[Prototype]]</c> rather than on
    /// <c>Object.prototype</c>, which is the one place the walk answers <c>undefined</c> from its own
    /// knowledge instead of from a probe.
    /// </summary>
    [Test]
    public void AChainEndingInANullPrototypeResolvesBothItsHitsAndItsMisses()
    {
        const string script = """
            var root = Object.create(null);
            root.tag = 'root';
            var o = Object.create(Object.create(root)), out = [];
            for (var i = 0; i < 4; i++) { out.push(o.tag + ':' + (o.missing === undefined ? 'absent' : '?')); }
            out[0] + '|' + out[3];
            """;
        new Engine().Evaluate(script).AsString().Should().Be("root:absent|root:absent");
    }

    /// <summary>
    /// A method call resolves its callee through a different entry point on the same node than a read does,
    /// and the two share the cache fields — so the deep form has to be exercised through both.
    /// </summary>
    [Test]
    public void ADeepMethodCallIsReResolvedWhenAnIntermediateLinkShadowsIt()
    {
        const string script = """
            class Base { run() { return 'base'; } }
            class Middle extends Base { }
            class Leaf extends Middle { }
            var leaf = new Leaf(), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 2) { Middle.prototype.run = function () { return 'middle'; }; }
                out.push(leaf.run());
            }
            out.join(',');
            """;
        new Engine().Evaluate(script).AsString().Should().Be("base,base,middle,middle,middle");
    }
}
