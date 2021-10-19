namespace Jint.Runtime.Modules
{
    public enum ModuleStatus
    {
        Unlinked,
        Linking,
        Linked,
        Evaluating,
        EvaluatingAsync,
        Evaluated
    }
}
