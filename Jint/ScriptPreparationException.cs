using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Jint;

public sealed class ScriptPreparationException : JintException
{
    public ScriptPreparationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// The stack probe for a recursion as deep as the script's syntax tree: building the interpreter's nodes and the
/// analyses run over a function's body. It fails such a walk with a <see cref="ScriptPreparationException"/>
/// while there is still stack to unwind on.
/// </summary>
/// <remarks>
/// <para>
/// The parser accepts trees far deeper than its own recursion: it builds a left-associative chain —
/// <c>a || b || …</c>, <c>x + y + …</c>, <c>o.p.q…</c>, <c>f()()…</c> — in a loop, one level of tree per operand.
/// A fixed level count either rejects ordinary generated code (a proxy auto-config file is one long
/// <c>||</c> chain, or one long <c>else if</c> chain) or admits a tree deep enough to overflow a small thread,
/// so every recursive walk over the tree asks the runtime for stack instead, the way the BCL's own tree walks
/// (<c>RegexNode</c>, <c>System.Linq.Expressions</c>) do.
/// </para>
/// <para>
/// The first <see cref="UncheckedDepth"/> levels on a thread do not ask, and that is a semantic choice, not
/// only a saving: a node is built lazily, when it first runs, so a <c>catch</c> block or a never-called
/// function can be built while a deep recursion has already spent the stack down to the runtime's reserve.
/// Such a build is shallow and always succeeded inside that reserve; failing it would turn the script's
/// catchable <c>RangeError</c> into an exception the script cannot catch.
/// </para>
/// <para>
/// Only cold paths enter it: once per interpreter node built or per node an analysis visits, never per
/// evaluation. What it decides at build time is also where a deep chain gets the probes it evaluates under,
/// so a chain of ordinary length is built and evaluated exactly as before.
/// </para>
/// </remarks>
internal static class SyntaxNesting
{
    /// <summary>
    /// Levels a thread recurses through the tree before probing. The runtime's reserve (128 KiB on a 64-bit
    /// process) covers this many build frames several times over.
    /// </summary>
    internal const int UncheckedDepth = 32;

    /// <summary>
    /// How many levels of a binary or logical operator chain, or arms of an <c>else if</c> chain, run between two
    /// evaluation probes (<c>JintStackProbeExpression</c>, <c>JintStackProbeStatement</c>). Small enough that the
    /// frames in between fit the runtime's reserve with room to spare.
    /// </summary>
    internal const int EvaluationProbeInterval = 64;

    // Thread-static rather than threaded through: the node builders are static and reached from dozens of
    // constructors, and a build never yields its thread midway.
    [ThreadStatic]
    private static int _depth;

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
        if (++_depth > UncheckedDepth && !RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            _depth--;
            ThrowTooDeep();
        }

        return default;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowTooDeep()
    {
        throw new ScriptPreparationException("Script nesting is too deep: preparing it would exhaust the stack.", null);
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
}
