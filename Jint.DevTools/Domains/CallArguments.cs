using System.Globalization;
using System.Numerics;
using Jint.DevTools.Protocol.Runtime;
using Jint.Native;
using JsonParser = Jint.Native.Json.JsonParser;

namespace Jint.DevTools.Domains;

/// <summary>
/// Turns the protocol's <c>CallArgument</c> — a handle, a literal, or a number the JSON cannot spell — into
/// the value the engine will actually see.
/// </summary>
/// <remarks>
/// <para>
/// Shared because two domains take one: <c>Runtime.callFunctionOn</c> passes a list of them, and
/// <c>Debugger.setVariableValue</c> passes exactly one. Both mean the same thing by it, and a second reading
/// of the same shape is a second set of edge cases.
/// </para>
/// <para>
/// <b>Nothing a client sends here executes on the way in.</b> A literal is read with the engine's own JSON
/// reader, which builds native arrays and objects and has no reviver, so the conversion runs no script.
/// </para>
/// </remarks>
internal static class CallArguments
{
    /// <summary>Reads a whole argument list, resolving every handle it names.</summary>
    internal static JsValue[] Resolve(DevToolsTarget target, CallArgument[]? arguments)
    {
        if (arguments is null || arguments.Length == 0)
        {
            return [];
        }

        var values = new JsValue[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            values[i] = Resolve(target, arguments[i]);
        }

        return values;
    }

    /// <summary>Reads one argument.</summary>
    internal static JsValue Resolve(DevToolsTarget target, CallArgument argument)
    {
        if (argument.ObjectId is { } objectId)
        {
            return target.Runtime.RemoteObjects.Resolve(objectId);
        }

        if (argument.UnserializableValue is { } unserializable)
        {
            return Unserializable(unserializable);
        }

        if (argument.Value is not { } value)
        {
            // All three absent is how a client spells `undefined`, which is the one value the protocol has
            // no member for.
            return JsValue.Undefined;
        }

        return new JsonParser(target.Runtime.Engine).Parse(value.GetRawText());
    }

    private static JsValue Unserializable(string text) => text switch
    {
        "NaN" => double.NaN,
        "Infinity" => double.PositiveInfinity,
        "-Infinity" => double.NegativeInfinity,
        "-0" => JsNumber.Create(-0d),
        _ => BigIntOrRefusal(text),
    };

    private static JsValue BigIntOrRefusal(string text)
    {
        if (text.Length > 1 && text[^1] == 'n' &&
            BigInteger.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return new JsBigInt(value);
        }

        // Chrome's wording for an unserializableValue it does not recognize.
        return Throw.ServerError<JsValue>("Invalid CallArgument: " + text);
    }
}
