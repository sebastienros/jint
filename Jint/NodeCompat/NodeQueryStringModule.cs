#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.NodeCompat;

/// <summary>
/// Builds the JavaScript surface of <c>node:querystring</c> over <see cref="NodeQueryString"/>.
/// <para>
/// https://nodejs.org/api/querystring.html
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>querystring</c> predates <c>URLSearchParams</c> and differs from it in three ways a caller notices, all
/// of them kept: the result of <c>parse</c> is prototype-less, so <c>obj.toString</c> and
/// <c>obj.hasOwnProperty</c> are <see langword="undefined"/> and a query string cannot smuggle a property in
/// through the prototype chain; a repeated key becomes an array rather than a multi-valued list; and the
/// separators are parameters, so <c>a:1;b:2</c> parses as readily as <c>a=1&amp;b=2</c>.
/// </para>
/// <para>
/// <c>escape</c> and <c>unescape</c> are replaceable, as Node documents them: <c>stringify</c> and
/// <c>parse</c> read them off the module object every time they run, so assigning <c>querystring.escape</c>
/// changes what <c>stringify</c> uses. The identity test Node makes against the original function is kept too
/// — it is what decides whether a number is written out directly or handed to the encoder first.
/// </para>
/// <para>
/// <c>querystring.unescapeBuffer</c> is absent: it answers with a <c>Buffer</c>, and <c>node:buffer</c> is
/// deliberately not one of the modules Jint provides.
/// </para>
/// </remarks>
internal static class NodeQueryStringModule
{
    private const int DefaultMaxKeys = 1000;

    internal static List<KeyValuePair<string, JsValue>> CreateExports(Engine engine)
    {
        var realm = engine.Realm;

        // The module object every export also hangs off, captured by the closures below so that a script
        // reassigning querystring.escape is observed by stringify, as Node documents.
        JsObject? moduleObject = null;

        var escape = NodeBuiltinHelpers.Operation(engine, realm, "escape", 1, (_, arguments) =>
            JsString.Create(NodeQueryString.Escape(Coerce(arguments.At(0)))));

        var unescape = NodeBuiltinHelpers.Operation(engine, realm, "unescape", 2, (_, arguments) =>
            JsString.Create(NodeQueryString.Unescape(
                TypeConverter.ToString(arguments.At(0)),
                TypeConverter.ToBoolean(arguments.At(1)))));

        var stringify = NodeBuiltinHelpers.Operation(engine, realm, "stringify", 4, (_, arguments) =>
            JsString.Create(Stringify(moduleObject!, escape, arguments)));

        var parse = NodeBuiltinHelpers.Operation(engine, realm, "parse", 4, (_, arguments) =>
            Parse(engine, moduleObject!, unescape, arguments));

        var entries = new List<KeyValuePair<string, JsValue>>
        {
            new("unescape", unescape),
            new("escape", escape),
            new("stringify", stringify),
            new("encode", stringify),
            new("parse", parse),
            new("decode", parse),
        };

        moduleObject = JsObject.CreateFromEntries(engine, entries);

        var exports = new List<KeyValuePair<string, JsValue>>(entries.Count + 1);
        exports.AddRange(entries);
        exports.Add(new KeyValuePair<string, JsValue>("default", moduleObject));
        return exports;
    }

