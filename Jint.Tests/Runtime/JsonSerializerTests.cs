using Jint.Native;
using Jint.Native.Json;

namespace Jint.Tests.Runtime;

public class JsonSerializerTests
{
    [Fact]
    public void CanStringifyBasicTypes()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        Assert.Equal("null", serializer.Serialize(JsValue.Null).ToString());
        Assert.Equal("true", serializer.Serialize(JsBoolean.True).ToString());
        Assert.Equal("false", serializer.Serialize(JsBoolean.False).ToString());
        Assert.Equal("\"\"", serializer.Serialize(new JsString("")).ToString());
        Assert.Equal("\"abc\"", serializer.Serialize(new JsString("abc")).ToString());
        Assert.Equal("1", serializer.Serialize(new JsNumber(1)).ToString());
        Assert.Equal("0.5", serializer.Serialize(new JsNumber(0.5)).ToString());
        Assert.Equal("{}", serializer.Serialize(new JsObject(engine)).ToString());
        Assert.Equal("[]", serializer.Serialize(new JsArray(engine)).ToString());

        Assert.Same(JsValue.Undefined, serializer.Serialize(JsValue.Undefined));
    }

    [Fact]
    public void EmptyObjectHasNoLineBreakWithSpaceDefined()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        Assert.Equal("{}", serializer.Serialize(new JsObject(engine), JsValue.Undefined, new JsString("  ")).ToString());
    }

    [Fact]
    public void EmptyArrayHasNoLineBreakWithSpaceDefined()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        Assert.Equal("[]", serializer.Serialize(new JsArray(engine), JsValue.Undefined, new JsString("  ")).ToString());
    }

    [Fact]
    public void StringCharactersGetEscaped()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        string actual = serializer.Serialize(new JsString("\"\\\t\r\n\f\r\b\ud834")).ToString();
        Assert.Equal("\"\\\"\\\\\\t\\r\\n\\f\\r\\b\\ud834\"", actual);
    }

    [Fact]
    public void JsonStringOutputIsIndentedWhenSpacerDefined()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = "b";
        instance["b"] = 2;
        instance["c"] = new JsArray(engine, new JsValue[] { new JsNumber(4), new JsNumber(5), new JsNumber(6) });
        instance["d"] = true;

        string actual = serializer.Serialize(instance, JsValue.Undefined, new JsNumber(2)).ToString();
        Assert.Equal("{\n  \"a\": \"b\",\n  \"b\": 2,\n  \"c\": [\n    4,\n    5,\n    6\n  ],\n  \"d\": true\n}", actual);
    }

    [Fact]
    public void JsonStringOutputIsCompactWithoutSpacer()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = "b";
        instance["b"] = 2;
        instance["c"] = new JsArray(engine, new JsValue[] { new JsNumber(4), new JsNumber(5), new JsNumber(6) });
        instance["d"] = true;

        string actual = serializer.Serialize(instance, JsValue.Undefined, JsValue.Undefined).ToString();
        Assert.Equal("{\"a\":\"b\",\"b\":2,\"c\":[4,5,6],\"d\":true}", actual);
    }

    [Fact]
    public void ArrayWithUndefinedWillBeNull()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsArray array = new JsArray(engine, new JsValue[] { JsValue.Undefined, new JsNumber(42) });
        string actual = serializer.Serialize(array, JsValue.Undefined, JsValue.Undefined).ToString();
        Assert.Equal("[null,42]", actual);
    }

    [Fact]
    public void ObjectPropertyWithUndefinedWillBeSkipped()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = JsValue.Undefined;
        instance["b"] = 42;
        string actual = serializer.Serialize(instance, JsValue.Undefined, JsValue.Undefined).ToString();
        Assert.Equal("{\"b\":42}", actual);
    }

    [Fact]
    public void NonStringObjectKeyWillSerializedAsString()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance[JsValue.Undefined] = 10;
        instance[JsValue.Null] = 21;
        instance[new JsNumber(10)] = 42;
        string actual = serializer.Serialize(instance, JsValue.Undefined, JsValue.Undefined).ToString();
        Assert.Equal("{\"10\":42,\"undefined\":10,\"null\":21}", actual);
    }

    [Fact]
    public void InfinityAndNaNGetsSerializedAsNull()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        JsArray array = new JsArray(engine, new JsValue[] { JsNumber.DoubleNegativeInfinity, JsNumber.DoublePositiveInfinity, JsNumber.DoubleNaN });
        string actual = serializer.Serialize(array, JsValue.Undefined, JsValue.Undefined).ToString();
        Assert.Equal("[null,null,null]", actual);
    }

    [Fact]
    public void ArrayAsReplacedDictatesPropertiesToSerializer()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = 21;
        instance["b"] = 42;
        JsValue replacer = new JsArray(engine, new JsValue[] { new JsString("b"), new JsString("z") });
        string actual = serializer.Serialize(instance, replacer, JsValue.Undefined).ToString();
        Assert.Equal("{\"b\":42}", actual);
    }

    [Theory]
    [InlineData("test123\n456", "\"test123\\n456\"")]
    [InlineData("test123456\n", "\"test123456\\n\"")]
    [InlineData("\u0002test\u0002", "\"\\u0002test\\u0002\"")]
    [InlineData("\u0002tes\tt\u0002", "\"\\u0002tes\\tt\\u0002\"")]
    [InlineData("t\u0002est\u0002", "\"t\\u0002est\\u0002\"")]
    [InlineData("test😀123456\n", "\"test😀123456\\n\"")]
    public void JsonStringEncodingFormatsContentCorrectly(string inputString, string expectedOutput)
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        string actual = serializer.Serialize(new JsString(inputString)).ToString();
        Assert.Equal(expectedOutput, actual);
    }
}