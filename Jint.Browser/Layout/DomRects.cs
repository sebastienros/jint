using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Layout;

/// <summary>
/// The rectangle every layout answer is handed back as, and the list a <c>getClientRects</c> answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shaped like <c>DOMRect</c>, and not an instance of it.</b> The eight members are
/// <c>DOMRectReadOnly</c>'s, in the order the interface declares them, so everything a page reads off a
/// rectangle is there; what is missing is the interface object and the prototype, so
/// <c>rect instanceof DOMRect</c> is <see langword="false"/> and <c>Object.prototype.toString</c> reports
/// <c>[object Object]</c>. Adding the interface means adding a constructible <c>DOMRect</c>, a
/// <c>DOMRectReadOnly</c> above it and a <c>DOMRectList</c> beside it to a package whose interface objects
/// are otherwise all generated from AngleSharp's metadata, which has none of the three; it is a separate
/// decision from giving the numbers meaning, and this is the half the flat model owes.
/// </para>
/// <para>
/// One process-shared <see cref="JsObjectLayout"/>, so every rectangle an engine builds shares a hidden
/// class and a page reading <c>.width</c> across a batch of them keeps a monomorphic inline cache.
/// </para>
/// </remarks>
internal static class DomRects
{
    /// <summary>The eight numbers <c>DOMRectReadOnly</c> declares, in the order the interface lists them.</summary>
    private static readonly JsObjectLayout _rect = new JsObjectLayout.Builder()
        .Add("x")
        .Add("y")
        .Add("width")
        .Add("height")
        .Add("top")
        .Add("right")
        .Add("bottom")
        .Add("left")
        .Build();

    /// <summary>A rectangle at the origin with no extent, which is what a box-less element answers.</summary>
    internal static JsObject Zero(Engine engine) => Of(engine, FlatBox.Empty);

    /// <summary>A rectangle, with the four derived edges filled in from the box's four numbers.</summary>
    internal static JsObject Of(Engine engine, in FlatBox box)
        => JsObject.Create(
            engine,
            _rect,
            [
                JsNumber.Create(box.X),
                JsNumber.Create(box.Y),
                JsNumber.Create(box.Width),
                JsNumber.Create(box.Height),
                JsNumber.Create(box.Y),
                JsNumber.Create(box.Right),
                JsNumber.Create(box.Bottom),
                JsNumber.Create(box.X),
            ]);

    /// <summary>
    /// The <c>DOMRectList</c> a <c>getClientRects</c> answers, which is an ordinary array here.
    /// </summary>
    /// <remarks>
    /// A <c>DOMRectList</c> is indexed, has a <c>length</c> and an <c>item(i)</c>; an array has the first
    /// two and not the third, which is the whole of the divergence and the same trade
    /// <c>Range.getClientRects</c> already made. Pages read <c>rects[0]</c> and <c>rects.length</c>.
    /// </remarks>
    internal static JsValue List(DomRealm realm, params JsValue[] rects)
        => realm.PrincipalRealm.Intrinsics.Array.ConstructFast(rects);
}
