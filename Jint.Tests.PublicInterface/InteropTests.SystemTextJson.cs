using System.Text.Json.Nodes;
using Jint.Native;
using Jint.Runtime.Interop;
using System.Text.Json;

namespace Jint.Tests.PublicInterface;

public partial class InteropTests
{
    [Fact]
    public void ArrayPrototypeFindWithInteropJsonArray()
    {
        var engine = GetEngine();

        var array = new JsonArray { "A", "B", "C" };
        engine.SetValue("array", array);

        engine.Evaluate("array.findIndex((x) => x === 'B')").Should().Be(1);
        engine.Evaluate("array.find((x) => x === 'B')").Should().Be('B');
    }

    [Fact]
    public void ArrayPrototypePushWithInteropJsonArray()
    {
        var engine = GetEngine();

        var array = new JsonArray { "A", "B", "C" };
        engine.SetValue("array", array);

        engine.Evaluate("array.push('D')");
        array.Should().HaveCount(4);
        (array[3]?.ToString()).Should().Be("D");
        engine.Evaluate("array.lastIndexOf('D')").Should().Be(3);
    }

    [Fact]
    public void ArrayPrototypePopWithInteropJsonArray()
    {
        var engine = GetEngine();

        var array = new JsonArray { "A", "B", "C" };
        engine.SetValue("array", array);

        engine.Evaluate("array.lastIndexOf('C')").Should().Be(2);
        array.Should().HaveCount(3);
        engine.Evaluate("array.pop()").Should().Be("C");
        array.Should().HaveCount(2);
        engine.Evaluate("array.lastIndexOf('C')").Should().Be(-1);
    }

