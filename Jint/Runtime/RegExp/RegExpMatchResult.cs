using System.Runtime.InteropServices;

namespace Jint.Runtime.RegExp;

/// <summary>
/// Result of a single regex match operation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RegExpMatchResult(
    bool Success,
    int Index,
    int Length,
    string Value,
    RegExpGroupResult[]? Groups)
{
    public static RegExpMatchResult NoMatch => new(false, 0, 0, "", null);
}

/// <summary>
/// Result for a single capture group within a match.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RegExpGroupResult(
    bool Success,
    int Index,
    int Length,
    string Value,
    string? Name);
