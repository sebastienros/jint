using System.Reflection;
using System.Text.Json.Nodes;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

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
            // JsonArray behave like JS array
            options.Interop.WrapObjectHandler = static (e, target, type) =>
            {
                var wrapped = new ObjectWrapper(e, target);
                if (target is JsonArray)
                {
                    wrapped.SetPrototypeOf(e.Realm.Intrinsics.Array.PrototypeObject);
                }
                return wrapped;
            };

            // we cannot access this[string] with anything else than JsonObject, otherwise itw will throw
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberFilter = static info =>
                {
                    if (info.DeclaringType != typeof(JsonObject) && info.Name == "Item" && info is PropertyInfo p)
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

        var result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
        Assert.Equal("Jane Doe", result[1].AsObject()["fullName"]);
    }
}
