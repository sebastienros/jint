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
/// <remarks>
/// Every kind that names actual values is spelled as the <see cref="InternalTypes"/> bits that
/// answer it, so checking a composed guard is one mask test against the value's <c>_type</c> rather
/// than a walk over the kinds — which matters most for the values that <em>fail</em> a guard, since
/// those pay the whole walk before falling back to the framed path. Two kinds sit outside that,
/// on the two bits above every <see cref="InternalTypes"/> flag: <see cref="Date"/>, because
/// <c>JsDate</c> has no <c>_type</c> bit of its own and is decided by a type test, and
/// <see cref="AnyValue"/>, which is resolved away at generation time and never reaches a shape.
/// <c>JsMap</c> and <c>JsSet</c> could have joined <see cref="Date"/> and deliberately did not: a
/// keyed-collection receiver guard is asked on a hot lookup, and putting it in the type-test tail
/// would have lengthened that tail for every kind, including the ones that merely fail.
/// <c>FastCallGuardValuesMatchInternalTypes</c> in <c>FastCallLaneTests</c> pins the correspondence,
/// including that no <see cref="InternalTypes"/> flag has grown into the two borrowed bits.
/// </remarks>
[Flags]
internal enum FastCallGuard
{
    /// <summary>No constraint.</summary>
    Any = 0,

    /// <summary>Building block of <see cref="Number"/>; declare that instead.</summary>
    NumberDouble = (int) InternalTypes.Number,

    /// <summary>Building block of <see cref="Number"/>; declare that instead.</summary>
    NumberInteger = (int) InternalTypes.Integer,

    /// <summary>
    /// Either representation a <c>JsNumber</c> can carry — the two are alternatives, so both bits
    /// have to be in the mask for "is already a number", which is what a numeric coercion is a no-op
    /// for.
    /// </summary>
    Number = NumberDouble | NumberInteger,

    String = (int) InternalTypes.String,
    Array = (int) InternalTypes.Array,

    /// <summary>
    /// A <c>JsMap</c>, which is exactly the brand every <c>Map.prototype</c> method checks. Proves the
    /// brand and nothing else, so a <c>Set</c> — or any other object — still reaches the TypeError.
    /// </summary>
    Map = (int) InternalTypes.Map,

    /// <summary><see cref="Map"/> for <c>JsSet</c>. Deliberately a separate kind, not a shared one.</summary>
    Set = (int) InternalTypes.Set,

    /// <summary>
    /// Only meaningful on an argument. Coercing <c>undefined</c> runs no user code for any converter
    /// the guards model, but the body can still tell it apart from a number (that is the whole point
    /// of <c>end === undefined</c> meaning "to the end of the string"), so it is never folded into
    /// the other kinds — a declaration has to ask for it.
    /// </summary>
    Undefined = (int) InternalTypes.Undefined,

    /// <summary>
    /// Every value, <em>declared</em>. <see cref="Any"/> is the absence of a constraint, which is also
    /// what the generator refuses to infer for a plain <c>JsValue</c> parameter — so <c>Any</c> cannot
    /// double as the author's positive claim that the body is leaf-safe whatever the value is. A
    /// keyed-collection lookup is: <c>SameValueZero</c> hashes and compares, and coerces nothing, so
    /// no key can reach a user <c>valueOf</c> the way a <c>String.prototype</c> argument can.
    /// <para>
    /// Declaration-only. The generator resolves it to <see cref="Any"/> — a claim that there is no
    /// hazard publishes no constraint — so it never appears in an emitted <see cref="FastCallShape"/>
    /// and <c>Satisfies</c> never sees it. It also subsumes anything composed with it.
    /// </para>
    /// </summary>
    AnyValue = 1 << 29,

    /// <summary>Deliberately not an <see cref="InternalTypes"/> bit — see the remarks on this enum.</summary>
    Date = 1 << 30,
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
    /// The guard doubles as an <see cref="InternalTypes"/> mask, so however many kinds it names the
    /// answer is one test — and a value that matches none of them, the case that has to fall back to
    /// the framed path, reaches that verdict in the same one test rather than after trying each kind
    /// in turn. Only <c>Date</c> needs more, because it is the one kind with no <c>_type</c> bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Satisfies(FastCallGuard guard, JsValue value)
    {
        if (guard == FastCallGuard.Any)
        {
            return true;
        }

        // In-box invariant, not a host contract: only the generator and the handful of hand-written
        // shapes build these, and AnyValue is resolved to Any before either can emit it. Reaching
        // here with it would merely deny every value — the safe direction, and therefore the one that
        // would go unnoticed without saying so.
        Debug.Assert(
            (guard & FastCallGuard.AnyValue) == FastCallGuard.Any,
            "FastCallGuard.AnyValue is declaration-only; a FastCallShape must never carry it.");

        if ((value._type & (InternalTypes) guard) != InternalTypes.Empty)
        {
            return true;
        }

        // The Date bit is above every InternalTypes flag, so it cannot have matched above.
        return (guard & FastCallGuard.Date) != FastCallGuard.Any && value is JsDate;
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
