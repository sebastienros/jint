using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native.Date;
using Jint.Runtime;

namespace Jint.Native.Function;

/// <summary>
/// A runtime precondition on a receiver or argument. Only the frameless lane consults these: the
/// arity-specialized invoke is semantics-preserving for any value, because the built-in still runs
/// its own coercions — dropping the call-stack frame is what needs the value to be provably unable
/// to reach user code.
/// <para>
/// Composable, because a built-in that coerces a raw <c>JsValue</c> argument in its body is
/// typically safe for more than one shape of value: <c>"abc".substring(start, end)</c> reaches no
/// user code for a number <em>or</em> for the <c>undefined</c> an omitted argument produces, and
/// requiring a single kind would cost the leaf lane every one-argument call site. <c>Any</c> is the
/// absence of a constraint rather than "all bits", so an unguarded position stays a zero.
/// </para>
/// </summary>
[Flags]
internal enum FastCallGuard : byte
{
    /// <summary>No constraint.</summary>
    Any = 0,
    Number = 1,
    String = 2,
    Date = 4,
    Array = 8,

    /// <summary>
    /// Only meaningful on an argument. Coercing <c>undefined</c> runs no user code for any converter
    /// the guards model, but the body can still tell it apart from a number (that is the whole point
    /// of <c>end === undefined</c> meaning "to the end of the string"), so it is never folded into
    /// the other kinds — a declaration has to ask for it.
    /// </summary>
    Undefined = 16,
}

/// <summary>
/// Per-(function, arity) verdict describing how a built-in may be invoked, resolved once per call
/// site and cached alongside the callee so no virtual dispatch happens per call.
///
/// The lanes have deliberately different eligibility:
/// <list type="bullet">
/// <item><see cref="Supported"/> enables the arity-specialized invoke — arguments passed in
/// registers instead of a pooled <c>JsValue[]</c>, straight to the target instead of through the
/// generated <c>switch (_slot)</c>. The call-stack frame is still pushed, so this is correct even
/// for built-ins that throw or re-enter user code.</item>
/// <item><see cref="Leaf"/> additionally allows the frame to be elided, but only when every guard
/// below holds for the actual receiver and arguments. A built-in that cannot run user code with a
/// number argument can absolutely do so with an object one (via <c>valueOf</c>), and that user code
/// can observe <c>error.stack</c> — so this is a per-call verdict, never a static property of the
/// method.</item>
/// <item><see cref="Variadic"/> selects <c>Function.CallFastVariadic</c> over <c>Function.CallFast</c>:
/// the built-in takes a <c>[Rest]</c> tail, so it needs the arguments as a span whose length is the
/// site's real arity rather than two registers padded with <c>undefined</c>. Shapes are resolved per
/// arity, so a variadic slot simply declines every arity the lane cannot carry.</item>
/// </list>
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FastCallShape(
    bool Supported,
    bool Leaf,
    bool Variadic,
    FastCallGuard Receiver,
    FastCallGuard Arg0,
    FastCallGuard Arg1)
{
    /// <summary>
    /// Whether <paramref name="value"/> matches any kind the composed <paramref name="guard"/> names.
    /// Ordered by how often built-ins declare each kind, so the common single-flag guards decide in
    /// one test; <c>Date</c> comes last because it is the only one that is not a <c>_type</c> bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Satisfies(FastCallGuard guard, JsValue value)
    {
        if (guard == FastCallGuard.Any)
        {
            return true;
        }

        var type = value._type;

        if ((guard & FastCallGuard.Number) == FastCallGuard.Number && (type & (InternalTypes.Number | InternalTypes.Integer)) != InternalTypes.Empty)
        {
            return true;
        }

        if ((guard & FastCallGuard.String) == FastCallGuard.String && (type & InternalTypes.String) != InternalTypes.Empty)
        {
            return true;
        }

        if ((guard & FastCallGuard.Undefined) == FastCallGuard.Undefined && (type & InternalTypes.Undefined) != InternalTypes.Empty)
        {
            return true;
        }

        if ((guard & FastCallGuard.Array) == FastCallGuard.Array && (type & InternalTypes.Array) != InternalTypes.Empty)
        {
            return true;
        }

        return (guard & FastCallGuard.Date) == FastCallGuard.Date && value is JsDate;
    }

    /// <summary>
    /// Whether the frame may be elided for this exact invocation. Both argument registers are always
    /// tested; a <see cref="Variadic"/> shape resolved for an arity below two therefore publishes
    /// <see cref="FastCallGuard.Any"/> for the registers its body will never see, since the padding
    /// the call site puts there is not part of the argument list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsLeafFor(JsValue thisObject, JsValue arg0, JsValue arg1)
        => Leaf
           && Satisfies(Receiver, thisObject)
           && Satisfies(Arg0, arg0)
           && Satisfies(Arg1, arg1);
}

/// <summary>
/// Debug-only verification that a <see cref="FastCallShape.Leaf"/> annotation tells the truth.
/// A frameless call must neither raise a JavaScript error nor re-enter interpreted code — both are
/// observable through <c>error.stack</c>, which is exactly what the missing frame would corrupt.
/// Auditing ~1000 built-ins by eye is not credible, so instead the claim is asserted at the two
/// places a violation must pass through and the existing Test262 / unit suites do the auditing:
/// any mis-annotated method trips the assert on the first test that exercises it.
/// Compiled out entirely in Release.
/// </summary>
internal static class LeafCallGuard
{
#if DEBUG
    // Thread-static rather than a field on Engine: a frameless call is synchronous and cannot
    // re-enter, so the counter never needs to span threads, and this keeps the assertion points
    // free of any plumbing to reach an Engine instance.
    [ThreadStatic]
    private static int _depth;
#endif

    // [Conditional] strips the CALL SITES, not the bodies, so the bodies need their own guard.
    [Conditional("DEBUG")]
    internal static void Enter()
    {
#if DEBUG
        _depth++;
#endif
    }

    [Conditional("DEBUG")]
    internal static void Exit()
    {
#if DEBUG
        _depth--;
#endif
    }

    /// <summary>
    /// Called from the places a leaf built-in must never reach: JavaScript error construction and
    /// the call-stack push that every interpreted call performs.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertNotInLeafCall(string what)
    {
#if DEBUG
        Debug.Assert(
            _depth == 0,
            $"A built-in annotated Leaf reached {what}. Its FastCallShape must not report Leaf (or must guard the argument shape that allows it).");
#endif
    }
}
