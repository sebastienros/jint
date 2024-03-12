using System.Reflection;
using System.Text.Json.Nodes;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public partial class InteropTests
{
    [Fact]
    public void AccessingJsonNodeShouldWork()
    {
        const string Json = """
        {
            "employees": {
                "boolean": true,
                "number": 123.456,
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

        var engine = new Engine(options =>
        {
            // make JsonArray behave like JS array
            options.Interop.WrapObjectHandler = static (e, target, type) =>
            {
                if (target is JsonArray)
                {
                    var wrapped = new ObjectWrapper(e, target);
                    wrapped.Prototype = e.Intrinsics.Array.PrototypeObject;
                    return wrapped;
                }

                if (target is JsonValue jsonValue)
                {
                    if (jsonValue.TryGetValue<bool>(out var boolValue))
                    {
                        return e.Construct("Boolean", boolValue ? JsBoolean.True : JsBoolean.False);
                    }

                    if (jsonValue.TryGetValue<double>(out var doubleValue))
                    {
                        return e.Construct("Number", JsNumber.Create(doubleValue));
                    }

                    return e.Construct("String", (JsString)jsonValue.ToString());
                }

                return new ObjectWrapper(e, target);
            };

            // we cannot access this[string] with anything else than JsonObject, otherwise itw will throw
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberFilter = static info =>
                {
                    if (info.ReflectedType != typeof(JsonObject) && info.Name == "Item" && info is PropertyInfo p)
                    {
                        var parameters = p.GetIndexParameters();
                        return parameters.Length != 1 || parameters[0].ParameterType != typeof(string);
                    }

                    return true;
                }
            };
        });

        engine
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
        Assert.True(engine.Evaluate("variables.employees.boolean == true").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.number == 123.456").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.other == 'abc'").AsBoolean());

        // mutating data via JS
        engine.Evaluate("variables.employees.type = 'array2'");
        engine.Evaluate("variables.employees.value[0].firstName = 'Jake'");

        //Assert.Equal("array2", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("Jake Doe", result[0].AsObject()["fullName"]);

        // mutating original object that is wrapped inside the engine
        variables["employees"]["boolean"] = false;
        variables["employees"]["number"] = 456.789;
        variables["employees"]["other"] = "def";
        variables["employees"]["type"] = "array";
        variables["employees"]["value"][0]["firstName"] = "John";

        Assert.Equal("array", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
        Assert.True(engine.Evaluate("variables.employees.boolean == false").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.number == 456.789").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.other == 'def'").AsBoolean());
    }
}
