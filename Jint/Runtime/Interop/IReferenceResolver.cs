using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// Reference resolver allows customizing behavior for reference resolving. This can be useful in cases where
/// you want to ignore long chain of property accesses that might throw if anything is null or undefined.
/// An example of such is <code>var a = obj.field.subField.value</code>. Custom resolver could accept chain to return
/// null/undefined on first occurrence.
/// </summary>
public interface IReferenceResolver
{
    /// <summary>
    /// When unresolvable reference occurs, check if another value can be provided instead of it.
    /// </summary>
    /// <remarks>
    /// A reference error will be thrown if this method return false.
    /// </remarks>
    /// <param name="engine">The current engine instance.</param>
    /// <param name="reference">The reference that is being processed.</param>
    /// <param name="value">Value that should be used instead of undefined.</param>
    /// <returns>Whether to use <paramref name="value" /> instead of undefined.</returns>
    bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value);

    /// <summary>
    /// When property reference is being processed, resolve to other value if needed.
    /// </summary>
    /// <param name="engine">The current engine instance.</param>
    /// <param name="reference">The reference that is being processed.</param>
    /// <param name="value">Value that should be used instead of reference target.</param>
    /// <returns>Whether to use <paramref name="value" /> instead of reference's value.</returns>
    bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value);

    /// <summary>
    /// When evaluating a function call and a target that is not an object is encountered,
    /// custom implementation can return a value to call.
    /// </summary>
    /// <remarks>
    /// A reference error will be thrown if this method return false.
    /// </remarks>
    /// <param name="engine">The current engine instance.</param>
    /// <param name="callee">The callee.</param>
    /// <param name="value">Value that should be used when this method return true. Should be <see cref="ICallable"/>.</param>
    /// <returns>Whether to use <paramref name="value" /> instead of undefined.</returns>
    bool TryGetCallable(Engine engine, object callee, out JsValue value);

    /// <summary>
    /// Check whether objects property value is valid.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>Whether to accept the value.</returns>
    bool CheckCoercible(JsValue value);
}

/// <summary>
/// The situations in which an <see cref="IReferenceResolver"/> wants to be consulted, declared when the
/// resolver is registered via
/// <see cref="OptionsExtensions.SetReferencesResolver(Options, IReferenceResolver, ReferenceResolverInterests)"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>subscription filter, not a promise</b>. Every situation the resolver does not subscribe to
/// is one the engine simply does not call it for, so there is no contract a host can violate by narrowing:
/// the engine behaves exactly as if no resolver were registered for those situations.
/// </para>
/// <para>
/// Narrowing matters because subscribing to a property reference over a <em>value</em> base — an object
/// (<see cref="ObjectPropertyBase"/>) or a non-nullish primitive (<see cref="PrimitivePropertyBase"/>) —
/// disables several interpreter fast paths: the member-read inline caches, the dense-array indexed-read
/// lane and the member-call callee lane all then have to route through a <see cref="Reference"/> so the
/// resolver gets its chance. The gate is that pair of interests, not the presence of a resolver: a
/// resolver declaring only <see cref="NullishPropertyBase"/> keeps every one of those lanes armed, since a
/// lane recognizes a nullish base itself and detours through a <see cref="Reference"/> for that one case.
/// A resolver registered through
/// <see cref="OptionsExtensions.SetReferencesResolver(Options, IReferenceResolver)"/> gets <see cref="All"/>,
/// which behaves exactly as before this filter existed.
/// </para>
/// </remarks>
[Flags]
public enum ReferenceResolverInterests
{
    /// <summary>
    /// The resolver is never consulted. Equivalent to registering no resolver at all.
    /// </summary>
    None = 0,

    /// <summary>
    /// Property references whose base is <c>null</c> or <c>undefined</c> — the null-propagation use case
    /// (<c>a.b.c</c> yielding <see cref="JsValue.Undefined"/> instead of throwing). Enables both
    /// <see cref="IReferenceResolver.CheckCoercible"/> and <see cref="IReferenceResolver.TryPropertyReference"/>
    /// for those references.
    /// </summary>
    NullishPropertyBase = 1 << 0,

    /// <summary>
    /// Property references whose base is an object. This is the costliest interest to subscribe to: it
    /// disables the non-computed member-read caches, the dense-array indexed-read lane and the member-call
    /// callee lane, because every property read has to be offered to
    /// <see cref="IReferenceResolver.TryPropertyReference"/> first.
    /// </summary>
    ObjectPropertyBase = 1 << 1,

    /// <summary>
    /// Property references whose base is a primitive other than <c>null</c>/<c>undefined</c> — a string,
    /// number, boolean, symbol or BigInt. Shares the read-lane cost with <see cref="ObjectPropertyBase"/>,
    /// since the base's type is only known once the base has been evaluated.
    /// </summary>
    PrimitivePropertyBase = 1 << 2,

    /// <summary>
    /// Unresolvable references — reads of names that resolve to no binding — routed through
    /// <see cref="IReferenceResolver.TryUnresolvableReference"/> instead of throwing a <c>ReferenceError</c>.
    /// </summary>
    UnresolvableReference = 1 << 3,

    /// <summary>
    /// Call expressions whose resolved callee is not callable, routed through
    /// <see cref="IReferenceResolver.TryGetCallable"/> instead of throwing a <c>TypeError</c>.
    /// </summary>
    NonCallableCallee = 1 << 4,

    /// <summary>
    /// Every situation. The default for resolvers registered without an explicit interest set.
    /// </summary>
    All = NullishPropertyBase | ObjectPropertyBase | PrimitivePropertyBase | UnresolvableReference | NonCallableCallee,
}
