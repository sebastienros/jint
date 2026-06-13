using Jint.Native;

namespace Jint.Tests.Runtime;

public class ObjectInstanceTests
{
    [Fact]
    public void KeysValuesEntriesCollectEnumerableStringKeys()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { a: 1, b: 2 };
            JSON.stringify([Object.keys(o), Object.values(o), Object.entries(o)]);
            """).AsString();

        Assert.Equal("[[\"a\",\"b\"],[1,2],[[\"a\",1],[\"b\",2]]]", result);
    }

    [Fact]
    public void KeysValuesEntriesFilterNonEnumerableAndSymbolKeys()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { a: 1 };
            Object.defineProperty(o, 'hidden', { value: 2, enumerable: false });
            o[Symbol('s')] = 3;
            o.b = 4;
            JSON.stringify([Object.keys(o), Object.values(o), Object.entries(o).length]);
            """).AsString();

        Assert.Equal("[[\"a\",\"b\"],[1,4],2]", result);
    }

    [Fact]
    public void EntriesPairsAreRealArrays()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var e = Object.entries({ a: 1 })[0];
            Array.isArray(e) && e.length === 2 && (e.push(3), e.length === 3);
            """).AsBoolean();

        Assert.True(result);
    }

    [Fact]
    public void KeysOnProxyConsultTraps()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var calls = [];
            var p = new Proxy({ a: 1, b: 2 }, {
                ownKeys: function (t) { calls.push('ownKeys'); return Reflect.ownKeys(t); },
                getOwnPropertyDescriptor: function (t, k) { calls.push('gopd:' + k); return Reflect.getOwnPropertyDescriptor(t, k); }
            });
            JSON.stringify([Object.keys(p), calls]);
            """).AsString();

        Assert.Equal("[[\"a\",\"b\"],[\"ownKeys\",\"gopd:a\",\"gopd:b\"]]", result);
    }

    [Fact]
    public void RemovingFirstPropertyFromObjectInstancePropertiesBucketAndEnumerating()
    {
        var engine = new Engine();
        var instance = new JsObject(engine);
        instance.FastSetDataProperty("bare", JsValue.Null);
        instance.FastSetDataProperty("scope", JsValue.Null);
        instance.RemoveOwnProperty("bare");
        var propertyNames = instance.GetOwnProperties().Select(x => x.Key).ToList();
        Assert.Equal(new JsValue[] { "scope" }, propertyNames);
    }

    [Theory]
    [InlineData("Array")]
    [InlineData("Boolean")]
    [InlineData("Date")]
    [InlineData("Error")]
    [InlineData("Number")]
    [InlineData("Object")]
    [InlineData("String")]
    public void ExtendingObjects(string baseType)
    {
        var code = $@"
                class My{baseType} extends {baseType} {{
                    constructor(...args) {{
                        super(...args);
                    }} 
                }}
                const o = new My{baseType}();
                o.constructor === My{baseType}";

        var engine = new Engine();
        Assert.True(engine.Evaluate(code).AsBoolean());
    }

    [Fact]
    public void ShouldHavePrototypeInPlaceByDefault()
    {
        var engine = new Engine();
        var instance = new JsObject(engine);
        Assert.NotNull(instance.GetPrototypeOf());
        Assert.Equal("[object Object]", instance.ToString());
    }
}