#nullable enable
namespace Jint.Runtime.RegExp.GregsEx;

/// <summary>Supported, explicitly versioned Unicode datasets. Members may be deprecated independently.</summary>
internal enum UnicodeVersion
{
    /// <summary>Unicode 16.0.0, paired with Emoji 16.0 for ECMAScript.</summary>
    Unicode16_0 = 1600,
    /// <summary>Unicode 17.0.0, paired with Emoji 17.0 for ECMAScript.</summary>
    Unicode17_0 = 1700
}

internal static class UnicodeVersionPolicy
{
    internal static void Validate(UnicodeVersion unicodeVersion)
    {
        if (unicodeVersion is not (UnicodeVersion.Unicode16_0 or UnicodeVersion.Unicode17_0))
        {
            throw new ArgumentOutOfRangeException(nameof(unicodeVersion));
        }
    }
}
