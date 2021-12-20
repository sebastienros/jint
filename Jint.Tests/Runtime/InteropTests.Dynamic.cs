using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Xunit;

namespace Jint.Tests.Runtime
{
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
        public void EngineShouldStringifyAnExpandoObjectCorrectly()
        {
            var engine = new Engine();

            dynamic expando = new ExpandoObject();
            expando.foo = 5;
            expando.bar = "A string";
            engine.SetValue(nameof(expando), expando);

            var result = engine.Evaluate($"JSON.stringify({nameof(expando)})").AsString();
            Assert.Equal("{\"foo\":5,\"bar\":\"A string\"}", result);
        }

        [Fact]
        public void EngineShouldStringifyAnExpandoObjectWithValuesCorrectly()
        {
            // https://github.com/sebastienros/jint/issues/995
            var engine = new Engine();

            dynamic expando = new ExpandoObject();
            expando.Values = 1;
            engine.SetValue("expando", expando);

            Assert.Equal("{\"Values\":1}", engine.Evaluate($"JSON.stringify(expando)").AsString());
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
        public void EngineShouldStringifyAnJObjectArrayWithValuesCorrectly()
        {
            //https://github.com/OrchardCMS/OrchardCore/issues/10648
            var engine = new Engine();
            var queryResults = new List<dynamic>();
            queryResults.Add(new { Text = "Text1", Value = 1 });
            queryResults.Add(new { Text = "Text2", Value = 2 });

            engine.SetValue("testSubject", queryResults.ToArray());
            var fromEngine2 = engine.Evaluate("return JSON.stringify(testSubject);");
            var result2 = fromEngine2.ToString();
            Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2}]", result2);
        }

        [Fact]
        public void EngineShouldStringifyDynamicObjectListWithValuesCorrectly()
        {
            var engine = new Engine();
            var source = new dynamic[] { new { Text = "Text1", Value = 1 }, new { Text = "Text2", Value = 2 } };

            var objects = source.ToList();
            engine.SetValue("testSubject", objects);
            var fromEngine = engine.Evaluate("return JSON.stringify(testSubject);");
            var result = fromEngine.ToString();
            Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2}]", result);
        }

        [Fact]
        public void EngineShouldStringifyDynamicObjectArrayWithValuesCorrectly()
        {
            var engine = new Engine();
            var source = new dynamic[] { new { Text = "Text1", Value = 1 }, new { Text = "Text2", Value = 2 } };

            engine.SetValue("testSubject", source.AsEnumerable());
            var fromEngine = engine.Evaluate("return JSON.stringify(testSubject);");
            var result = fromEngine.ToString();
            Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2}]", result);
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
    }
}