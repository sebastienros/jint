#if !NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Expression = System.Linq.Expressions.Expression;
#endif

namespace Jint.Runtime.Interop;

/// <summary>
/// System.Text.Json's node types, recognized by name: Jint references the assembly only where the shared framework
/// carries it (.NET 8 on), but a host on any target framework can hand script a node from the package.
/// </summary>
internal static class SystemTextJsonNodes
{
    private const string JsonNodeTypeName = "System.Text.Json.Nodes.JsonNode";
    private const string JsonObjectTypeName = "System.Text.Json.Nodes.JsonObject";
    private const string JsonArrayTypeName = "System.Text.Json.Nodes.JsonArray";

    internal static bool IsJsonNode(Type? type) => IsNamed(type, JsonNodeTypeName);

    internal static bool IsJsonObject(Type? type) => IsNamed(type, JsonObjectTypeName);

    internal static bool IsJsonArray(Type? type) => IsNamed(type, JsonArrayTypeName);

    private static bool IsNamed(Type? type, string fullName)
        => type is not null && string.Equals(type.FullName, fullName, StringComparison.Ordinal);

#if !NET8_0_OR_GREATER
    private const string JsonValueTypeName = "System.Text.Json.Nodes.JsonValue";

    /// <summary>
    /// Per runtime type, how to read a <c>JsonValue</c>, or <see langword="null"/> for every other type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, JsonValueReader?> _readers = new();

    /// <summary>
    /// The number, string, boolean or null a <c>JsonValue</c> holds, by the rules of the typed lane .NET 8 on uses
    /// (<c>DefaultObjectConverter.ConvertSystemTextJsonValue</c>); <see langword="false"/> for any other value, and
    /// for a <c>JsonValue</c> holding an object or an array, which is wrapped as before.
    /// </summary>
    /// <remarks>
    /// Recognized by its type, once per runtime type: <c>JsonValue</c>'s concrete subclasses are internal and differ
    /// between package versions, so the test is whether the type derives from it. A number is asked of the node
    /// through <c>TryGetValue&lt;T&gt;</c> in the typed lane's order, because the writer behind
    /// <c>ToJsonString()</c> refuses NaN and the infinities; the JSON text answers only what those cannot, which
    /// tells a string from a boolean, a null, a decimal, an object and an array in every package version.
    /// </remarks>
    internal static bool TryConvertValue(object value, Type runtimeType, [NotNullWhen(true)] out JsValue? result)
    {
        var reader = _readers.GetOrAdd(runtimeType, static type => CreateReader(type));
        if (reader is null)
        {
            result = null;
            return false;
        }

        result = Convert(reader, value);
        return result is not null;
    }

    private static JsValue? Convert(JsonValueReader reader, object value)
    {
        if (reader.TryGetInt32(value, out var intValue))
        {
            return JsNumber.Create(intValue);
        }

        if (reader.TryGetDouble(value, out var doubleValue))
        {
            return JsNumber.Create(doubleValue);
        }

        if (reader.TryGetSingle(value, out var floatValue) && !float.IsFinite(floatValue))
        {
            return JsNumber.Create(floatValue);
        }

        var json = reader.ToJsonString(value);
        if (json.Length == 0)
        {
            return null;
        }

        switch (json[0])
        {
            case '"':
                // what the typed lane reads too: a string node's ToString() is the string itself, unquoted
                return JsString.Create(value.ToString());
            case 't' when string.Equals(json, "true", StringComparison.Ordinal):
                return JsBoolean.True;
            case 'f' when string.Equals(json, "false", StringComparison.Ordinal):
                return JsBoolean.False;
            case 'n' when string.Equals(json, "null", StringComparison.Ordinal):
                return JsValue.Null;
            case '{':
            case '[':
                return null;
            default:
                // a number neither an int nor a double holds, a decimal say: its text, as the typed lane reads it
                return NumberParser.TryParseDouble(json.AsSpan(), out doubleValue)
                    ? JsNumber.Create(doubleValue)
                    : JsNumber.DoubleNaN;
        }
    }

