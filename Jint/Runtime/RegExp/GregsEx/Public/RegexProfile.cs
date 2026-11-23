#nullable enable
namespace Jint.Runtime.RegExp.GregsEx;

/// <summary>A closed, immutable profile identity. Only delivered library profiles can derive.</summary>
internal abstract class RegexProfile
{
    private protected RegexProfile() { }

    /// <summary>The reproducible syntax/semantics identity.</summary>
    public abstract string Id { get; }

    /// <summary>The reproducible classification-data identity.</summary>
    public abstract string DataId { get; }
}

/// <summary>The text alphabet admitted by experimental RegularCore v1.</summary>
internal enum RegularCoreMode
{
    /// <summary>Strict Unicode scalar matching over UTF-16 or UTF-8.</summary>
    UnicodeScalars,
    /// <summary>Individual UTF-16 units including lone surrogates, over char inputs only.</summary>
    Utf16CodeUnits,
    /// <summary>Values 0..255, over raw byte inputs only.</summary>
    RawBytes
}

/// <summary>Experimental RegularCore v1 options.</summary>
[Flags]
internal enum RegularCoreFlags
{
    /// <summary>Case-sensitive matching; dot excludes LF.</summary>
    None = 0,
    /// <summary>Dot also admits LF.</summary>
    DotAll = 1
}

/// <summary>An experimental regular subset, with no compatibility claim for another flavor.</summary>
internal sealed class RegularCoreProfile : RegexProfile
{
    /// <summary>Versioned syntax and semantics identity.</summary>
    public override string Id => "RegularCore-1-experimental";

    /// <summary>Fixed ASCII classes and algorithmic encoding, independent of host Unicode tables.</summary>
    public override string DataId => "GregsEx-ASCII-1";

    /// <summary>The legal matching alphabet.</summary>
    public RegularCoreMode Mode { get; }

    /// <summary>Validated profile flags.</summary>
    public RegularCoreFlags Flags { get; }

    /// <summary>Creates an explicit experimental text policy.</summary>
    public RegularCoreProfile(RegularCoreMode mode = RegularCoreMode.UnicodeScalars,
        RegularCoreFlags flags = RegularCoreFlags.None)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if ((flags & ~RegularCoreFlags.DotAll) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }

        Mode = mode;
        Flags = flags;
    }

    /// <summary>Checks representation admission without parsing a pattern.</summary>
    public bool Supports(RegexInputKind inputKind) => Mode switch
    {
        RegularCoreMode.UnicodeScalars => inputKind is RegexInputKind.Utf16 or RegexInputKind.Utf8,
        RegularCoreMode.Utf16CodeUnits => inputKind == RegexInputKind.Utf16,
        RegularCoreMode.RawBytes => inputKind == RegexInputKind.Bytes,
        _ => false
    };
}
