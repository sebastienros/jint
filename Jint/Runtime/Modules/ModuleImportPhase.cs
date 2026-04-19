namespace Jint.Runtime.Modules;

/// <summary>
/// Phase of a module import, as introduced by the
/// <see href="https://github.com/tc39/proposal-defer-import-eval">deferred import evaluation</see> and
/// <see href="https://github.com/tc39/proposal-source-phase-imports">source phase imports</see> proposals.
/// </summary>
public enum ModuleImportPhase
{
    /// <summary>Regular import: the module is loaded, linked, and evaluated.</summary>
    Evaluation,

    /// <summary>Deferred import: the module is loaded and linked, but evaluation is deferred until a namespace property is accessed.</summary>
    Defer,

    /// <summary>Source-phase import: only the module source representation is requested (JS modules have no source representation).</summary>
    Source,
}
