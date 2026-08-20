#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Messaging;

/// <summary>
/// <c>MessageChannel.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#messagechannel
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class MessageChannelPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly MessageChannelConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString MessageChannelToStringTag = new("MessageChannel");

    internal MessageChannelPrototype(
        Engine engine,
        Realm realm,
        MessageChannelConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messagechannel-port1
    /// </summary>
    [JsAccessor("port1", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsMessagePort Port1Get(JsValue thisObject) => Brand(thisObject).Port1;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messagechannel-port2
    /// </summary>
    [JsAccessor("port2", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsMessagePort Port2Get(JsValue thisObject) => Brand(thisObject).Port2;

    private JsMessageChannel Brand(JsValue thisObject)
    {
        if (thisObject is JsMessageChannel channel)
        {
            return channel;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a MessageChannel");
        return null!;
    }
}
#endif
