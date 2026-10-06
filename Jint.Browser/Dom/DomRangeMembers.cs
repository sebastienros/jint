using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>WebIDL conversions and Browser clone reactions around native DOM §5.5.</summary>
internal static class DomRangeMembers
{
    internal static JsValue ComparePoint(DomRealm realm, DomRange range, JsValue[] arguments)
    {
        var node = DomBindings.IdentityArgument(arguments, 0, "Range.comparePoint");
        var offset = DomConvert.RequiredUInt32(arguments, 1, "Range.comparePoint");
        return DomConvert.Number(range.ComparePoint(node, offset, realm.NativeReadCheckpoint, realm.CancellationToken));
    }

    internal static JsValue IsPointInRange(DomRealm realm, DomRange range, JsValue[] arguments)
    {
        var node = DomBindings.IdentityArgument(arguments, 0, "Range.isPointInRange");
        var offset = DomConvert.RequiredUInt32(arguments, 1, "Range.isPointInRange");
        return DomConvert.Bool(range.IsPointInRange(node, offset, realm.NativeReadCheckpoint, realm.CancellationToken));
    }

    internal static JsValue CloneContents(DomRealm realm, DomRange range)
        => CopyContents(realm, range, extract: false);

    internal static JsValue ExtractContents(DomRealm realm, DomRange range)
        => CopyContents(realm, range, extract: true);

    private static JsValue CopyContents(DomRealm realm, DomRange range, bool extract)
    {
        var fragment = extract ? range.ExtractContents() : range.CloneContents();
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, fragment);
        return realm.WrapNodeValue(fragment);
    }
}
