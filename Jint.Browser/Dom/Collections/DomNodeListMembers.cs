using AngleSharp.Dom;
using Jint.Native;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The <a href="https://dom.spec.whatwg.org/#interface-nodelist">DOM §4.2.10</a> <c>NodeList</c> members
/// whose WebIDL contract <c>INodeList</c>'s CLR surface does not keep.
/// </summary>
/// <remarks>
/// One member so far, and it is the same shape as <see cref="DomTokenListMembers.Item"/>'s:
/// <c>getter Node? item(unsigned long index)</c> is an
/// <a href="https://webidl.spec.whatwg.org/#dfn-indexed-property-getter">indexed property getter</a>, and an
/// indexed getter never throws for an index past the end — it answers the nullable return type's null. The
/// CLR indexer behind it is a <c>List&lt;T&gt;</c>'s, which raises
/// <c>ArgumentOutOfRangeException</c>; <c>dom/nodes/Node-childNodes.html</c> asserts
/// <c>children.item(2) === null</c> beside <c>children[2] === undefined</c>, and the two answers are
/// different on purpose.
/// </remarks>
internal static class DomNodeListMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-nodelist-item — <c>null</c> out of range, never a throw.</summary>
    internal static JsValue Item(DomRealm realm, INodeList list, JsValue[] arguments)
    {
        // WebIDL's unsigned long: -1 is 4294967295, which is out of range rather than an error, and that is
        // the whole of what an indexed getter promises.
        var index = DomConvert.RequiredUInt32(arguments, 0, "NodeList.item");
        return index >= (uint) list.Length ? JsValue.Null : realm.WrapNodeValue(list[(int) index]);
    }
}
