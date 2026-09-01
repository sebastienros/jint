using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Jint.DevTools.Protocol;

/// <summary>
/// Reads a command's <c>params</c> member into the type the generated dispatch declares.
/// </summary>
/// <remarks>
/// It is the one place a client's JSON becomes a CLR object, so it is also the one place the protocol's
/// <c>-32602</c> is decided. Every read goes through a <see cref="JsonTypeInfo{T}"/> from the
/// source-generated context, so nothing here reflects over a type and the assembly stays AOT-clean.
/// </remarks>
internal static class ProtocolPayload
{
    /// <summary>An absent <c>params</c> member reads as the empty object, so a command with only optional
    /// parameters works without one and a command with required parameters answers <c>-32602</c>.</summary>
    private static ReadOnlySpan<byte> EmptyObject => "{}"u8;

    /// <summary>
    /// Deserializes <paramref name="parameters"/> through <paramref name="typeInfo"/>, turning every failure
    /// into the protocol's invalid-parameters error.
    /// </summary>
    internal static T Read<T>(JsonElement? parameters, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            var value = parameters is { ValueKind: JsonValueKind.Object } element
                ? element.Deserialize(typeInfo)
                : JsonSerializer.Deserialize(EmptyObject, typeInfo);

            if (value is null)
            {
                Throw.InvalidParams("Invalid parameters", "the params member deserialized to nothing");
            }

            return value;
        }
        catch (JsonException exception)
        {
            // The message names the member and what was wrong with it, which is the half of a -32602 a
            // client author can act on; the code is the half a client library switches on.
            return Throw.InvalidParams<T>("Invalid parameters", exception.Message);
        }
    }
}
