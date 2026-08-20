#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// The value conversions the <c>URL</c> and <c>URLSearchParams</c> bindings share.
/// </summary>
internal static class UrlValues
{
    /// <summary>
    /// WebIDL's USVString conversion, https://webidl.spec.whatwg.org/#js-USVString: ToString, then every
    /// unpaired surrogate replaced by U+FFFD. Every argument and every assigned value of both interfaces is
    /// declared USVString, so this is the one door strings come in through.
    /// </summary>
    internal static string ToUsvString(JsValue value)
        => UrlCharacters.ToScalarValueString(TypeConverter.ToString(value));

    /// <summary>
    /// An <c>optional USVString</c> argument with no default value: an omitted argument and an explicitly
    /// passed <c>undefined</c> are both "not present", which is what
    /// https://webidl.spec.whatwg.org/#es-overloads says and what WPT's "Two-argument has() respects undefined
    /// as second arg" pins.
    /// </summary>
    internal static string? ToOptionalUsvString(JsValue value)
        => value.IsUndefined() ? null : ToUsvString(value);
}
#endif
