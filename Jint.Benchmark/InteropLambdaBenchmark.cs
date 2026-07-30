using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Benchmark;

/// <summary>
/// Finding a record in a host-supplied collection, across the four shapes an embedder hands over
/// (<see cref="TestDataType"/>) and the five ways a host can drive the script function.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, carrying only that row's own
/// <c>findIt</c>. It used to be one engine on which <c>[GlobalSetup]</c> evaluated <em>both</em> script
/// variants — and since both declare <c>function findIt</c> at global scope, the second install even
/// overwrote the first's global binding — so every row was measured on an engine carrying the other
/// variant's handler-tree, call-site and wrapper-cache state. The rows still measure the same warm
/// invocation paths, and engine construction and function compilation stay in <c>[GlobalSetup]</c>,
/// outside the measurement. <b>Numbers from this class are not comparable to any published before the
/// harness changed.</b></para>
/// </summary>
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

    // One (engine, findIt) pair per row: the function is engine-affine, so isolating the engine means
    // compiling that row's own script on it.
    private Engine _inlineEngineInvokeEngine;
    private Function _inlineEngineInvokeFunction;

    private Engine _inlineEngine;
    private Function _inlineFunction;

    private Engine _inlineCSharpEngine;
    private Func<JsValue, JsValue[], JsValue> _inlineCSharpFunction;

    private Engine _forLoopEngine;
    private Function _forLoopFunction;

    private Engine _forLoopEngineInvokeEngine;
    private Function _forLoopEngineInvokeFunction;

    [Params(TestDataType.ClrObject, TestDataType.Dictionary, TestDataType.JsonNode, TestDataType.JsValue)]
    public TestDataType Type { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
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

        // Each row compiles its own findIt on its own engine, so no row inherits the other script
        // variant's handler-tree, call-site or wrapper-cache state.
        _inlineEngineInvokeEngine = new Engine();
        _inlineEngineInvokeFunction = (Function) _inlineEngineInvokeEngine.Evaluate(ScriptInline + "findIt;");

        _inlineEngine = new Engine();
        _inlineFunction = (Function) _inlineEngine.Evaluate(ScriptInline + "findIt;");

        _inlineCSharpEngine = new Engine();
        _inlineCSharpFunction = (Func<JsValue, JsValue[], JsValue>) ((Function) _inlineCSharpEngine.Evaluate(ScriptInline + "findIt;")).ToObject();

        _forLoopEngine = new Engine();
        _forLoopFunction = (Function) _forLoopEngine.Evaluate(ScriptForLoop + "findIt;");

        _forLoopEngineInvokeEngine = new Engine();
        _forLoopEngineInvokeFunction = (Function) _forLoopEngineInvokeEngine.Evaluate(ScriptForLoop + "findIt;");
    }

    [Benchmark]
    public void InlineEngineInvoke()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _inlineEngineInvokeEngine.Invoke(_inlineEngineInvokeFunction!, [_data, FindValue]).ToObject();
        }
    }

    [Benchmark]
    public void Inline()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _inlineFunction!.Call(JsValue.FromObject(_inlineEngine, _data), JsValue.FromObject(_inlineEngine, FindValue)).ToObject();
        }
    }

    [Benchmark]
    public void InlineCSharp()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _inlineCSharpFunction(JsValue.Undefined, [JsValue.FromObject(_inlineCSharpEngine, _data), JsValue.FromObject(_inlineCSharpEngine, FindValue)]).ToObject();
        }
    }

    [Benchmark(Baseline = true)]
    public void ForLoop()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _forLoopFunction!.Call(JsValue.FromObject(_forLoopEngine, _data), JsValue.FromObject(_forLoopEngine, FindValue)).ToObject();
        }
    }

    [Benchmark]
    public void ForLoopEngineInvoke()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var value = _forLoopEngineInvokeEngine.Invoke(_forLoopEngineInvokeFunction!, [_data, FindValue]).ToObject();
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
