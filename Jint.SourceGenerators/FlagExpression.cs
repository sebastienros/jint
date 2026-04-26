namespace Jint.SourceGenerators;

/// <summary>
/// Translates a <c>Jint.Runtime.Descriptors.PropertyFlag</c> integer value into a stable C# expression.
/// </summary>
/// <remarks>
/// Mirrors the public values of <c>Jint.Runtime.Descriptors.PropertyFlag</c>. Kept in lockstep with that enum;
/// if values change upstream, decomposition output changes but stays semantically correct because we still
/// emit OR-of-single-bits as a fallback. The cross-assembly coupling is mitigated by emitting symbolic names
/// whenever they exist and falling back to a typed cast only as a last resort.
/// </remarks>
internal static class FlagExpression
{
    private const string TypePrefix = "global::Jint.Runtime.Descriptors.PropertyFlag";

    public static string From(int? explicitValue, string omittedDefaultName)
    {
        if (explicitValue is null) return TypePrefix + "." + omittedDefaultName;
        return Decompose(explicitValue.Value);
    }

    private static string Decompose(int value)
    {
        if (value == 0) return TypePrefix + ".None";

        // Match well-known named combinations first; these read better than OR-of-bits.
        switch (value)
        {
            case 42: return TypePrefix + ".AllForbidden";
            case 21: return TypePrefix + ".ConfigurableEnumerableWritable";
            case 37: return TypePrefix + ".NonConfigurable";
            case 41: return TypePrefix + ".OnlyEnumerable";
            case 22: return TypePrefix + ".NonEnumerable";
            case 38: return TypePrefix + ".OnlyWritable";
        }

        var parts = new List<string>(8);
        var remaining = value;
        for (var bit = 0; bit < 32 && remaining != 0; bit++)
        {
            var mask = 1 << bit;
            if ((remaining & mask) == 0) continue;
            var name = SingleBitName(mask);
            if (name is null)
            {
                // Unknown bit — bail and emit a plain typed cast so we never lose information.
                return "(" + TypePrefix + ")" + value;
            }
            parts.Add(TypePrefix + "." + name);
            remaining &= ~mask;
        }

        return string.Join(" | ", parts);
    }

    private static string? SingleBitName(int mask) => mask switch
    {
        1 => "Enumerable",
        2 => "EnumerableSet",
        4 => "Writable",
        8 => "WritableSet",
        16 => "Configurable",
        32 => "ConfigurableSet",
        256 => "CustomJsValue",
        512 => "MutableBinding",
        1024 => "NonData",
        _ => null,
    };
}
