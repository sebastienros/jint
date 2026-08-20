#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>KeyUsage</c> enumeration — https://w3c.github.io/webcrypto/#dfn-KeyUsage — as a flag set.
/// </summary>
/// <remarks>
/// The bits are declared in the order the specification lists the recognized key usage values, which is what
/// makes <see cref="KeyUsages.ToArray"/> produce the "normalized value" of a usages list by iterating them:
/// "the usage intersection of usages and a sequence containing all recognized key usage values" is, for a
/// flag set, simply the bits that are set, read in declaration order —
/// https://w3c.github.io/webcrypto/#concept-usage-intersection. Duplicates in the script's own sequence
/// collapse on the way in for the same reason.
/// </remarks>
[Flags]
internal enum KeyUsage
{
    None = 0,
    Encrypt = 1 << 0,
    Decrypt = 1 << 1,
    Sign = 1 << 2,
    Verify = 1 << 3,
    DeriveKey = 1 << 4,
    DeriveBits = 1 << 5,
    WrapKey = 1 << 6,
    UnwrapKey = 1 << 7,
}

/// <summary>
/// Conversions between a script's <c>sequence&lt;KeyUsage&gt;</c> and <see cref="KeyUsage"/>.
/// </summary>
internal static class KeyUsages
{
    /// <summary>
    /// The recognized key usage values, in the order the specification lists them. Index <c>i</c> names bit
    /// <c>1 &lt;&lt; i</c> of <see cref="KeyUsage"/>.
    /// </summary>
    private static readonly string[] _names =
    [
        "encrypt", "decrypt", "sign", "verify", "deriveKey", "deriveBits", "wrapKey", "unwrapKey",
    ];

    private static readonly JsString[] _jsNames =
    [
        new("encrypt"), new("decrypt"), new("sign"), new("verify"),
        new("deriveKey"), new("deriveBits"), new("wrapKey"), new("unwrapKey"),
    ];

    /// <summary>
    /// The WebIDL <c>sequence&lt;KeyUsage&gt;</c> conversion, https://webidl.spec.whatwg.org/#es-sequence:
    /// the value is iterated with the iterator protocol and every element is converted to the
    /// <c>KeyUsage</c> enumeration, https://webidl.spec.whatwg.org/#es-enumeration — a string that is not one
    /// of the eight recognized values is a <c>TypeError</c>, not a <c>SyntaxError</c>. The
    /// <c>SyntaxError</c>s in this API are about usages that are recognized but wrong <i>for the algorithm</i>,
    /// which is a decision each algorithm's own steps make later.
    /// </summary>
    internal static KeyUsage ReadSequence(CryptoContext context, JsValue value, string what)
    {
        var usages = KeyUsage.None;
        var iterator = value.GetIterator(context.Realm);

        try
        {
            while (iterator.TryIteratorStepValue(out var element))
            {
                var name = TypeConverter.ToString(element);
                if (!TryParse(name, out var usage))
                {
                    context.ThrowTypeError(
                        what + ": '" + name + "' is not a valid value for the enumeration KeyUsage (encrypt, decrypt, sign, verify, deriveKey, deriveBits, wrapKey, unwrapKey).");
                }

                usages |= usage;
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return usages;
    }

    /// <summary>
    /// The <c>key_ops</c> field of a JWK carries the very same names, so importing one reuses this. A name
    /// that is not a recognized usage is not an error here — it merely cannot match a requested usage.
    /// </summary>
    internal static bool TryParse(string name, out KeyUsage usage)
    {
        for (var i = 0; i < _names.Length; i++)
        {
            if (string.Equals(name, _names[i], StringComparison.Ordinal))
            {
                usage = (KeyUsage) (1 << i);
                return true;
            }
        }

        usage = KeyUsage.None;
        return false;
    }

    /// <summary>
    /// The usages as an ECMAScript array of strings, in the recognized order — what the <c>usages</c>
    /// attribute and a JWK's <c>key_ops</c> field are both made of.
    /// </summary>
    internal static JsArray ToArray(Engine engine, KeyUsage usages)
    {
        var count = System.Numerics.BitOperations.PopCount((uint) usages);
        var values = new JsValue[count];

        var next = 0;
        for (var i = 0; i < _jsNames.Length; i++)
        {
            if (((int) usages & (1 << i)) != 0)
            {
                values[next++] = _jsNames[i];
            }
        }

        return new JsArray(engine, values);
    }

    /// <summary>The usages as a comma-separated list, for an error message.</summary>
    internal static string Describe(KeyUsage usages)
    {
        if (usages == KeyUsage.None)
        {
            return "(none)";
        }

        var parts = new List<string>();
        for (var i = 0; i < _names.Length; i++)
        {
            if (((int) usages & (1 << i)) != 0)
            {
                parts.Add(_names[i]);
            }
        }

        return string.Join(", ", parts);
    }
}
#endif
