using Jint.Browser.Dom.Collections;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The shapes the generator leaves to a hand-written file, named from <c>overrides.json</c>'s <c>manual</c>
/// list so that the interface still gets its place in the generated prototype chain, its interface object and
/// its type-map entry.
/// </summary>
internal static class DomManualShapes
{
    /// <summary>
    /// <c>HTMLCollection.prototype</c>. It is hand-written because AngleSharp's
    /// <c>IHtmlCollection&lt;T&gt;</c> is generic and invariant, so a static member body cannot name the
    /// receiver's element type; both members reach the wrapper's own two virtuals instead, which
    /// <c>DomHtmlCollectionObject&lt;T&gt;</c> closes over the type it was created with.
    /// </summary>
    /// <remarks>
    /// <c>length</c> is deliberately absent: <c>ArrayLikeObject</c> makes it an own property of the
    /// collection, and a prototype accessor of the same name could only ever be shadowed. That deviation is
    /// documented on <see cref="DomCollectionBase"/>.
    /// </remarks>
    internal static JsObjectShape HtmlCollection()
        => new JsObjectShape.Builder()
            .ToStringTag("HTMLCollection")
            .PerRealmSlot("constructor", enumerable: false)

            // https://webidl.spec.whatwg.org/#js-iterable — the interface supports indexed properties, so its
            // prototype carries @@iterator; the generated collection shapes get the same line from the
            // emitter, and this one is hand-written only because the whole shape is.
            .PerRealmSlot(
                global::Jint.Native.Symbol.GlobalSymbolRegistry.Iterator,
                DomIterator.ArrayValues)
            .Method(
                "item",
                static (thisObj, args) =>
                {
                    var self = Receiver(thisObj, "HTMLCollection.item");
                    return self.Item(DomConvert.OptionalUInt32(args, 0, 0));
                },
                length: 1)
            .Method(
                "namedItem",
                static (thisObj, args) =>
                {
                    var self = Receiver(thisObj, "HTMLCollection.namedItem");
                    return self.NamedItem(DomConvert.RequiredText(args, 0, "HTMLCollection.namedItem"));
                },
                length: 1)
            .Build();

    private static DomCollectionBase Receiver(JsValue thisObject, string member)
    {
        if (thisObject is DomCollectionBase collection)
        {
            return collection;
        }

        if (thisObject is Jint.Native.Object.ObjectInstance instance)
        {
            Jint.Runtime.Throw.TypeError(instance.Engine.Realm, "Failed to execute '" + member + "': Illegal invocation");
        }

        Jint.Runtime.Throw.TypeErrorNoEngine("Failed to execute '" + member + "': Illegal invocation");
        return null!;
    }
}
