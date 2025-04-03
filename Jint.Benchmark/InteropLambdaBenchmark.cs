using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Benchmark;

[RankColumn]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class InteropLambdaBenchmark
{
    private TestData[] _testArray;
    private TestDataRoot _root;
    private object _data;
    private const string FindValue = "SomeKind22222";

    private const int Iterations = 10;

    private Engine _engine;

    private const string ScriptInline = """
                                        function findIt(data, value) {
                                            return data.array.find(x => x.value == value);
                                        }
                                        """;

    private const string ScriptForLoop = """
                                         function findIt(data, value) {
                                             const array = data.array;
                                             const length = array.length;
                                             for (let i = 0; i < length; i++) {
                                                 const item = array[i];
                                                 if (item.value == value) {
                                                     return item;
                                                 }
                                             }
                                             
                                             return null;
                                         }
                                         """;

    private Function _forLoopFunction;
    private Function _inlineFunction;
    private Func<JsValue, JsValue[], JsValue> _inlineCSharpFunction;

    [Params(TestDataType.ClrObject, TestDataType.Dictionary, TestDataType.JsonNode, TestDataType.JsValue)]
    public TestDataType Type { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();

        _testArray = [new TestData("SomeKind00000"), new TestData("SomeKind1111"), new TestData(FindValue)];
        _root = new TestDataRoot(_testArray);

        if (Type == TestDataType.ClrObject)
        {
            _data = _root;
        }
        else if (Type == TestDataType.JsonNode)
        {
            _data = JsonSerializer.SerializeToNode(_root, JsonDefaults.JsonSerializerOptions);
        }
        else if (Type == TestDataType.Dictionary)
        {
            _data = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(_root, JsonDefaults.JsonSerializerOptions), JsonDefaults.JsonSerializerOptions);
        }
        else if (Type == TestDataType.JsValue)
        {
            _data = JsonSerializer.Deserialize<JsObject>(JsonSerializer.Serialize(_root, JsonDefaults.JsonSerializerOptions), JsonDefaults.JsonSerializerOptions);
        }

        _inlineFunction = (Function) _engine.Evaluate(ScriptInline + "findIt;");
        _inlineCSharpFunction = (Func<JsValue, JsValue[], JsValue>) _inlineFunction.ToObject();
        _forLoopFunction = (Function) _engine.Evaluate(ScriptForLoop + "findIt;");
    }

    [Benchmark]
    public void InlineEngineInvoke()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _engine.Invoke(_inlineFunction!, [_data, FindValue]).ToObject();
        }
    }

    [Benchmark]
    public void Inline()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _inlineFunction!.Call(JsValue.FromObject(_engine, _data), JsValue.FromObject(_engine, FindValue)).ToObject();
        }
    }

    [Benchmark]
    public void InlineCSharp()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _inlineCSharpFunction(JsValue.Undefined, [JsValue.FromObject(_engine, _data), JsValue.FromObject(_engine, FindValue)]).ToObject();
        }
    }

    [Benchmark(Baseline = true)]
    public void ForLoop()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _forLoopFunction!.Call(JsValue.FromObject(_engine, _data), JsValue.FromObject(_engine, FindValue)).ToObject();
        }
    }

    [Benchmark]
    public void ForLoopEngineInvoke()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _engine.Invoke(_forLoopFunction!, [_data, FindValue]).ToObject();
        }
    }
}

public class TestDataRoot
{
    public TestData[] array { get; set; }

    public TestDataRoot(TestData[] array)
    {
        this.array = array;
    }
}


public record TestData(string value);

public sealed class DictionaryStringObjectJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var dictionary = new Dictionary<string, object>(JsonDefaults.DictionaryCapacity);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            string propertyName = reader.GetString();

            reader.Read();

            dictionary[propertyName] = ReadValue(ref reader, options);
        }

        throw new JsonException();
    }

    private object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out long l))
                {
                    return l;
                }

                return reader.GetDouble();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                return Read(ref reader, typeof(Dictionary<string, object>), options);
            case JsonTokenType.StartArray:
                var list = new List<object>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        return list;
                    }

                    list.Add(ReadValue(ref reader, options));
                }

                throw new JsonException();
            default:
                throw new JsonException();
        }
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            WriteValue(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case null:
                writer.WriteNullValue();
                break;
            case Dictionary<string, object> dict:
                writer.WriteStartObject();
                foreach (var kvp in dict)
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteValue(writer, kvp.Value, options);
                }

                writer.WriteEndObject();
                break;
            case List<object> list:
                writer.WriteStartArray();
                foreach (var item in list)
                {
                    WriteValue(writer, item, options);
                }

                writer.WriteEndArray();
                break;
            case JsonNode node:
                JsonSerializer.Serialize(writer, node, options);
                break;
            default:
                throw new InvalidOperationException($"Unsupported type: {value?.GetType()}");
        }
    }
}

public sealed class NativeJsValueJsonConverter : JsonConverter<JsObject>
{
    private readonly Engine _engine = new();

    public override JsObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var dictionary = new JsObject(_engine);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            string propertyName = reader.GetString();

            reader.Read();

            dictionary[propertyName] = ReadValue(ref reader, options);
        }

        throw new JsonException();
    }

    private JsValue ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out long l))
                {
                    return l;
                }

                return reader.GetDouble();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                return Read(ref reader, typeof(JsObject), options);
            case JsonTokenType.StartArray:
                var list = new JsArray(_engine);
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        return list;
                    }

                    list.Push(ReadValue(ref reader, options));
                }

                throw new JsonException();
            default:
                throw new JsonException();
        }
    }

    public override void Write(Utf8JsonWriter writer, JsObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value.GetOwnProperties())
        {
            writer.WritePropertyName(kvp.Key.ToString());
            WriteValue(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case null:
                writer.WriteNullValue();
                break;
            case Dictionary<string, object> dict:
                writer.WriteStartObject();
                foreach (var kvp in dict)
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteValue(writer, kvp.Value, options);
                }

                writer.WriteEndObject();
                break;
            case List<object> list:
                writer.WriteStartArray();
                foreach (var item in list)
                {
                    WriteValue(writer, item, options);
                }

                writer.WriteEndArray();
                break;
            case JsonNode node:
                JsonSerializer.Serialize(writer, node, options);
                break;
            default:
                throw new InvalidOperationException($"Unsupported type: {value?.GetType()}");
        }
    }
}

public static class JsonDefaults
{
    public const int DictionaryCapacity = 4;

    public static JsonSerializerOptions JsonSerializerOptions { get; }

    static JsonDefaults()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DictionaryStringObjectJsonConverter());
        options.Converters.Add(new NativeJsValueJsonConverter());

        JsonSerializerOptions = options;
    }
}

public enum TestDataType
{
    ClrObject,
    JsonNode,
    Dictionary,
    JsValue
}
