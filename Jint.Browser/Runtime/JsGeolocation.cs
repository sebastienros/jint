using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Runtime;

/// <summary>
/// <c>navigator.geolocation</c>: where a client said the page is, or an honest refusal when nobody has said.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is exactly one position and it never moves.</b>
/// <c>Emulation.setGeolocationOverride</c> is the only source, so <c>watchPosition</c> delivers once and is
/// never called again — the same shape of honest simplification <c>IntersectionObserver</c> and
/// <c>ResizeObserver</c> make, and for the same reason: nothing here can change the answer, and a watch that
/// delivered nothing at all would stop a page that waits for its first fix before rendering.
/// </para>
/// <para>
/// <b>With no override the error callback is called with <c>POSITION_UNAVAILABLE</c></b>, which is what a
/// browser answers when it has no fix — rather than <c>PERMISSION_DENIED</c>, which would tell a page to stop
/// asking, or a position at latitude zero, which would be a lie a map draws.
/// </para>
/// <para>
/// <b>The position and the error are plain objects shaped like their interfaces</b>, not instances of
/// <c>GeolocationPosition</c>, <c>GeolocationCoordinates</c> and <c>GeolocationPositionError</c>. That is
/// <c>Layout/DomRects</c>' trade exactly: every member a page reads is there, the three interface objects
/// are not, so <c>position instanceof GeolocationPosition</c> is <see langword="false"/>. The error carries
/// its own three constants, because <c>err.code === err.PERMISSION_DENIED</c> is how the check is written.
/// </para>
/// <para>
/// Both callbacks run as a task on the engine's own queue, never inline, because
/// <a href="https://w3c.github.io/geolocation/#getcurrentposition-method">Geolocation</a> queues them — a
/// page that calls <c>getCurrentPosition</c> and then assigns state on the next line must not have the
/// callback run first.
/// </para>
/// </remarks>
internal sealed class JsGeolocation : ObjectInstance
{
    /// <summary>https://w3c.github.io/geolocation/#dom-geolocationpositionerror-position_unavailable</summary>
    private const int PositionUnavailable = 2;

    private static readonly JsObjectLayout _position = new JsObjectLayout.Builder()
        .Add("coords")
        .Add("timestamp")
        .Build();

    private static readonly JsObjectLayout _coordinates = new JsObjectLayout.Builder()
        .Add("latitude")
        .Add("longitude")
        .Add("accuracy")
        .Add("altitude")
        .Add("altitudeAccuracy")
        .Add("heading")
        .Add("speed")
        .Build();

    private static readonly JsObjectLayout _error = new JsObjectLayout.Builder()
        .Add("code")
        .Add("message")
        .Add("PERMISSION_DENIED")
        .Add("POSITION_UNAVAILABLE")
        .Add("TIMEOUT")
        .Build();

    private readonly PageRuntime _runtime;
    private HashSet<int>? _cleared;
    private int _nextWatch;

    internal JsGeolocation(PageRuntime runtime, ObjectInstance prototype)
        : base(runtime.Engine, ObjectClass.Object)
    {
        _runtime = runtime;
        _prototype = prototype;
    }

    /// <summary>https://w3c.github.io/geolocation/#getcurrentposition-method</summary>
    internal JsValue GetCurrentPosition(JsValue[] arguments)
    {
        Deliver(arguments, watch: 0);
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/geolocation/#watchposition-method</summary>
    internal JsValue WatchPosition(JsValue[] arguments)
    {
        var watch = ++_nextWatch;
        Deliver(arguments, watch);
        return JsNumber.Create(watch);
    }

    /// <summary>https://w3c.github.io/geolocation/#clearwatch-method</summary>
    internal JsValue ClearWatch(JsValue[] arguments)
    {
        (_cleared ??= []).Add(TypeConverter.ToInt32(arguments.At(0)));
        return JsValue.Undefined;
    }

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsGeolocation Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsGeolocation geolocation)
        {
            return geolocation;
        }

        var message = "Failed to execute '" + member + "' on 'Geolocation': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <inheritdoc />
    public override string ToString() => "[object Geolocation]";

    private void Deliver(JsValue[] arguments, int watch)
    {
        // https://w3c.github.io/geolocation/#getcurrentposition-method step 1: the success callback
        // is required, and passing something that is not callable is a TypeError before anything is queued.
        if (arguments.At(0) is not ICallable success)
        {
            Throw.TypeError(
                _runtime.Engine.Realm,
                "Failed to execute 'getCurrentPosition' on 'Geolocation': parameter 1 is not of type 'Function'.");
            return;
        }

        var failure = arguments.At(1) as ICallable;
        var engine = _runtime.Engine;

        engine.AddToEventLoop(() =>
        {
            if (watch != 0 && _cleared is { } cleared && cleared.Contains(watch))
            {
                return;
            }

            if (_runtime.Emulation.Geolocation is { } fix)
            {
                engine.Call((JsValue) success, JsValue.Undefined, [Position(engine, fix, _runtime.Now)]);
                return;
            }

            if (failure is not null)
            {
                engine.Call(
                    (JsValue) failure,
                    JsValue.Undefined,
                    [Error(engine, PositionUnavailable, "No geolocation override is set for this page.")]);
            }
        }, EventLoopJobKind.Task);
    }

    private static JsObject Position(Engine engine, in GeolocationOverride fix, double timestamp) => JsObject.Create(
        engine,
        _position,
        [
            JsObject.Create(
                engine,
                _coordinates,
                [
                    JsNumber.Create(fix.Latitude),
                    JsNumber.Create(fix.Longitude),
                    JsNumber.Create(fix.Accuracy),
                    Optional(fix.Altitude),
                    Optional(fix.AltitudeAccuracy),
                    Optional(fix.Heading),
                    Optional(fix.Speed),
                ]),
            JsNumber.Create(timestamp),
        ]);

    private static JsObject Error(Engine engine, int code, string message) => JsObject.Create(
        engine,
        _error,
        [
            JsNumber.Create(code),
            JsString.Create(message),
            JsNumber.Create(1),
            JsNumber.Create(2),
            JsNumber.Create(3),
        ]);

    /// <summary>
    /// A coordinate the client did not supply is <see langword="null"/>, which is what the interface declares
    /// its nullable members as — never zero, which a page reads as sea level or as due north.
    /// </summary>
    private static JsValue Optional(double? value) => value is { } number ? JsNumber.Create(number) : JsValue.Null;
}