    private static JsonValueReader? CreateReader(Type type)
    {
        Type? jsonValueType = null;
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (IsNamed(t, JsonValueTypeName))
            {
                jsonValueType = t;
                break;
            }
        }

        if (jsonValueType is null || !IsJsonNode(jsonValueType.BaseType))
        {
            return null;
        }

        return _readersByJsonValueType.GetOrAdd(jsonValueType, static t => CreateJsonValueReader(t));
    }

    /// <summary>
    /// One reader per System.Text.Json assembly's <c>JsonValue</c>, shared by all of its concrete subclasses.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, JsonValueReader?> _readersByJsonValueType = new();

    /// <summary>
    /// Binds the reader's questions to <paramref name="jsonValueType"/>'s members, or answers <see langword="null"/>,
    /// which wraps the value as before, when a package version or the runtime does not let them be bound.
    /// </summary>
    /// <remarks>
    /// <c>TryGetValue&lt;T&gt;</c> is a generic virtual method, which .NET Framework will not bind an open-instance
    /// delegate to, so the reads are compiled expressions instead; they are built once per assembly.
    /// </remarks>
    private static JsonValueReader? CreateJsonValueReader(Type jsonValueType)
    {
        // public bool TryGetValue<T>(out T value), declared on JsonValue in every package version
        MethodInfo? tryGetValue = null;
        foreach (var method in jsonValueType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (string.Equals(method.Name, "TryGetValue", StringComparison.Ordinal)
                && method.IsGenericMethodDefinition
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is { Length: 1 } parameters
                && parameters[0].ParameterType.IsByRef)
            {
                tryGetValue = method;
                break;
            }
        }

        // public string ToJsonString(JsonSerializerOptions? options = null), declared on JsonNode
        MethodInfo? toJsonString = null;
        foreach (var method in jsonValueType.BaseType!.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (string.Equals(method.Name, "ToJsonString", StringComparison.Ordinal)
                && method.ReturnType == typeof(string)
                && method.GetParameters() is { Length: 1 } parameters
                && !parameters[0].ParameterType.IsValueType)
            {
                toJsonString = method;
                break;
            }
        }

        if (tryGetValue is null || toJsonString is null)
        {
            return null;
        }

        try
        {
            var node = Expression.Parameter(typeof(object), "node");
            var typedNode = Expression.Convert(node, jsonValueType);

            TryRead<TResult> BindTryGetValue<TResult>()
            {
                var result = Expression.Parameter(typeof(TResult).MakeByRefType(), "result");
                var call = Expression.Call(typedNode, tryGetValue.MakeGenericMethod(typeof(TResult)), result);
                return Expression.Lambda<TryRead<TResult>>(call, node, result).Compile();
            }

            var toJson = Expression.Lambda<Func<object, string>>(
                Expression.Call(typedNode, toJsonString, Expression.Constant(null, toJsonString.GetParameters()[0].ParameterType)),
                node).Compile();

            return new JsonValueReader(BindTryGetValue<int>(), BindTryGetValue<double>(), BindTryGetValue<float>(), toJson);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException or MemberAccessException)
        {
            return null;
        }
    }

    private delegate bool TryRead<TResult>(object node, out TResult result);

    /// <summary>
    /// The four questions the lane asks a <c>JsonValue</c>.
    /// </summary>
    private sealed class JsonValueReader(TryRead<int> tryGetInt32, TryRead<double> tryGetDouble, TryRead<float> tryGetSingle, Func<object, string> toJsonString)
    {
        public bool TryGetInt32(object value, out int result) => tryGetInt32(value, out result);

        public bool TryGetDouble(object value, out double result) => tryGetDouble(value, out result);

        public bool TryGetSingle(object value, out float result) => tryGetSingle(value, out result);

        public string ToJsonString(object value) => toJsonString(value);
    }
#endif
}