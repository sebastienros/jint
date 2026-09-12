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
    internal static JsObjectShape HtmlCollection()
        => new JsObjectShape.Builder()
            .ToStringTag("HTMLCollection")
            .PerRealmSlot("constructor", enumerable: false)
            // https://webidl.spec.whatwg.org/#es-attributes — IDL attributes are enumerable, configurable
            // accessors on the interface prototype.
            .Accessor("length", static (thisObj, _) => JsNumber.Create(Receiver(thisObj, "HTMLCollection.length").Length))

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

    /// <summary>
    /// <c>HTMLAllCollection.prototype</c>. Hand-written for the reason its wrapper is: HTML gives
    /// <c>document.all</c> three members that are not <c>HTMLCollection</c>'s, and the interface inherits
    /// nothing, so none of them could come from the chain either.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#htmlallcollection —
    /// <c>item</c> takes an <em>optional</em> <c>DOMString</c> (so its <c>length</c> is 0, and an explicit
    /// <c>undefined</c> is "not provided" and answers null), <c>namedItem</c> requires one (so its
    /// <c>length</c> is 1, and calling it with none is a <c>TypeError</c>), and both answer an element, an
    /// <c>HTMLCollection</c> or <c>null</c>.
    /// </remarks>
    internal static JsObjectShape HtmlAllCollection()
        => new JsObjectShape.Builder()
            .ToStringTag("HTMLAllCollection")
            .PerRealmSlot("constructor", enumerable: false)
            .Accessor("length", static (thisObj, _) => JsNumber.Create(AllCollection(thisObj, "HTMLAllCollection.length").Length))

            // https://webidl.spec.whatwg.org/#js-iterable — this interface declares the indexed property
            // support itself now that it inherits nothing, so @@iterator is its own rather than inherited.
            .PerRealmSlot(
                global::Jint.Native.Symbol.GlobalSymbolRegistry.Iterator,
                DomIterator.ArrayValues)
            .Method(
                "item",
                static (thisObj, args) =>
                {
                    var self = AllCollection(thisObj, "HTMLAllCollection.item");
                    return self.Item(DomConvert.OptionalText(args, 0, null));
                },
                length: 0)
            .Method(
                "namedItem",
                static (thisObj, args) =>
                {
                    var self = AllCollection(thisObj, "HTMLAllCollection.namedItem");
                    return self.NamedItem(DomConvert.RequiredText(args, 0, "HTMLAllCollection.namedItem"));
                },
                length: 1)
            .Build();

    private static DomCollectionBase Receiver(JsValue thisObject, string member)
        => thisObject as DomCollectionBase ?? IllegalInvocation<DomCollectionBase>(thisObject, member);

    private static DomHtmlAllCollectionObject AllCollection(JsValue thisObject, string member)
        => thisObject as DomHtmlAllCollectionObject ?? IllegalInvocation<DomHtmlAllCollectionObject>(thisObject, member);

    private static T IllegalInvocation<T>(JsValue thisObject, string member) where T : class
    {
        if (thisObject is Jint.Native.Object.ObjectInstance instance)
        {
            Jint.Runtime.Throw.TypeError(instance.Engine.Realm, "Failed to execute '" + member + "': Illegal invocation");
        }

        Jint.Runtime.Throw.TypeErrorNoEngine("Failed to execute '" + member + "': Illegal invocation");
        return null!;
    }
}
