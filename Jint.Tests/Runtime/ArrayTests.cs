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
