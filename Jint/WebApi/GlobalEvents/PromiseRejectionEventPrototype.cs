#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// <c>PromiseRejectionEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#the-promiserejectionevent-interface
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, so an <c>unhandledrejection</c> event has every
/// <c>Event</c> member and <c>event instanceof Event</c> holds inside the listener.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PromiseRejectionEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PromiseRejectionEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PromiseRejectionEventToStringTag = new("PromiseRejectionEvent");

    internal PromiseRejectionEventPrototype(
        Engine engine,
        Realm realm,
        PromiseRejectionEventConstructor constructor,
        ObjectInstance eventPrototype) : base(engine, realm)
    {
        _prototype = eventPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-promiserejectionevent-promise
    /// </summary>
    [JsAccessor("promise", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue PromiseGet(JsValue thisObject) => Brand(thisObject).Promise;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-promiserejectionevent-reason
    /// </summary>
    [JsAccessor("reason", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ReasonGet(JsValue thisObject) => Brand(thisObject).Reason;

    private JsPromiseRejectionEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsPromiseRejectionEvent rejectionEvent)
        {
            return rejectionEvent;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a PromiseRejectionEvent");
        return null!;
    }
}
#endif
