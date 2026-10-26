using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint;

/// <summary>
/// How deep a script's syntax tree may nest, and the stack probe for every recursion that walks it: the hoisting
/// walk, the analyses a function definition runs over its body, and building the interpreter's nodes.
/// </summary>
/// <remarks>
/// <para>
/// Two bounds, for two kinds of nesting. Most nesting is evaluated by a recursion nothing probes, so the hoisting
/// walk keeps the fixed limit of <see cref="MaxUnprobedDepth"/> levels sebastienros/jint#2340 introduced. An
/// operator chain (<c>a || b || …</c>, <c>x + y + …</c>) and an <c>else if</c> chain are exempt: they are what
/// generated code is long in (sebastienros/jint#4356), the parser builds the first in a loop, and both carry an
/// evaluation probe every <see cref="EvaluationProbeInterval"/> levels, so they are bounded by the stack instead.
/// </para>
/// <para>
/// A walk probes once it is <see cref="UncheckedDepth"/> levels deep, and fails only when the runtime's reserve
/// is gone <em>and</em> the walk itself has spent <see cref="UncheckedStackBytes"/> of it. A node is built
/// lazily, where it first runs, so a <c>catch</c> block can be built at the bottom of a recursion that spent the
/// stack down to the reserve; such a build is shallow and has always run inside the reserve.
/// </para>
/// <para>
/// A walk that does run out fails with a <see cref="ScriptPreparationException"/> whose inner exception is a
/// <see cref="RangeErrorException"/>. A statement list turns that into a <c>RangeError</c> the script can catch,
/// so a deep tree built while script runs fails the way V8's lazy compilation does; anywhere else the host sees the
/// <see cref="ScriptPreparationException"/>.
/// </para>
/// <para>
/// Only cold paths enter it: once per interpreter node built or per node an analysis visits, never per evaluation.
/// </para>
/// </remarks>
internal static class SyntaxNesting
{
    /// <summary>
    /// The levels a path through the tree may nest without an evaluation probe on it.
    /// </summary>
    internal const int MaxUnprobedDepth = 256;

    /// <summary>
    /// How many levels of an operator chain, or arms of an <c>else if</c> chain, run between two evaluation probes
    /// (<c>JintStackProbeExpression</c>, <c>JintStackProbeStatement</c>). Small enough that the frames in between
    /// fit the runtime's reserve with room to spare.
    /// </summary>
    internal const int EvaluationProbeInterval = 64;

    /// <summary>
    /// The levels a walk takes before it asks the runtime for stack at all.
    /// </summary>
    internal const int UncheckedDepth = 32;

    /// <summary>
    /// How much of the runtime's reserve (128 KiB on a 64-bit process) a walk that started inside it may spend.
    /// </summary>
    internal const int UncheckedStackBytes = 64 * 1024;

    // Thread-static rather than threaded through: the node builders are static and reached from dozens of
    // constructors, and a build never yields its thread midway.
    [ThreadStatic]
    private static int _depth;

    [ThreadStatic]
    private static nuint _walkBase;

    /// <summary>
    /// Whether the node being built sits at a depth <see cref="EvaluationProbeInterval"/> divides, which is
    /// where a chain gets an evaluation probe.
    /// </summary>
    internal static bool IsEvaluationProbeDepth
    {
        get
        {
            var depth = _depth;
            return depth > 0 && depth % EvaluationProbeInterval == 0;
        }
    }

    /// <summary>
    /// Enters one more level of a recursion over the syntax tree; dispose the result on the way out.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Level Enter()
    {
        var depth = ++_depth;
        if (depth == 1)
        {
            _walkBase = StackPosition();
        }
        else if (depth > UncheckedDepth && !HasHeadroom(_walkBase))
        {
            _depth--;
            ThrowTooDeep();
        }

        return default;
    }

    /// <summary>
    /// Whether a walk that started at <paramref name="walkBase"/> may go one level deeper.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool HasHeadroom(nuint walkBase)
    {
        // The stack grows down on every platform .NET runs on.
        return RuntimeHelpers.TryEnsureSufficientExecutionStack()
               || (long) walkBase - (long) StackPosition() < UncheckedStackBytes;
    }

    /// <summary>
    /// An address in the caller's stack frame, close enough to its stack pointer to measure what a walk has spent.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static unsafe nuint StackPosition()
    {
        byte marker = 0;
        return (nuint) (&marker);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowTooDeep()
    {
        throw new ScriptPreparationException(
            "Script nesting is too deep: preparing it would exhaust the stack.",
            new RangeErrorException("Maximum call stack size exceeded"));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowExceedsMaximumDepth()
    {
        throw new ScriptPreparationException($"Script nesting exceeds maximum depth of {MaxUnprobedDepth} levels.", null);
    }

    /// <summary>
    /// One level entered by <see cref="Enter"/>; a <see langword="using"/> declaration leaves it on every way out.
    /// </summary>
    internal readonly ref struct Level
    {
        // CA1822: an instance member is what the using pattern binds to; the state it releases is the
        // thread's, not the struct's.
#pragma warning disable CA1822
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _depth--;
#pragma warning restore CA1822
    }

    /// <summary>
    /// What the hoisting walk knows about a path through the tree: how many levels it has gone since the last
    /// evaluation probe on it, conservatively.
    /// </summary>
    /// <remarks>
    /// A chain's probes are placed where the build depth is a multiple of <see cref="EvaluationProbeInterval"/>,
    /// which the walk cannot see. It knows only that any run of that many consecutive chain levels contains one,
    /// except at the run's last level, which is never wrapped. So the first interval of a run counts as unprobed
    /// on top of whatever lies above the run, and any level after it as at most one interval and one level past a
    /// probe.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Unprobed(int Depth, int RunEntry, int RunDepth)
    {
        internal static Unprobed Root => new(1, 0, -1);

        /// <summary>A level of nesting that carries no probe.</summary>
        internal Unprobed Enter() => new(Depth + 1, 0, -1);

        /// <summary>
        /// The next level of a chain: an operator's operand that is itself an operator, or an <c>if</c>'s
        /// <c>else if</c>. A node that was not already in a chain is the chain's first level.
        /// </summary>
        internal Unprobed ContinueChain()
        {
            var runEntry = RunDepth < 0 ? Depth : RunEntry;
            var runDepth = RunDepth < 0 ? 1 : RunDepth + 1;
            var depth = runDepth < EvaluationProbeInterval ? runEntry + runDepth : EvaluationProbeInterval + 1;
            return new Unprobed(depth, runEntry, runDepth);
        }
    }
}
