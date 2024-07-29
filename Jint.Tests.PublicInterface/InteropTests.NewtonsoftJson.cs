using Jint.Native;
using Jint.Runtime;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

public partial class InteropTests
{
    [Fact]
    public void AccessingJObjectShouldWork()
    {
        var o = new JObject
        {
            new JProperty("name", "test-name")
        };
        _engine.SetValue("o", o);
        Assert.True(_engine.Evaluate("return o.name == 'test-name'").AsBoolean());
    }

    [Fact]
    public void AccessingJArrayViaIntegerIndexShouldWork()
    {
        var o = new JArray("item1", "item2");
        _engine.SetValue("o", o);
        Assert.True(_engine.Evaluate("return o[0] == 'item1'").AsBoolean());
        Assert.True(_engine.Evaluate("return o[1] == 'item2'").AsBoolean());
    }

    [Fact]
    public void DictionaryLikeShouldCheckIndexerAndFallBackToProperty()
    {
        const string json = @"{ ""Type"": ""Cat"" }";
        var jObjectWithTypeProperty = JObject.Parse(json);

        _engine.SetValue("o", jObjectWithTypeProperty);

        var typeResult = _engine.Evaluate("o.Type");

        // JToken requires conversion
        Assert.Equal("Cat", TypeConverter.ToString(typeResult));

        // weak equality does conversions from native types
        Assert.True(_engine.Evaluate("o.Type == 'Cat'").AsBoolean());
    }

    [Fact]
    public void ShouldBeAbleToIndexJObjectWithStrings()
    {
        var engine = new Engine();

        const string json = @"
            {
                'Properties': {
                    'expirationDate': {
                        'Value': '2021-10-09T00:00:00Z'
                    }
                }
            }";

        var obj = JObject.Parse(json);
        engine.SetValue("o", obj);
        var value = engine.Evaluate("o.Properties.expirationDate.Value");
        var dateInstance = Assert.IsAssignableFrom<JsDate>(value);
        Assert.Equal(DateTime.Parse("2021-10-09T00:00:00Z").ToUniversalTime(), dateInstance.ToDateTime());
    }

    // https://github.com/OrchardCMS/OrchardCore/issues/10648
    [Fact]
    public void EngineShouldStringifyAnJObjectListWithValuesCorrectly()
    {
        var engine = new Engine();
        var queryResults = new List<dynamic>
        {
            new { Text = "Text1", Value = 1 },
            new { Text = "Text2", Value = 2 }
        };

        engine.SetValue("testSubject", queryResults.Select(x => JObject.FromObject(x)));
        var fromEngine = engine.Evaluate("return JSON.stringify(testSubject);");
        var result = fromEngine.ToString();

        // currently we do not materialize LINQ enumerables
        // Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2}]", result);

        Assert.Equal("{\"Current\":null}", result);
    }

    [Fact]
    public void EngineShouldStringifyJObjectFromObjectListWithValuesCorrectly()
    {
        var engine = new Engine();

        var source = new dynamic[]
        {
            new { Text = "Text1", Value = 1 },
            new { Text = "Text2", Value = 2, Null = (object) null, Date = new DateTime(2015, 6, 25, 0, 0, 0, DateTimeKind.Utc) }
        };

        engine.SetValue("testSubject", source.Select(x => JObject.FromObject(x)).ToList());
        var fromEngine = engine.Evaluate("return JSON.stringify(testSubject);");
        var result = fromEngine.ToString();

        Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2,\"Null\":null,\"Date\":\"2015-06-25T00:00:00.000Z\"}]", result);
    }

    [Fact]
    public void DecimalsShouldBeHandledFromJObjects()
    {
        var test = JObject.FromObject(new
        {
            DecimalValue = 123.456m
        });
        _engine.SetValue("test", test);
        var fromInterop = _engine.Evaluate("test.DecimalValue");
        var number = Assert.IsType<JsNumber>(fromInterop);
        Assert.Equal(123.456d, number.AsNumber());
    }

    [Fact]
    public void ShouldBeAbleToChangePropertyWithNameValue()
    {
        var engine = new Engine();

        var input = Newtonsoft.Json.JsonConvert.DeserializeObject(@"{ ""value"": ""ORIGINAL"" }");
        var result = engine
            .SetValue("input", input)
            .Evaluate("input.value = \"CHANGED\"; input.value")
            .AsString();

        Assert.Equal("CHANGED", result);
    }

    [Fact]
    public void ArraysShouldPassThroughCorrectly()
    {
        var engine = new Engine();

        const string Json = """
                            {
                                'entries': [
                                    { 'id': 1, 'name': 'One' },
                                    { 'id': 2, 'name': 'Two' },
                                    { 'id': 3, 'name': 'Three' }
                                ]
                            }
                            """;

        var obj = JObject.Parse(Json);
        engine.SetValue("o", obj);

        var names = engine.Evaluate("o.entries.map(e => e.name)").AsArray();

        Assert.Equal((uint) 3, names.Length);
        Assert.Equal("One", names[0]);
        Assert.Equal("Two", names[1]);
        Assert.Equal("Three", names[2]);
    }
}