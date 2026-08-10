using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class ArrayTests
{
    private readonly Engine _engine;

    public ArrayTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    [Fact]
    public void FilterSkipsHoles()
    {
        var result = _engine.Evaluate("JSON.stringify([1,,3].filter(function(x) { return true; }))").AsString();

        result.Should().Be("[1,3]");
    }

    [Fact]
    public void HoleReadFindsInheritedIndexOnArrayPrototype()
    {
        // The pristine-prototypes shortcut must disengage the moment Array.prototype (or
        // Object.prototype) gains an index property: hole reads and `in` then walk the chain.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var a = [1, , 3];
            var before = [a[1], 1 in a, a[10], 10 in a];
            Array.prototype[1] = 'ap';
            Object.prototype[10] = 'op';
            var after = [a[1], 1 in a, a[10], 10 in a];
            JSON.stringify([before, after]);
            """).AsString();

        result.Should().Be("[[null,false,null,false],[\"ap\",true,\"op\",true]]");
    }

    [Fact]
    public void HoleReadHonorsIndexGetterOnArrayItself()
    {
        // an exotic own descriptor clears the fast-access invariant on the instance
        var engine = new Engine();
        var result = engine.Evaluate("""
            var a = [1, 2, 3];
            Object.defineProperty(a, '5', { get: function () { return 'got'; } });
            [a[5], 5 in a, a[7] === undefined].join(',');
            """).AsString();

        result.Should().Be("got,true,true");
    }

    [Fact]
    public void HoleReadWalksCustomPrototypeChain()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var proto = { 1: 'inherited' };
            var a = [0];
            a.length = 3;
            Object.setPrototypeOf(a, proto);
            [a[1], 1 in a, a[2] === undefined, 2 in a].join(',');
            """).AsString();

        result.Should().Be("inherited,true,true,false");
    }

    [Fact]
    public void FilterSubclassUsesSpecies()
    {
        var result = _engine.Evaluate("""
            class A extends Array {}
            var a = A.from([1, 2, 3, 4]);
            var filtered = a.filter(x => x % 2 === 0);
            filtered instanceof A && filtered.length === 2 && filtered[0] === 2 && filtered[1] === 4;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
    public void FilterRespectsOwnConstructorProperty()
    {
        var result = _engine.Evaluate("""
            var a = [1, 2, 3, 4];
            var captured = null;
            a.constructor = function(len) { captured = len; return new Array(len); };
            a.constructor[Symbol.species] = a.constructor;
            var filtered = a.filter(x => x > 2);
            captured === 0 && filtered.length === 2 && filtered[0] === 3 && filtered[1] === 4;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
    public void FilterThrowingCallbackLeavesEngineUsable()
    {
        Invoking(() => _engine.Evaluate("[1, 2, 3].filter(function(x) { if (x === 2) { throw new Error('boom'); } return true; })")).Should().ThrowExactly<JavaScriptException>();

        var result = _engine.Evaluate("JSON.stringify([1, 2, 3, 4].filter(function(x) { return x % 2 === 0; }))").AsString();
        result.Should().Be("[2,4]");
    }

    [Fact]
    public void FilterCallbackMutatingSource()
    {
        // elements appended during iteration are not visited (len captured up front),
        // shrinking makes the tail absent
        var grow = _engine.Evaluate("""
            var a = [1, 2, 3];
            JSON.stringify(a.filter(function(x) { a.push(x * 10); return true; }));
            """).AsString();
        grow.Should().Be("[1,2,3]");

        var shrink = _engine.Evaluate("""
            var b = [1, 2, 3, 4, 5];
            JSON.stringify(b.filter(function(x) { b.length = 2; return true; }));
            """).AsString();
        shrink.Should().Be("[1,2]");
    }

    [Fact]
    public void FlatSkipsNestedHoles()
    {
        var result = _engine.Evaluate("JSON.stringify([1, [2, , 3], , [4]].flat())").AsString();

        result.Should().Be("[1,2,3,4]");
    }

    [Fact]
    public void FlatInfiniteDepth()
    {
        var result = _engine.Evaluate("JSON.stringify([1, [2, [3, [4, [5]]]]].flat(Infinity))").AsString();

        result.Should().Be("[1,2,3,4,5]");
    }

    [Fact]
    public void FlatSubclassUsesSpecies()
    {
        var result = _engine.Evaluate("""
            class A extends Array {}
            var a = A.from([[1], [2]]);
            var flattened = a.flat();
            flattened instanceof A && flattened.length === 2;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
    public void FlatMapThrowingMapperLeavesEngineUsable()
    {
        Invoking(() => _engine.Evaluate("[1, 2, 3].flatMap(function(x) { if (x === 2) { throw new Error('boom'); } return [x]; })")).Should().ThrowExactly<JavaScriptException>();

        var result = _engine.Evaluate("JSON.stringify([1, 2].flatMap(function(x) { return [x, x * 10]; }))").AsString();
        result.Should().Be("[1,10,2,20]");
    }

    [Fact]
    public void ConcatGenericSpreadableAbsentIndicesBecomeUndefinedProperties()
    {
        // mirrors the long-standing slow-path deviation: absent indices of a generic
        // spreadable are written as undefined-valued own properties, not holes
        var result = _engine.Evaluate("""
            var obj = { length: 3, 0: 'a', 2: 'c' };
            obj[Symbol.isConcatSpreadable] = true;
            var r = [].concat(obj);
            JSON.stringify([r.length, 1 in r, r[0], r[2]]);
            """).AsString();

        result.Should().Be("[3,true,\"a\",\"c\"]");
    }

    [Fact]
    public void ConcatMixedHoleyArrayAndSpreadableObject()
    {
        var result = _engine.Evaluate("""
            var obj = { length: 3, 0: 'a', 2: 'c' };
            obj[Symbol.isConcatSpreadable] = true;
            var r = ['x'].concat([1, , 3], obj, 'tail');
            JSON.stringify([r.length, 2 in r, 5 in r, r[0], r[1], r[3], r[4], r[6], r[7]]);
            """).AsString();

        result.Should().Be("[8,false,true,\"x\",1,3,\"a\",\"c\",\"tail\"]");
    }

    [Fact]
    public void ConcatOwnConstructorPropertyFallsBackToSlowPath()
    {
        var result = _engine.Evaluate("""
            var a = ['x'];
            a.constructor = Array;
            var r = a.concat([1, , 3]);
            JSON.stringify([r.length, 2 in r, r[0], r[1], r[3]]);
            """).AsString();

        result.Should().Be("[4,false,\"x\",1,3]");
    }

    [Fact]
    public void ConcatSparseModeReceiverDoesNotCrash()
    {
        var result = _engine.Evaluate("""
            var s = [];
            s[5000000] = 1;
            var r = s.concat([2]);
            JSON.stringify([r.length, r[5000000], r[5000001], 0 in r]);
            """).AsString();

        result.Should().Be("[5000002,1,2,false]");
    }

    [Fact]
    public void ConcatSparseModeArgumentDoesNotCrash()
    {
        var result = _engine.Evaluate("""
            var s = [];
            s[5000000] = 1;
            var r = ['x'].concat(s);
            JSON.stringify([r.length, r[0], r[5000001], 1 in r]);
            """).AsString();

        result.Should().Be("[5000002,\"x\",1,false]");
    }

    [Fact]
    public void ArrayFromIteratorCollectsAllValues()
    {
        var result = _engine.Evaluate("""
            var fromSet = Array.from(new Set([1, 2, 3, 2, 1]));
            function* gen() { yield 'a'; yield 'b'; }
            var fromGen = Array.from(gen());
            JSON.stringify([fromSet, fromGen]);
            """).AsString();

        result.Should().Be("[[1,2,3],[\"a\",\"b\"]]");
    }

    [Fact]
    public void ArrayFromIteratorWithMapperPassesIndices()
    {
        var result = _engine.Evaluate("JSON.stringify(Array.from(new Set(['a', 'b']), function (v, i) { return v + i; }))").AsString();

        result.Should().Be("[\"a0\",\"b1\"]");
    }

    [Fact]
    public void ArrayFromThrowingMapperClosesIterator()
    {
        var result = _engine.Evaluate("""
            var closed = false;
            var iterable = {};
            iterable[Symbol.iterator] = function () {
                var i = 0;
                return {
                    next: function () { return { value: i++, done: i > 5 }; },
                    return: function () { closed = true; return { done: true }; }
                };
            };
            try { Array.from(iterable, function (x) { if (x === 2) { throw new Error('boom'); } return x; }); } catch (e) { }
            closed;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
    public void ArrayFromSubclassUsesConstructor()
    {
        var result = _engine.Evaluate("""
            class A extends Array {}
            var a = A.from(new Set([1, 2]));
            a instanceof A && a.length === 2 && a[0] === 1 && a[1] === 2;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
    public void ArrayPrototypeToStringWithArray()
    {
        var result = _engine.Evaluate("Array.prototype.toString.call([1,2,3]);").AsString();

        result.Should().Be("1,2,3");
    }

    [Fact]
    public void ArrayPrototypeToStringWithNumber()
    {
        var result = _engine.Evaluate("Array.prototype.toString.call(1);").AsString();

        result.Should().Be("[object Number]");
    }

    [Fact]
    public void ArrayPrototypeToStringWithObject()
    {
        var result = _engine.Evaluate("Array.prototype.toString.call({});").AsString();

        result.Should().Be("[object Object]");
    }

    [Fact]
    public void ArrayPrototypeJoinWithCircularReference()
    {
        var result = _engine.Evaluate("Array.prototype.join.call((c = [1, 2, 3, 4], b = [1, 2, 3, 4], b[1] = c, c[1] = b, c))").AsString();

        result.Should().Be("1,1,,3,4,3,4");
    }

    [Fact]
    public void ArrayPrototypeToLocaleStringWithCircularReference()
    {
        var result = _engine.Evaluate("Array.prototype.toLocaleString.call((c = [1, 2, 3, 4], b = [1, 2, 3, 4], b[1] = c, c[1] = b, c))").AsString();

        result.Should().Be("1,1,,3,4,3,4");
    }

    [Fact]
    public void EmptyStringKey()
    {
        var result = _engine.Evaluate("var x=[];x[\"\"]=8;x[\"\"];").AsNumber();

        result.Should().Be(8);
    }

    [Fact]
    public void LargeArraySize()
    {
        const string code = @"
            let arr = [];
            for (let i = 0; i < 10000; i++) arr.push(i);
            for (let i=0;i<10000;i++) arr.splice(0, 1);
            ";
        var engine = new Engine();
        engine.Execute(code);
    }

    [Fact]
    public void ArrayLengthFromInitialState()
    {
        var engine = new Engine();
        var array = new JsArray(engine);
        var length = (int) array.Length;
        length.Should().Be(0);
    }

    [Fact]
    public void ArraySortIsStable()
    {
        const string code = @"
                var items = [
                    { name: 'Edward', value: 0 },
                    { name: 'Sharpe', value: 0 },
                    { name: 'And', value: 0 },
                    { name: 'The', value: 1 },
                    { name: 'Magnetic', value: 0 },
                    { name: 'Zeros', value: 0 }
                ];

                // sort by value
                function compare(a, b) {
                    return a.value - b.value;
                }

                var a = items.sort();

                assert(a[0].name == 'Edward');
                assert(a[1].name == 'Sharpe');
                assert(a[2].name == 'And');
                assert(a[3].name == 'The');
                assert(a[4].name == 'Magnetic');
                assert(a[5].name == 'Zeros');

                var a = items.sort(compare);

                assert(a[0].name == 'Edward');
                assert(a[1].name == 'Sharpe');
                assert(a[2].name == 'And');
                assert(a[3].name == 'Magnetic');
                assert(a[4].name == 'Zeros');
                assert(a[5].name == 'The');
            ";

        _engine.Execute(code);
    }

    [Fact]
    public void ExtendingArrayAndInstanceOf()
    {
        const string script = @"
                class MyArr extends Array {
                    constructor(...args) {
                        super(...args);
                    } 
                }";

        _engine.Execute(script);
        _engine.Evaluate("const a = new MyArr(1,2);");
        _engine.Evaluate("a instanceof MyArr").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void IteratorShouldBeConvertibleToArray()
    {
        _engine.Evaluate("Array.from(['hello', 'again'].values()).join(';')").Should().Be("hello;again");
        _engine.Evaluate("Array.from(new Map([['hello', 'world'], ['another', 'value']]).keys()).join(';')").Should().Be("hello;another");
    }

    [Fact]
    public void ArrayFromShouldNotFlattenInputArray()
    {
        _engine.Evaluate("[...['a', 'b']].join(';')").Should().Be("a;b");
        _engine.Evaluate("[...['a', 'b'].entries()].join(';')").Should().Be("0,a;1,b");
        _engine.Evaluate("Array.from(['c', 'd'].entries()).join(';')").Should().Be("0,c;1,d");
        _engine.Evaluate("Array.from([[0, 'e'],[1, 'f']]).join(';')").Should().Be("0,e;1,f");
    }

    [Fact]
    public void ArrayEntriesShouldReturnKeyValuePairs()
    {
        _engine.Evaluate("Array.from(['hello', 'world'].entries()).join()").Should().Be("0,hello,1,world");
        _engine.Evaluate("Array.from(['hello', 'world'].entries()).join(';')").Should().Be("0,hello;1,world");
        _engine.Evaluate("Array.from([,1,5,].entries()).join(';')").Should().Be("0,;1,1;2,5");
    }

    [Fact]
    public void IteratorsShouldHaveIteratorSymbol()
    {
        _engine.Execute("assert(!!['hello'].values()[Symbol.iterator])");
        _engine.Execute("assert(!!new Map([['hello', 'world']]).keys()[Symbol.iterator])");
    }


    [Fact]
    public void ArraySortDoesNotCrashInDebugMode()
    {
        var engine = new Engine(o =>
        {
            o.DebugMode(true);
        });
        engine.SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));

        const string code = @"
                var items = [5,2,4,1];
                items.sort((a,b) => a - b);
                equal('1,2,4,5', items.join());
            ";

        engine.Execute(code);
    }

    [Fact]
    public void ArrayConstructorFromHoles()
    {
        _engine.Evaluate("var a = Array(...[,,]);");
        _engine.Evaluate("\"0\" in a").AsBoolean().Should().BeTrue();
        _engine.Evaluate("\"1\" in a").AsBoolean().Should().BeTrue();
        _engine.Evaluate("'' + a[0] + a[1]").Should().Be("undefinedundefined");
    }

    [Fact]
    public void ArrayIsSubclassable()
    {
        _engine.Evaluate("class C extends Array {}");
        _engine.Evaluate("var c = new C();");
        _engine.Evaluate("c.map(Boolean) instanceof C").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void HasProperIteratorPrototypeChain()
    {
        const string Script = @"
        // Iterator instance
        var iterator = [][Symbol.iterator]();
        // %ArrayIteratorPrototype%
        var proto1 = Object.getPrototypeOf(iterator);
        // %IteratorPrototype%
        var proto2 = Object.getPrototypeOf(proto1);";

        var engine = new Engine();
        engine.Execute(Script);
        engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ArrayFrom()
    {
        const string Script = @"
            // Array.from -> Get -> [[Get]]
            var get = [];
            var p = new Proxy({length: 2, 0: '', 1: ''}, { get: function(o, k) { get.push(k); return o[k]; }});
            Array.from(p);";

        var engine = new Engine();
        engine.Execute(Script);

        engine.Evaluate("get[0] === Symbol.iterator").AsBoolean().Should().BeTrue();
        engine.Evaluate("get.slice(1) + ''").AsString().Should().Be("length,0,1");
    }

    [Fact]
    public void ArrayFromStringUsingMapping()
    {
        var engine = new Engine();
        var array = engine.Evaluate("Array.from('fff', (s) => Number.parseInt(s, 16))").AsArray();
        array.Length.Should().Be((uint) 3);
        array[0].Should().Be((uint) 15);
        array[1].Should().Be((uint) 15);
        array[2].Should().Be((uint) 15);
    }

    [Fact]
    public void Iteration()
    {
        const string Script = @"
            // Array.prototype methods -> Get -> [[Get]]
            var methods = ['copyWithin', 'every', 'fill', 'filter', 'find', 'findIndex', 'forEach',
              'indexOf', 'join', 'lastIndexOf', 'map', 'reduce', 'reduceRight', 'some'];
            var get;
            var p = new Proxy({length: 2, 0: '', 1: ''}, { get: function(o, k) { get.push(k); return o[k]; }});
            for(var i = 0; i < methods.length; i+=1) {
              get = [];
              Array.prototype[methods[i]].call(p, Function());
              var actual = get + '';
              var expected = (
                methods[i] === 'fill' ? ""length"" :
                methods[i] === 'every' ? ""length,0"" :
                methods[i] === 'lastIndexOf' || methods[i] === 'reduceRight' ? ""length,1,0"" :
                ""length,0,1"");

              if (actual !== expected) {
                throw methods[i] + ': ' + actual + ' !== ' + expected;
              }
            }
            return true;";

        var engine = new Engine();
        engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void Concat()
    {
        const string Script = @"
            // Array.prototype.concat -> Get -> [[Get]]
            var get = [];
            var arr = [1];
            arr.constructor = void undefined;
            var p = new Proxy(arr, { get: function(o, k) { get.push(k); return o[k]; }});
            Array.prototype.concat.call(p,p);";

        var engine = new Engine();
        engine.Execute(Script);

        engine.Evaluate("get[0]").Should().Be("constructor");
        engine.Evaluate("get[1] === Symbol.isConcatSpreadable").AsBoolean().Should().BeTrue();
        engine.Evaluate("get[2]").Should().Be("length");
        engine.Evaluate("get[3]").Should().Be("0");
        engine.Evaluate("get[4] === get[1] && get[5] === get[2] && get[6] === get[3]").AsBoolean().Should().BeTrue();
        engine.Evaluate("get.length").Should().Be(7);
    }

    [Fact]
    public void ConcatHandlesHolesCorrectly()
    {
        const string Code = """
           function colors(specifier) {
             var n = specifier.length / 6 | 0, colors = new Array(n), i = 0;
             while (i < n) colors[i] = "#" + specifier.slice(i * 6, ++i * 6);
             return colors;
           }
        
           new Array(3).concat("d8b365f5f5f55ab4ac","a6611adfc27d80cdc1018571").map(colors);
        """;

        var engine = new Engine();

        var a = engine.Evaluate(Code).AsArray();

        a.Length.Should().Be(5);
        a[0].Should().BeUndefined();
        a[1].Should().BeUndefined();
        a[2].Should().BeUndefined();
        a[3].Should().BeOfType<JsArray>().Which.AsEnumerable().Should().ContainInOrder("#d8b365", "#f5f5f5", "#5ab4ac");
        a[4].Should().BeOfType<JsArray>().Which.AsEnumerable().Should().ContainInOrder("#a6611a", "#dfc27d", "#80cdc1", "#018571");
    }

    [Fact]
    public void Shift()
    {
        const string Script = @"
// Array.prototype.shift -> Get -> [[Get]]
var get = [];
var p = new Proxy([0,1,2,3], { get: function(o, k) { get.push(k); return o[k]; }});
Array.prototype.shift.call(p);
return get + '' === ""length,0,1,2,3"";";

        var engine = new Engine();
        engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldBeAbleToInitFromArray()
    {
        var engine = new Engine();
        var propertyDescriptors = new JsArray(engine, [1]).GetOwnProperties().ToArray();
        propertyDescriptors.Length.Should().Be(2);
        propertyDescriptors[0].Key.Should().Be("0");
        propertyDescriptors[0].Value.Value.Should().Be(1);
        propertyDescriptors[1].Key.Should().Be("length");
        propertyDescriptors[1].Value.Value.Should().Be(1);
    }

    [Fact]
    public void ArrayFromSortTest()
    {
        var item1 = new KeyValuePair<string, string>("Id1", "0020");
        var item2 = new KeyValuePair<string, string>("Id2", "0001");

        var engine = new Engine();
        engine.SetValue("Root", new { Inner = new { Items = new[] { item1, item2 } } });

        var result = engine.Evaluate("Array.from(Root.Inner.Items).sort((a, b) => a.Value === '0001' ? -1 : 1)").AsArray();

        var enumerableResult = result
            .Select(x => (KeyValuePair<string, string>) ((IObjectWrapper) x).Target)
            .ToList();

        enumerableResult.Should().HaveCount(2);
        enumerableResult[0].Key.Should().Be(item2.Key);
        enumerableResult[1].Key.Should().Be(item1.Key);
    }

    /// <summary>
    /// reverse and fill take a dense fast path that works on the backing store directly. reverse must
    /// decline it whenever the range contains a hole, since the spec creates and deletes properties
    /// there and a plain swap cannot express that; fill may keep it, because filling makes every index
    /// in the range present. Every expectation was verified against V8.
    /// </summary>
    [Theory]
    // reverse, fully populated: takes the fast path
    [InlineData("[1,2,3].reverse()", "[3,2,1]len=3")]
    [InlineData("[1,2,3,4].reverse()", "[4,3,2,1]len=4")]
    [InlineData("[].reverse()", "[]len=0")]
    [InlineData("[7].reverse()", "[7]len=1")]
    // undefined is a present value, not a hole, so it still qualifies
    [InlineData("[1,undefined,3].reverse()", "[3,undefined,1]len=3")]
    // reverse with holes: must fall through and preserve them
    [InlineData("[1,,3].reverse()", "[3,<hole>,1]len=3")]
    [InlineData("[,,3].reverse()", "[3,<hole>,<hole>]len=3")]
    [InlineData("new Array(3).reverse()", "[<hole>,<hole>,<hole>]len=3")]
    [InlineData("(function(){var a=[1,2,3];a[10]=11;return a.reverse();})()", "[11,<hole>,<hole>,<hole>,<hole>,<hole>,<hole>,<hole>,3,2,1]len=11")]
    // length shortened below the backing store
    [InlineData("(function(){var a=[1,2,3,4,5];a.length=3;return a.reverse();})()", "[3,2,1]len=3")]
    // fill: ranges, clamping, fractional and inverted
    [InlineData("[1,2,3,4].fill(0)", "[0,0,0,0]len=4")]
    [InlineData("[1,2,3,4].fill(0,1,3)", "[1,0,0,4]len=4")]
    [InlineData("[1,2,3].fill(0,-2)", "[1,0,0]len=3")]
    [InlineData("[1,2,3].fill(0,-100,100)", "[0,0,0]len=3")]
    [InlineData("[1,2,3].fill(0,2,1)", "[1,2,3]len=3")]
    [InlineData("[1,2,3].fill(0,1.7,2.9)", "[1,0,3]len=3")]
    [InlineData("[].fill(1)", "[]len=0")]
    [InlineData("[1,2,3].fill(undefined,1)", "[1,undefined,undefined]len=3")]
    // fill turns holes into present values
    [InlineData("[1,,3].fill(9,1,2)", "[1,9,3]len=3")]
    [InlineData("new Array(3).fill(5)", "[5,5,5]len=3")]
    [InlineData("(function(){var a=[1,2,3];a[8]=9;return a.fill(7,2,6);})()", "[1,2,7,7,7,7,<hole>,<hole>,9]len=9")]
    public void ReverseAndFillPreserveHoleSemantics(string expression, string expected)
    {
        var engine = new Engine();
        engine.Execute("""
            function d(a) {
                var parts = [];
                for (var i = 0; i < a.length; i++) parts.push(i in a ? String(a[i]) : "<hole>");
                return "[" + parts.join(",") + "]len=" + a.length;
            }
            """);
        engine.Evaluate($"d({expression})").AsString().Should().Be(expected);
    }

    [Fact]
    public void ReverseAndFillReturnTheSameObjectAndHandleArrayLikes()
    {
        var engine = new Engine();

        engine.Evaluate("var a = [1,2,3]; a.reverse() === a").AsBoolean().Should().BeTrue();
        engine.Evaluate("var b = [1,2,3]; b.fill(0) === b").AsBoolean().Should().BeTrue();

        // array-likes never reach the dense path
        engine.Evaluate("""
            var o = { length: 3, 0: 'a', 2: 'c' };
            Array.prototype.reverse.call(o);
            o[0] + '|' + (1 in o) + '|' + o[2];
            """).AsString().Should().Be("c|false|a");

        engine.Evaluate("""
            var p = { length: 3, 0: 'a' };
            Array.prototype.fill.call(p, 'z', 1);
            p[0] + p[1] + p[2];
            """).AsString().Should().Be("azz");

        // an extra non-index property does not disturb the element reversal
        engine.Evaluate("var q = [1,2,3]; q.foo = 1; q.reverse().join(',')").AsString().Should().Be("3,2,1");

        engine.Evaluate("var r = [1,2,3]; r.copyWithin(0,1) === r").AsBoolean().Should().BeTrue();

        engine.Evaluate("""
            var s = { length: 5, 0: 'a', 1: 'b', 2: 'c', 3: 'd', 4: 'e' };
            Array.prototype.copyWithin.call(s, 0, 3);
            s[0] + s[1] + s[2] + s[3] + s[4];
            """).AsString().Should().Be("decde");

        engine.Evaluate("""
            var u = { length: 3, 0: 'a', 2: 'a' };
            Array.prototype.lastIndexOf.call(u, 'a');
            """).AsNumber().Should().Be(2);
    }

    /// <summary>
    /// copyWithin and lastIndexOf take the same dense fast path. copyWithin relies on Array.Copy
    /// behaving as if the source were staged in a temporary, which is what the generic path's
    /// direction flip exists for, so overlap in both directions is covered here; a hole in the source
    /// range makes the spec delete the target index, so those cases must decline the lane. Every
    /// expectation was verified against V8.
    /// </summary>
    [Theory]
    // copyWithin, no overlap
    [InlineData("[1,2,3,4,5].copyWithin(0,3)", "[4,5,3,4,5]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(1,3)", "[1,4,5,4,5]len=5")]
    // copyWithin, overlapping in both directions
    [InlineData("[1,2,3,4,5].copyWithin(3,0)", "[1,2,3,1,2]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(0,1)", "[2,3,4,5,5]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(1,0,3)", "[1,1,2,3,5]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(-2,-3,-1)", "[1,2,3,3,4]len=5")]
    // no-op, empty and clamped ranges
    [InlineData("[1,2,3,4,5].copyWithin(0,0)", "[1,2,3,4,5]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(2,2,2)", "[1,2,3,4,5]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(10,0)", "[1,2,3,4,5]len=5")]
    [InlineData("[1,2,3,4,5].copyWithin(0,10)", "[1,2,3,4,5]len=5")]
    [InlineData("[].copyWithin(0,0)", "[]len=0")]
    [InlineData("[1,2,3].copyWithin(0,1.6,2.9)", "[2,2,3]len=3")]
    // a hole in the source range deletes the target index, so the lane must decline
    [InlineData("[1,,3,4].copyWithin(0,1,3)", "[<hole>,3,3,4]len=4")]
    [InlineData("[1,2,,4].copyWithin(1,2)", "[1,<hole>,4,4]len=4")]
    [InlineData("(function(){var a=[1,2,3];a[8]=9;return a.copyWithin(0,7,9);})()", "[<hole>,9,3,<hole>,<hole>,<hole>,<hole>,<hole>,9]len=9")]
    [InlineData("(function(){var a=[1,2,3,4,5];a.length=3;return a.copyWithin(0,1);})()", "[2,3,3]len=3")]
    public void CopyWithinPreservesOverlapAndHoleSemantics(string expression, string expected)
    {
        var engine = new Engine();
        engine.Execute("""
            function d(a) {
                var parts = [];
                for (var i = 0; i < a.length; i++) parts.push(i in a ? String(a[i]) : "<hole>");
                return "[" + parts.join(",") + "]len=" + a.length;
            }
            """);
        engine.Evaluate($"d({expression})").AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData("[1,2,3,2,1].lastIndexOf(2)", 3)]
    [InlineData("[1,2,3,2,1].lastIndexOf(2,2)", 1)]
    [InlineData("[1,2,3].lastIndexOf(9)", -1)]
    [InlineData("[1,2,3,2].lastIndexOf(2,-1)", 3)]
    [InlineData("[1,2,3,2].lastIndexOf(2,-3)", 1)]
    [InlineData("[1,2,3].lastIndexOf(1,-100)", -1)]
    [InlineData("[1,2,3].lastIndexOf(3,100)", 2)]
    [InlineData("[].lastIndexOf(1)", -1)]
    [InlineData("[1,2,3].lastIndexOf(2,1.9)", 1)]
    // a hole is not undefined
    [InlineData("[1,,1].lastIndexOf(undefined)", -1)]
    [InlineData("[1,undefined,1].lastIndexOf(undefined)", 1)]
    // strict equality: NaN never matches, +0 matches -0, no coercion
    [InlineData("[NaN].lastIndexOf(NaN)", -1)]
    [InlineData("[0].lastIndexOf(-0)", 0)]
    [InlineData("['1'].lastIndexOf(1)", -1)]
    // a match beyond the dense backing store
    [InlineData("(function(){var a=[1,2,3];a[8]=2;return a.lastIndexOf(2);})()", 8)]
    // length shortened below the backing store hides the trailing elements
    [InlineData("(function(){var a=[1,2,3,4,5];a.length=2;return a.lastIndexOf(3);})()", -1)]
    public void LastIndexOfMatchesStrictEqualityAndHoles(string expression, int expected)
    {
        new Engine().Evaluate(expression).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.fill step 8.c is <c>Set(O, Pk, value, true)</c>, so a
    /// write that cannot succeed must throw. fill previously passed <c>throwOnError: false</c> and silently
    /// did nothing, unlike reverse next to it. Verified against V8: frozen throws, sealed succeeds (its
    /// existing elements stay writable).
    /// </summary>
    [Fact]
    public void FillThrowsWhenAnElementCannotBeWritten()
    {
        var engine = new Engine();

        Invoking(() => engine.Evaluate("var a = [1,2,3]; Object.freeze(a); a.fill(0);"))
            .Should().Throw<JavaScriptException>().WithMessage("*not*");

        // the frozen array is left untouched
        engine.Evaluate("a.join(',')").AsString().Should().Be("1,2,3");

        // a single non-writable element is enough to fail the whole fill
        Invoking(() => engine.Evaluate("""
            var b = [1,2,3];
            Object.defineProperty(b, '1', { value: 2, writable: false, enumerable: true, configurable: true });
            b.fill(0);
            """)).Should().Throw<JavaScriptException>();

        // sealed keeps its elements writable, so fill still succeeds
        engine.Evaluate("var c = [1,2,3]; Object.seal(c); c.fill(0); c.join(',')").AsString().Should().Be("0,0,0");
    }

    /// <summary>
    /// <c>toReversed</c> and <c>with</c> both build a brand-new array by reading the source per index, so a
    /// dense source admits a bulk copy. Every case here is one the bulk copy cannot express and must
    /// therefore decline: holes (step 5.c / 9.c is a full <c>[[Get]]</c>, which for a missing index reaches
    /// the prototype chain, and the result array has the index <em>present</em> either way), an index
    /// property inherited from <c>Array.prototype</c>, a sparse array whose length exceeds its backing
    /// store, and a non-array array-like. Expected values match V8.
    /// </summary>
    [Theory]
    [InlineData("[1,2,3].toReversed()", "[3,2,1]len=3")]
    [InlineData("[].toReversed()", "[]len=0")]
    [InlineData("[1].toReversed()", "[1]len=1")]
    [InlineData("[1,,3].toReversed()", "[3,undefined,1]len=3")]
    [InlineData("(function(){var a=[1,2];a[5]=6;return a.toReversed();})()", "[6,undefined,undefined,undefined,2,1]len=6")]
    [InlineData("Array.prototype.toReversed.call({length:3, 0:'a', 2:'c'})", "[c,undefined,a]len=3")]
    [InlineData("(function(){Array.prototype[1]='p';try{return [0,,2].toReversed();}finally{delete Array.prototype[1];}})()", "[2,p,0]len=3")]
    [InlineData("[1,2,3,4].with(1,'X')", "[1,X,3,4]len=4")]
    [InlineData("[1,2,3,4].with(-1,'X')", "[1,2,3,X]len=4")]
    [InlineData("[1,,3].with(0,'X')", "[X,undefined,3]len=3")]
    [InlineData("[1,,3].with(1,'X')", "[1,X,3]len=3")]
    [InlineData("(function(){var a=[1,2];a[4]=5;return a.with(0,'X');})()", "[X,2,undefined,undefined,5]len=5")]
    [InlineData("Array.prototype.with.call({length:3, 0:'a', 2:'c'}, 1, 'X')", "[a,X,c]len=3")]
    [InlineData("(function(){Array.prototype[1]='p';try{return [0,,2].with(0,'X');}finally{delete Array.prototype[1];}})()", "[X,p,2]len=3")]
    public void ToReversedAndWithProduceADenseResultWhateverTheSource(string expression, string expected)
    {
        var engine = new Engine();
        engine.Execute("""
            function d(a) {
                var parts = [];
                for (var i = 0; i < a.length; i++) parts.push(i in a ? String(a[i]) : "<hole>");
                return "[" + parts.join(",") + "]len=" + a.length;
            }
            """);
        engine.Evaluate($"d({expression})").AsString().Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.with captures <c>len</c> at step 2, before step 3's
    /// <c>ToIntegerOrInfinity(index)</c> gets to run arbitrary user code, and then reads every element with
    /// a live <c>? Get(O, Pk)</c> at step 9.c. A <c>valueOf</c> that shrinks the source therefore produces a
    /// result of the <em>original</em> length whose tail is <c>undefined</c> — a bulk copy out of the
    /// backing store, whose capacity still covers those indices, would hand back the stale elements
    /// instead. Expected values match V8.
    /// </summary>
    [Fact]
    public void WithReadsTheLiveSourceWhenArgumentCoercionShrinksTheArray()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            var a = [0,1,2,3,4,5,6,7];
            var r = a.with({ valueOf: function () { a.length = 4; return 1; } }, 'X');
            r.length + '|' + r.join(',') + '|' + (5 in r) + '|' + String(r[5]) + '|' + a.length
            """).AsString();

        result.Should().Be("8|0,X,2,3,,,,|true|undefined|4");

        // a source that grows during the coercion is read live too, but only up to the captured length
        var grown = engine.Evaluate("""
            var b = [0,1];
            var s = b.with({ valueOf: function () { b.push(9, 9, 9); return 0; } }, 'Y');
            s.length + '|' + s.join(',')
            """).AsString();

        grown.Should().Be("2|Y,1");
    }

    /// <summary>
    /// The same window seen from the other side: a coercion that replaces the source's backing store (by
    /// making it sparse, or by moving an index property onto the prototype) must not be read through a
    /// reference the lane captured beforehand.
    /// </summary>
    [Fact]
    public void WithReadsTheLiveSourceWhenArgumentCoercionDeoptimizesTheArray()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            var a = [0,1,2,3];
            var r = a.with({ valueOf: function () { Object.defineProperty(a, '2', { get: function () { return 'G'; }, configurable: true }); return 0; } }, 'X');
            r.join(',')
            """).AsString();

        result.Should().Be("X,1,G,3");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.copywithin captures <c>len</c> at step 3, before the
    /// target/start/end coercions at steps 5, 7 and 9 get to run arbitrary user code. Keeping that stale
    /// bound is correct for the copy loop, but the loop writes through <c>Set(O, toKey, fromVal, true)</c>,
    /// which runs ArrayDefineOwnProperty (https://tc39.es/ecma262/#sec-arraysetlength) and so extends
    /// <c>length</c> back over any index a shrinking <c>valueOf</c> deleted. A raw span copy inside the
    /// backing store does not, and would leave own index properties at or beyond <c>length</c> — a state
    /// an Array can never reach through the spec. Expected values match V8.
    /// </summary>
    [Fact]
    public void CopyWithinRestoresLengthWhenArgumentCoercionShrinksTheArray()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            var a = [0,1,2,3,4,5,6,7];
            a.copyWithin(4, 0, { valueOf: function () { a.length = 4; return 3; } });
            a.length + '|' + a.join(',') + '|' + (5 in a)
            """).AsString();

        result.Should().Be("7|0,1,2,3,0,1,2|true");

        // nothing may survive past the length the array reports
        engine.Evaluate("a.length").AsNumber().Should().Be(7);
        engine.Evaluate("7 in a").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(a).join(',')").AsString().Should().Be("0,1,2,3,4,5,6");
    }

    /// <summary>
    /// The <see cref="CopyWithinRestoresLengthWhenArgumentCoercionShrinksTheArray"/> defect in its fill
    /// twin: https://tc39.es/ecma262/#sec-array.prototype.fill captures <c>len</c> at step 3 and coerces
    /// start/end at steps 4 and 6, then writes each index with <c>Set(O, Pk, value, true)</c>.
    /// </summary>
    [Fact]
    public void FillRestoresLengthWhenArgumentCoercionShrinksTheArray()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            var a = [0,1,2,3,4,5,6,7];
            a.fill(9, 0, { valueOf: function () { a.length = 4; return 8; } });
            a.length + '|' + a.join(',') + '|' + (5 in a)
            """).AsString();

        result.Should().Be("8|9,9,9,9,9,9,9,9|true");

        engine.Evaluate("8 in a").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(a).join(',')").AsString().Should().Be("0,1,2,3,4,5,6,7");
    }

    /// <summary>
    /// Writing a hole makes the element exist, and
    /// https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor step 3.b refuses to create a property on
    /// a non-extensible object — which <c>Set(O, Pk, value, true)</c> then turns into a TypeError
    /// (fill step 8.c, copyWithin step 12.b.iii). The array keeps whatever the loop managed to write
    /// before the failure, so this is a partial update, not an atomic one. Verified against V8.
    /// <para>
    /// The <c>true</c> is baked into the spec algorithm, so the throw does not depend on the caller's
    /// strictness — both modes are pinned here.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("'use strict';")]
    public void FillThrowsWhenItWouldCreateAnElementOnANonExtensibleArray(string prologue)
    {
        var engine = new Engine();

        Invoking(() => engine.Evaluate(prologue + "var a = [0,,2]; Object.preventExtensions(a); a.fill(9);"))
            .Should().Throw<JavaScriptException>();

        // index 0 existed and was written before the hole at 1 failed
        engine.Evaluate("a[0] + '|' + (1 in a) + '|' + a[2] + '|' + a.length").AsString().Should().Be("9|false|2|3");

        // no hole, nothing to create: the same array is filled without complaint
        engine.Evaluate(prologue + "var b = [0,1,2]; Object.preventExtensions(b); b.fill(9); b.join(',')")
            .AsString().Should().Be("9,9,9");
    }

    [Theory]
    [InlineData("")]
    [InlineData("'use strict';")]
    public void CopyWithinThrowsWhenItWouldCreateAnElementOnANonExtensibleArray(string prologue)
    {
        var engine = new Engine();

        Invoking(() => engine.Evaluate(prologue + "var a = [1,2,,4]; Object.preventExtensions(a); a.copyWithin(2,0,2);"))
            .Should().Throw<JavaScriptException>();

        // the destination hole at index 2 is the first write, so nothing changed at all
        engine.Evaluate("a[0] + '|' + a[1] + '|' + (2 in a) + '|' + a[3]").AsString().Should().Be("1|2|false|4");

        // hole-free destination: the copy goes through
        engine.Evaluate(prologue + "var b = [1,2,3,4]; Object.preventExtensions(b); b.copyWithin(2,0,2); b.join(',')")
            .AsString().Should().Be("1,2,1,2");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.includes takes <c>fromIndex</c> as an unbounded
    /// integral Number: step 8 sets <c>k</c> to it directly and step 10 loops <c>while k &lt; len</c>, so
    /// any value at or past the length simply finds nothing. Narrowing it to a machine integer first is
    /// what breaks — <c>(long) 1e20</c> saturates to <c>long.MaxValue</c>, whose low 32 bits are −1, and
    /// the dense scan then indexes the backing array at −1 and escapes as a CLR
    /// <see cref="IndexOutOfRangeException"/> that no JavaScript <c>try</c> can catch.
    /// </summary>
    [Theory]
    [InlineData("[1,2,3].includes(1, 1e20)", false)]
    [InlineData("[1,2,3].includes(1, 9e18)", false)]
    [InlineData("[1,2,3].includes(1, 2147483648)", false)]
    [InlineData("[1,2,3].includes(undefined, 1e20)", false)]
    [InlineData("[1,2,3].includes(1, -1e20)", true)]
    [InlineData("[1,2,3].includes(1, Infinity)", false)]
    [InlineData("[1,2,3].includes(1, -Infinity)", true)]
    [InlineData("[1,2,3].includes(3, 2)", true)]
    [InlineData("[1,2,3].includes(1, 3)", false)]
    [InlineData("[1,2,3].includes(1, -3)", true)]
    public void IncludesClampsAnOutOfRangeFromIndexInsteadOfNarrowingIt(string expression, bool expected)
    {
        new Engine().Evaluate(expression).AsBoolean().Should().Be(expected);
    }

    /// <summary>
    /// The same shape on a non-dense receiver, so the generic loop is covered too.
    /// </summary>
    [Fact]
    public void IncludesClampsAnOutOfRangeFromIndexOnArrayLikes()
    {
        var engine = new Engine();

        engine.Evaluate("Array.prototype.includes.call({ 0: 'a', length: 1 }, 'a', 1e20)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Array.prototype.includes.call({ 0: 'a', length: 1 }, 'a', -1e20)").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.every step 3 rejects an uncallable callback
    /// <em>before</em> the iteration begins, so an empty array is no excuse to skip the check. Every one
    /// of every's siblings already got this right; only <c>every</c> short-circuited on length first.
    /// </summary>
    [Fact]
    public void EveryValidatesTheCallbackEvenWhenThereIsNothingToIterate()
    {
        var engine = new Engine();

        Invoking(() => engine.Evaluate("[].every(null)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("[].every(undefined)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("[].every({})")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("[].every('nope')")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("Array.prototype.every.call({ length: 0 }, null)")).Should().Throw<JavaScriptException>();

        // a callable one still returns true vacuously, and the non-empty behaviour is unchanged
        engine.Evaluate("[].every(function () { return false; })").AsBoolean().Should().BeTrue();
        engine.Evaluate("[1,2].every(function (x) { return x > 0; })").AsBoolean().Should().BeTrue();
        engine.Evaluate("[1,2].every(function (x) { return x > 1; })").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// The siblings, pinned as the control: they already validated first, and must keep doing so.
    /// </summary>
    [Theory]
    [InlineData("[].some(null)")]
    [InlineData("[].forEach(null)")]
    [InlineData("[].map(null)")]
    [InlineData("[].filter(null)")]
    [InlineData("[].find(null)")]
    [InlineData("[].findIndex(null)")]
    [InlineData("[].findLast(null)")]
    [InlineData("[].findLastIndex(null)")]
    [InlineData("[].flatMap(null)")]
    [InlineData("[].reduce(null, 0)")]
    [InlineData("[].reduceRight(null, 0)")]
    public void EmptyArrayCallbackMethodsValidateTheCallback(string expression)
    {
        var engine = new Engine();

        Invoking(() => engine.Evaluate(expression)).Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void SortIsStableForEqualElements()
    {
        // ES2019 requires Array.prototype.sort to be stable. The element count matters: List<T>.Sort
        // falls back to insertion sort at 16 elements or fewer and happens to be stable there, so a
        // smaller input passes even on an unstable implementation.
        const string Script = """
            var items = [];
            for (var i = 0; i < 32; i++) {
                items.push({ order: i });
            }
            items.sort(function () { return 0; });
            items.map(function (x) { return x.order; }).join(',');
            """;

        var engine = new Engine();

        engine.Evaluate(Script).AsString().Should().Be(string.Join(",", Enumerable.Range(0, 32)));
    }

    [Theory]
    [InlineData("items.sort(cmp).length", "a === 1 ? -1 : 1")]
    [InlineData("items.toSorted(cmp).length", "a === 1 ? -1 : 1")]
    [InlineData("new Int32Array(items).sort(cmp).length", "a === 1 ? -1 : 1")]
    [InlineData("items.sort(cmp).length", "-1")]
    [InlineData("items.toSorted(cmp).length", "-1")]
    [InlineData("new Int32Array(items).sort(cmp).length", "-1")]
    public void SortTerminatesWithAnInconsistentComparator(string expression, string comparisonResult)
    {
        // A comparison function that never returns 0 and is not antisymmetric is legal JavaScript: the
        // resulting order is implementation-defined, but the sort still has to finish
        // (https://tc39.es/ecma262/#sec-sortcompare). Both framework families used to get that wrong,
        // each in its own way, and neither failure was anything a script could catch. LINQ's sort on
        // .NET Framework is a quicksort with no depth limit and no fallback, so it spins forever;
        // .NET Core's introsort detects the inconsistency and throws ArgumentException instead.
        //
        // The element count and the bluntness of the comparator both matter. .NET Core insertion-sorts
        // 16 elements or fewer without ever noticing, and `a === 1 ? -1 : 1` happens not to trip the
        // detector even above that, so the array has 20 elements and `return -1` is one of the cases.
        var engine = new Engine();
        engine.Execute("var items = [5,3,8,1,9,2,7,4,6,0,15,11,13,10,14,12,17,16,19,18];");
        engine.Execute($"function cmp(a, b) {{ return {comparisonResult}; }}");

        engine.Evaluate(expression).AsNumber().Should().Be(20);
    }

    [Fact]
    public void PopWrappedGenericList()
    {
        var engine = new Engine();
        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);
        var result = engine.Evaluate("list.pop()").AsNumber();

        result.Should().Be(3);
        list.Should().HaveCount(2);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
    }
}
