using System.Dynamic;
using System.Text;
using FluentAssertions;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public partial class InteropTests
{
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
    public void CanStringifyTimeSpanUsingCustomToJsonHook()
    {
        var engine = new Engine(options =>
        {
            options.Interop.MemberAccessor = (e, target, member) =>
            {
                // custom serializer hook for known object types
                if (member == "toJSON" && target is TimeSpan)
                {
                    return new ClrFunction(e, "toJSON", (thisObject, _) =>
                    {
                        // here could check other types too
                        var wrappedInstance = ((IObjectWrapper) thisObject).Target;
                        return wrappedInstance.ToString();
                    });
                }

                return null;
            };
        });

        var expected = Newtonsoft.Json.JsonConvert.SerializeObject(TimeSpan.FromSeconds(3));

        engine.SetValue("TimeSpan", TypeReference.CreateTypeReference<TimeSpan>(engine));
        var value = engine.Evaluate("JSON.stringify(TimeSpan.FromSeconds(3));");

        Assert.Equal(expected, value);
    }

    [Fact]
    public void CanStringifyUsingSerializeToJson()
    {
        object testObject = new { Foo = "bar", FooBar = new { Foo = 123.45, Foobar = new DateTime(2022, 7, 16, 0, 0, 0, DateTimeKind.Utc) } };

        // without interop

        var e = new Engine();
        e.SetValue("TimeSpan", typeof(TimeSpan));
#if NETFRAMEWORK
        e.Evaluate("JSON.stringify(TimeSpan.FromSeconds(3))").AsString().Should().Be("""{"Ticks":30000000,"Days":0,"Hours":0,"Milliseconds":0,"Minutes":0,"Seconds":3,"TotalDays":0.00003472222222222222,"TotalHours":0.0008333333333333333,"TotalMilliseconds":3000,"TotalMinutes":0.05,"TotalSeconds":3}""");
#else
        e.Evaluate("JSON.stringify(TimeSpan.FromSeconds(3))").AsString().Should().Be("""{"Ticks":30000000,"Days":0,"Hours":0,"Milliseconds":0,"Microseconds":0,"Nanoseconds":0,"Minutes":0,"Seconds":3,"TotalDays":0.00003472222222222222,"TotalHours":0.0008333333333333334,"TotalMilliseconds":3000,"TotalMicroseconds":3000000,"TotalNanoseconds":3000000000,"TotalMinutes":0.05,"TotalSeconds":3}""");
#endif

        e.SetValue("TestObject", testObject);
        e.Evaluate("JSON.stringify(TestObject)").AsString().Should().Be("""{"Foo":"bar","FooBar":{"Foo":123.45,"Foobar":"2022-07-16T00:00:00.000Z"}}""");

        // interop using Newtonsoft serializer, for example with snake case naming

        var engine = new Engine(options =>
        {
            options.Interop.SerializeToJson = Serialize;
        });
        engine.SetValue("TimeSpan", TypeReference.CreateTypeReference<TimeSpan>(engine));
        engine.SetValue("TestObject", testObject);

        var expected = Serialize(TimeSpan.FromSeconds(3), string.Empty);
        var actual = engine.Evaluate("JSON.stringify(TimeSpan.FromSeconds(3));");
        actual.AsString().Should().Be(expected);

        expected = Serialize(testObject, string.Empty);
        actual = engine.Evaluate("JSON.stringify(TestObject)");
        actual.AsString().Should().Be(expected);

        actual = engine.Evaluate("JSON.stringify({ nestedValue: TestObject })");
        actual.AsString().Should().Be($$"""{"nestedValue":{{expected}}}""");

        static string Serialize(object o, string space, string currentIndent = null)
        {
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                {
                    NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                }
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(o, settings);
        }
    }

    [Fact]
    public void CanStringifyUsingSerializeToJsonWithIndentation()
    {
        object testObject = new { Foo = "bar", FooBar = new { Foo = 123.45, Array = Array.Empty<int>() } };
        var e = new Engine(o => o.Interop.SerializeToJson = SerializeIndentation);
        e.SetValue("TestObject", testObject);
        e.Evaluate("JSON.stringify(TestObject, null, 4)").AsString().Should().Be(
            """
            {
                "foo": "bar",
                "foo_bar": {
                    "foo": 123.45,
                    "array": []
                }
            }
            """
        );

        e.Evaluate("JSON.stringify({ nestedValue: TestObject }, null, 4)").AsString().Should().Be(
            """
            {
                "nestedValue": {
                    "foo": "bar",
                    "foo_bar": {
                        "foo": 123.45,
                        "array": []
                    }
                }
            }
            """.Replace("\r\n", "\n")
        );

        static string SerializeIndentation(object o, string space, string currentIndent)
        {
            var jsonSerializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                {
                    NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                }
            });

            var sb = new StringBuilder(256);
            var sw = new StringWriter(sb);
            using var jsonWriter = string.IsNullOrEmpty(space)
                ? new Newtonsoft.Json.JsonTextWriter(sw)
                : new(sw)
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    Indentation = space.Length,
                    IndentChar = space[0] // assuming `space` only contains one kind of character
                };

            jsonSerializer.Serialize(jsonWriter, o);

            if (string.IsNullOrEmpty(currentIndent))
            {
                return sw.ToString();
            }
            else
            {
                var lines = sw.ToString().Split('\n');
                var stringBuilder = new StringBuilder();

                for (var i = 0; i < lines.Length; i++)
                {
                    if (i == 0)
                    {
                        stringBuilder.AppendLine(lines[i]);
                    }
                    else
                    {
                        stringBuilder.AppendLine(currentIndent + lines[i]);
                    }
                }

                return stringBuilder.ToString().Replace("\r", string.Empty).TrimEnd('\n');
            }
        }
    }

    [Fact]
    public void CanStringifyClrObjectWithToJsonMethod()
    {
        // Test for GitHub issue: CLR class with toJSON method throws error on JSON.stringify
        var engine = new Engine();

        var obj = new TestObjectWithToJson { Name = "Test", Value = 42 };
        engine.SetValue("testObj", obj);

        var result = engine.Evaluate("JSON.stringify(testObj)").AsString();
        // toJSON returns a string, so it gets double-quoted like any string value
        result.Should().Be("\"custom:Test:42\"");
    }

    [Fact]
    public void CanStringifyClrObjectWithToJsonMethodReturningObject()
    {
        // Test toJSON method that returns an object
        var engine = new Engine();

        var obj = new TestObjectWithToJsonReturningObject { Name = "Test", Value = 42 };
        engine.SetValue("testObj", obj);

        var result = engine.Evaluate("JSON.stringify(testObj)").AsString();
        result.Should().Be("{\"customName\":\"Test\",\"customValue\":42}");
    }

    [Fact]
    public void CanStringifyArrayOfClrObjectsWithToJsonMethod()
    {
        // Test that toJSON works in arrays
        var engine = new Engine();

        var arr = new[] {
            new TestObjectWithToJsonReturningObject { Name = "First", Value = 1 },
            new TestObjectWithToJsonReturningObject { Name = "Second", Value = 2 }
        };
        engine.SetValue("testArr", arr);

        var result = engine.Evaluate("JSON.stringify(testArr)").AsString();
        result.Should().Be("[{\"customName\":\"First\",\"customValue\":1},{\"customName\":\"Second\",\"customValue\":2}]");
    }

    [Fact]
    public void ToJsonMethodIsNotEnumerable()
    {
        // Verify that toJSON is non-enumerable like built-in JS toJSON methods
        var engine = new Engine();

        var obj = new TestObjectWithToJson { Name = "Test", Value = 42 };
        engine.SetValue("testObj", obj);

        // toJSON should not appear in Object.keys() since it's non-enumerable
        var keys = engine.Evaluate("Object.keys(testObj)").ToString();
        keys.Should().NotContain("toJSON");

        // But it should exist on the object
        var hasToJson = engine.Evaluate("typeof testObj.toJSON").AsString();
        hasToJson.Should().Be("function");
    }

    private class TestObjectWithToJson
    {
        public string Name { get; set; }
        public int Value { get; set; }

        public string toJSON()
        {
            // toJSON that returns a string - the string itself will be serialized
            return $"custom:{Name}:{Value}";
        }
    }

    private class TestObjectWithToJsonReturningObject
    {
        public string Name { get; set; }
        public int Value { get; set; }

        public object toJSON()
        {
            // toJSON that returns an object - the object will be serialized
            return new { customName = Name, customValue = Value };
        }
    }

}
