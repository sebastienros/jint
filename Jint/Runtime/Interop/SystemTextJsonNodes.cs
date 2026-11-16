#if !NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
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
    /// Per runtime type, how to read a <c>JsonValue</c>'s JSON text, or <see langword="null"/> for every other type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<object, string>?> _jsonTextReaders = new();

    /// <summary>
    /// The number, string, boolean or null a <c>JsonValue</c> holds, by the rules of the typed lane .NET 8 on uses
    /// (<c>DefaultObjectConverter.ConvertSystemTextJsonValue</c>); <see langword="false"/> for any other value, and
    /// for a <c>JsonValue</c> holding an object or an array, which is wrapped as before.
    /// </summary>
    /// <remarks>
    /// Recognized by its type, once per runtime type: <c>JsonValue</c>'s concrete subclasses are internal and differ
    /// between package versions, so the test is whether the type derives from it. Its kind is read from
    /// <c>ToJsonString()</c>, the one member every package version has that tells a string from any other kind.
    /// </remarks>
    internal static bool TryConvertValue(object value, Type runtimeType, [NotNullWhen(true)] out JsValue? result)
    {
        var reader = _jsonTextReaders.GetOrAdd(runtimeType, static type => CreateJsonTextReader(type));
        if (reader is null)
        {
            result = null;
            return false;
        }

        result = ConvertJsonText(value, reader(value));
        return result is not null;
    }

    private static JsValue? ConvertJsonText(object value, string json)
    {
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
                return ConvertJsonNumber(json);
        }
    }

    /// <summary>
    /// A JSON number as the nearest JavaScript number, as <c>DefaultObjectConverter.ConvertSystemTextJsonNumber</c>
    /// reads one: an integer that fits an <see cref="int"/> first, then the nearest double.
    /// </summary>
    /// <remarks>
    /// The text cannot say whether the node was parsed or built around a CLR number, which the typed lane's
    /// <c>TryGetValue&lt;int&gt;</c> can; the two agree on every value but one. A parsed <c>-0</c> is <c>+0</c> on both,
    /// and so is <c>JsonValue.Create(-0.0)</c> here, which the typed lane reads as <c>-0</c>.
    /// </remarks>
    private static JsNumber ConvertJsonNumber(string json)
    {
        var integral = true;
        foreach (var c in json)
        {
            if (c is '.' or 'e' or 'E')
            {
                integral = false;
                break;
            }
        }

        if (integral && int.TryParse(json, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var intValue))
        {
            return JsNumber.Create(intValue);
        }

        return NumberParser.TryParseDouble(json.AsSpan(), out var doubleValue)
            ? JsNumber.Create(doubleValue)
            : JsNumber.DoubleNaN;
    }

    private static Func<object, string>? CreateJsonTextReader(Type type)
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

        var jsonNodeType = jsonValueType?.BaseType;
        if (!IsJsonNode(jsonNodeType))
        {
            return null;
        }

        // public string ToJsonString(JsonSerializerOptions? options = null), in every package version
        foreach (var method in jsonNodeType!.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!string.Equals(method.Name, "ToJsonString", StringComparison.Ordinal) || method.ReturnType != typeof(string))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && !parameters[0].ParameterType.IsValueType)
            {
                return (Func<object, string>) typeof(SystemTextJsonNodes)
                    .GetMethod(nameof(CreateTypedJsonTextReader), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(jsonNodeType, parameters[0].ParameterType)
                    .Invoke(null, [method])!;
            }
        }

        return null;
    }

    private static Func<object, string> CreateTypedJsonTextReader<TNode, TOptions>(MethodInfo toJsonString)
        where TNode : class
        where TOptions : class
    {
        var call = (Func<TNode, TOptions?, string>) Delegate.CreateDelegate(typeof(Func<TNode, TOptions?, string>), toJsonString);
        return node => call((TNode) node, null);
    }
#endif
}
