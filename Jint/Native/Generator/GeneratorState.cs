namespace Jint.Native.Generator;

internal enum GeneratorState
{
    SuspendedStart,
    SuspendedYield,
    Executing,
    Completed
}