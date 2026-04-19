namespace Jint.Runtime.Modules;

/// <summary>
/// Phase of a module import, as introduced by the
/// <see href="https://github.com/tc39/proposal-defer-import-eval">deferred import evaluation</see> and
/// <see href="https://github.com/tc39/proposal-source-phase-imports">source phase imports</see> proposals.
/// </summary>
internal enum ModuleImportPhase
{
    /// <summary>Regular import: the module is loaded, linked, and evaluated.</summary>
    Evaluation,

    /// <summary>Deferred import: the module is loaded and linked, but evaluation is deferred until a namespace property is accessed.</summary>
    Defer,

    /// <summary>Source-phase import: only the module source representation is requested (JS modules have no source representation).</summary>
    Source,
}

public readonly record struct ModuleImportAttribute(string Key, string Value);

public readonly record struct ModuleRequest(string Specifier, ModuleImportAttribute[] Attributes)
{
    /// <summary>
    /// Phase of this request. Internal — not part of the public API surface because
    /// <see cref="ModuleImportPhase"/> is an implementation detail of the defer/source-phase proposals.
    /// </summary>
    internal ModuleImportPhase Phase { get; init; } = ModuleImportPhase.Evaluation;

    /// <summary>
    /// https://tc39.es/proposal-import-attributes/#sec-ModuleRequestsEqual
    /// </summary>
    public bool Equals(ModuleRequest other)
    {
        if (!string.Equals(Specifier, other.Specifier, StringComparison.Ordinal))
        {
            return false;
        }

        if (Phase != other.Phase)
        {
            return false;
        }

        if (this.Attributes.Length != other.Attributes.Length)
        {
            return false;
        }

        if (Attributes.Length == 0
            || (Attributes.Length == 1 && Attributes[0].Equals(other.Attributes[0])))
        {
            return true;
        }

        foreach (var pair in Attributes)
        {
            if (Array.IndexOf(other.Attributes, pair) == -1)
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        // Same pattern as OptionalSourceBreakLocationEqualityComparer — HashCode.Combine is net6+ only.
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Specifier) * 397) ^ (int) Phase;
        }
    }
}
