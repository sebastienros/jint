using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class ArrayTests
{
    private readonly Engine _engine;

    public ArrayTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    [Fact]
    public void ArrayPrototypeToStringWithArray()
    {
        var result = _engine.Evaluate("Array.prototype.toString.call([1,2,3]);").AsString();

        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void ArrayPrototypeToStringWithNumber()
    {
        var result = _engine.Evaluate("Array.prototype.toString.call(1);").AsString();

        Assert.Equal("[object Number]", result);
    }

    [Fact]
    public void ArrayPrototypeToStringWithObject()
    {
        var result = _engine.Evaluate("Array.prototype.toString.call({});").AsString();

        Assert.Equal("[object Object]", result);
    }

    [Fact]
    public void ArrayPrototypeJoinWithCircularReference()
    {
        var result = _engine.Evaluate("Array.prototype.join.call((c = [1, 2, 3, 4], b = [1, 2, 3, 4], b[1] = c, c[1] = b, c))").AsString();

        Assert.Equal("1,1,,3,4,3,4", result);
    }

    [Fact]
    public void ArrayPrototypeToLocaleStringWithCircularReference()
    {
        var result = _engine.Evaluate("Array.prototype.toLocaleString.call((c = [1, 2, 3, 4], b = [1, 2, 3, 4], b[1] = c, c[1] = b, c))").AsString();

        Assert.Equal("1,1,,3,4,3,4", result);
    }

    [Fact]
    public void EmptyStringKey()
    {
        var result = _engine.Evaluate("var x=[];x[\"\"]=8;x[\"\"];").AsNumber();

        Assert.Equal(8, result);
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
        Assert.Equal(0, length);
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
        Assert.True(_engine.Evaluate("a instanceof MyArr").AsBoolean());
    }

    [Fact]
    public void IteratorShouldBeConvertibleToArray()
    {
        Assert.Equal("hello;again", _engine.Evaluate("Array.from(['hello', 'again'].values()).join(';')"));
        Assert.Equal("hello;another", _engine.Evaluate("Array.from(new Map([['hello', 'world'], ['another', 'value']]).keys()).join(';')"));
    }

    [Fact]
    public void ArrayFromShouldNotFlattenInputArray()
    {
        Assert.Equal("a;b", _engine.Evaluate("[...['a', 'b']].join(';')"));
        Assert.Equal("0,a;1,b", _engine.Evaluate("[...['a', 'b'].entries()].join(';')"));
        Assert.Equal("0,c;1,d", _engine.Evaluate("Array.from(['c', 'd'].entries()).join(';')"));
        Assert.Equal("0,e;1,f", _engine.Evaluate("Array.from([[0, 'e'],[1, 'f']]).join(';')"));
    }

    [Fact]
    public void ArrayEntriesShouldReturnKeyValuePairs()
    {
        Assert.Equal("0,hello,1,world", _engine.Evaluate("Array.from(['hello', 'world'].entries()).join()"));
        Assert.Equal("0,hello;1,world", _engine.Evaluate("Array.from(['hello', 'world'].entries()).join(';')"));
        Assert.Equal("0,;1,1;2,5", _engine.Evaluate("Array.from([,1,5,].entries()).join(';')"));
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
        engine.SetValue("equal", new Action<object, object>(Assert.Equal));

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
        Assert.True(_engine.Evaluate("\"0\" in a").AsBoolean());
        Assert.True(_engine.Evaluate("\"1\" in a").AsBoolean());
        Assert.Equal("undefinedundefined", _engine.Evaluate("'' + a[0] + a[1]"));
    }

    [Fact]
    public void ArrayIsSubclassable()
    {
        _engine.Evaluate("class C extends Array {}");
        _engine.Evaluate("var c = new C();");
        Assert.True(_engine.Evaluate("c.map(Boolean) instanceof C").AsBoolean());
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
        Assert.True(engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean());
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

        Assert.True(engine.Evaluate("get[0] === Symbol.iterator").AsBoolean());
        Assert.Equal("length,0,1", engine.Evaluate("get.slice(1) + ''").AsString());
    }

    [Fact]
    public void ArrayFromStringUsingMapping()
    {
        var engine = new Engine();
        var array = engine.Evaluate("Array.from('fff', (s) => Number.parseInt(s, 16))").AsArray();
        Assert.Equal((uint) 3, array.Length);
        Assert.Equal((uint) 15, array[0]);
        Assert.Equal((uint) 15, array[1]);
        Assert.Equal((uint) 15, array[2]);
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
        Assert.True(engine.Evaluate(Script).AsBoolean());
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

        Assert.Equal("constructor", engine.Evaluate("get[0]"));
        Assert.True(engine.Evaluate("get[1] === Symbol.isConcatSpreadable").AsBoolean());
        Assert.Equal("length", engine.Evaluate("get[2]"));
        Assert.Equal("0", engine.Evaluate("get[3]"));
        Assert.True(engine.Evaluate("get[4] === get[1] && get[5] === get[2] && get[6] === get[3]").AsBoolean());
        Assert.Equal(7, engine.Evaluate("get.length"));
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
        a[0].Should().Be(JsValue.Undefined);
        a[1].Should().Be(JsValue.Undefined);
        a[2].Should().Be(JsValue.Undefined);
        a[3].Should().BeOfType<JsArray>().Which.Should().ContainInOrder("#d8b365", "#f5f5f5", "#5ab4ac");
        a[4].Should().BeOfType<JsArray>().Which.Should().ContainInOrder("#a6611a", "#dfc27d", "#80cdc1", "#018571");
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
        Assert.True(engine.Evaluate(Script).AsBoolean());
    }

    [Fact]
    public void ShouldBeAbleToInitFromArray()
    {
        var engine = new Engine();
        var propertyDescriptors = new JsArray(engine, new JsValue[] { 1 }).GetOwnProperties().ToArray();
        Assert.Equal(2, propertyDescriptors.Length);
        Assert.Equal("0", propertyDescriptors[0].Key);
        Assert.Equal(1, propertyDescriptors[0].Value.Value);
        Assert.Equal("length", propertyDescriptors[1].Key);
        Assert.Equal(1, propertyDescriptors[1].Value.Value);
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
}
