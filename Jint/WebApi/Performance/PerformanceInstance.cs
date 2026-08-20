#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>performance</c> object — an instance of the <c>Performance</c> interface.
/// <para>
/// https://w3c.github.io/hr-time/#sec-performance
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Both members are answered from <c>Options.WebApi.Timers.TimeProvider</c>, the very clock the timers are
/// scheduled against, so a host that installs a fake one drives <c>setTimeout</c> and <c>performance.now()</c>
/// coherently instead of watching one of them stand still while the other runs. <see cref="TimeOriginGet"/>
/// and <see cref="Now"/> are the two halves of one reading: the origin is the wall-clock moment the engine's
/// web-API state was created, and <c>now()</c> is the monotonic duration since that same moment, so
/// <c>performance.timeOrigin + performance.now()</c> is the current time in Unix milliseconds.
/// </para>
/// <para>
/// <b>The time origin is per engine, not per evaluation cycle.</b> A pooled engine that a host recycles with
/// <c>Engine.Advanced.RestoreGlobalSnapshot</c> keeps the origin it was built with, so <c>now()</c> goes on
/// growing across cycles and a script cannot use it to tell how long <i>its own</i> cycle has been running.
/// That is deliberate: the origin is what makes the readings monotonic, and rewinding it at a restore would
/// hand the next cycle a clock that had gone backwards — which is the one thing
/// https://w3c.github.io/hr-time/#dom-performance-now forbids outright.
/// </para>
/// <para>
/// Not implemented, and absent rather than throwing so that feature detection sees the truth: the
/// <c>Performance Timeline</c> surface (<c>mark</c>, <c>measure</c>, <c>getEntries*</c>, <c>clearMarks</c>),
/// <c>toJSON</c>, and the <c>EventTarget</c> this interface inherits from.
/// </para>
/// <para>
/// Two documented simplifications against WebIDL, the same pair <c>console</c> and <c>crypto</c> carry. There
/// is no <c>Performance</c> interface object and no <c>Performance.prototype</c>, so <c>now</c> and
/// <c>timeOrigin</c> are own properties of this object with the attributes an ECMAScript built-in has, rather
/// than those of a WebIDL interface prototype's members; both still brand-check their receiver, and
/// <c>Object.keys(performance)</c> answers the empty array here exactly as it does in a browser. And the
/// object is installed as an ordinary enumerable data property of the global rather than through the
/// <c>[Replaceable]</c> accessor pair WebIDL gives it.
/// </para>
/// <para>
/// One deliberate divergence: the readings are <b>not coarsened</b>. https://w3c.github.io/hr-time/#dfn-coarsen-time
/// asks a browser to round to at best 100 microseconds because a page shares a process with cross-origin
/// data that a fine clock helps steal. An embedded engine has no cross-origin anything, and a host that wants
/// a coarse clock supplies a coarse <see cref="TimeProvider"/>; the resolution here is simply whatever that
/// provider gives, which for <see cref="TimeProvider.System"/> is the <c>Stopwatch</c> tick.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class PerformanceInstance : BuiltinShapeObject
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceToStringTag = new("Performance");

    private readonly Realm _realm;
    private readonly WebApiEngineState _state;

    private PerformanceInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype, WebApiEngineState state)
        : base(engine)
    {
        _realm = realm;
        _state = state;
        _prototype = objectPrototype;
    }

    internal static PerformanceInstance Create(Engine engine, Realm realm, ObjectPrototype objectPrototype)
    {
        var state = engine._webApi;
        if (state is null)
        {
            // Unreachable: the global that reaches this property is installed only where the state was
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The performance object was reached on an engine that has no web-API state.");
        }

        return new PerformanceInstance(engine, realm, objectPrototype, state);
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/hr-time/#dom-performance-now — "the number of milliseconds in the current high
    /// resolution time", which is the duration from this engine's time origin to now, read from the monotonic
    /// clock.
    /// </summary>
    [JsFunction(Name = "now", Length = 0)]
    private JsNumber Now(JsValue thisObject)
    {
        Brand(thisObject, "Failed to execute 'now' on 'Performance'");
        return JsNumber.Create(_state.CurrentHighResolutionTime);
    }

    /// <summary>
    /// https://w3c.github.io/hr-time/#dom-performance-timeorigin — the duration from the Unix epoch to this
    /// engine's time origin, in milliseconds.
    /// </summary>
    [JsAccessor("timeOrigin")]
    private JsNumber TimeOriginGet(JsValue thisObject)
    {
        Brand(thisObject, "Failed to read the 'timeOrigin' property from 'Performance'");
        return JsNumber.Create(_state.TimeOrigin);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private void Brand(JsValue thisObject, string what)
    {
        if (thisObject is not PerformanceInstance)
        {
            Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a Performance object.");
        }
    }
}
#endif
