using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the shape-building copy-idiom targets: object spread, object rest (destructuring and
/// function parameters), Object.fromEntries and Object.assign onto a fresh empty target all start
/// their result object in incremental shape-building mode (ObjectConstructor.ConstructShapeBuilding),
/// so the copied properties intern a shared hidden class instead of building a per-object dictionary.
/// Every observable behavior — key order, duplicate handling, getter evaluation, __proto__ semantics,
/// integer-like-key fallback, generator suspension — must be unchanged from the dictionary path.
/// </summary>
public class ObjectSpreadShapeTests
{
    // Shape-building targets are scoped to the genuine copy idiom: a literal only opts in when it
    // contains a spread. A non-fast literal WITHOUT a spread (here forced onto the general build path
    // by a function-valued property) stays on the plain dictionary target — it is a one-off layout
    // with no reuse, and opting it into incremental shape-building exposed untested deopt edges that
    // corrupted real-world bundler output (babel-standalone). This white-box test pins the boundary;
    // the behavioral guard is the Jint.Tests.CommonScripts suite.
    [Fact]
    public void NonSpreadGeneralLiteralIsNotShapeBuilding()
    {
        var engine = new Engine();

        // Non-fast (function-valued property), no spread => must NOT be shape mode.
        var general = Assert.IsType<JsObject>(engine.Evaluate("({ a: 1, b: 2, fn: function () {} })"));
        Assert.True((general._type & InternalTypes.ShapeMode) == InternalTypes.Empty);
        Assert.True((general._type & InternalTypes.ShapeBuilding) == InternalTypes.Empty);

        // The same key set reached through a spread => shape mode (the intended optimization).
        engine.Execute("var src = { a: 1, b: 2 };");
        var spread = Assert.IsType<JsObject>(engine.Evaluate("({ ...src, fn: function () {} })"));
        Assert.True((spread._type & InternalTypes.ShapeMode) != InternalTypes.Empty);

        // Behavior is identical either way.
        Assert.Equal("1,2", engine.Evaluate("(function () { var o = { a: 1, b: 2, fn: function () {} }; return o.a + ',' + o.b; })()").AsString());
    }

    [Fact]
    public void SpreadCopiesLayoutOrderGettersOnceAndSkipsNonEnumerables()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var calls = 0;
                var sym = Symbol('s');
                var src = { a: 1, get b() { calls++; return 2; }, c: 3 };
                Object.defineProperty(src, 'hidden', { value: 42, enumerable: false });
                src[sym] = 'symval';
                var o = { ...src };
                var bDesc = Object.getOwnPropertyDescriptor(o, 'b');
                return JSON.stringify({
                    keys: Object.keys(o),
                    values: [o.a, o.b, o.c],
                    calls: calls,
                    hasHidden: Object.prototype.hasOwnProperty.call(o, 'hidden'),
                    symCopied: o[sym],
                    bIsData: bDesc.get === undefined && bDesc.value === 2
                });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","b","c"],"values":[1,2,3],"calls":1,"hasHidden":false,"symCopied":"symval","bIsData":true}""", result);
    }

    [Fact]
    public void SpreadBeyondInlineCapacityKeepsLayout()
    {
        var engine = new Engine();
        // More than four properties exercises the overflow slot array growth in TryShapeAdd.
        var result = engine.Evaluate("""
            (function () {
                var src = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6 };
                var o = { ...src };
                return Object.keys(o).join() + '|' + Object.values(o).join();
            })()
            """).AsString();

        Assert.Equal("a,b,c,d,e,f|1,2,3,4,5,6", result);
    }

    [Fact]
    public void SpreadFromArraySourceFallsBackWithNumericFirstOrder()
    {
        var engine = new Engine();
        // Integer-like keys never enter a shape (the CreateDataProperty pre-check routes them to the
        // dictionary), so spec enumeration order — integer indices ascending, then string keys — holds.
        Assert.Equal("0,1,2", engine.Evaluate("Object.keys({ ...[10, 20, 30] }).join()").AsString());
        Assert.Equal("10,20,30", engine.Evaluate("Object.values({ ...[10, 20, 30] }).join()").AsString());
        Assert.Equal("0,1,x", engine.Evaluate("Object.keys({ ...[1, 2], x: 3 }).join()").AsString());
        // String wrapper object: index-like own keys take the same dictionary fallback.
        Assert.Equal("a,b", engine.Evaluate("Object.values({ ...Object('ab') }).join()").AsString());
        Assert.Equal("0,1", engine.Evaluate("Object.keys({ ...Object('ab') }).join()").AsString());
    }

    [Fact]
    public void SpreadStaticKeyCombinationsAndDuplicateKeyKeepsFirstPositionLastValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var src = { b: 2, c: 3 };
                var before = { x: 1, ...src };
                var after = { ...src, x: 1 };
                // CreateDataProperty on an existing shape key overwrites in place: last value wins,
                // first-occurrence position kept.
                var dup = { a: 1, ...{ a: 2 } };
                return JSON.stringify({
                    before: Object.keys(before),
                    after: Object.keys(after),
                    dupKeys: Object.keys(dup),
                    dupValue: dup.a
                });
            })()
            """).AsString();

        Assert.Equal("""{"before":["x","b","c"],"after":["b","c","x"],"dupKeys":["a"],"dupValue":2}""", result);
    }

    [Fact]
    public void SpreadWithTrailingAccessorDeoptsWithCorrectAttributes()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var src = { a: 1, b: 2 };
                var o = { ...src, get g() { return this.a + this.b; } };
                var d = Object.getOwnPropertyDescriptor(o, 'g');
                return JSON.stringify({
                    keys: Object.keys(o),
                    g: o.g,
                    hasGetter: typeof d.get === 'function',
                    enumerable: d.enumerable,
                    configurable: d.configurable
                });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","b","g"],"g":3,"hasGetter":true,"enumerable":true,"configurable":true}""", result);
    }

    [Fact]
    public void SpreadWithStaticProtoKeySetsPrototypeWithoutOwnKey()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var p = { inherited: 'yes' };
                var o = { ...{ a: 1 }, __proto__: p };
                return JSON.stringify({
                    keys: Object.keys(o),
                    protoOk: Object.getPrototypeOf(o) === p,
                    ownProto: Object.prototype.hasOwnProperty.call(o, '__proto__'),
                    inherited: o.inherited
                });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a"],"protoOk":true,"ownProto":false,"inherited":"yes"}""", result);
    }

    [Fact]
    public void SpreadOfOwnProtoDataPropertyCreatesOwnKeyWithoutChangingPrototype()
    {
        var engine = new Engine();
        // Spread uses CreateDataProperty semantics: an own "__proto__" data property on the source is
        // copied as an own data property, and the target's prototype stays Object.prototype — the
        // inherited __proto__ accessor must NOT fire (contrast with Object.assign below).
        var result = engine.Evaluate("""
            (function () {
                var p = { marker: 'proto' };
                var src = { ['__proto__']: p };
                var o = { ...src };
                return JSON.stringify({
                    ownProto: Object.prototype.hasOwnProperty.call(o, '__proto__'),
                    protoUnchanged: Object.getPrototypeOf(o) === Object.prototype,
                    value: o['__proto__'] === p
                });
            })()
            """).AsString();

        Assert.Equal("""{"ownProto":true,"protoUnchanged":true,"value":true}""", result);
    }

    [Fact]
    public void ObjectRestOrderExclusionAndEmptyRest()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var { a, ...rest } = { a: 1, b: 2, c: 3 };
                var { x, ...empty } = { x: 1 };
                return JSON.stringify({
                    keys: Object.keys(rest),
                    b: rest.b,
                    c: rest.c,
                    hasA: 'a' in rest,
                    emptyKeys: Object.keys(empty),
                    emptyIsObject: typeof empty === 'object'
                });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["b","c"],"b":2,"c":3,"hasA":false,"emptyKeys":[],"emptyIsObject":true}""", result);
    }

    [Fact]
    public void ObjectRestNestedPatternAndMemberExpressionTarget()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var { x: { y, ...inner }, ...outer } = { x: { y: 1, z: 2, w: 3 }, q: 4 };
                var a;
                var target = {};
                ({ a, ...target.rest } = { a: 1, b: 2, c: 3 });
                return JSON.stringify({
                    inner: Object.keys(inner),
                    outer: Object.keys(outer),
                    z: inner.z,
                    q: outer.q,
                    member: Object.keys(target.rest),
                    memberB: target.rest.b
                });
            })()
            """).AsString();

        Assert.Equal("""{"inner":["z","w"],"outer":["q"],"z":2,"q":4,"member":["b","c"],"memberB":2}""", result);
    }

    [Fact]
    public void ObjectRestParameterForm()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function f({ a, ...r }) { return JSON.stringify({ keys: Object.keys(r), b: r.b, c: r.c }); }
                return f({ a: 1, b: 2, c: 3 });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["b","c"],"b":2,"c":3}""", result);
    }

    [Fact]
    public void ObjectRestFromArraySourceFallsBackWithNumericOrder()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var { 0: first, ...rest } = ['x', 'y', 'z'];
                return JSON.stringify({ first: first, keys: Object.keys(rest), one: rest[1], two: rest[2] });
            })()
            """).AsString();

        Assert.Equal("""{"first":"x","keys":["1","2"],"one":"y","two":"z"}""", result);
    }

    [Fact]
    public void FromEntriesKeepsOrderAndDuplicateKeysLastValueWinsFirstPosition()
    {
        var engine = new Engine();
        Assert.Equal("x,y,z", engine.Evaluate("""Object.keys(Object.fromEntries([['x', 1], ['y', 2], ['z', 3]])).join()""").AsString());
        // Duplicate key re-add on the building shape: value overwritten in place.
        Assert.Equal("""{"a":3,"b":2}""", engine.Evaluate("""JSON.stringify(Object.fromEntries([['a', 1], ['b', 2], ['a', 3]]))""").AsString());
    }

    [Fact]
    public void FromEntriesSymbolAndIntegerLikeKeys()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var sym = Symbol('k');
                var o = Object.fromEntries([[sym, 1], ['a', 2]]);
                var o2 = Object.fromEntries([['5', 'five'], ['a', 'x'], ['0', 'zero']]);
                return JSON.stringify({
                    symVal: o[sym],
                    a: o.a,
                    keys2: Object.keys(o2),
                    five: o2[5],
                    zero: o2[0]
                });
            })()
            """).AsString();

        Assert.Equal("""{"symVal":1,"a":2,"keys2":["0","5","a"],"five":"five","zero":"zero"}""", result);
    }

    [Fact]
    public void FromEntriesPastMegamorphicGuardKeepsAllEntriesInOrder()
    {
        var engine = new Engine();
        // 80 distinct keys trips the MaxShapeProperties guard mid-build (at key #65); the object
        // converts to dictionary mode and the remaining entries continue there — everything present,
        // insertion order kept.
        var result = engine.Evaluate("""
            (function () {
                var entries = [];
                for (var i = 0; i < 80; i++) entries.push(['k' + i, i]);
                var o = Object.fromEntries(entries);
                var keys = Object.keys(o);
                var ok = keys.length === 80;
                for (var i = 0; i < 80; i++) {
                    ok = ok && keys[i] === ('k' + i) && o['k' + i] === i;
                }
                return ok;
            })()
            """).AsBoolean();

        Assert.True(result);
    }

    [Fact]
    public void AssignOverwritesAcrossSources()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var o = Object.assign({}, { a: 1, b: 2 }, { b: 3, c: 4 });
                return JSON.stringify({ keys: Object.keys(o), a: o.a, b: o.b, c: o.c });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","b","c"],"a":1,"b":3,"c":4}""", result);
    }

    [Fact]
    public void AssignInvokesInheritedProtoSetterWithoutOwnKey()
    {
        var engine = new Engine();
        // Spec pin: Object.assign uses Set semantics, so a source's own "__proto__" data property must
        // fire Object.prototype's inherited __proto__ accessor on the target — the target's prototype
        // changes and NO own key appears. This must hold identically on the shape-building target
        // (Set walks the prototype chain first for keys absent from the shape).
        var result = engine.Evaluate("""
            (function () {
                var p = { marker: 'proto' };
                var src = { ['__proto__']: p };
                var srcOwn = Object.prototype.hasOwnProperty.call(src, '__proto__');
                var o = Object.assign({}, src);
                return JSON.stringify({
                    srcOwn: srcOwn,
                    protoOk: Object.getPrototypeOf(o) === p,
                    ownProto: Object.prototype.hasOwnProperty.call(o, '__proto__'),
                    marker: o.marker,
                    keys: Object.keys(o)
                });
            })()
            """).AsString();

        Assert.Equal("""{"srcOwn":true,"protoOk":true,"ownProto":false,"marker":"proto","keys":[]}""", result);
    }

    [Fact]
    public void AssignOntoNonEmptyAndFrozenTargetsIsUnchanged()
    {
        var engine = new Engine();
        // Non-virgin targets never switch representation.
        var result = engine.Evaluate("""
            (function () {
                var t = { x: 0 };
                var r = Object.assign(t, { a: 1 });
                return JSON.stringify({ same: r === t, keys: Object.keys(t), a: t.a });
            })()
            """).AsString();
        Assert.Equal("""{"same":true,"keys":["x","a"],"a":1}""", result);

        var frozen = engine.Evaluate("""
            (function () {
                try { Object.assign(Object.freeze({ z: 1 }), { a: 2 }); return 'no-throw'; }
                catch (e) { return e instanceof TypeError ? 'TypeError' : 'other'; }
            })()
            """).AsString();
        Assert.Equal("TypeError", frozen);

        var frozenEmpty = engine.Evaluate("""
            (function () {
                try { Object.assign(Object.freeze({}), { a: 2 }); return 'no-throw'; }
                catch (e) { return e instanceof TypeError ? 'TypeError' : 'other'; }
            })()
            """).AsString();
        Assert.Equal("TypeError", frozenEmpty);
    }

    [Fact]
    public void AssignResultSupportsDeleteAndDefineProperty()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var o = Object.assign({}, { a: 1, b: 2, c: 3 });
                delete o.b;
                Object.defineProperty(o, 'd', { value: 4, enumerable: false });
                return JSON.stringify({ keys: Object.keys(o), a: o.a, d: o.d, hasB: 'b' in o });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","c"],"a":1,"d":4,"hasB":false}""", result);
    }

    [Fact]
    public void GeneratorSuspensionMidLiteralKeepsExactVisibilityAndDoesNotRerunSpread()
    {
        var engine = new Engine();
        // Yield between property definitions: the spread has already copied when the frame suspends
        // (mutations to the source while suspended must not appear), the yielded-value property is
        // defined only on resume, and later properties follow — insertion order intact. The half-built
        // object rides in the suspend data with its ShapeBuilding flag, so the resume keeps extending
        // the same shape.
        var result = engine.Evaluate("""
            (function () {
                var src = { a: 1 };
                function* g() {
                    return { ...src, b: yield 'suspended', c: 3 };
                }
                var it = g();
                var first = it.next().value;
                src.late = 'nope'; // mutated while suspended — must not be re-copied
                var res = it.next(2).value;
                return JSON.stringify({
                    first: first,
                    keys: Object.keys(res),
                    a: res.a, b: res.b, c: res.c,
                    late: 'late' in res
                });
            })()
            """).AsString();

        Assert.Equal("""{"first":"suspended","keys":["a","b","c"],"a":1,"b":2,"c":3,"late":false}""", result);
    }

    [Fact]
    public void GeneratorSuspensionWithAccessorLiteralStaysOnDictionaryPath()
    {
        var engine = new Engine();
        // A getter in the literal keeps the target unshaped (accessor defines deopt shapes); the
        // suspension bookkeeping must behave exactly as before.
        var result = engine.Evaluate("""
            (function () {
                function* g() {
                    return { ...{ x: 1 }, get y() { return this.x + 1; }, z: yield 'p' };
                }
                var it = g();
                it.next();
                var res = it.next(9).value;
                return JSON.stringify({ keys: Object.keys(res), x: res.x, y: res.y, z: res.z });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["x","y","z"],"x":1,"y":2,"z":9}""", result);
    }

    [Fact]
    public void CopyIdiomsWithSameLayoutShareTheInternedShape()
    {
        var engine = new Engine();
        engine.Execute("var src = { a: 1, b: 2, c: 3 };");

        var spread1 = Assert.IsType<JsObject>(engine.Evaluate("({ ...src })"));
        var spread2 = Assert.IsType<JsObject>(engine.Evaluate("({ ...src })"));
        var rest = Assert.IsType<JsObject>(engine.Evaluate("(function () { var { x, ...r } = { x: 0, a: 1, b: 2, c: 3 }; return r; })()"));
        var fromEntries = Assert.IsType<JsObject>(engine.Evaluate("Object.fromEntries([['a', 1], ['b', 2], ['c', 3]])"));
        var assigned = Assert.IsType<JsObject>(engine.Evaluate("Object.assign({}, src)"));

        Assert.NotSame(spread1, spread2);
        Assert.True((spread1._type & InternalTypes.ShapeMode) != InternalTypes.Empty);

        // Same prototype + same key sequence => the very same interned Shape instance, across all
        // four copy idioms.
        Assert.Same(spread1.ShapeOf, spread2.ShapeOf);
        Assert.Same(spread1.ShapeOf, rest.ShapeOf);
        Assert.Same(spread1.ShapeOf, fromEntries.ShapeOf);
        Assert.Same(spread1.ShapeOf, assigned.ShapeOf);
    }

    [Fact]
    public void DuplicateClassFieldInitializersKeepSingleSlotAcrossHotInstances()
    {
        var engine = new Engine();
        // Past the hot-constructor promote threshold (16) instances build in shape mode; the duplicate
        // field initializer re-adds an existing key on the building shape — single key, last value.
        var result = engine.Evaluate("""
            (function () {
                class A { x = 1; x = 2 }
                var last;
                for (var i = 0; i < 25; i++) last = new A();
                return JSON.stringify({ keys: Object.keys(last), x: last.x });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["x"],"x":2}""", result);
    }

    [Fact]
    public void SpreadResultStaysFullyMutable()
    {
        var engine = new Engine();
        // Post-build mutations on a shaped result: adds extend the building shape, deletes and
        // defineProperty deopt — all observably plain-object behavior.
        var result = engine.Evaluate("""
            (function () {
                var o = { ...{ a: 1, b: 2 } };
                o.c = 3;
                delete o.a;
                Object.defineProperty(o, 'd', { value: 4, enumerable: true, writable: false, configurable: false });
                var dDesc = Object.getOwnPropertyDescriptor(o, 'd');
                return JSON.stringify({ keys: Object.keys(o), c: o.c, d: o.d, dWritable: dDesc.writable });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["b","c","d"],"c":3,"d":4,"dWritable":false}""", result);
    }

    // ---- Source-shape adoption fast path: `{ ...src }` where src is itself a shape-mode object ----

    [Fact]
    public void SpreadOfShapeSourceAdoptsSourceInternedShape()
    {
        var engine = new Engine();
        engine.Execute("var src = { a: 1, b: 2, c: 3, d: 4 };");

        var src = Assert.IsType<JsObject>(engine.Evaluate("src"));
        var clone1 = Assert.IsType<JsObject>(engine.Evaluate("({ ...src })"));
        var clone2 = Assert.IsType<JsObject>(engine.Evaluate("({ ...src })"));

        // The clone reuses the source's exact interned leaf shape (same prototype + same key
        // sequence + same attributes), so the member inline cache stays monomorphic across the
        // source and every clone. Distinct objects, one shape.
        Assert.NotSame(src, clone1);
        Assert.NotSame(clone1, clone2);
        Assert.True((clone1._type & InternalTypes.ShapeMode) != InternalTypes.Empty);
        Assert.Same(src.ShapeOf, clone1.ShapeOf);
        Assert.Same(clone1.ShapeOf, clone2.ShapeOf);

        // `{ ...src, x }` extends the adopted shape by one interned transition — the same shape a
        // streamed build would land on — so those clones also share a shape with each other.
        var ext1 = Assert.IsType<JsObject>(engine.Evaluate("({ ...src, x: 1 })"));
        var ext2 = Assert.IsType<JsObject>(engine.Evaluate("({ ...src, x: 2 })"));
        Assert.Same(ext1.ShapeOf, ext2.ShapeOf);
        Assert.NotSame(src.ShapeOf, ext1.ShapeOf);
        Assert.Equal("a,b,c,d,x", engine.Evaluate("Object.keys({ ...src, x: 1 }).join()").AsString());
    }

    [Fact]
    public void SpreadCloneHasIndependentSlotStorageInlineAndOverflow()
    {
        var engine = new Engine();
        // Writing an own slot of the clone must never reach back into the source, for both in-object
        // (a..d) and overflow (e, f) slots. Spread copies value references, so a nested object stays
        // shared (shallow), but the top-level slots are separate storage.
        var result = engine.Evaluate("""
            (function () {
                var src = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, nested: { v: 1 } };
                var t = { ...src };
                t.a = 99;   // inline slot
                t.f = 88;   // overflow slot
                t.nested.v = 42; // shallow: mutates the shared referent
                return JSON.stringify({
                    srcA: src.a, srcF: src.f, tA: t.a, tF: t.f,
                    sharedNested: src.nested.v, sameNestedRef: src.nested === t.nested
                });
            })()
            """).AsString();

        Assert.Equal("""{"srcA":1,"srcF":6,"tA":99,"tF":88,"sharedNested":42,"sameNestedRef":true}""", result);
    }

    [Fact]
    public void SpreadOfCustomPrototypeSourceDeclinesAndResultHasObjectPrototype()
    {
        var engine = new Engine();
        // A source with a non-Object.prototype prototype must not have its shape adopted onto an
        // Object.prototype target (interned shapes are rooted per prototype). The fast path declines
        // and streams; only the source's own enumerable data property is copied and the result's
        // prototype is Object.prototype.
        var result = engine.Evaluate("""
            (function () {
                var proto = { inheritedKey: 'p' };
                var src = Object.create(proto);
                src.a = 1;
                src.b = 2;
                var o = { ...src };
                return JSON.stringify({
                    keys: Object.keys(o),
                    hasInheritedOwn: Object.prototype.hasOwnProperty.call(o, 'inheritedKey'),
                    protoIsObjectProto: Object.getPrototypeOf(o) === Object.prototype,
                    a: o.a, b: o.b
                });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","b"],"hasInheritedOwn":false,"protoIsObjectProto":true,"a":1,"b":2}""", result);
    }

    [Fact]
    public void SpreadOfShapeSourceWithIntegerLikeKeyKeepsSpecOrderAndValues()
    {
        var engine = new Engine();
        // A fast-built literal can be shape-mode while carrying a digit-leading key; own-key order then
        // needs the numeric-first sort, produced on enumeration by the shape->dictionary deopt. Adopting
        // such a shape is faithful because the same deopt applies to the source and the clone alike.
        var result = engine.Evaluate("""
            (function () {
                var src = { a: 1, '0': 2, b: 3 };
                var o = { ...src };
                return JSON.stringify({ keys: Object.keys(o), zero: o['0'], a: o.a, b: o.b });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["0","a","b"],"zero":2,"a":1,"b":3}""", result);
    }

    [Fact]
    public void SpreadOfSymbolCarryingShapeSourceDeclinesButCopiesSymbol()
    {
        var engine = new Engine();
        // Symbols live outside the shape, so a source carrying an enumerable symbol declines the
        // wholesale adopt and streams — the enumerable symbol is copied, a non-enumerable one skipped.
        var result = engine.Evaluate("""
            (function () {
                var e = Symbol('e'), h = Symbol('h');
                var src = { a: 1, b: 2 };
                src[e] = 'enum';
                Object.defineProperty(src, h, { value: 'hidden', enumerable: false });
                var o = { ...src };
                return JSON.stringify({
                    keys: Object.keys(o),
                    a: o.a, b: o.b,
                    enumSym: o[e],
                    hiddenSym: o[h] === undefined
                });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","b"],"a":1,"b":2,"enumSym":"enum","hiddenSym":true}""", result);
    }

    [Fact]
    public void SpreadNotFirstElementStreamsAndKeepsOrder()
    {
        var engine = new Engine();
        // When the spread is not the first element the target already has slots, so the wholesale adopt
        // is declined and the source keys stream in after the leading static key.
        var result = engine.Evaluate("""
            (function () {
                var src = { b: 2, c: 3 };
                var o = { a: 1, ...src, d: 4 };
                return JSON.stringify({ keys: Object.keys(o), values: Object.values(o) });
            })()
            """).AsString();

        Assert.Equal("""{"keys":["a","b","c","d"],"values":[1,2,3,4]}""", result);
    }

    [Fact]
    public void SpreadTrailingKeyDoesNotLeakIntoAdoptedSource()
    {
        var engine = new Engine();
        // The adopted clone must own its storage: adding a trailing key extends only the clone.
        var result = engine.Evaluate("""
            (function () {
                var src = { a: 1, b: 2 };
                var o = { ...src, c: 3 };
                return JSON.stringify({
                    cloneKeys: Object.keys(o),
                    srcKeys: Object.keys(src),
                    srcHasC: Object.prototype.hasOwnProperty.call(src, 'c')
                });
            })()
            """).AsString();

        Assert.Equal("""{"cloneKeys":["a","b","c"],"srcKeys":["a","b"],"srcHasC":false}""", result);
    }
}
