using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;
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
/// generated code is long in (sebastienros/jint#4356), and both carry an evaluation probe every
/// <see cref="EvaluationProbeInterval"/> levels, so they are bounded by the stack instead. A flattened <c>+</c> chain
/// evaluates every operand from its first node, so one whose nested form would have carried a probe carries one of its
/// own around it (<see cref="SpansEvaluationProbeDepth"/>).
/// </para>
/// <para>
/// A walk probes once it is <see cref="UncheckedDepth"/> levels deep. A node is built lazily, where it first runs, so a
/// walk can start low: a <c>catch</c> block is built at the bottom of the recursion that threw into it. So a walk may
/// go on past the runtime's reserve, but only if it started above the reserve and only while it has spent less than
/// <see cref="Allowance"/>, a quarter of the reserve, in all. Why that cannot overflow: a walk that started above the
/// reserve is no further into it than it has spent, so it stops within a quarter of the way in and one level, leaving
/// the rest for the exception that ends it. A walk that starts high has spent the allowance before it reaches the
/// reserve, and stops at its edge, as everything else the runtime probes does; only one that starts within the
/// allowance of the edge, a lazy build at the bottom of a recursion, spends any of the reserve. A walk that started
/// inside the reserve gets no allowance, and fails at its first probe, having taken at most
/// <see cref="UncheckedDepth"/> levels; the interpreter without these probes took any number.
/// </para>
/// <para>
/// A walk that runs out fails with a <see cref="ScriptPreparationException"/> marked by a
/// <see cref="NestingTooDeepException"/>. A statement list turns the mark into a <c>RangeError</c> the script can
/// catch, so a tree built while script runs fails the way V8's lazy compilation does, and building a function when it
/// is called does the same (rejecting an async function's promise). The engine's entry point removes the mark on the
/// way out, so the host, and a script whose host callback ran another script, see a
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
    /// How much stack a walk that started above the runtime's reserve may spend in all, once the reserve is gone: a
    /// quarter of the reserve <c>RuntimeHelpers.TryEnsureSufficientExecutionStack</c> keeps, which is 128 KiB in a
    /// 64-bit process and 64 KiB in a 32-bit one. Not half: a build that threw halfway into the reserve has ended the
    /// process while its exception was dispatched.
    /// </summary>
    internal static readonly int Allowance = IntPtr.Size == 8 ? 32 * 1024 : 16 * 1024;

    internal const string RangeErrorMessage = "Maximum call stack size exceeded";

    // Thread-static rather than threaded through: the node builders are static and reached from dozens of
    // constructors, and a build never yields its thread midway.
    [ThreadStatic]
    private static int _depth;

    [ThreadStatic]
    private static StackBudget _budget;

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
    /// Whether the node being built, or one of the <paramref name="levels"/> levels below it, sits where
    /// <see cref="IsEvaluationProbeDepth"/> holds: whether the nested chain a flattened node stands in for would have
    /// put an evaluation probe under one of its nodes.
    /// </summary>
    internal static bool SpansEvaluationProbeDepth(int levels)
    {
        var depth = _depth;
        var offset = depth % EvaluationProbeInterval;
        return (depth > 0 && offset == 0) || offset + levels >= EvaluationProbeInterval;
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
            _budget.Start();
        }
        else if (depth > UncheckedDepth && !_budget.HasHeadroom())
        {
            _depth--;
            ThrowTooDeep();
        }

        return default;
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is a walk that ran out of stack, as <see cref="ThrowTooDeep"/> raises it.
    /// </summary>
    internal static bool IsTooDeep(Exception exception)
        => exception is ScriptPreparationException { InnerException: NestingTooDeepException };

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowTooDeep()
    {
        throw new ScriptPreparationException(
            "Script nesting is too deep: preparing it would exhaust the stack.",
            new NestingTooDeepException());
    }

    /// <summary>
    /// The exception a host sees for a walk that ran out of stack: the same, without the mark that lets a statement
    /// list catch it.
    /// </summary>
    internal static ScriptPreparationException ForHost(ScriptPreparationException exception)
        => new(exception.Message, innerException: null);

    /// <summary>
    /// The <c>RangeError</c> a script sees for a walk that ran out of stack.
    /// </summary>
    internal static JsValue CreateRangeError(Realm realm) => realm.Intrinsics.RangeError.Construct(RangeErrorMessage);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowExceedsMaximumDepth()
    {
        throw new ScriptPreparationException($"Script nesting exceeds maximum depth of {MaxUnprobedDepth} levels.", null);
    }

    /// <summary>
    /// An address in the caller's stack frame, close enough to its stack pointer to measure what a walk has spent.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe nuint StackPosition()
    {
        byte marker = 0;
        return (nuint) (&marker);
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
    /// The stack one walk may still spend; see the class remarks for the rule and why it holds.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal struct StackBudget
    {
        private nuint _base;
        private bool _startedAboveReserve;

        /// <summary>
        /// Called as the walk starts.
        /// </summary>
        public void Start()
        {
            _base = StackPosition();
            _startedAboveReserve = RuntimeHelpers.TryEnsureSufficientExecutionStack();
        }

        /// <summary>
        /// Whether the walk may go one level deeper.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HasHeadroom()
        {
            // The stack grows down on every platform .NET runs on.
            return RuntimeHelpers.TryEnsureSufficientExecutionStack()
                   || (_startedAboveReserve && (long) _base - (long) StackPosition() < Allowance);
        }
    }

    /// <summary>
    /// What the hoisting walk knows about a path through the tree: how many levels it has gone since the last
    /// evaluation probe on it, conservatively.
    /// </summary>
    /// <remarks>
    /// A chain's probes are placed where the build depth (or an <c>else if</c>'s position) is a multiple of
    /// <see cref="EvaluationProbeInterval"/>, which the walk cannot see. It knows only that any run of that many
    /// consecutive chain levels contains such a position, and that the position carries no probe when it is the
    /// chain's last level. So the first interval of a run, its last level included, counts as unprobed on top of
    /// whatever lies above the run, and any level after it as at most one interval and one level past a probe. A
    /// flattened <c>+</c> chain has no levels below its first node, so the walk counts levels it does not have; it
    /// carries a probe of its own wherever its nested form would have carried one, so the count still holds.
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
            var depth = runDepth <= EvaluationProbeInterval ? runEntry + runDepth : EvaluationProbeInterval + 1;
            return new Unprobed(depth, runEntry, runDepth);
        }
    }
}

/// <summary>
/// Marks the <see cref="ScriptPreparationException"/> of a walk over the syntax tree that ran out of stack.
/// </summary>
internal sealed class NestingTooDeepException : JintException
{
    public NestingTooDeepException() : base(SyntaxNesting.RangeErrorMessage)
    {
    }
}
