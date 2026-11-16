#nullable enable

using System.Text.Json.Nodes;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A System.Text.Json <c>JsonValue</c> crosses into script as the number, string, boolean or null it holds, and a
/// <c>JsonObject</c> or <c>JsonArray</c> serializes to its JSON text, on every target framework (#4681).
/// </summary>
/// <remarks>
/// The expected answers are what Node gives for the same JSON parsed by <c>JSON.parse</c>:
/// <see href="https://tc39.es/ecma262/#sec-typeof-operator">typeof</see> of a primitive names its type,
/// <see href="https://tc39.es/ecma262/#sec-json.stringify">JSON.stringify</see> writes the text back, and
/// <see href="https://tc39.es/ecma262/#sec-object.keys">Object.keys</see> lists the members in order. On .NET
/// Framework a <c>JsonValue</c> used to be wrapped as a CLR object, so <c>typeof</c> answered <c>object</c> and
/// <c>JSON.stringify</c> walked into the node's <c>Root</c> and threw <c>Cyclic reference detected.</c>
/// </remarks>
public class HostSystemTextJsonValueTests
{
    private const string ObjectJson = """{"a":1,"b":"x","c":[1,2],"d":{"e":true},"n":null}""";
    private const string ArrayJson = """[1,"two",{"three":3},[false,null,2.5]]""";

    private static Engine CreateEngine() => new Engine()
        .SetValue("obj", JsonNode.Parse(ObjectJson))
        .SetValue("arr", JsonNode.Parse(ArrayJson))
        .SetValue("holder", new NodeHolder(JsonNode.Parse("""{"k":"v"}""")!, JsonValue.Create(5)));

    [TestCase("JSON.stringify(obj)", ObjectJson)]
    [TestCase("JSON.stringify(arr)", ArrayJson)]
    [TestCase("JSON.stringify(holder.Node)", """{"k":"v"}""")]
    [TestCase("JSON.stringify(obj.d)", """{"e":true}""")]
    [TestCase("JSON.stringify(arr[3])", "[false,null,2.5]")]
    [TestCase("JSON.stringify({ wrapped: obj.c, value: holder.Value })", """{"wrapped":[1,2],"value":5}""")]
    public void StringifyGivesTheJsonText(string script, string expected)
    {
        CreateEngine().Evaluate(script).AsString().Should().Be(expected);
    }

    [TestCase("typeof obj.a", "number")]
    [TestCase("typeof obj.b", "string")]
    [TestCase("typeof obj.d.e", "boolean")]
    [TestCase("typeof obj.c[0]", "number")]
    [TestCase("typeof arr[0]", "number")]
    [TestCase("typeof arr[1]", "string")]
    [TestCase("typeof arr[2].three", "number")]
    [TestCase("typeof arr[3][0]", "boolean")]
    [TestCase("typeof arr[3][2]", "number")]
    [TestCase("typeof holder.Value", "number")]
    [TestCase("typeof obj.c", "object")]
    [TestCase("typeof obj.d", "object")]
    public void AValueCrossesAsThePrimitiveItHolds(string script, string expected)
    {
        CreateEngine().Evaluate(script).AsString().Should().Be(expected);
    }

    [Test]
    public void AValueIsEqualToThePrimitiveItHolds()
    {
        CreateEngine().Evaluate("""
            [
                obj.a === 1,
                obj.b === 'x',
                obj.d.e === true,
                obj.n === null,
                obj.c[1] === 2,
                arr[1] === 'two',
                arr[2].three === 3,
                arr[3][0] === false,
                arr[3][1] === null,
                arr[3][2] === 2.5,
                holder.Value + 1 === 6
            ].join()
            """).AsString().Should().Be("true,true,true,true,true,true,true,true,true,true,true");
    }

    [Test]
    public void ObjectKeysListsTheMembers()
    {
        CreateEngine().Evaluate("Object.keys(obj).join()").AsString().Should().Be("a,b,c,d,n");
    }

    [Test]
    public void AValueCreatedByTheHostCrossesAsItsPrimitive()
    {
        var engine = new Engine()
            .SetValue("number", JsonValue.Create(5))
            .SetValue("fraction", JsonValue.Create(1.5))
            .SetValue("money", JsonValue.Create(1.1m))
            .SetValue("big", JsonValue.Create(long.MaxValue))
            .SetValue("text", JsonValue.Create("é\"\n<&>"))
            .SetValue("yes", JsonValue.Create(true))
            .SetValue("no", JsonValue.Create(false));

        engine.Evaluate("""
            [typeof number, typeof fraction, typeof money, typeof big, typeof text, typeof yes, typeof no].join()
            """).AsString().Should().Be("number,number,number,number,string,boolean,boolean");

        engine.Evaluate("""
            [
                number === 5,
                fraction === 1.5,
                money === 1.1,
                big === 9223372036854775807,
                text === 'é"\n<&>',
                yes === true,
                no === false
            ].join()
            """).AsString().Should().Be("true,true,true,true,true,true,true");
    }

    [Test]
    public void AParsedStringIsUnescapedAndAParsedNumberIsTheNearestDouble()
    {
        var engine = new Engine().SetValue("node", JsonNode.Parse("""
            {"s":"é\"\\\n<&>","small":0.1,"exp":-1.5e-7,"huge":1e400}
            """));

        engine.Evaluate("""
            [
                node.s === 'é"\\\n<&>',
                node.small === 0.1,
                node.exp === -1.5e-7,
                node.huge === Infinity
            ].join()
            """).AsString().Should().Be("true,true,true,true");
    }

    public sealed class NodeHolder(JsonNode node, JsonNode value)
    {
        public JsonNode Node { get; } = node;

        public JsonNode Value { get; } = value;
    }
}