    /// <summary>
    /// <c>querystring.stringify(obj[, sep[, eq[, options]]])</c>.
    /// <para>
    /// https://nodejs.org/api/querystring.html#querystringstringifyobj-sep-eq-options
    /// </para>
    /// </summary>
    private static string Stringify(JsObject moduleObject, JsValue originalEscape, JsCallArguments arguments)
    {
        var target = arguments.At(0);
        if (target is not ObjectInstance obj)
        {
            // "obj: The object to serialize into a URL query string" — anything else answers with "".
            return string.Empty;
        }

        var sep = Separator(arguments.At(1), "&");
        var eq = Separator(arguments.At(2), "=");

        var encode = moduleObject.Get("escape");
        var options = arguments.At(3);
        if (options is ObjectInstance optionsObject && optionsObject.Get("encodeURIComponent") is ICallable custom)
        {
            encode = (JsValue) custom;
        }

        // The identity test Node makes: only the built-in encoder is trusted to leave a finite number alone.
        var builtinEncoder = ReferenceEquals(encode, originalEscape);

        var fields = new StringBuilder();
        var keys = obj.EnumerableOwnProperties(ObjectInstance.EnumerableOwnPropertyNamesKind.Key);

        for (uint i = 0; i < keys.Length; i++)
        {
            keys.TryGetValue(i, out var keyValue);
            var key = TypeConverter.ToString(keyValue);
            var value = obj.Get(key);
            var encodedKey = Convert(JsString.Create(key), encode, builtinEncoder) + eq;

            if (value.IsArray())
            {
                var array = ArrayOperations.For(value.AsObject(), forWrite: false);
                var count = array.GetLongLength();
                if (count == 0)
                {
                    continue;
                }

                if (fields.Length > 0)
                {
                    fields.Append(sep);
                }

                for (ulong j = 0; j < count; j++)
                {
                    if (j > 0)
                    {
                        fields.Append(sep);
                    }

                    fields.Append(encodedKey);
                    fields.Append(Convert(array.Get(j), encode, builtinEncoder));
                }

                continue;
            }

            if (fields.Length > 0)
            {
                fields.Append(sep);
            }

            fields.Append(encodedKey);
            fields.Append(Convert(value, encode, builtinEncoder));
        }

        return fields.ToString();
    }

    /// <summary>
    /// Node's <c>encodeStringified</c> and <c>encodeStringifiedCustom</c>. "Numeric values must be finite. Any
    /// other input values will be coerced to empty strings."
    /// </summary>
    /// <remarks>
    /// The two differ in one place: with the built-in encoder a finite number below <c>1e21</c> is written out
    /// as-is, because none of its characters needs escaping, while <c>1e21</c> and above switch to exponential
    /// notation whose <c>+</c> does. A custom encoder is handed every value, since only it knows what it
    /// escapes.
    /// </remarks>
    private static string Convert(JsValue value, JsValue encode, bool builtinEncoder)
    {
        if (!builtinEncoder)
        {
            return CallEncoder(encode, StringifyPrimitive(value));
        }

        if (value is JsString text)
        {
            return text.Length == 0 ? string.Empty : NodeQueryString.Escape(text.ToString());
        }

        if (value.IsNumber())
        {
            var number = value.AsNumber();
            if (double.IsFinite(number))
            {
                var rendered = TypeConverter.ToString(number);
                return Math.Abs(number) < 1e21 ? rendered : NodeQueryString.Escape(rendered);
            }

            return string.Empty;
        }

        return StringifyPrimitive(value);
    }

    /// <summary>Node's <c>stringifyPrimitive</c>.</summary>
    private static string StringifyPrimitive(JsValue value)
    {
        if (value is JsString text)
        {
            return text.ToString();
        }

        if (value.IsNumber())
        {
            var number = value.AsNumber();
            return double.IsFinite(number) ? TypeConverter.ToString(number) : string.Empty;
        }

        if (value.IsBigInt())
        {
            return TypeConverter.ToString(value);
        }

        if (value.IsBoolean())
        {
            return value.AsBoolean() ? "true" : "false";
        }

        return string.Empty;
    }

    private static string CallEncoder(JsValue encode, string value)
    {
        var callable = (ICallable) encode;
        return TypeConverter.ToString(callable.Call(JsValue.Undefined, [JsString.Create(value)]));
    }

    /// <summary>
    /// <c>querystring.parse(str[, sep[, eq[, options]]])</c>.
    /// <para>
    /// https://nodejs.org/api/querystring.html#querystringparsestr-sep-eq-options
    /// </para>
    /// </summary>
    private static JsObject Parse(Engine engine, JsObject moduleObject, JsValue originalUnescape, JsCallArguments arguments)
    {
        // "The object returned by the querystring.parse() method does not prototypically inherit from the
        // JavaScript Object", so a key called "toString" is just a key.
        var result = new JsObject(engine);
        result.SetPrototypeOf(JsValue.Null);

        if (arguments.At(0) is not JsString input || input.Length == 0)
        {
            return result;
        }

        var sep = Separator(arguments.At(1), "&");
        var eq = Separator(arguments.At(2), "=");

        var pairs = DefaultMaxKeys;
        var decode = moduleObject.Get("unescape");
        var options = arguments.At(3);
        if (options is ObjectInstance optionsObject)
        {
            var maxKeys = optionsObject.Get("maxKeys");
            if (maxKeys.IsNumber())
            {
                var requested = TypeConverter.ToInt32(maxKeys);

                // "-1 is used in place of a value like Infinity for meaning unlimited pairs."
                pairs = requested > 0 ? requested : -1;
            }

            if (optionsObject.Get("decodeURIComponent") is ICallable custom)
            {
                decode = (JsValue) custom;
            }
        }

        var customDecode = !ReferenceEquals(decode, originalUnescape);

        // With a custom decoder Node hands it "%20" rather than an already-substituted space, so the decoder
        // sees the query string as written.
        var plusReplacement = customDecode ? "%20" : " ";

        var segments = Split(input.ToString(), sep);
        var accumulated = new List<KeyValuePair<string, JsValue>>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0)
            {
                if (i == segments.Count - 1)
                {
                    // "We ended on an empty substring": nothing to add, and the budget is not spent.
                    break;
                }

                if (--pairs == 0)
                {
                    break;
                }

                continue;
            }

