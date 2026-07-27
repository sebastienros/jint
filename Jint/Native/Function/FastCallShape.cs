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
/// </summary>
internal enum FastCallGuard : byte
{
    /// <summary>No constraint.</summary>
    Any = 0,
    Number,
    String,
    Date,
    Array,
}

/// <summary>
/// Per-(function, arity) verdict describing how a built-in may be invoked, resolved once per call
/// site and cached alongside the callee so no virtual dispatch happens per call.
///
/// The two lanes have deliberately different eligibility:
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
/// </list>
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FastCallShape(
    bool Supported,
    bool Leaf,
    FastCallGuard Receiver,
    FastCallGuard Arg0,
    FastCallGuard Arg1)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Satisfies(FastCallGuard guard, JsValue value)
    {
        if (guard == FastCallGuard.Any)
        {
            return true;
        }

        return guard switch
        {
            FastCallGuard.Number => (value._type & (InternalTypes.Number | InternalTypes.Integer)) != InternalTypes.Empty,
            FastCallGuard.String => (value._type & InternalTypes.String) != InternalTypes.Empty,
            FastCallGuard.Array => (value._type & InternalTypes.Array) != InternalTypes.Empty,
            FastCallGuard.Date => value is JsDate,
            _ => false,
        };
    }

    /// <summary>
    /// Whether the frame may be elided for this exact invocation.
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
