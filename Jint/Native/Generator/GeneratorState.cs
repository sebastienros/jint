namespace Jint.Native.Generator;

internal enum GeneratorState
{
    Undefined,
    SuspendedStart,
    SuspendedYield,
    Executing,
    Completed
}