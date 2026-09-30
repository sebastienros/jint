using Jint.Browser.Runtime;
using Jint.Native.Object;
using Jint.WebApi.Events;

namespace Jint.Browser.SystemState;

/// <summary>
/// <c>window.visualViewport</c>: the part of the layout viewport a user can see, which here is all of it.
/// </summary>
/// <remarks>
/// <para>
/// https://drafts.csswg.org/cssom-view/#the-visualviewport-interface — there is no pinch zoom, so the scale
/// is one, the offsets into the layout viewport are zero, and the page offsets are the layout viewport's own
/// scroll position. The size is the viewport's, with no scrollbar to subtract.
/// </para>
/// <para>
/// <b><c>resize</c> fires when the viewport changes size</b>, from the notification that reaches a
/// <c>MediaQueryList</c>. <c>scroll</c> never does: it reports the visual viewport moving within the layout
/// viewport, and scrolling the document moves both together.
/// </para>
/// </remarks>
internal sealed class JsVisualViewport : JsEventTarget
{
    private readonly PageRuntime _runtime;
    private Viewport _last;

    internal JsVisualViewport(PageRuntime runtime, ObjectInstance prototype)
        : base(runtime.Engine, runtime.Engine._mainRealm)
    {
        _runtime = runtime;
        _prototype = prototype;
        _last = runtime.Viewport;
    }

    /// <summary>The page this viewport belongs to.</summary>
    internal PageRuntime Runtime => _runtime;

    /// <summary>Fires <c>resize</c> if the viewport change moved its width or height.</summary>
    internal void ViewportChanged()
    {
        var viewport = _runtime.Viewport;
        if (viewport.Width == _last.Width && viewport.Height == _last.Height)
        {
            _last = viewport;
            return;
        }

        _last = viewport;
        PageEvents.Fire(_runtime, this, "resize");
    }

    /// <inheritdoc />
    public override string ToString() => "[object VisualViewport]";
}
