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

        Assert.Equal(1, engine.Evaluate("array.findIndex((x) => x === 'B')"));
        Assert.Equal('B', engine.Evaluate("array.find((x) => x === 'B')"));
    }

    [Fact]
    public void ArrayPrototypePushWithInteropJsonArray()
    {
        var engine = GetEngine();

        var array = new JsonArray { "A", "B", "C" };
        engine.SetValue("array", array);

        engine.Evaluate("array.push('D')");
        Assert.Equal(4, array.Count);
        Assert.Equal("D", array[3]?.ToString());
        Assert.Equal(3, engine.Evaluate("array.lastIndexOf('D')"));
    }

    [Fact]
    public void ArrayPrototypePopWithInteropJsonArray()
    {
        var engine = GetEngine();

        var array = new JsonArray { "A", "B", "C" };
        engine.SetValue("array", array);

        Assert.Equal(2, engine.Evaluate("array.lastIndexOf('C')"));
        Assert.Equal(3, array.Count);
        Assert.Equal("C", engine.Evaluate("array.pop()"));
        Assert.Equal(2, array.Count);
        Assert.Equal(-1, engine.Evaluate("array.lastIndexOf('C')"));
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
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
        Assert.Equal("Jane Doe", result[1].AsObject()["fullName"]);
        Assert.True(engine.Evaluate("variables.employees.trueValue == true").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.number == 123.456").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.other == 'abc'").AsBoolean());

        // mutating data via JS
        engine.Evaluate("variables.employees.type = 'array2'");
        engine.Evaluate("variables.employees.value[0].firstName = 'Jake'");

        //Assert.Equal("array2", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("Jake Doe", result[0].AsObject()["fullName"]);

        // Validate boolean value in the if condition.
        Assert.Equal(1, engine.Evaluate("if(!falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(falseValue===false){ return 1 ;} else {return 0;}").AsNumber());
        Assert.True(engine.Evaluate("!variables.zeroNumber").AsBoolean());
        Assert.True(engine.Evaluate("!variables.emptyString").AsBoolean());
        Assert.True(engine.Evaluate("!variables.nullValue").AsBoolean());
        var result2 = engine.Evaluate("!variables.falseValue");
        var result3 = engine.Evaluate("!falseValue");
        var result4 = engine.Evaluate("variables.falseValue");
        var result5 = engine.Evaluate("falseValue");
        Assert.NotNull(result2);

        Assert.Equal(1, engine.Evaluate("if(variables.falseValue===false){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(falseValue===variables.falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(!variables.falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(!variables.employees.falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(0, engine.Evaluate("if(!variables.employees.trueValue) return 1 ; else return 0;").AsNumber());


        // mutating original object that is wrapped inside the engine
        variables["employees"]["trueValue"] = false;
        variables["employees"]["number"] = 456.789;
        variables["employees"]["other"] = "def";
        variables["employees"]["type"] = "array";
        variables["employees"]["value"][0]["firstName"] = "John";

        Assert.Equal("array", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
        Assert.True(engine.Evaluate("variables.employees.trueValue == false").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.number == 456.789").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.other == 'def'").AsBoolean());
    }

    [Fact]
    public void AccessingSystemTextJsonNumericTypes()
    {
        var engine = GetEngine();

        Assert.Equal(15, engine.SetValue("int", JsonValue.Create(15)).Evaluate("int"));
        Assert.Equal(15.0, engine.SetValue("double", JsonValue.Create(15.0)).Evaluate("double"));
        Assert.Equal(15f, engine.SetValue("float", JsonValue.Create(15.0f)).Evaluate("float"));
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
