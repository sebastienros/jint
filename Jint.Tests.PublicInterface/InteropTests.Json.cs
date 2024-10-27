using System.Dynamic;
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

        var expected = Serialize(TimeSpan.FromSeconds(3));
        var actual = engine.Evaluate("JSON.stringify(TimeSpan.FromSeconds(3));");
        actual.AsString().Should().Be(expected);

        expected = Serialize(testObject);
        actual = engine.Evaluate("JSON.stringify(TestObject)");
        actual.AsString().Should().Be(expected);

        actual = engine.Evaluate("JSON.stringify({ nestedValue: TestObject })");
        actual.AsString().Should().Be($$"""{"nestedValue":{{expected}}}""");
        return;

        string Serialize(object o)
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
}