            string rawKey;
            string rawValue;
            var separatorIndex = segment.IndexOf(eq, StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                rawKey = segment;
                rawValue = string.Empty;
            }
            else
            {
                rawKey = segment.Substring(0, separatorIndex);
                rawValue = segment.Substring(separatorIndex + eq.Length);
            }

            var key = Decode(rawKey, plusReplacement, customDecode, decode);
            var value = Decode(rawValue, plusReplacement, customDecode, decode);

            AddKeyValue(engine, accumulated, index, key, value);

            if (--pairs == 0)
            {
                break;
            }
        }

        for (var i = 0; i < accumulated.Count; i++)
        {
            result.FastSetDataProperty(accumulated[i].Key, accumulated[i].Value);
        }

        return result;
    }

    /// <summary>
    /// Node's <c>addKeyVal</c>: "the first value is the string value, the second and later ones extend it into
    /// an array".
    /// </summary>
    private static void AddKeyValue(
        Engine engine,
        List<KeyValuePair<string, JsValue>> accumulated,
        Dictionary<string, int> index,
        string key,
        string value)
    {
        var entry = JsString.Create(value);

        if (!index.TryGetValue(key, out var position))
        {
            index[key] = accumulated.Count;
            accumulated.Add(new KeyValuePair<string, JsValue>(key, entry));
            return;
        }

        var existing = accumulated[position].Value;
        if (existing is JsArray array)
        {
            array.Push(entry);
            return;
        }

        var replacement = new JsArray(engine, [existing, entry]);
        accumulated[position] = new KeyValuePair<string, JsValue>(key, replacement);
    }

    /// <summary>
    /// One component of a pair: <c>+</c> first, then the decoder — and only when there is something to decode,
    /// which for the built-in decoder means a percent-encoded byte is actually present.
    /// </summary>
    private static string Decode(string component, string plusReplacement, bool customDecode, JsValue decode)
    {
        var substituted = component.IndexOf('+') < 0 ? component : component.Replace("+", plusReplacement);
        if (substituted.Length == 0)
        {
            return substituted;
        }

        if (!customDecode)
        {
            return PercentEncoding.ContainsPercentEncodedByte(substituted.AsSpan())
                ? NodeQueryString.Unescape(substituted, decodeSpaces: false)
                : substituted;
        }

        // Node's decodeStr: a decoder that throws is not fatal, the built-in one answers instead.
        try
        {
            return TypeConverter.ToString(((ICallable) decode).Call(JsValue.Undefined, [JsString.Create(substituted)]));
        }
        catch (JavaScriptException)
        {
            return NodeQueryString.Unescape(substituted, decodeSpaces: true);
        }
    }

    /// <summary>
    /// A <c>sep</c> or <c>eq</c> argument. Node applies <c>||</c> to it, so every falsy value — absent, null,
    /// an empty string, <c>0</c> — means the default.
    /// </summary>
    private static string Separator(JsValue value, string fallback)
        => TypeConverter.ToBoolean(value) ? TypeConverter.ToString(value) : fallback;

    private static List<string> Split(string input, string separator)
    {
        var result = new List<string>();
        var start = 0;
        while (true)
        {
            var next = input.IndexOf(separator, start, StringComparison.Ordinal);
            if (next < 0)
            {
                result.Add(input.Substring(start));
                return result;
            }

            result.Add(input.Substring(start, next - start));
            start = next + separator.Length;
        }
    }

    private static string Coerce(JsValue value) => TypeConverter.ToString(value);
}
#endif
