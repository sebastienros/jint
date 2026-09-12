using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// A <c>DeviceMotionEventAcceleration</c> instance — the three axes of one acceleration reading.
/// <para>
/// https://w3c.github.io/deviceorientation/#devicemotioneventacceleration
/// </para>
/// </summary>
/// <remarks>
/// The interface has no constructor: WebIDL gives it none, and the only thing that ever builds one is
/// <c>DeviceMotionEvent</c>'s own constructor, from the <c>DeviceMotionEventAccelerationInit</c> dictionary
/// member. Every axis is <c>double?</c>, and <see langword="null"/> is what an axis the hardware cannot
/// measure answers — which here is every axis the page did not itself supply.
/// </remarks>
internal sealed class JsDeviceMotionAcceleration : ObjectInstance
{
    internal JsDeviceMotionAcceleration(Engine engine, ObjectInstance prototype, double? x, double? y, double? z)
        : base(engine, ObjectClass.Object)
    {
        X = x;
        Y = y;
        Z = z;
        Prototype = prototype;
    }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotioneventacceleration-x.</summary>
    internal double? X { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotioneventacceleration-y.</summary>
    internal double? Y { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotioneventacceleration-z.</summary>
    internal double? Z { get; }

    public override string ToString() => "[object DeviceMotionEventAcceleration]";
}

/// <summary>
/// A <c>DeviceMotionEventRotationRate</c> instance — the three axes of one rotation-rate reading.
/// <para>
/// https://w3c.github.io/deviceorientation/#devicemotioneventrotationrate
/// </para>
/// </summary>
/// <remarks>Constructed only by <c>DeviceMotionEvent</c>'s constructor; see <see cref="JsDeviceMotionAcceleration"/>.</remarks>
internal sealed class JsDeviceMotionRotationRate : ObjectInstance
{
    internal JsDeviceMotionRotationRate(Engine engine, ObjectInstance prototype, double? alpha, double? beta, double? gamma)
        : base(engine, ObjectClass.Object)
    {
        Alpha = alpha;
        Beta = beta;
        Gamma = gamma;
        Prototype = prototype;
    }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotioneventrotationrate-alpha.</summary>
    internal double? Alpha { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotioneventrotationrate-beta.</summary>
    internal double? Beta { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotioneventrotationrate-gamma.</summary>
    internal double? Gamma { get; }

    public override string ToString() => "[object DeviceMotionEventRotationRate]";
}

/// <summary>
/// A <c>DeviceMotionEvent</c> instance.
/// <para>
/// https://w3c.github.io/deviceorientation/#devicemotionevent
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no sensor here, so every value is the dictionary's.</b> The specification's construction steps
/// are implemented in full — including that <c>acceleration</c>, <c>accelerationIncludingGravity</c> and
/// <c>rotationRate</c> are dictionary members with <i>no default</i>, so an absent one leaves the attribute
/// <see langword="null"/> while a present one (<c>null</c> included, which WebIDL converts to a dictionary of
/// defaults) produces a real object whose axes are all <see langword="null"/>.
/// </para>
/// <para>
/// <b><c>requestPermission()</c> is deliberately absent.</b> It is a static operation that resolves with a
/// permission state for a sensor; there is neither a sensor nor a permission model here, and a member that
/// could only ever answer "granted" to a page that will then never receive an event is a lie the page cannot
/// detect. Its absence is what a page's own feature detection is written for, and it is what the two engines
/// that never shipped the member answer too.
/// </para>
/// </remarks>
internal sealed class JsDeviceMotionEvent : JsEvent
{
    internal JsDeviceMotionEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue acceleration,
        JsValue accelerationIncludingGravity,
        JsValue rotationRate,
        double interval)
        : base(engine, type, init, timeStamp)
    {
        Acceleration = acceleration;
        AccelerationIncludingGravity = accelerationIncludingGravity;
        RotationRate = rotationRate;
        Interval = interval;
    }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotionevent-acceleration.</summary>
    internal JsValue Acceleration { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotionevent-accelerationincludinggravity.</summary>
    internal JsValue AccelerationIncludingGravity { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotionevent-rotationrate.</summary>
    internal JsValue RotationRate { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-devicemotionevent-interval.</summary>
    internal double Interval { get; }
}

/// <summary>
/// A <c>DeviceOrientationEvent</c> instance.
/// <para>
/// https://w3c.github.io/deviceorientation/#deviceorientationevent
/// </para>
/// </summary>
/// <remarks>
/// The three angles are <c>double?</c> and default to <see langword="null"/>, which is the value an
/// implementation that cannot provide the reading is required to give — so a constructed event that says
/// nothing reads back as a browser on a device with no orientation sensor does.
/// <c>requestPermission()</c> is absent for the reason <see cref="JsDeviceMotionEvent"/> gives.
/// </remarks>
internal sealed class JsDeviceOrientationEvent : JsEvent
{
    internal JsDeviceOrientationEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        double? alpha,
        double? beta,
        double? gamma,
        bool absolute)
        : base(engine, type, init, timeStamp)
    {
        Alpha = alpha;
        Beta = beta;
        Gamma = gamma;
        Absolute = absolute;
    }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-deviceorientationevent-alpha.</summary>
    internal double? Alpha { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-deviceorientationevent-beta.</summary>
    internal double? Beta { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-deviceorientationevent-gamma.</summary>
    internal double? Gamma { get; }

    /// <summary>https://w3c.github.io/deviceorientation/#dom-deviceorientationevent-absolute.</summary>
    internal bool Absolute { get; }
}
