namespace Jint.Runtime.Modules;

internal enum ModuleStatus
{
    /// <summary>
    /// The initial state of a cyclic module record: its requested modules have not been loaded yet. The
    /// load phase (<see cref="CyclicModuleRecord.LoadRequestedModules"/>) moves every module it visited to
    /// <see cref="Unlinked"/> once the whole graph has loaded, so linking never observes this state.
    /// </summary>
    New,

    Unlinked,
    Linking,
    Linked,
    Evaluating,
    EvaluatingAsync,
    Evaluated
}
