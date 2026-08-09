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

        return AttributesEqual(Attributes, other.Attributes);
    }

    /// <summary>
    /// Attribute-list comparison as spelled out by
    /// <see href="https://tc39.es/proposal-import-attributes/#sec-ModuleRequestsEqual">ModuleRequestsEqual</see>:
    /// same length and same members, order-insensitive.
    /// </summary>
    internal static bool AttributesEqual(ModuleImportAttribute[] a, ModuleImportAttribute[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        if (a.Length == 0
            || (a.Length == 1 && a[0].Equals(b[0])))
        {
            return true;
        }

        foreach (var pair in a)
        {
            if (Array.IndexOf(b, pair) == -1)
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        // Hand-rolled rather than HashCode.Combine, which is netstandard2.1+ and so absent on net462
        // and netstandard2.0, where no shim exists either. Backfilling it would mean carrying xxHash32,
        // and for two or three inputs the multiply-xor below is the cheaper hash anyway.
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Specifier) * 397) ^ (int) Phase;
        }
    }
}

/// <summary>
/// Keys a <c>[[LoadedModules]]</c> list: specifier and attributes only, deliberately ignoring the import
/// phase that <see cref="ModuleRequest.Equals(ModuleRequest)"/> counts. A module record's
/// <see href="https://tc39.es/ecma262/#table-module-record-fields">[[LoadedModules]]</see> field is keyed on
/// the specifier/attributes pair, and the defer/source-phase proposals do not change that: <c>import x</c>
/// and <c>import defer x</c> of one specifier denote the same module record. Keying on the phase as well
/// would ask the host twice for the same referrer/specifier pair, which
/// <see href="https://tc39.es/ecma262/#sec-HostLoadImportedModule">HostLoadImportedModule</see> forbids.
/// </summary>
internal sealed class LoadedModuleRequestComparer : IEqualityComparer<ModuleRequest>
{
    internal static readonly LoadedModuleRequestComparer Instance = new();

    public bool Equals(ModuleRequest x, ModuleRequest y)
        => string.Equals(x.Specifier, y.Specifier, StringComparison.Ordinal)
           && ModuleRequest.AttributesEqual(x.Attributes, y.Attributes);

    public int GetHashCode(ModuleRequest obj) => StringComparer.Ordinal.GetHashCode(obj.Specifier);
}
