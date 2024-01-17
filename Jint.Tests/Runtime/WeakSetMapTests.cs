using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class WeakSetMapTests
{
    [Fact]
    public void WeakMapShouldThrowWhenCalledWithoutNew()
    {
        var engine = new Engine();
        var e = Assert.Throws<JavaScriptException>(() => engine.Execute("{ const m = new WeakMap(); WeakMap.call(m,[]); }"));
        Assert.Equal("Constructor WeakMap requires 'new'", e.Message);
    }

    [Fact]
    public void WeakSetShouldThrowWhenCalledWithoutNew()
    {
        var engine = new Engine();
        var e = Assert.Throws<JavaScriptException>(() => engine.Execute("{ const s = new WeakSet(); WeakSet.call(s,[]); }"));
        Assert.Equal("Constructor WeakSet requires 'new'", e.Message);
    }

    public static IEnumerable<object[]> PrimitiveKeys = new TheoryData<JsValue>
    {
        JsValue.Null,
        JsValue.Undefined,
        0,
        100.04,
        double.NaN,
        "hello",
        true
    };

    [Theory]
    [MemberData(nameof(PrimitiveKeys))]
    public void WeakSetAddShouldThrowForPrimitiveKey(JsValue key)
    {
        var engine = new Engine();
        var weakSet = new JsWeakSet(engine);

        var e = Assert.Throws<JavaScriptException>(() => weakSet.WeakSetAdd(key));
        Assert.StartsWith("WeakSet value must be an object or symbol, got ", e.Message);

        Assert.False(weakSet.WeakSetHas(key));
    }

    [Theory]
    [MemberData(nameof(PrimitiveKeys))]
    public void WeakMapSetShouldThrowForPrimitiveKey(JsValue key)
    {
        var engine = new Engine();
        var weakMap = new JsWeakMap(engine);

        var e = Assert.Throws<JavaScriptException>(() => weakMap.WeakMapSet(key, new JsObject(engine)));
        Assert.StartsWith("WeakMap key must be an object, got ", e.Message);

        Assert.False(weakMap.WeakMapHas(key));
    }

    [Fact]
    public void WeakSetWithInteropObject()
    {
        var engine = new Engine(options => options.Interop.TrackObjectWrapperIdentity = true);

        engine.SetValue("context", new { Item = new Item { Value = "Test" } });

        Assert.Equal(true, engine.Evaluate(@"
		    var set1 = new WeakSet();
		    set1.add(context.Item);
		    return set1.has(context.Item);
	    "));

        Assert.Equal(true, engine.Evaluate(@"
		    var item = context.Item;
		    var set2 = new WeakSet();
		    set2.add(item);
		    return set2.has(item);
    	"));
    }

    [Fact]
    public void StringifyWithoutCircularReferences()
    {
        var parent = new Parent { Value = "Parent" };
        var child = new Child { Value = "Child" };
        parent.Child = child;
        child.Parent = parent;

        var engine = new Engine(options => options.Interop.TrackObjectWrapperIdentity = true);

        engine.SetValue("context", new { Parent = parent });

        engine.Execute(@"
		    function stringifyWithoutCircularReferences(obj, setConstructor) {
		      var getCircularReplacer = () => {
			    var seen = new setConstructor();
			    return (key, value) => {
			      if (typeof value === ""object"" && value !== null) {
				    if (seen.has(value)) {
				      return undefined;
				    }
				    seen.add(value);
			      }
			      return value;
			    };
		      };
		      return JSON.stringify(obj, getCircularReplacer());
		    }
	    ");

        // If I use Set, everything works as expected
        const string Expected = "{\"Value\":\"Parent\",\"Child\":{\"Value\":\"Child\"}}";
        Assert.Equal(Expected, engine.Evaluate("stringifyWithoutCircularReferences(context.Parent, Set)"));

        // With WeakSet I get an error
        Assert.Equal(Expected, engine.Evaluate("stringifyWithoutCircularReferences(context.Parent, WeakSet)"));
    }

    private class Item
    {
        public string Value { get; set; }
    }

    private class Parent
    {
        public string Value { get; set; }
        public Child Child { get; set; }
    }

    private class Child
    {
        public string Value { get; set; }
        public Parent Parent { get; set; }
    }
}