    [Fact]
    public void AccessingJsonNodeShouldWork()
    {
        const string Json = """
                            {
                                "falseValue": false,
                                "employees": {
                                    "trueValue": true,
                                    "falseValue": false,
                                    "number": 123.456,
                                    "zeroNumber": 0,
                                    "emptyString":"",
                                    "nullValue":null,
                                    "other": "abc",
                                    "type": "array",
                                    "value": [
                                        {
                                            "firstName": "John",
                                            "lastName": "Doe"
                                        },
                                        {
                                            "firstName": "Jane",
                                            "lastName": "Doe"
                                        }
                                    ]
                                }
                            }
                            """;

        var variables = JsonNode.Parse(Json);

        var engine = GetEngine();

        engine
            .SetValue("falseValue", false)
            .SetValue("variables", variables)
            .Execute("""
                         function populateFullName() {
                             return variables['employees'].value.map(item => {
                                 var newItem =
                                 {
                                     "firstName": item.firstName,
                                     "lastName": item.lastName,
                                     "fullName": item.firstName + ' ' + item.lastName
                                 };
                     
                                 return newItem;
                             });
                         }
                     """);

        // reading data
        var result = engine.Evaluate("populateFullName()").AsArray();
        result.Length.Should().Be((uint) 2);
        result[0].AsObject()["fullName"].Should().Be("John Doe");
        result[1].AsObject()["fullName"].Should().Be("Jane Doe");
        engine.Evaluate("variables.employees.trueValue == true").AsBoolean().Should().BeTrue();
        engine.Evaluate("variables.employees.number == 123.456").AsBoolean().Should().BeTrue();
        engine.Evaluate("variables.employees.other == 'abc'").AsBoolean().Should().BeTrue();

        // mutating data via JS
        engine.Evaluate("variables.employees.type = 'array2'");
        engine.Evaluate("variables.employees.value[0].firstName = 'Jake'");

        //engine.Evaluate("variables['employees']['type']").ToString().Should().Be("array2");

        result = engine.Evaluate("populateFullName()").AsArray();
        result.Length.Should().Be((uint) 2);
        result[0].AsObject()["fullName"].Should().Be("Jake Doe");

        // Validate boolean value in the if condition.
        engine.Evaluate("if(!falseValue){ return 1 ;} else {return 0;}").AsNumber().Should().Be(1);
        engine.Evaluate("if(falseValue===false){ return 1 ;} else {return 0;}").AsNumber().Should().Be(1);
        engine.Evaluate("!variables.zeroNumber").AsBoolean().Should().BeTrue();
        engine.Evaluate("!variables.emptyString").AsBoolean().Should().BeTrue();
        engine.Evaluate("!variables.nullValue").AsBoolean().Should().BeTrue();
        var result2 = engine.Evaluate("!variables.falseValue");
        var result3 = engine.Evaluate("!falseValue");
        var result4 = engine.Evaluate("variables.falseValue");
        var result5 = engine.Evaluate("falseValue");
        result2.Should().NotBeNull();

        engine.Evaluate("if(variables.falseValue===false){ return 1 ;} else {return 0;}").AsNumber().Should().Be(1);
        engine.Evaluate("if(falseValue===variables.falseValue){ return 1 ;} else {return 0;}").AsNumber().Should().Be(1);
        engine.Evaluate("if(!variables.falseValue){ return 1 ;} else {return 0;}").AsNumber().Should().Be(1);
        engine.Evaluate("if(!variables.employees.falseValue){ return 1 ;} else {return 0;}").AsNumber().Should().Be(1);
        engine.Evaluate("if(!variables.employees.trueValue) return 1 ; else return 0;").AsNumber().Should().Be(0);


        // mutating original object that is wrapped inside the engine
        variables["employees"]["trueValue"] = false;
        variables["employees"]["number"] = 456.789;
        variables["employees"]["other"] = "def";
        variables["employees"]["type"] = "array";
        variables["employees"]["value"][0]["firstName"] = "John";

        engine.Evaluate("variables['employees']['type']").ToString().Should().Be("array");

        result = engine.Evaluate("populateFullName()").AsArray();
        result.Length.Should().Be((uint) 2);
        result[0].AsObject()["fullName"].Should().Be("John Doe");
        engine.Evaluate("variables.employees.trueValue == false").AsBoolean().Should().BeTrue();
        engine.Evaluate("variables.employees.number == 456.789").AsBoolean().Should().BeTrue();
        engine.Evaluate("variables.employees.other == 'def'").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AccessingSystemTextJsonNumericTypes()
    {
        var engine = GetEngine();

        engine.SetValue("int", JsonValue.Create(15)).Evaluate("int").Should().Be(15);
        engine.SetValue("double", JsonValue.Create(15.0)).Evaluate("double").Should().Be(15.0);
        engine.SetValue("float", JsonValue.Create(15.0f)).Evaluate("float").Should().Be(15f);
    }

    private static Engine GetEngine()
    {
        var engine = new Engine(options =>
        {
#if !NET8_0_OR_GREATER
            // Jint doesn't know about the types statically as they are not part of the out-of-the-box experience
            options.AddObjectConverter(SystemTextJsonValueConverter.Instance);
#endif
        });

        return engine;
    }
}

file sealed class SystemTextJsonValueConverter : IObjectConverter
{
    public static readonly SystemTextJsonValueConverter Instance = new();

    private SystemTextJsonValueConverter()
    {
    }

    public bool TryConvert(Engine engine, object value, out JsValue result)
    {
        if (value is JsonValue jsonValue)
        {
            var valueKind = jsonValue.GetValueKind();
            result = valueKind switch
            {
                JsonValueKind.Object or JsonValueKind.Array => JsValue.FromObject(engine, jsonValue),
                JsonValueKind.String => jsonValue.ToString(),
#pragma warning disable IL2026, IL3050
                JsonValueKind.Number => jsonValue.TryGetValue<int>(out var intValue) ? JsNumber.Create(intValue) : JsonSerializer.Deserialize<double>(jsonValue),
#pragma warning restore IL2026, IL3050
                JsonValueKind.True => JsBoolean.True,
                JsonValueKind.False => JsBoolean.False,
                JsonValueKind.Undefined => JsValue.Undefined,
                JsonValueKind.Null => JsValue.Null,
                _ => JsValue.Undefined
            };

            return true;
        }

        result = JsValue.Undefined;
        return false;
    }
}
