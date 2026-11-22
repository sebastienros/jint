using Jint.Native;

namespace Jint.Tests.Runtime;

public class ObjectInstanceTests
{
    [Test]
    public void KeysValuesEntriesCollectEnumerableStringKeys()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { a: 1, b: 2 };
            JSON.stringify([Object.keys(o), Object.values(o), Object.entries(o)]);
            """).AsString();

        result.Should().Be("[[\"a\",\"b\"],[1,2],[[\"a\",1],[\"b\",2]]]");
    }

    [Test]
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

        result.Should().Be("[[\"a\",\"b\"],[1,4],2]");
    }

    [Test]
    public void EntriesPairsAreRealArrays()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var e = Object.entries({ a: 1 })[0];
            Array.isArray(e) && e.length === 2 && (e.push(3), e.length === 3);
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
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

        result.Should().Be("[[\"a\",\"b\"],[\"ownKeys\",\"gopd:a\",\"gopd:b\"]]");
    }

    /// <summary>
    /// A redefinition compares its value with SameValue
    /// (https://tc39.es/ecma262/#sec-validateandapplypropertydescriptor), so -0 is not the 0 a property holds:
    /// a non-writable 0 refuses it and a writable one stores it, even when every attribute is restated.
    /// </summary>
    [Test]
    public void RedefiningZeroAsNegativeZeroIsAChangeOfValue()
    {
        var engine = new Engine();
        engine.Execute("""
            var fixed = {};
            Object.defineProperty(fixed, 'x', { value: 0, writable: false, enumerable: true, configurable: false });
            var open = {};
            Object.defineProperty(open, 'x', { value: 0, writable: true, enumerable: true, configurable: true });
            """);

        engine.Evaluate("Reflect.defineProperty(fixed, 'x', { value: -0, writable: false, enumerable: true, configurable: false })")
            .AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.is(fixed.x, 0)").AsBoolean().Should().BeTrue();

        engine.Evaluate("Reflect.defineProperty(open, 'x', { value: -0, writable: true, enumerable: true, configurable: true })")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.is(open.x, -0)").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RemovingFirstPropertyFromObjectInstancePropertiesBucketAndEnumerating()
    {
        var engine = new Engine();
        var instance = new JsObject(engine);
        instance.DefineOwnDataPropertyUnchecked("bare", JsValue.Null);
        instance.DefineOwnDataPropertyUnchecked("scope", JsValue.Null);
        instance.RemoveOwnProperty("bare");
        var propertyNames = instance.GetOwnProperties().Select(x => x.Key).ToList();
        propertyNames.Should().Equal(new JsValue[] { "scope" });
    }

    [TestCase("Array")]
    [TestCase("Boolean")]
    [TestCase("Date")]
    [TestCase("Error")]
    [TestCase("Number")]
    [TestCase("Object")]
    [TestCase("String")]
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
        engine.Evaluate(code).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ShouldHavePrototypeInPlaceByDefault()
    {
        var engine = new Engine();
        var instance = new JsObject(engine);
        instance.GetPrototypeOf().Should().NotBeNull();
        instance.ToString().Should().Be("[object Object]");
    }
}