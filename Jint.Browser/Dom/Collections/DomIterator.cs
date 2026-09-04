using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The value of <c>Symbol.iterator</c> on a collection's interface prototype.
/// </summary>
/// <remarks>
/// <para>
/// https://webidl.spec.whatwg.org/#js-iterable — an interface that supports indexed properties has
/// <c>@@iterator</c>, and its value is the realm's <c>%Array.prototype.values%</c> itself rather than a
/// function wrapping it, which is what makes <c>NodeList.prototype[Symbol.iterator] ===
/// Array.prototype[Symbol.iterator]</c> hold in a browser and here. <c>ArrayLikeObject</c> deliberately
/// installs no iterator of its own and its class remarks prescribe exactly this value: <c>for..of</c>,
/// spread, <c>Array.from</c> and array destructuring then route through the engine's array-like iterator
/// against <c>TryGetIndex</c>.
/// </para>
/// <para>
/// It is declared on the shape as a per-realm slot rather than written onto each wrapper, so it is one
/// unmaterialized descriptor on the prototype instead of an own property on every collection a page ever
/// touches — and the value is read from <see cref="DomRealm.PrincipalRealm"/> rather than from the running
/// realm, so a collection first reached inside a <c>ShadowRealm</c> callback still iterates with the array
/// iterator its own object belongs to.
/// </para>
/// </remarks>
internal static class DomIterator
{
    /// <summary>
    /// The array iterator of the realm <paramref name="prototype"/> belongs to. Shaped as a
    /// <c>JsObjectShape</c> per-realm slot factory, which is how the generated shapes name it.
    /// </summary>
    internal static JsValue ArrayValues(ObjectInstance prototype)
        => DomRealm.Of(prototype.Engine).PrincipalRealm.Intrinsics.Array.PrototypeObject.Get(GlobalSymbolRegistry.Iterator);
}
