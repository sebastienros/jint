#nullable enable
namespace Jint.Runtime.RegExp.GregsEx;

/// <summary>Edition 16 regular-expression flags, independent of .NET regex options.</summary>
[Flags]
internal enum EcmaScriptFlags
{
    /// <summary>Legacy mode without flags.</summary>
    None = 0,
    /// <summary>Expose match indices (d).</summary>
    HasIndices = 1,
    /// <summary>Global host matching (g).</summary>
    Global = 2,
    /// <summary>Case-insensitive matching (i).</summary>
    IgnoreCase = 4,
    /// <summary>Multiline anchors (m).</summary>
    Multiline = 8,
    /// <summary>Dot includes line terminators (s).</summary>
    DotAll = 16,
    /// <summary>Unicode-aware mode (u).</summary>
    Unicode = 32,
    /// <summary>Unicode sets mode (v), incompatible with u.</summary>
    UnicodeSets = 64,
    /// <summary>Sticky host matching (y).</summary>
    Sticky = 128
}

/// <summary>Immutable edition/data descriptor. A descriptor does not implement parsing or matching.</summary>
internal sealed class EcmaScriptProfile : RegexProfile
{
    /// <summary>Pinned grammar and builtin matching identity.</summary>
    public override string Id => "EcmaScript2025";
    /// <summary>Pinned classification, case and string-property data identity.</summary>
    public override string DataId => UnicodeVersion == UnicodeVersion.Unicode16_0
        ? "Unicode-16.0.0+Emoji-16.0" : "Unicode-17.0.0+Emoji-17.0";
    /// <summary>The immutable Unicode and corresponding Emoji dataset selection.</summary>
    public UnicodeVersion UnicodeVersion { get; }
    /// <summary>Normalized validated flags.</summary>
    public EcmaScriptFlags Flags { get; }
    /// <summary>Whether u or v selects code-point consumption, including lone surrogates.</summary>
    public bool IsUnicodeAware => (Flags & (EcmaScriptFlags.Unicode | EcmaScriptFlags.UnicodeSets)) != 0;
    /// <summary>Annex B pattern grammar is enabled exactly in legacy mode.</summary>
    public bool UsesAnnexB => !IsUnicodeAware;

    /// <summary>Creates a descriptor; unknown bits and combined u/v are argument errors.</summary>
    public EcmaScriptProfile(EcmaScriptFlags flags) : this(flags, UnicodeVersion.Unicode17_0) { }

    /// <summary>Creates a descriptor with validated flags and an explicit, supported Unicode dataset.</summary>
    public EcmaScriptProfile(EcmaScriptFlags flags = EcmaScriptFlags.None,
        UnicodeVersion unicodeVersion = UnicodeVersion.Unicode17_0)
    {
        UnicodeVersionPolicy.Validate(unicodeVersion);
        if ((flags & ~(EcmaScriptFlags)255) != 0 ||
            (flags & (EcmaScriptFlags.Unicode | EcmaScriptFlags.UnicodeSets)) ==
            (EcmaScriptFlags.Unicode | EcmaScriptFlags.UnicodeSets))
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }

        Flags = flags;
        UnicodeVersion = unicodeVersion;
    }

    /// <summary>Only the complete UTF-16 string domain is admitted by this profile.</summary>
    public bool Supports(RegexInputKind kind) => kind == RegexInputKind.Utf16;

    /// <summary>Validates the original flag string with flag-domain diagnostics.</summary>
    public static bool TryParseFlags(ReadOnlySpan<char> flags, out EcmaScriptProfile? profile,
        out EcmaScriptDiagnostic diagnostic)
        => TryParseFlags(flags, out profile, out diagnostic, UnicodeVersion.Unicode17_0);

    /// <summary>Validates flags for the selected Unicode dataset; undefined versions are argument errors.</summary>
    public static bool TryParseFlags(ReadOnlySpan<char> flags, out EcmaScriptProfile? profile,
        out EcmaScriptDiagnostic diagnostic, UnicodeVersion unicodeVersion)
    {
        UnicodeVersionPolicy.Validate(unicodeVersion);
        var parsed = EcmaScriptFlags.None;
        for (var i = 0; i < flags.Length; i++)
        {
            var flag = flags[i] switch
            {
                'd' => EcmaScriptFlags.HasIndices,
                'g' => EcmaScriptFlags.Global,
                'i' => EcmaScriptFlags.IgnoreCase,
                'm' => EcmaScriptFlags.Multiline,
                's' => EcmaScriptFlags.DotAll,
                'u' => EcmaScriptFlags.Unicode,
                'v' => EcmaScriptFlags.UnicodeSets,
                'y' => EcmaScriptFlags.Sticky,
                _ => EcmaScriptFlags.None
            };
            if (flag == EcmaScriptFlags.None || (parsed & flag) != 0 ||
                (flag == EcmaScriptFlags.Unicode && (parsed & EcmaScriptFlags.UnicodeSets) != 0) ||
                (flag == EcmaScriptFlags.UnicodeSets && (parsed & EcmaScriptFlags.Unicode) != 0))
            {
                profile = null;
                diagnostic = new(new(RegexDiagnosticCode.InvalidSyntax, i, 1,
                    RegexOffsetUnit.Utf16CodeUnits), EcmaScriptDiagnosticLocation.Flags, "ES-001");
                return false;
            }
            parsed |= flag;
        }
        profile = new(parsed, unicodeVersion);
        diagnostic = default;
        return true;
    }
}
