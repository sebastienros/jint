using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Json;

/// <summary>
/// Represents a Raw JSON value created by JSON.rawJSON().
/// https://tc39.es/proposal-json-parse-with-source/#sec-json.rawjson
/// </summary>
internal sealed class JsRawJson : ObjectInstance
{
    internal static readonly JsString RawJsonPropertyName = new("rawJSON");

    private readonly string _rawJson;

    private JsRawJson(Engine engine, string rawJson) : base(engine, ObjectClass.Object)
    {
        _rawJson = rawJson;
        // Set prototype to null per spec
        _prototype = null;
    }

    /// <summary>
    /// Creates a new RawJSON object.
    /// https://tc39.es/proposal-json-parse-with-source/#sec-json.rawjson
    /// </summary>
    internal static JsRawJson Create(Engine engine, string rawJsonText)
    {
        var obj = new JsRawJson(engine, rawJsonText);

        // Create the rawJSON property
        obj.DefineOwnProperty(RawJsonPropertyName, new PropertyDescriptor(new JsString(rawJsonText), PropertyFlag.ConfigurableEnumerableWritable));

        // Freeze the object
        obj.PreventExtensions();
        obj.SetIntegrityLevel(IntegrityLevel.Frozen);

        return obj;
    }

    /// <summary>
    /// Gets the raw JSON string.
    /// </summary>
    internal string RawJson => _rawJson;
}
