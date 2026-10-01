using Jint.Browser.Runtime;
using Jint.Native.Object;
using Jint.WebApi.Events;

namespace Jint.Browser.SystemState;

/// <summary>
/// <c>screen.orientation</c>: which way up the emulated screen is, and the event a page hears when it turns.
/// </summary>
/// <remarks>
/// <para>
/// https://w3c.github.io/screen-orientation/#screenorientation-interface — the type follows the viewport, so
/// <c>Emulation.setDeviceMetricsOverride</c> with a portrait size turns the screen, and the angle is zero for
/// either primary orientation, which is what Chrome reports for an emulated device in its natural position.
/// </para>
/// <para>
/// <b><c>change</c> fires when the viewport's aspect flips</b>, from the same notification that reaches a
/// <c>MediaQueryList</c>, so an <c>(orientation: portrait)</c> listener and this one hear one change together.
/// </para>
/// </remarks>
internal sealed class JsScreenOrientation : JsEventTarget
{
    private readonly PageRuntime _runtime;
    private string _lastType;

    internal JsScreenOrientation(PageRuntime runtime, ObjectInstance prototype)
        : base(runtime.Engine, runtime.Engine._mainRealm)
    {
        _runtime = runtime;
        _prototype = prototype;
        _lastType = OrientationType;
    }

    /// <summary>https://w3c.github.io/screen-orientation/#dom-screenorientation-type</summary>
    internal string OrientationType
        => _runtime.Viewport.Width >= _runtime.Viewport.Height ? "landscape-primary" : "portrait-primary";

    /// <summary>Fires <c>change</c> if the viewport change turned the screen.</summary>
    internal void ViewportChanged()
    {
        var type = OrientationType;
        if (string.Equals(type, _lastType, StringComparison.Ordinal))
        {
            return;
        }

        _lastType = type;
        PageEvents.Fire(_runtime, this, "change");
    }

    /// <inheritdoc />
    public override string ToString() => "[object ScreenOrientation]";
}
