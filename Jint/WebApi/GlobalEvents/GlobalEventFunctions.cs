#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Events;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// The three event-target operations on the global scope: <c>addEventListener</c>,
/// <c>removeEventListener</c> and <c>dispatchEvent</c>.
/// <para>
/// https://dom.spec.whatwg.org/#interface-eventtarget
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A browser's <c>Window</c> <i>implements</i> <c>EventTarget</c> and inherits these three from
/// <c>EventTarget.prototype</c>. Jint's global object deliberately does not (see
/// <see cref="GlobalEventTarget"/>), so they are installed as ordinary global operations bound to the
/// engine's synthetic target — this class only owns them so that one realm builds one of each, exactly as
/// <c>TimerFunctions</c> does for the timer globals.
/// </para>
/// <para>
/// The one consequence a script could notice: <b>they ignore their <c>this</c></b>. A browser brand-checks the
/// receiver, so <c>const f = addEventListener; f('error', h)</c> raises "Illegal invocation" there and works
/// here. The three shapes that matter — <c>addEventListener(…)</c>, <c>self.addEventListener(…)</c> and
/// <c>globalThis.addEventListener(…)</c> — behave the same either way, and the argument conversions are
/// literally <c>EventTarget</c>'s, down to the message a <c>TypeError</c> carries.
/// </para>
/// </remarks>
internal sealed class GlobalEventFunctions
{
    private readonly Engine _engine;
    private readonly Realm _realm;

    private ClrFunction? _addEventListener;
    private ClrFunction? _removeEventListener;
    private ClrFunction? _dispatchEvent;

    private GlobalEventFunctions(Engine engine, Realm realm)
    {
        _engine = engine;
        _realm = realm;
    }

    internal static GlobalEventFunctions Create(Engine engine, Realm realm)
    {
        if (engine._webApi is null)
        {
            // Unreachable: the globals that reach this property are installed only where the web-API state
            // was created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The global event operations were reached on an engine that has no web-API state.");
        }

        return new GlobalEventFunctions(engine, realm);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-addeventlistener
    /// </summary>
    internal ClrFunction AddEventListener =>
        _addEventListener ??= Operation("addEventListener", 2, (_, arguments) =>
        {
            EventTargetArguments.AddListener(_realm, Target, arguments);
            return JsValue.Undefined;
        });

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-removeeventlistener
    /// </summary>
    internal ClrFunction RemoveEventListener =>
        _removeEventListener ??= Operation("removeEventListener", 2, (_, arguments) =>
        {
            EventTargetArguments.RemoveListener(_realm, Target, arguments);
            return JsValue.Undefined;
        });

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-dispatchevent
    /// </summary>
    internal ClrFunction DispatchEvent =>
        _dispatchEvent ??= Operation("dispatchEvent", 1, (_, arguments) =>
            JsBoolean.Create(EventTargetArguments.DispatchEvent(_engine, _realm, Target, arguments.At(0))));

    /// <summary>
    /// The engine's synthetic global target, created on the first of these calls — so an engine that enabled
    /// the feature and never registered a listener has still allocated nothing.
    /// </summary>
    private GlobalEventTarget Target => _engine._webApi!.GlobalEventTarget;

    /// <summary>
    /// A WebIDL operation: <c>length</c> counts the required arguments only and is configurable but neither
    /// writable nor enumerable — https://webidl.spec.whatwg.org/#dfn-create-operation-function.
    /// </summary>
    private ClrFunction Operation(string name, int length, JsCallDelegate body)
        => new(_engine, _realm, name, body, length, PropertyFlag.Configurable);
}
#endif
