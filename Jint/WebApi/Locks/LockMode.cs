#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Locks;

/// <summary>
/// https://w3c.github.io/web-locks/#dictdef-lockoptions — the <c>LockMode</c> enumeration,
/// <c>enum LockMode { "shared", "exclusive" };</c>.
/// </summary>
/// <remarks>
/// "If an <c>exclusive</c> lock is held, then no other locks with that name can be granted. If a
/// <c>shared</c> lock is held, other <c>shared</c> locks with that name can be granted — but not any
/// <c>exclusive</c> locks." — https://w3c.github.io/web-locks/#modes-and-scheduling.
/// </remarks>
internal enum LockMode
{
    /// <summary>The default the <c>LockOptions</c> dictionary declares.</summary>
    Exclusive,

    /// <summary>The readers' half of the readers-writer pattern the modes model.</summary>
    Shared,
}

/// <summary>
/// The two strings the <c>LockMode</c> enumeration is written with, and the conversion a WebIDL
/// enumeration-typed dictionary member performs — https://webidl.spec.whatwg.org/#es-enumeration.
/// </summary>
internal static class LockModeNames
{
    internal const string Exclusive = "exclusive";
    internal const string Shared = "shared";

    private static readonly JsString _exclusive = new(Exclusive);
    private static readonly JsString _shared = new(Shared);

    /// <summary>The string a <c>Lock</c>'s <c>mode</c> attribute answers with.</summary>
    internal static JsString ToJsString(LockMode mode) => mode == LockMode.Shared ? _shared : _exclusive;

    /// <summary>
    /// "If S is not one of E's enumeration values, then throw a <c>TypeError</c>" — the value is converted to
    /// a string first, so <c>null</c> is the string <c>"null"</c> and therefore a <c>TypeError</c> too, which
    /// is what <c>web-locks/acquire.https.any.js</c> asserts.
    /// </summary>
    internal static LockMode Parse(Realm realm, JsValue value, string what)
    {
        var text = TypeConverter.ToString(value);
        if (string.Equals(text, Exclusive, StringComparison.Ordinal))
        {
            return LockMode.Exclusive;
        }

        if (string.Equals(text, Shared, StringComparison.Ordinal))
        {
            return LockMode.Shared;
        }

        Throw.TypeError(realm, $"{what}: the provided value '{text}' is not a valid enum value of type LockMode.");
        return default;
    }
}
#endif
