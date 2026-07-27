using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// The reference implementation of the use case <see cref="IReferenceResolver"/>'s own documentation leads
/// with: <i>"ignore long chain of property accesses that might throw if anything is null or undefined"</i>.
/// A property read whose base is <c>null</c> or <c>undefined</c> yields that base instead of throwing, so
/// <c>a.b.c.d</c> evaluates to <c>undefined</c> when <c>a.b</c> is <c>undefined</c>, and to <c>null</c> when
/// <c>a.b</c> is <c>null</c>. Every other situation is declined, leaving standard behaviour intact.
/// </summary>
/// <remarks>
/// <para>
/// Register it with the interest it actually uses, which keeps every unrelated interpreter fast path armed:
/// </para>
/// <code>
/// var engine = new Engine(options => options.SetReferencesResolver(
///     NullPropagatingReferenceResolver.Instance,
///     ReferenceResolverInterests.NullishPropertyBase));
/// </code>
/// <para>
/// The engine recognizes <see cref="Instance"/> by identity and serves the propagation inline — no interface
/// dispatch and no pooled <see cref="Reference"/> — so a host that needs nothing but propagation should use
/// this singleton rather than an equivalent hand-written resolver. The observable behaviour of the two is
/// identical either way; only the cost differs.
/// </para>
/// <para>
/// <b>Reads propagate, calls still throw.</b> This resolver covers property <i>reads</i>. A call on a nullish
/// base — <c>a.b.foo()</c> where <c>a.b</c> is nullish — still raises a <c>TypeError</c>, because the read
/// yields the nullish base and a nullish value is not callable. That is a deliberate boundary, not an
/// oversight: making the call short-circuit as well would be implementing <c>?.()</c> semantics implicitly.
/// A host that needs callable substitution implements <see cref="IReferenceResolver.TryGetCallable"/> in its
/// own resolver. Writes (<c>a.b = 1</c>), destructuring of a nullish value and iterable spread are likewise
/// unaffected and keep throwing, matching optional chaining, for which those are not valid targets.
/// </para>
/// <para>
/// <b>This is a deviation from ECMAScript, owned by the host.</b> A conforming program's <c>a.b</c> on a
/// nullish <c>a</c> is a <c>TypeError</c>; here it is a value. Jint offers no engine option that turns the
/// deviation on — the host takes it on explicitly, by passing this resolver to
/// <see cref="OptionsExtensions.SetReferencesResolver(Options, IReferenceResolver, ReferenceResolverInterests)"/>,
/// the extension point that already exists for exactly this purpose. Engines that register no resolver are
/// entirely unaffected.
/// </para>
/// </remarks>
public sealed class NullPropagatingReferenceResolver : IReferenceResolver
{
    /// <summary>
    /// The singleton instance. The engine compares the registered resolver against this reference, so
    /// registering it is what enables the inline lane described on the type.
    /// </summary>
    public static readonly NullPropagatingReferenceResolver Instance = new();

    private NullPropagatingReferenceResolver()
    {
    }

    /// <summary>
    /// Declined: an unresolvable identifier keeps raising a <c>ReferenceError</c>. Propagation is about
    /// property chains, not about undeclared names.
    /// </summary>
    public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
    {
        value = JsValue.Undefined;
        return false;
    }

    /// <summary>
    /// Propagates a <c>null</c>/<c>undefined</c> base: <paramref name="value"/> arrives bound to the
    /// reference's base, so accepting it unchanged makes the base itself the result of the read. Any other
    /// base is declined and read normally.
    /// </summary>
    public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
    {
        return value.IsNullOrUndefined();
    }

    /// <summary>
    /// Declined: a non-callable callee keeps raising a <c>TypeError</c>. See the remarks on the type for why
    /// calls are outside the scope of propagation.
    /// </summary>
    public bool TryGetCallable(Engine engine, object callee, out JsValue value)
    {
        value = JsValue.Undefined;
        return false;
    }

    /// <summary>
    /// Accepts a <c>null</c>/<c>undefined</c> base so the read reaches
    /// <see cref="TryPropertyReference"/> instead of throwing first.
    /// </summary>
    public bool CheckCoercible(JsValue value)
    {
        return value.IsNullOrUndefined();
    }
}
