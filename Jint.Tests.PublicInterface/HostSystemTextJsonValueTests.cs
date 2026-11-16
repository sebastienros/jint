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

    [Theory]
    [InlineData("JSON.stringify(obj)", ObjectJson)]
    [InlineData("JSON.stringify(arr)", ArrayJson)]
    [InlineData("JSON.stringify(holder.Node)", """{"k":"v"}""")]
    [InlineData("JSON.stringify(obj.d)", """{"e":true}""")]
    [InlineData("JSON.stringify(arr[3])", "[false,null,2.5]")]
    [InlineData("JSON.stringify({ wrapped: obj.c, value: holder.Value })", """{"wrapped":[1,2],"value":5}""")]
    public void StringifyGivesTheJsonText(string script, string expected)
    {
        CreateEngine().Evaluate(script).AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData("typeof obj.a", "number")]
    [InlineData("typeof obj.b", "string")]
    [InlineData("typeof obj.d.e", "boolean")]
    [InlineData("typeof obj.c[0]", "number")]
    [InlineData("typeof arr[0]", "number")]
    [InlineData("typeof arr[1]", "string")]
    [InlineData("typeof arr[2].three", "number")]
    [InlineData("typeof arr[3][0]", "boolean")]
    [InlineData("typeof arr[3][2]", "number")]
    [InlineData("typeof holder.Value", "number")]
    [InlineData("typeof obj.c", "object")]
    [InlineData("typeof obj.d", "object")]
    public void AValueCrossesAsThePrimitiveItHolds(string script, string expected)
    {
        CreateEngine().Evaluate(script).AsString().Should().Be(expected);
    }

    [Fact]
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

    [Fact]
    public void ObjectKeysListsTheMembers()
    {
        CreateEngine().Evaluate("Object.keys(obj).join()").AsString().Should().Be("a,b,c,d,n");
    }

    [Fact]
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

    [Fact]
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

    /// <summary>
    /// NaN and the infinities have no JSON spelling, and System.Text.Json's writer refuses them, so they must be read
    /// from the node rather than from its text. Negative zero keeps its sign, as <c>-0</c> does in script.
    /// </summary>
    [Fact]
    public void ANonFiniteNumberOrNegativeZeroCrossesAsItself()
    {
        var engine = new Engine()
            .SetValue("nan", JsonValue.Create(double.NaN))
            .SetValue("pinf", JsonValue.Create(double.PositiveInfinity))
            .SetValue("ninf", JsonValue.Create(double.NegativeInfinity))
            .SetValue("fnan", JsonValue.Create(float.NaN))
            .SetValue("fpinf", JsonValue.Create(float.PositiveInfinity))
            .SetValue("fninf", JsonValue.Create(float.NegativeInfinity))
            .SetValue("negzero", JsonValue.Create(-0.0));

        engine.Evaluate("""
            [nan, pinf, ninf, fnan, fpinf, fninf, negzero].map(x => typeof x).join()
            """).AsString().Should().Be("number,number,number,number,number,number,number");

        engine.Evaluate("""
            [
                Number.isNaN(nan),
                pinf === Infinity,
                ninf === -Infinity,
                Number.isNaN(fnan),
                fpinf === Infinity,
                fninf === -Infinity,
                Object.is(negzero, -0)
            ].join()
            """).AsString().Should().Be("true,true,true,true,true,true,true");
    }

    [Fact]
    public void AStringThatReadsAsAnotherKindStaysAString()
    {
        var engine = new Engine()
            .SetValue("node", JsonNode.Parse("""{"s":"5","t":"true","n":"null","o":"{}"}"""))
            .SetValue("created", JsonValue.Create("5"));

        engine.Evaluate("""
            [
                typeof node.s, node.s === '5',
                typeof node.t, node.t === 'true',
                typeof node.n, node.n === 'null',
                typeof node.o, node.o === '{}',
                typeof created, created === '5'
            ].join()
            """).AsString().Should().Be("string,true,string,true,string,true,string,true,string,true");
    }

    /// <summary>
    /// A <c>JsonValue</c> created around a CLR object or collection holds no primitive, so it is wrapped like any
    /// other CLR object. Converting it used to come back to the same conversion and overflow the stack on .NET 10.
    /// </summary>
    [Fact]
    public void AValueHoldingAnObjectOrAnArrayIsWrapped()
    {
#pragma warning disable IL2026, IL3050 // the reflection-based JsonValue.Create<T> is what builds such a value
        var poco = JsonValue.Create(new JsonValuePoco());
        var list = JsonValue.Create(new[] { 1, 2 });
#pragma warning restore IL2026, IL3050

        var engine = new Engine().SetValue("poco", poco).SetValue("list", list);

        engine.Evaluate("typeof poco + ',' + typeof list").AsString().Should().Be("object,object");
        engine.Evaluate("poco").ToObject().Should().BeSameAs(poco);
        engine.Evaluate("list").ToObject().Should().BeSameAs(list);
    }

    public sealed class JsonValuePoco
    {
        public int K { get; set; } = 1;
    }

    public sealed class NodeHolder(JsonNode node, JsonNode value)
    {
        public JsonNode Node { get; } = node;

        public JsonNode Value { get; } = value;
    }
}
