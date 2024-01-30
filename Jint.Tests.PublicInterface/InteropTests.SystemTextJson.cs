using System.Reflection;
using System.Text.Json.Nodes;
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
                var wrapped = new ObjectWrapper(e, target);
                if (target is JsonArray)
                {
                    wrapped.Prototype = e.Intrinsics.Array.PrototypeObject;
                }
                return wrapped;
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

        // mutating data via JS
        engine.Evaluate("variables.employees.type = 'array2'");
        engine.Evaluate("variables.employees.value[0].firstName = 'Jake'");

        Assert.Equal("array2", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("Jake Doe", result[0].AsObject()["fullName"]);

        // mutating original object that is wrapped inside the engine
        variables["employees"]["type"] = "array";
        variables["employees"]["value"][0]["firstName"] = "John";

        Assert.Equal("array", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
    }
}
