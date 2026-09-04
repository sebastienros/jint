#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Messaging;

/// <summary>
/// <c>MessagePort.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#messageport
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so a port has <c>addEventListener</c> and the
/// rest, and <c>port instanceof EventTarget</c> holds. The <c>onclose</c> event handler and the <c>close</c>
/// event that goes with it are a recent addition to the standard and are deliberately absent rather than
/// present-and-never-firing.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class MessagePortPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly MessagePortConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString MessagePortToStringTag = new("MessagePort");

    internal MessagePortPrototype(
        Engine engine,
        Realm realm,
        MessagePortConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-postmessage
    /// </summary>
    /// <remarks>
    /// Two overloads share one name — <c>postMessage(message, sequence&lt;object&gt; transfer)</c> and
    /// <c>postMessage(message, optional StructuredSerializeOptions options)</c> — and WebIDL's overload
    /// resolution picks between them by asking whether the second argument is iterable
    /// (https://webidl.spec.whatwg.org/#es-overloads step 12.3): an object with a callable
    /// <c>@@iterator</c> is the sequence, anything else is the dictionary. That is what makes both
    /// <c>postMessage(x, [buf])</c> and <c>postMessage(x, { transfer: [buf] })</c> work.
    /// </remarks>
    [JsFunction(Name = "postMessage", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue PostMessage(JsValue thisObject, JsCallArguments arguments)
    {
        var port = Brand(thisObject);

        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to execute 'postMessage' on 'MessagePort': 1 argument required, but only 0 present.");
        }

        var transferList = ReadTransferArgument(arguments.At(1));
        port.PostMessage(arguments[0], transferList);
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-start
    /// </summary>
    [JsFunction(Name = "start", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Start(JsValue thisObject)
    {
        Brand(thisObject).Start();
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-close
    /// </summary>
    [JsFunction(Name = "close", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Close(JsValue thisObject)
    {
        Brand(thisObject).Close();
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-messageport-onmessage — the
    /// <c>onmessage</c> event handler IDL attribute,
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    /// <remarks>
    /// The handler is one entry of the port's own event listener list, so it takes its turn in registration
    /// order among the <c>addEventListener('message', …)</c> listeners rather than running before or after all
    /// of them, exactly as <c>AbortSignal</c>'s <c>onabort</c> does.
    /// </remarks>
    [JsAccessor("onmessage", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsMessagePort.MessageEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-messageport-onmessage, setter half.
    /// </summary>
    /// <remarks>
    /// <b>Assigning this starts the port.</b> "The first time a <c>MessagePort</c> object's <c>onmessage</c>
    /// IDL attribute is set, the port's port message queue must be enabled, as if the <c>start()</c> method
    /// had been called" — which is why <c>port.onmessage = f</c> delivers and
    /// <c>port.addEventListener('message', f)</c> on its own does not. Jint enables on <i>any</i> assignment,
    /// including one that clears the handler, which is what the specification's "is set" says and what
    /// browsers do.
    /// </remarks>
    /// <remarks>
    /// <b>This is the one handler attribute in the engine with a step after the set</b>, and it is written
    /// here rather than as a hook on <see cref="EventHandlerAttributes"/>: the shared half is the same three
    /// rules every other attribute obeys, and the port-specific half is one line a reader can see. The order
    /// matters - the handler is in place before the queue is enabled, so a message the enabling delivers
    /// finds it.
    /// </remarks>
    [JsAccessor("onmessage", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageSet(JsValue thisObject, JsValue value)
    {
        var port = Brand(thisObject);
        EventHandlerAttributes.Set(port, JsMessagePort.MessageEventType, value);
        port.Start();
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-messageport-onmessageerror.
    /// </summary>
    /// <remarks>
    /// Assigning it does <i>not</i> start the port — only <c>onmessage</c> does — and nothing in Jint ever
    /// fires a <c>messageerror</c>; see <see cref="JsMessagePort"/> for why.
    /// </remarks>
    [JsAccessor("onmessageerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageErrorGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsMessagePort.MessageErrorEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-messageport-onmessageerror, setter
    /// half.
    /// </summary>
    [JsAccessor("onmessageerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageErrorSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsMessagePort.MessageErrorEventType, value);

    /// <summary>
    /// Resolves <c>postMessage</c>'s second argument to a transfer list. See the operation's remarks for the
    /// overload rule.
    /// </summary>
    private List<JsValue>? ReadTransferArgument(JsValue argument)
    {
        const string Operation = "postMessage' on 'MessagePort";

        if (argument is ObjectInstance && GetMethod(_realm, argument, GlobalSymbolRegistry.Iterator) is not null)
        {
            return StructuredSerializeOptions.ReadTransferSequence(_realm, argument, Operation);
        }

        return StructuredSerializeOptions.ReadTransferOption(_realm, argument, Operation);
    }

    private JsMessagePort Brand(JsValue thisObject)
    {
        if (thisObject is JsMessagePort port)
        {
            return port;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a MessagePort");
        return null!;
    }
}
#endif
