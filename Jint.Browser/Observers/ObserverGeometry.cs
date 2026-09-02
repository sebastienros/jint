using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Observers;

/// <summary>
/// The boxes an <c>IntersectionObserverEntry</c> and a <c>ResizeObserverEntry</c> carry, at the only size a
/// page with no layout can honestly be told: zero.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are plain objects, not <c>DOMRectReadOnly</c> instances.</b> There is no box model in this version
/// and therefore no rectangle interface anywhere in the package — <c>Element.getBoundingClientRect</c> does
/// not exist either — so introducing <c>DOMRectReadOnly</c> here would mean shipping an interface whose only
/// instances are zeros. The flat-box model (campaign item C4) is what gives every element a deterministic
/// rectangle; when it lands, these become the real thing and a page reading <c>entry.boundingClientRect.width</c>
/// starts getting an answer instead of <c>0</c>. Until then the shape of the value is right and the numbers
/// are not, which is the honest half.
/// </para>
/// <para>
/// One process-shared <see cref="JsObjectLayout"/> per shape, so every rectangle an engine builds shares a
/// hidden class and a page reading <c>.width</c> across a batch of entries keeps a monomorphic inline cache.
/// </para>
/// </remarks>
internal static class ObserverGeometry
{
    /// <summary>
    /// The eight numbers <c>DOMRectReadOnly</c> declares, in the order the interface lists them, plus the
    /// <c>toJSON</c> the CSSOM View <c>DOMRect</c> stringifier is spelled as.
    /// </summary>
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

    /// <summary>
    /// https://drafts.csswg.org/resize-observer/#resizeobserversize — the two writing-mode-relative lengths a
    /// box size reports.
    /// </summary>
    private static readonly JsObjectLayout _boxSize = new JsObjectLayout.Builder()
        .Add("inlineSize")
        .Add("blockSize")
        .Build();

    /// <summary>A rectangle at the origin with no extent.</summary>
    internal static JsObject ZeroRect(Engine engine) => Rect(engine, 0, 0, 0, 0);

    /// <summary>A rectangle, with the four derived edges filled in from the four given numbers.</summary>
    internal static JsObject Rect(Engine engine, double x, double y, double width, double height)
        => JsObject.Create(
            engine,
            _rect,
            [
                JsNumber.Create(x),
                JsNumber.Create(y),
                JsNumber.Create(width),
                JsNumber.Create(height),
                JsNumber.Create(y),
                JsNumber.Create(x + width),
                JsNumber.Create(y + height),
                JsNumber.Create(x),
            ]);

    /// <summary>A one-element array holding the single fragment a box with no layout has.</summary>
    internal static JsValue BoxSizes(Engine engine, double inlineSize, double blockSize)
    {
        var size = JsObject.Create(engine, _boxSize, [JsNumber.Create(inlineSize), JsNumber.Create(blockSize)]);
        return engine._mainRealm.Intrinsics.Array.ConstructFast(new JsValue[] { size });
    }

    /// <summary>The receiver check an entry's members start with.</summary>
    internal static T Brand<T>(JsValue thisObject, string interfaceName, string member) where T : ObjectInstance
    {
        if (thisObject is T entry)
        {
            return entry;
        }

        var message = "Failed to read the '" + member + "' property from '" + interfaceName + "': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Jint.Runtime.Throw.TypeError(instance.Engine.Realm, message);
        }

        Jint.Runtime.Throw.TypeErrorNoEngine(message);
        return null!;
    }
}
