using System.Dynamic;
using Jint.Native;
using Jint.Native.Symbol;

namespace Jint.Tests.PublicInterface;

public partial class InteropTests
{
    [Fact]
    public void CanAccessExpandoObject()
    {
        var engine = new Engine();
        dynamic expando = new ExpandoObject();
        expando.Name = "test";
        engine.SetValue("expando", expando);
        Assert.Equal("test", engine.Evaluate("expando.Name").ToString());
    }

    [Fact]
    public void DebugView()
    {
        // allows displaying different local variables under debugger

        var engine = new Engine();
        var boolNet = true;
        var boolJint = (JsBoolean) boolNet;
        var doubleNet = 12.34;
        var doubleJint = (JsNumber) doubleNet;
        var integerNet = 42;
        var integerJint = (JsNumber) integerNet;
        var stringNet = "ABC";
        var stringJint = (JsString) stringNet;
        var arrayNet = new[] { 1, 2, 3 };
        var arrayListNet = new List<int> { 1, 2, 3 };
        var arrayJint = new JsArray(engine, arrayNet.Select(x => (JsNumber) x).ToArray());

        var objectNet = new Person { Name = "name", Age = 12 };
        var objectJint = new JsObject(engine);
        objectJint["name"] = "name";
        objectJint["age"] = 12;
        objectJint[GlobalSymbolRegistry.ToStringTag] = "Object";

        var dictionaryNet = new Dictionary<JsValue, JsValue>();
        dictionaryNet["name"] = "name";
        dictionaryNet["age"] = 12;
        dictionaryNet[GlobalSymbolRegistry.ToStringTag] = "Object";
    }

    [Fact]
    public void CanAccessMemberNamedItemThroughExpando()
    {
        var parent = (IDictionary<string, object>) new ExpandoObject();
        var child = (IDictionary<string, object>) new ExpandoObject();
        var values = (IDictionary<string, object>) new ExpandoObject();

        parent["child"] = child;
        child["item"] = values;
        values["title"] = "abc";

        _engine.SetValue("parent", parent);
        Assert.Equal("abc", _engine.Evaluate("parent.child.item.title"));
    }

    [Fact]
    public void ShouldForOfOnExpandoObject()
    {
        dynamic o = new ExpandoObject();
        o.a = 1;
        o.b = 2;

        _engine.SetValue("dynamic", o);

        var result = _engine.Evaluate("var l = ''; for (var x of dynamic) l += x; return l;").AsString();

        Assert.Equal("a,1b,2", result);
    }

    [Fact]
    public void ShouldConvertObjectInstanceToExpando()
    {
        _engine.Evaluate("var o = {a: 1, b: 'foo'}");
        var result = _engine.GetValue("o");

        dynamic value = result.ToObject();

        Assert.Equal(1, value.a);
        Assert.Equal("foo", value.b);

        var dic = (IDictionary<string, object>) result.ToObject();

        Assert.Equal(1d, dic["a"]);
        Assert.Equal("foo", dic["b"]);
    }

    [Fact]
    public void CanAccessDynamicObject()
    {
        var test = new DynamicClass();
        var engine = new Engine();

        engine.SetValue("test", test);

        Assert.Equal("a", engine.Evaluate("test.a").AsString());
        Assert.Equal("b", engine.Evaluate("test.b").AsString());

        engine.Evaluate("test.a = 5; test.b = 10; test.Name = 'Jint'");

        Assert.Equal(5, engine.Evaluate("test.a").AsNumber());
        Assert.Equal(10, engine.Evaluate("test.b").AsNumber());

        Assert.Equal("Jint", engine.Evaluate("test.Name").AsString());
        Assert.True(engine.Evaluate("test.ContainsKey('a')").AsBoolean());
        Assert.True(engine.Evaluate("test.ContainsKey('b')").AsBoolean());
        Assert.False(engine.Evaluate("test.ContainsKey('c')").AsBoolean());
    }

    [Fact]
    public void ShouldAccessCustomDynamicObjectProperties()
    {
        var t = new DynamicType
        {
            ["MemberKey"] = new MemberType
            {
                Field = 4
            }
        };
        var e = new Engine().SetValue("dynamicObj", t);
        Assert.Equal(4, ((dynamic) t).MemberKey.Field);
        Assert.Equal(4, e.Evaluate("dynamicObj.MemberKey.Field"));
    }

    private class MemberType
    {
        public int Field;
    }

    private class DynamicType : DynamicObject
    {
        private readonly Dictionary<string, object> _data = new();

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (_data.ContainsKey(binder.Name))
            {
                result = this[binder.Name];
                return true;
            }

            return base.TryGetMember(binder, out result);
        }

        public object this[string key]
        {
            get
            {
                _data.TryGetValue(key, out var value);
                return value;
            }
            set => _data[key] = value;
        }
    }

    private class DynamicClass : DynamicObject
    {
        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = binder.Name;
            if (_properties.TryGetValue(binder.Name, out var value))
            {
                result = value;
            }

            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _properties[binder.Name] = value;
            return true;
        }

        public string Name { get; set; }

        public bool ContainsKey(string key)
        {
            return _properties.ContainsKey(key);
        }
    }

    private class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
