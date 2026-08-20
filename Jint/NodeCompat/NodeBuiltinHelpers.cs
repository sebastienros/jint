using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.NodeCompat;

/// <summary>
/// The small amount of JavaScript-facing plumbing every <c>node:</c> builtin module shares: how a function is
/// created, and how an argument of the wrong type is refused.
/// </summary>
internal static class NodeBuiltinHelpers
{
    /// <summary>
    /// A strict UTF-8 decoder, so that an invalid byte sequence is a failure rather than a run of U+FFFD —
    /// which is how <c>decodeURIComponent</c>'s <c>URIError</c> is detected without running any script.
    /// </summary>
    private static readonly Encoding _strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// One exported function. The realm-pinned constructor, for the same reason <c>process</c>'s methods use
    /// it: a module's exports are built lazily, and the function must belong to the realm that owns the object
    /// it came from. <c>length</c> counts the declared parameters and is configurable but neither writable nor
    /// enumerable, as an ordinary function's is.
    /// </summary>
    internal static ClrFunction Operation(Engine engine, Realm realm, string name, int length, JsCallDelegate body)
        => new(engine, realm, name, body, length, PropertyFlag.Configurable);

    /// <summary>
    /// Node's <c>validateString</c>: the argument has to <em>be</em> a string, never merely convert to one, so
    /// <c>path.join(1)</c> throws rather than joining <c>"1"</c>.
    /// </summary>
    /// <remarks>
    /// The message is the shape of Node's <c>ERR_INVALID_ARG_TYPE</c>, which is a <c>TypeError</c> there too,
    /// with one deliberate omission: Node appends the offending value (<c>Received type number (42)</c>) and
    /// this does not. Rendering a value means calling its <c>toString</c>, which is script the host never
    /// asked to run and which can throw from inside the error path; naming the type is what the caller
    /// actually needs.
    /// </remarks>
    internal static string RequireString(Realm realm, JsValue value, string argumentName)
    {
        if (value is JsString text)
        {
            return text.ToString();
        }

        Throw.TypeError(realm, $"The \"{argumentName}\" argument must be of type string. Received {Describe(value)}");
        return null!;
    }

    /// <summary>
    /// Node's <c>validateObject</c>, as <c>path.format</c> applies it: <see langword="null"/> and every
    /// primitive are refused.
    /// </summary>
    internal static ObjectInstance RequireObject(Realm realm, JsValue value, string argumentName)
    {
        if (value is ObjectInstance instance)
        {
            return instance;
        }

        Throw.TypeError(realm, $"The \"{argumentName}\" argument must be of type object. Received {Describe(value)}");
        return null!;
    }

    /// <summary>
    /// <c>decodeURIComponent</c>, https://tc39.es/ecma262/#sec-decodeuricomponent, reduced to the question its
    /// two callers actually have: did it succeed, and with what. Both of them — <c>querystring.unescape</c>
    /// and <c>url.fileURLToPath</c> — need the failure rather than an exception, because Node catches it in one
    /// and rethrows it as its own error in the other.
    /// </summary>
    /// <remarks>
    /// The two failure modes are the specification's: a <c>%</c> not followed by two hexadecimal digits, and a
    /// run of percent-decoded bytes that is not a valid UTF-8 sequence. A run has to be complete and
    /// well-formed on its own, because a multi-byte sequence interrupted by a literal character is exactly what
    /// <c>decodeURIComponent</c> rejects.
    /// </remarks>
    internal static bool TryDecodeUriComponent(string value, out string result)
    {
        if (value.IndexOf('%') < 0)
        {
            // Nothing to decode and nothing that can fail: every other character is copied through.
            result = value;
            return true;
        }

        var builder = new ValueStringBuilder(stackalloc char[128]);
        var bytes = new List<byte>();
        try
        {
            for (var i = 0; i < value.Length;)
            {
                var c = value[i];
                if (c != '%')
                {
                    if (!FlushUtf8(ref builder, bytes))
                    {
                        result = string.Empty;
                        return false;
                    }

                    builder.Append(c);
                    i++;
                    continue;
                }

                if (i + 2 >= value.Length
                    || !TryHexDigit(value[i + 1], out var high)
                    || !TryHexDigit(value[i + 2], out var low))
                {
                    result = string.Empty;
                    return false;
                }

                bytes.Add((byte) ((high << 4) | low));
                i += 3;
            }

            if (!FlushUtf8(ref builder, bytes))
            {
                result = string.Empty;
                return false;
            }

            result = builder.AsSpan().ToString();
            return true;
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static bool FlushUtf8(ref ValueStringBuilder builder, List<byte> bytes)
    {
        if (bytes.Count == 0)
        {
            return true;
        }

        string text;
        try
        {
            text = _strictUtf8.GetString(bytes.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        builder.Append(text);
        bytes.Clear();
        return true;
    }

    private static bool TryHexDigit(char c, out int value)
    {
        if (c >= '0' && c <= '9')
        {
            value = c - '0';
            return true;
        }

        if (c >= 'A' && c <= 'F')
        {
            value = c - 'A' + 10;
            return true;
        }

        if (c >= 'a' && c <= 'f')
        {
            value = c - 'a' + 10;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// The tail of an <c>ERR_INVALID_ARG_TYPE</c> message. Never touches the value itself, so building it can
    /// neither run user code nor throw.
    /// </summary>
    private static string Describe(JsValue value)
    {
        if (value.IsUndefined())
        {
            return "undefined";
        }

        if (value.IsNull())
        {
            return "null";
        }

        if (value.IsObject())
        {
            return "an instance of Object";
        }

        return "type " + TypeOf(value);
    }

    private static string TypeOf(JsValue value)
    {
        if (value.IsBoolean())
        {
            return "boolean";
        }

        if (value.IsNumber())
        {
            return "number";
        }

        if (value.IsBigInt())
        {
            return "bigint";
        }

        if (value.IsSymbol())
        {
            return "symbol";
        }

        return "string";
    }
}
