namespace Jint.Runtime.Modules;

internal enum ModuleStatus
{
    Unlinked,
    Linking,
    Linked,
    Evaluating,
    EvaluatingAsync,
    Evaluated
}
