namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-objects
/// The execution state of an async generator.
/// </summary>
internal enum AsyncGeneratorState
{
    /// <summary>
    /// The async generator has been created but not yet started.
    /// Transitions to Executing on first next/return/throw call.
    /// </summary>
    SuspendedStart,

    /// <summary>
    /// The async generator is suspended at a yield expression.
    /// Transitions to Executing when resumed.
    /// </summary>
    SuspendedYield,

    /// <summary>
    /// The async generator is currently executing statements.
    /// Transitions to SuspendedYield (on yield) or Completed.
    /// </summary>
    Executing,

    /// <summary>
    /// The async generator is awaiting the value of a return() request made before it started or after it completed
    /// (https://tc39.es/ecma262/#sec-asyncgeneratorawaitreturn).
    /// Transitions to Completed when the awaited promise settles.
    /// </summary>
    AwaitingReturn,

    /// <summary>
    /// The async generator has completed execution.
    /// All subsequent next() calls return {done: true}.
    /// </summary>
    Completed
}
