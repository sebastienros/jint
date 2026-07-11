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
}
