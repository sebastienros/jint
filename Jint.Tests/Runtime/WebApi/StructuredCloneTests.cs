#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>structuredClone</c> as the HTML Standard specifies it —
/// https://html.spec.whatwg.org/multipage/structured-data.html#dom-structuredclone.
/// </summary>
public class StructuredCloneTests
{
    private static Engine WebEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        // Every "does this throw, and with what?" assertion goes through this, so a DataCloneError is
        // distinguished from the TypeError the argument conversion raises.
        engine.Execute("function err(f) { try { f(); return 'no error'; } catch (e) { return e.name; } }");
        return engine;
    }

    private static string Err(Engine engine, string body) => engine.Evaluate("err(function() { " + body + " })").AsString();

    // ---------------------------------------------------------------- the function itself

    [Fact]
    public void IsAWebIdlOperationOnTheGlobal()
    {
        var engine = WebEngine();

        engine.Evaluate("typeof structuredClone").AsString().Should().Be("function");
        engine.Evaluate("structuredClone.name").AsString().Should().Be("structuredClone");
        engine.Evaluate("structuredClone.length").AsNumber().Should().Be(1);

        // An operation is not a constructor.
        Err(engine, "new structuredClone(1)").Should().Be("TypeError");
    }

    [Fact]
    public void RequiresTheValueArgument()
    {
        var engine = WebEngine();

        Err(engine, "structuredClone()").Should().Be("TypeError");

        // ... but an explicit undefined is a value like any other.
        engine.Evaluate("structuredClone(undefined) === undefined").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- primitives

    [Theory]
    [InlineData("undefined")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("'text'")]
    [InlineData("123456789012345678901234567890n")]
    public void ReturnsAPrimitiveAsItself(string expression)
    {
        var engine = WebEngine();

        engine.Evaluate($"structuredClone({expression}) === {expression} || Object.is(structuredClone({expression}), {expression})")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void PreservesNegativeZeroAndNaN()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.is(structuredClone(-0), -0)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Number.isNaN(structuredClone(NaN))").AsBoolean().Should().BeTrue();
        engine.Evaluate("structuredClone(Infinity)").AsNumber().Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void RefusesASymbol()
    {
        var engine = WebEngine();

        Err(engine, "structuredClone(Symbol('x'))").Should().Be("DataCloneError");
        Err(engine, "structuredClone({ s: Symbol('x') })").Should().Be("DataCloneError");
    }

    // ---------------------------------------------------------------- ordinary objects

    [Fact]
    public void ClonesTheOwnEnumerableStringKeyedProperties()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var source = { a: 1, b: 'two', c: { deep: true } };
            Object.defineProperty(source, 'hidden', { value: 'no', enumerable: false });
            source[Symbol('key')] = 'no';
            var clone = structuredClone(source);");

        engine.Evaluate("clone !== source").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.a").AsNumber().Should().Be(1);
        engine.Evaluate("clone.b").AsString().Should().Be("two");
        engine.Evaluate("clone.c.deep").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.c !== source.c").AsBoolean().Should().BeTrue();

        // Neither a non-enumerable property nor a symbol-keyed one is in EnumerableOwnProperties.
        engine.Evaluate("'hidden' in clone").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertySymbols(clone).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void RecreatesEveryPropertyAsAPlainDataProperty()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var source = {};
            Object.defineProperty(source, 'frozen', { value: 1, enumerable: true, writable: false, configurable: false });
            var descriptor = Object.getOwnPropertyDescriptor(structuredClone(source), 'frozen');");

        // CreateDataProperty, so the clone's attributes are the defaults whatever the source's were.
        engine.Evaluate("descriptor.value").AsNumber().Should().Be(1);
        engine.Evaluate("descriptor.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("descriptor.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("descriptor.configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void GivesEveryCloneTheCurrentRealmsObjectPrototype()
    {
        var engine = WebEngine();

        // A class instance loses its prototype: the algorithm records properties, never the [[Prototype]].
        engine.Execute("class Point { constructor() { this.x = 1; } greet() { return 'hi'; } } var clone = structuredClone(new Point());");
        engine.Evaluate("Object.getPrototypeOf(clone) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone instanceof Point").AsBoolean().Should().BeFalse();
        engine.Evaluate("clone.x").AsNumber().Should().Be(1);

        // And a null-prototype object gains one: deserialization creates "a new Object in targetRealm".
        engine.Execute("var bare = Object.create(null); bare.a = 1; var bareClone = structuredClone(bare);");
        engine.Evaluate("Object.getPrototypeOf(bareClone) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("bareClone.a").AsNumber().Should().Be(1);
    }

    [Fact]
    public void InvokesGettersAndClonesWhatTheyReturn()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var calls = 0;
            var source = { get value() { calls++; return { nested: calls }; } };
            var clone = structuredClone(source);");

        engine.Evaluate("calls").AsNumber().Should().Be(1);
        engine.Evaluate("clone.value.nested").AsNumber().Should().Be(1);

        // The clone holds the produced value as data, so reading it again does not re-run the getter.
        engine.Evaluate("clone.value").AsObject().Should().BeSameAs(engine.Evaluate("clone.value").AsObject());
        engine.Evaluate("calls").AsNumber().Should().Be(1);
    }

    [Fact]
    public void WalksDepthFirstInPropertyOrder()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var order = [];
            var source = {
                get a() { order.push('a'); return { get inner() { order.push('inner'); return 1; } }; },
                get b() { order.push('b'); return 2; }
            };
            structuredClone(source);");

        // A value's own graph is finished before its next sibling is read, which is the order the
        // specification's recursion produces and the only order a getter can tell apart.
        engine.Evaluate("order.join(',')").AsString().Should().Be("a,inner,b");
    }

    [Fact]
    public void SkipsAKeyAnEarlierGetterDeleted()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var source = { get a() { delete this.b; return 1; }, b: 2 };
            var clone = structuredClone(source);");

        // The key list is snapshot up front, so 'b' is still on it — the HasOwnProperty re-check is what
        // keeps it out of the clone.
        engine.Evaluate("clone.a").AsNumber().Should().Be(1);
        engine.Evaluate("'b' in clone").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void PropagatesWhatAGetterThrows()
    {
        var engine = WebEngine();

        Err(engine, "structuredClone({ get boom() { throw new RangeError('nope'); } })").Should().Be("RangeError");
    }

    // ---------------------------------------------------------------- identity and cycles

    [Fact]
    public void PreservesCyclesThroughTheMemoryMap()
    {
        var engine = WebEngine();

        engine.Execute("var source = { name: 'root' }; source.self = source; var clone = structuredClone(source);");

        engine.Evaluate("clone.self === clone").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone !== source").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void PreservesSharedIdentity()
    {
        var engine = WebEngine();

        engine.Execute("var shared = { v: 1 }; var clone = structuredClone({ x: shared, y: shared, z: [shared] });");

        engine.Evaluate("clone.x === clone.y").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.z[0] === clone.x").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.x !== shared").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void GivesEachCallItsOwnMemory()
    {
        var engine = WebEngine();

        engine.Execute("var source = { a: 1 };");
        engine.Evaluate("structuredClone(source) !== structuredClone(source)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesADeeplyNestedGraphWithoutOverflowingTheStack()
    {
        var engine = WebEngine();

        // The specification's algorithm recurses once per edge and bounds nothing; a native stack overflow
        // takes the process down rather than raising something a host can catch, so the walk is iterative and
        // this depth simply works.
        engine.Execute(@"
            var root = {};
            var cursor = root;
            for (var i = 0; i < 100000; i++) { cursor.next = {}; cursor = cursor.next; }
            var clone = structuredClone(root);
            var depth = 0;
            for (var node = clone; node.next; node = node.next) { depth++; }");

        engine.Evaluate("depth").AsNumber().Should().Be(100000);
        engine.Evaluate("clone !== root").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesADeeplyNestedArrayAndMapChainWithoutOverflowingTheStack()
    {
        var engine = WebEngine();

        // The array and Map frames are separate from the plain-object one, so they get their own depth pin.
        engine.Execute(@"
            var arrayRoot = [];
            var arrayCursor = arrayRoot;
            var mapRoot = new Map();
            var mapCursor = mapRoot;
            for (var i = 0; i < 100000; i++) {
                var nextArray = []; arrayCursor.push(nextArray); arrayCursor = nextArray;
                var nextMap = new Map(); mapCursor.set('next', nextMap); mapCursor = nextMap;
            }
            var arrayClone = structuredClone(arrayRoot);
            var mapClone = structuredClone(mapRoot);
            var arrayDepth = 0;
            for (var a = arrayClone; a.length; a = a[0]) { arrayDepth++; }
            var mapDepth = 0;
            for (var m = mapClone; m.size; m = m.get('next')) { mapDepth++; }");

        engine.Evaluate("arrayDepth").AsNumber().Should().Be(100000);
        engine.Evaluate("mapDepth").AsNumber().Should().Be(100000);
    }

    // ---------------------------------------------------------------- installation

    [Fact]
    public void IsInstalledOnlyWhenTheFeatureIsNamed()
    {
        new Engine().Evaluate("typeof structuredClone").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis(WebApiFeatures.Console)).Evaluate("typeof structuredClone").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis()).Evaluate("typeof structuredClone").AsString().Should().Be("function");
    }

    [Fact]
    public void IsAnUnmaterializedLazyGlobalWithTheWebIdlAttributes()
    {
        var engine = WebEngine();
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("structuredClone");

        descriptor.Should().BeOfType<Jint.Runtime.Descriptors.Specialized.LazyPropertyDescriptor<Engine>>();
        descriptor._value.Should().BeNull();

        // A WebIDL operation on the global is writable, enumerable and configurable.
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = WebEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof structuredClone')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof structuredClone").AsString().Should().Be("function");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = WebEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var kept = structuredClone({ a: 1 });");
        engine.Realm.GlobalObject.GetOwnProperty("structuredClone")._value.Should().NotBeNull();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Realm.GlobalObject.GetOwnProperty("structuredClone")._value.Should().BeNull();
        engine.Evaluate("structuredClone({ a: 1 }).a").AsNumber().Should().Be(1);
        engine.Evaluate("typeof kept").AsString().Should().Be("undefined");
    }

    // ---------------------------------------------------------------- arrays

    [Fact]
    public void ClonesAnArrayWithItsLengthHolesAndExtraProperties()
    {
        var engine = WebEngine();

        engine.Execute("var source = [1, , 3]; source.extra = 'x'; source.length = 5; var clone = structuredClone(source);");

        engine.Evaluate("Array.isArray(clone)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(clone) === Array.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.length").AsNumber().Should().Be(5);
        engine.Evaluate("clone[0]").AsNumber().Should().Be(1);
        engine.Evaluate("1 in clone").AsBoolean().Should().BeFalse();
        engine.Evaluate("clone[2]").AsNumber().Should().Be(3);
        engine.Evaluate("clone.extra").AsString().Should().Be("x");
    }

    [Fact]
    public void ClonesNestedArrays()
    {
        var engine = WebEngine();

        engine.Execute("var clone = structuredClone([[1, [2, [3]]], { a: [4] }]);");

        engine.Evaluate("clone[0][1][1][0]").AsNumber().Should().Be(3);
        engine.Evaluate("clone[1].a[0]").AsNumber().Should().Be(4);
    }

    // ---------------------------------------------------------------- Date, RegExp, boxed primitives

    [Fact]
    public void ClonesADateByItsTimeValue()
    {
        var engine = WebEngine();

        engine.Evaluate("structuredClone(new Date(1234567890123)).getTime()").AsNumber().Should().Be(1234567890123d);
        engine.Evaluate("Number.isNaN(structuredClone(new Date(NaN)).getTime())").AsBoolean().Should().BeTrue();
        engine.Evaluate("structuredClone(new Date(0)) instanceof Date").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesARegExpBySourceAndFlagsButNotLastIndex()
    {
        var engine = WebEngine();

        engine.Execute("var source = /a(b+)c/gi; source.lastIndex = 5; var clone = structuredClone(source);");

        engine.Evaluate("clone.source").AsString().Should().Be("a(b+)c");
        engine.Evaluate("clone.flags").AsString().Should().Be("gi");

        // [[OriginalSource]] and [[OriginalFlags]] serialize; lastIndex does not, so the clone starts at 0.
        engine.Evaluate("clone.lastIndex").AsNumber().Should().Be(0);
        engine.Evaluate("clone.exec('xxABBBC')[1]").AsString().Should().Be("BBB");
        engine.Evaluate("clone instanceof RegExp").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesABoxedPrimitiveByItsDataSlot()
    {
        var engine = WebEngine();

        engine.Evaluate("typeof structuredClone(new Number(3))").AsString().Should().Be("object");
        engine.Evaluate("structuredClone(new Number(3)).valueOf()").AsNumber().Should().Be(3);
        engine.Evaluate("structuredClone(new String('hi')).valueOf()").AsString().Should().Be("hi");
        engine.Evaluate("structuredClone(new Boolean(true)).valueOf()").AsBoolean().Should().BeTrue();
        engine.Evaluate("structuredClone(Object(7n)).valueOf() === 7n").AsBoolean().Should().BeTrue();

        // The data slot is all that is recorded: a boxed primitive's own properties are not walked.
        engine.Execute("var boxed = new Number(3); boxed.tag = 'x';");
        engine.Evaluate("structuredClone(boxed).tag === undefined").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- Map and Set

    [Fact]
    public void ClonesAMapWithItsEntriesInOrder()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var key = { id: 1 };
            var source = new Map([['a', 1], [key, { v: 2 }], [3, 'three']]);
            var clone = structuredClone(source);");

        engine.Evaluate("clone instanceof Map").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.size").AsNumber().Should().Be(3);
        engine.Evaluate("Array.from(clone.keys()).map(function(k) { return typeof k; }).join(',')").AsString()
            .Should().Be("string,object,number");
        engine.Evaluate("clone.get('a')").AsNumber().Should().Be(1);
        engine.Evaluate("clone.has(key)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Array.from(clone.values())[1].v").AsNumber().Should().Be(2);
    }

    [Fact]
    public void ClonesASetWithItsValuesInOrder()
    {
        var engine = WebEngine();

        engine.Execute("var clone = structuredClone(new Set(['a', 2, { v: 3 }]));");

        engine.Evaluate("clone instanceof Set").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.size").AsNumber().Should().Be(3);
        engine.Evaluate("Array.from(clone)[2].v").AsNumber().Should().Be(3);
    }

    [Fact]
    public void LetsACollectionContainItself()
    {
        var engine = WebEngine();

        // The container is registered in memory before its contents are walked, which is what makes this
        // terminate at all.
        engine.Execute("var map = new Map(); map.set('self', map); var mapClone = structuredClone(map);");
        engine.Evaluate("mapClone.get('self') === mapClone").AsBoolean().Should().BeTrue();

        engine.Execute("var set = new Set(); set.add(set); var setClone = structuredClone(set);");
        engine.Evaluate("setClone.has(setClone)").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- errors

    [Theory]
    [InlineData("Error")]
    [InlineData("EvalError")]
    [InlineData("RangeError")]
    [InlineData("ReferenceError")]
    [InlineData("SyntaxError")]
    [InlineData("TypeError")]
    [InlineData("URIError")]
    public void KeepsAWhitelistedErrorName(string name)
    {
        var engine = WebEngine();

        engine.Execute($"var clone = structuredClone(new {name}('boom'));");

        engine.Evaluate("clone.name").AsString().Should().Be(name);
        engine.Evaluate($"clone instanceof {name}").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.message").AsString().Should().Be("boom");
    }

    [Fact]
    public void ReducesAnyOtherErrorNameToError()
    {
        var engine = WebEngine();

        engine.Execute("var renamed = new TypeError('x'); renamed.name = 'MyError'; var clone = structuredClone(renamed);");
        engine.Evaluate("clone.name").AsString().Should().Be("Error");
        engine.Evaluate("Object.getPrototypeOf(clone) === Error.prototype").AsBoolean().Should().BeTrue();

        // AggregateError is not on the list either, and its `errors` is not carried.
        engine.Execute("var aggregate = structuredClone(new AggregateError([new Error('inner')], 'outer'));");
        engine.Evaluate("aggregate.name").AsString().Should().Be("Error");
        engine.Evaluate("aggregate.message").AsString().Should().Be("outer");
        engine.Evaluate("aggregate.errors === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CarriesOnlyTheErrorsNameMessageAndStack()
    {
        var engine = WebEngine();

        engine.Execute("var source = new TypeError('boom'); source.extra = 'dropped'; var clone = structuredClone(source);");

        // An Error's serialization does not set `deep`, so nothing but the three recorded fields survives.
        engine.Evaluate("clone.extra === undefined").AsBoolean().Should().BeTrue();

        // `message` comes back as a writable, non-enumerable, configurable own property.
        engine.Evaluate("Object.keys(clone).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyDescriptor(clone, 'message').enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyDescriptor(clone, 'message').writable").AsBoolean().Should().BeTrue();

        // The stack is the "interesting accompanying data" both specifications invite a user agent to carry,
        // and it is the source's, not the clone site's.
        engine.Evaluate("clone.stack").AsString().Should().Be(engine.Evaluate("source.stack").AsString());
    }

    [Fact]
    public void OmitsAnAbsentOrAccessorMessage()
    {
        var engine = WebEngine();

        // An error with no own message inherits Error.prototype.message, and the clone must not turn that
        // into an own property.
        engine.Execute("var clone = structuredClone(new Error());");
        engine.Evaluate("Object.getOwnPropertyDescriptor(clone, 'message') === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.message").AsString().Should().Be("");

        // An accessor `message` is read as undefined rather than invoked.
        engine.Execute(@"
            var invoked = false;
            var source = new Error('ignored');
            Object.defineProperty(source, 'message', { get: function() { invoked = true; return 'from getter'; }, configurable: true });
            var accessorClone = structuredClone(source);");
        engine.Evaluate("invoked").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyDescriptor(accessorClone, 'message') === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesADomExceptionByNameAndMessage()
    {
        var engine = WebEngine();

        engine.Execute("var source = new DOMException('gone', 'AbortError'); var clone = structuredClone(source);");

        // https://webidl.spec.whatwg.org/#idl-DOMException declares the interface [Serializable].
        engine.Evaluate("clone !== source").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone instanceof DOMException").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.name").AsString().Should().Be("AbortError");
        engine.Evaluate("clone.message").AsString().Should().Be("gone");
        engine.Evaluate("clone.code").AsNumber().Should().Be(20);
        engine.Evaluate("clone.stack").AsString().Should().Be(engine.Evaluate("source.stack").AsString());
    }

    // ---------------------------------------------------------------- ArrayBuffer and views

    [Fact]
    public void CopiesAnArrayBuffersBytes()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var source = new ArrayBuffer(4);
            new Uint8Array(source).set([1, 2, 3, 4]);
            var clone = structuredClone(source);
            new Uint8Array(source)[0] = 99;");

        engine.Evaluate("clone !== source").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.byteLength").AsNumber().Should().Be(4);
        engine.Evaluate("Array.from(new Uint8Array(clone)).join(',')").AsString().Should().Be("1,2,3,4");
    }

    [Fact]
    public void KeepsAResizableArrayBufferResizable()
    {
        var engine = WebEngine();

        engine.Execute("var clone = structuredClone(new ArrayBuffer(4, { maxByteLength: 8 }));");

        engine.Evaluate("clone.resizable").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.maxByteLength").AsNumber().Should().Be(8);
        engine.Evaluate("clone.byteLength").AsNumber().Should().Be(4);
        engine.Execute("clone.resize(8);");
        engine.Evaluate("clone.byteLength").AsNumber().Should().Be(8);
    }

    [Fact]
    public void RefusesADetachedArrayBuffer()
    {
        var engine = WebEngine();

        engine.Execute("var buffer = new ArrayBuffer(4); structuredClone(0, { transfer: [buffer] });");

        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        Err(engine, "structuredClone(buffer)").Should().Be("DataCloneError");
        Err(engine, "structuredClone({ b: buffer })").Should().Be("DataCloneError");
    }

    [Fact]
    public void RefusesASharedArrayBuffer()
    {
        var engine = WebEngine();

        // Serializable only in a cross-origin-isolated agent cluster, which Jint has no notion of.
        Err(engine, "structuredClone(new SharedArrayBuffer(4))").Should().Be("DataCloneError");
    }

    [Fact]
    public void ClonesTwoViewsOfOneBufferAsTwoViewsOfOneClonedBuffer()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(8);
            new Uint8Array(buffer).set([1, 2, 3, 4, 5, 6, 7, 8]);
            var clone = structuredClone({ head: new Uint8Array(buffer, 0, 4), tail: new Uint8Array(buffer, 4, 4) });");

        // The memory map is what makes this hold: the buffer is serialized once.
        engine.Evaluate("clone.head.buffer === clone.tail.buffer").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.head.buffer !== buffer").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.head.byteOffset").AsNumber().Should().Be(0);
        engine.Evaluate("clone.tail.byteOffset").AsNumber().Should().Be(4);
        engine.Evaluate("Array.from(clone.tail).join(',')").AsString().Should().Be("5,6,7,8");

        // Writing through one view is visible in the other, exactly as in the source.
        engine.Execute("clone.head[0] = 42;");
        engine.Evaluate("new Uint8Array(clone.tail.buffer)[0]").AsNumber().Should().Be(42);
    }

    [Theory]
    [InlineData("Int8Array")]
    [InlineData("Uint8Array")]
    [InlineData("Uint8ClampedArray")]
    [InlineData("Int16Array")]
    [InlineData("Uint16Array")]
    [InlineData("Int32Array")]
    [InlineData("Uint32Array")]
    [InlineData("Float32Array")]
    [InlineData("Float64Array")]
    public void ClonesEveryNumericTypedArrayKind(string kind)
    {
        var engine = WebEngine();

        engine.Execute($"var source = new {kind}([1, 2, 3]); var clone = structuredClone(source);");

        engine.Evaluate($"clone instanceof {kind}").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.length").AsNumber().Should().Be(3);
        engine.Evaluate("Array.from(clone).join(',')").AsString().Should().Be("1,2,3");
        engine.Evaluate("clone.buffer !== source.buffer").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesABigIntTypedArray()
    {
        var engine = WebEngine();

        engine.Execute("var clone = structuredClone(new BigInt64Array([1n, -2n]));");

        engine.Evaluate("clone instanceof BigInt64Array").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone[1] === -2n").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClonesADataViewWithItsOffsetAndLength()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(12);
            var source = new DataView(buffer, 2, 8);
            source.setInt32(0, 123456);
            var clone = structuredClone(source);");

        engine.Evaluate("clone instanceof DataView").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.byteOffset").AsNumber().Should().Be(2);
        engine.Evaluate("clone.byteLength").AsNumber().Should().Be(8);
        engine.Evaluate("clone.getInt32(0)").AsNumber().Should().Be(123456);
        engine.Evaluate("clone.buffer.byteLength").AsNumber().Should().Be(12);
        engine.Evaluate("clone.buffer !== buffer").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void KeepsALengthTrackingViewTracking()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(4, { maxByteLength: 8 });
            var clone = structuredClone({ array: new Uint8Array(buffer), view: new DataView(buffer) });");

        engine.Evaluate("clone.array.length").AsNumber().Should().Be(4);
        engine.Evaluate("clone.view.byteLength").AsNumber().Should().Be(4);

        engine.Execute("clone.array.buffer.resize(8);");
        engine.Evaluate("clone.array.length").AsNumber().Should().Be(8);
        engine.Evaluate("clone.view.byteLength").AsNumber().Should().Be(8);
    }

    [Fact]
    public void RefusesAViewThatItsBufferNoLongerCovers()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(8, { maxByteLength: 8 });
            var array = new Uint8Array(buffer, 4, 4);
            var view = new DataView(buffer, 4, 4);
            buffer.resize(2);");

        Err(engine, "structuredClone(array)").Should().Be("DataCloneError");
        Err(engine, "structuredClone(view)").Should().Be("DataCloneError");
    }

    // ---------------------------------------------------------------- transfer

    [Fact]
    public void TransfersAnArrayBufferInsteadOfCopyingIt()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(4);
            new Uint8Array(buffer).set([1, 2, 3, 4]);
            var clone = structuredClone({ payload: buffer }, { transfer: [buffer] });");

        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("clone.payload.byteLength").AsNumber().Should().Be(4);
        engine.Evaluate("Array.from(new Uint8Array(clone.payload)).join(',')").AsString().Should().Be("1,2,3,4");
    }

    [Fact]
    public void MapsATransferredBufferReachedFromTheValueToTheTransferredResult()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(4);
            new Uint8Array(buffer).set([9, 8, 7, 6]);
            var clone = structuredClone({ direct: buffer, view: new Uint8Array(buffer) }, { transfer: [buffer] });");

        // One result buffer, reached both directly and through the view.
        engine.Evaluate("clone.view.buffer === clone.direct").AsBoolean().Should().BeTrue();
        engine.Evaluate("Array.from(clone.view).join(',')").AsString().Should().Be("9,8,7,6");
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
    }

    [Fact]
    public void TransfersABufferTheValueNeverReaches()
    {
        var engine = WebEngine();

        engine.Execute("var buffer = new ArrayBuffer(4); var clone = structuredClone('unrelated', { transfer: [buffer] });");

        engine.Evaluate("clone").AsString().Should().Be("unrelated");
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
    }

    [Fact]
    public void DetachesOnlyAfterTheWholeGraphHasBeenWalked()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(4);
            var source = {
                get first() { new Uint8Array(buffer).set([1, 2, 3, 4]); return 'ok'; },
                second: buffer
            };
            var clone = structuredClone(source, { transfer: [buffer] });");

        // A getter reached during the walk can still read and write the buffer, and what it wrote is what
        // the clone carries — the transfer steps run after serialization.
        engine.Evaluate("clone.first").AsString().Should().Be("ok");
        engine.Evaluate("Array.from(new Uint8Array(clone.second)).join(',')").AsString().Should().Be("1,2,3,4");
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
    }

    [Fact]
    public void RefusesADuplicateInTheTransferList()
    {
        var engine = WebEngine();

        engine.Execute("var buffer = new ArrayBuffer(4);");
        Err(engine, "structuredClone(0, { transfer: [buffer, buffer] })").Should().Be("DataCloneError");

        // The refusal happens before anything is detached.
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(4);
    }

    [Fact]
    public void RefusesANonTransferableEntry()
    {
        var engine = WebEngine();

        Err(engine, "structuredClone(0, { transfer: [{}] })").Should().Be("DataCloneError");
        Err(engine, "structuredClone(0, { transfer: [new Uint8Array(4)] })").Should().Be("DataCloneError");
        Err(engine, "structuredClone(0, { transfer: [new SharedArrayBuffer(4)] })").Should().Be("DataCloneError");
    }

    [Fact]
    public void RefusesAnAlreadyDetachedEntry()
    {
        var engine = WebEngine();

        engine.Execute("var buffer = new ArrayBuffer(4); structuredClone(0, { transfer: [buffer] });");
        Err(engine, "structuredClone(0, { transfer: [buffer] })").Should().Be("DataCloneError");
    }

    [Fact]
    public void RefusesABufferAGetterDetachedDuringTheWalk()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(4);
            var source = { get sabotage() { structuredClone(0, { transfer: [buffer] }); return 1; } };");

        // The detached check on the transfer list is step 5.1, after serialization — so this is caught, and
        // caught as a DataCloneError.
        Err(engine, "structuredClone(source, { transfer: [buffer] })").Should().Be("DataCloneError");
    }

    [Fact]
    public void ValidatesTheOptionsDictionaryBeforeCloningAnything()
    {
        var engine = WebEngine();

        // An omitted, undefined or null dictionary is the empty dictionary.
        engine.Evaluate("structuredClone(1, undefined)").AsNumber().Should().Be(1);
        engine.Evaluate("structuredClone(1, null)").AsNumber().Should().Be(1);
        engine.Evaluate("structuredClone(1, {})").AsNumber().Should().Be(1);
        engine.Evaluate("structuredClone(1, { transfer: undefined })").AsNumber().Should().Be(1);
        engine.Evaluate("structuredClone(1, { transfer: [] })").AsNumber().Should().Be(1);

        // Anything else is a WebIDL conversion failure, which is a TypeError and not a DataCloneError.
        Err(engine, "structuredClone(1, 5)").Should().Be("TypeError");
        Err(engine, "structuredClone(1, 'nope')").Should().Be("TypeError");
        Err(engine, "structuredClone(1, { transfer: 5 })").Should().Be("TypeError");
        Err(engine, "structuredClone(1, { transfer: [1] })").Should().Be("TypeError");
    }

    [Fact]
    public void TakesTheTransferListThroughTheIteratorProtocol()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var buffer = new ArrayBuffer(4);
            var clone = structuredClone(buffer, { transfer: new Set([buffer]) });");

        // A WebIDL sequence is built from any iterable, not from an array-like.
        engine.Evaluate("clone.byteLength").AsNumber().Should().Be(4);
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
    }

    // ---------------------------------------------------------------- refusals

    [Theory]
    [InlineData("function() {}")]
    [InlineData("class Foo {}")]
    [InlineData("Math.max")]
    [InlineData("new Proxy({}, {})")]
    [InlineData("new WeakMap()")]
    [InlineData("new WeakSet()")]
    [InlineData("new WeakRef({})")]
    [InlineData("Promise.resolve(1)")]
    [InlineData("new Map()[Symbol.iterator]()")]
    [InlineData("(function* () {})()")]
    public void RefusesAnUncloneableObject(string expression)
    {
        var engine = WebEngine();

        Err(engine, $"structuredClone({expression})").Should().Be("DataCloneError");
        Err(engine, $"structuredClone({{ nested: {expression} }})").Should().Be("DataCloneError");
    }

    [Fact]
    public void RaisesTheRefusalAsADomException()
    {
        var engine = WebEngine();

        engine.Execute(@"
            var caught;
            try { structuredClone(function() {}); } catch (e) { caught = e; }");

        engine.Evaluate("caught instanceof DOMException").AsBoolean().Should().BeTrue();
        engine.Evaluate("caught.name").AsString().Should().Be("DataCloneError");
        engine.Evaluate("caught.code").AsNumber().Should().Be(25);
        engine.Evaluate("typeof caught.message").AsString().Should().Be("string");
    }

    [Fact]
    public void RefusesADomExceptionEvenWhenTheGlobalWasShadowed()
    {
        // The algorithm reaches the interface object through the realm's intrinsics, so a script that
        // replaced the global cannot make the failure unreportable.
        var engine = WebEngine();
        engine.Execute("var real = DOMException; DOMException = 1;");

        engine.Execute("var caught; try { structuredClone(Symbol()); } catch (e) { caught = e; }");
        engine.Evaluate("caught instanceof real").AsBoolean().Should().BeTrue();
        engine.Evaluate("caught.name").AsString().Should().Be("DataCloneError");
    }
}
#endif
