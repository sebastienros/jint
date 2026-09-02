using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Observers;

/// <summary>
/// The box sizes a <c>ResizeObserverEntry</c> carries, and the receiver check both observers' entries share.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rectangles are real now, and they are <see cref="Layout.DomRects"/>'.</b> This file used to own a
/// rectangle whose every instance was zeros, because there was no box model for one to report; the flat
/// layout is that model, so an entry answers the target's own box and a page reading
/// <c>entry.boundingClientRect.width</c> gets a number that agrees with <c>getBoundingClientRect</c> and
/// with <c>DOM.getBoxModel</c>. What has not changed is that a rectangle is a plain object rather than a
/// <c>DOMRectReadOnly</c> instance, and <see cref="Layout.DomRects"/> says why.
/// </para>
/// <para>
/// One process-shared <see cref="JsObjectLayout"/> per shape, so every box size an engine builds shares a
/// hidden class and a page reading <c>.inlineSize</c> across a batch of entries keeps a monomorphic inline
/// cache.
/// </para>
/// </remarks>
internal static class ObserverGeometry
{
    /// <summary>
    /// https://drafts.csswg.org/resize-observer/#resizeobserversize — the two writing-mode-relative lengths a
    /// box size reports.
    /// </summary>
    private static readonly JsObjectLayout _boxSize = new JsObjectLayout.Builder()
        .Add("inlineSize")
        .Add("blockSize")
        .Build();

    /// <summary>A one-element array holding the single fragment a box that is never split has.</summary>
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
