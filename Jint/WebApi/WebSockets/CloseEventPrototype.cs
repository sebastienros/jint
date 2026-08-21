#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// <c>CloseEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://websockets.spec.whatwg.org/#the-closeevent-interface
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, so a <c>close</c> event has every <c>Event</c> member
/// and <c>event instanceof Event</c> holds inside the listener.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class CloseEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CloseEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CloseEventToStringTag = new("CloseEvent");

    internal CloseEventPrototype(
        Engine engine,
        Realm realm,
        CloseEventConstructor constructor,
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
    /// https://websockets.spec.whatwg.org/#dom-closeevent-wasclean
    /// </summary>
    [JsAccessor("wasClean", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean WasCleanGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).WasClean);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-closeevent-code
    /// </summary>
    [JsAccessor("code", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber CodeGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Code);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-closeevent-reason
    /// </summary>
    [JsAccessor("reason", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ReasonGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Reason);

    private JsCloseEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsCloseEvent close)
        {
            return close;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CloseEvent");
        return null!;
    }
}
#endif
