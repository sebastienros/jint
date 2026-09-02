using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// Wires <c>Symbol.iterator</c> onto a collection instance.
/// </summary>
/// <remarks>
/// <para>
/// <c>ArrayLikeObject</c> deliberately installs no iterator and its class remarks prescribe this exact wiring:
/// point <c>Symbol.iterator</c> at <c>Array.prototype[Symbol.iterator]</c> — which is what WHATWG
/// specifications do for <c>NodeList</c> — and <c>for..of</c>, spread, <c>Array.from</c> and array
/// destructuring all route through the engine's array-like iterator against <c>TryGetIndex</c>.
/// </para>
/// <para>
/// It goes on the <em>instance</em> rather than on the interface prototype, which is a divergence worth
/// naming: a browser puts it on <c>NodeList.prototype</c> as an <c>iterable&lt;&gt;</c> declaration, so
/// <c>NodeList.prototype[Symbol.iterator]</c> answers there and answers <c>undefined</c> here. The value is
/// the same function object either way, and nothing a script does with a collection can tell the difference;
/// moving it to the shape means giving <c>JsObjectShape</c> a symbol-keyed member, which is additive and can
/// happen later.
/// </para>
/// </remarks>
internal static class DomIterator
{
    internal static void Install(DomRealm realm, ObjectInstance collection)
    {
        var iterator = realm.PrincipalRealm.Intrinsics.Array.PrototypeObject.Get(GlobalSymbolRegistry.Iterator);
        collection.DefineOwnPropertyUnchecked(
            GlobalSymbolRegistry.Iterator,
            new PropertyDescriptor(iterator, PropertyFlag.NonEnumerable));
    }
}
