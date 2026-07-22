using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class WeakSetMapTests
{
    [Fact]
    public void WeakMapShouldThrowWhenCalledWithoutNew()
    {
        var engine = new Engine();
        var e = Invoking(() => engine.Execute("{ const m = new WeakMap(); WeakMap.call(m,[]); }")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Constructor WeakMap requires 'new'");
    }

    [Fact]
    public void WeakSetShouldThrowWhenCalledWithoutNew()
    {
        var engine = new Engine();
        var e = Invoking(() => engine.Execute("{ const s = new WeakSet(); WeakSet.call(s,[]); }")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Constructor WeakSet requires 'new'");
    }

    public static TheoryData<JsValue> PrimitiveKeys = new()
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

        var e = Invoking(() => weakSet.WeakSetAdd(key)).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().StartWith("WeakSet value must be an object or symbol, got ");

        weakSet.WeakSetHas(key).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(PrimitiveKeys))]
    public void WeakMapSetShouldThrowForPrimitiveKey(JsValue key)
    {
        var engine = new Engine();
        var weakMap = new JsWeakMap(engine);

        var e = Invoking(() => weakMap.WeakMapSet(key, new JsObject(engine))).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().StartWith("WeakMap key must be an object, got ");

        weakMap.WeakMapHas(key).Should().BeFalse();
    }

    [Fact]
    public void WeakSetWithInteropObject()
    {
        var engine = new Engine(options => options.Interop.TrackObjectWrapperIdentity = true);

        engine.SetValue("context", new { Item = new Item { Value = "Test" } });

        engine.Evaluate(@"
		    var set1 = new WeakSet();
		    set1.add(context.Item);
		    return set1.has(context.Item);
	    ").Should().BeTrue();

        engine.Evaluate(@"
		    var item = context.Item;
		    var set2 = new WeakSet();
		    set2.add(item);
		    return set2.has(item);
    	").Should().BeTrue();
    }

    [Fact]
    public void RecentObjectWrapperCacheMaintainsIdentityForRepeatedWraps()
    {
        var engine = new Engine(options => options.Interop.CacheRecentObjectWrappers = true);

        engine.SetValue("context", new { Item = new Item { Value = "Test" } });

        engine.Evaluate("return context.Item === context.Item;").Should().BeTrue();

        engine.Evaluate(@"
		    var set1 = new WeakSet();
		    set1.add(context.Item);
		    return set1.has(context.Item);
	    ").Should().BeTrue();
    }

    [Fact]
    public void RecentObjectWrapperCacheIsBounded()
    {
        var engine = new Engine(options => options.Interop.CacheRecentObjectWrappers = true);

        var items = new List<Item>();
        for (var i = 0; i < 16; i++)
        {
            items.Add(new Item { Value = i.ToString() });
        }

        engine.SetValue("items", items);

        // touching more distinct objects than the cache holds must stay correct (oldest evicted)
        engine.Evaluate(@"
		    for (var i = 0; i < 16; i++) {
		      if (items[i].Value !== '' + i) {
		        return false;
		      }
		    }
		    return items[15] === items[15];
	    ").Should().BeTrue();
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
        engine.Evaluate("stringifyWithoutCircularReferences(context.Parent, Set)").Should().Be(Expected);

        // With WeakSet I get an error
        engine.Evaluate("stringifyWithoutCircularReferences(context.Parent, WeakSet)").Should().Be(Expected);
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
