using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Layout;

/// <summary>
/// The rectangle every layout answer is handed back as, and the list a <c>getClientRects</c> answers.
/// </summary>
/// <remarks>
/// A real <c>DOMRect</c> (https://drafts.fxtf.org/geometry/#domrect) from the realm the answer is given in,
/// or a <c>DOMRectReadOnly</c> where the interface declares one — an observer entry's rectangles. Either is a
/// fresh object each time, so a page may keep or mutate the one it is given.
/// </remarks>
internal static class DomRects
{
    /// <summary>A rectangle at the origin with no extent, which is what a box-less element answers.</summary>
    internal static JsValue Zero(DomRealm realm) => Of(realm, FlatBox.Empty);

    /// <summary>A <c>DOMRect</c>, or with <paramref name="readOnly"/> a <c>DOMRectReadOnly</c>, over the box's four numbers.</summary>
    internal static JsValue Of(DomRealm realm, in FlatBox box, bool readOnly = false)
        => realm.Geometry.CreateRect(!readOnly, box.X, box.Y, box.Width, box.Height);

    /// <summary>
    /// The <c>DOMRectList</c> a <c>getClientRects</c> answers, which is an ordinary array here.
    /// </summary>
    /// <remarks>
    /// A <c>DOMRectList</c> is indexed, has a <c>length</c> and an <c>item(i)</c>; an array has the first
    /// two and not the third, which is the whole of the divergence and the same trade
    /// <c>Range.getClientRects</c> already made. Pages read <c>rects[0]</c> and <c>rects.length</c>.
    /// </remarks>
    internal static JsValue List(DomRealm realm, params JsValue[] rects)
        => realm.OwningRealm.Intrinsics.Array.ConstructFast(rects);
}
