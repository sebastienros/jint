#if NET8_0_OR_GREATER
using SystemEncoding = System.Text.Encoding;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Files;
using Jint.WebApi.Url;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// <c>WebSocket.prototype</c> — the interface prototype object.
/// <para>
/// https://websockets.spec.whatwg.org/#the-websocket-interface
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so a socket has <c>addEventListener</c> and the
/// rest, and <c>socket instanceof EventTarget</c> holds. The attributes are accessors that brand-check their
/// receiver, as WebIDL specifies, so <c>WebSocket.prototype.readyState</c> is a <c>TypeError</c> rather than
/// an answer. https://webidl.spec.whatwg.org/#es-constants defines the four ready-state constants one after
/// another in the order the IDL declares them, and that order is observable, so they are declared below in it
/// and <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class WebSocketPrototype : Prototype
{
    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 2: "If reasonBytes is longer than 123
    /// bytes, then throw a SyntaxError DOMException" — the protocol's 125-byte control frame payload, less
    /// the two the status code takes.
    /// </summary>
    private const int MaxReasonBytes = 123;

    private static readonly JsString _arrayBuffer = new(JsWebSocket.BinaryTypeArrayBuffer);
    private static readonly JsString _blob = new(JsWebSocket.BinaryTypeBlob);

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WebSocketConstructor _constructor;

    [JsProperty(Name = "CONNECTING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Connecting = JsNumber.Create(JsWebSocket.Connecting);
    [JsProperty(Name = "OPEN", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Open = JsNumber.Create(JsWebSocket.Open);
    [JsProperty(Name = "CLOSING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Closing = JsNumber.Create(JsWebSocket.Closing);
    [JsProperty(Name = "CLOSED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Closed = JsNumber.Create(JsWebSocket.Closed);

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString WebSocketToStringTag = new("WebSocket");

    internal WebSocketPrototype(
        Engine engine,
        Realm realm,
        WebSocketConstructor constructor,
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
    /// https://websockets.spec.whatwg.org/#dom-websocket-url
    /// </summary>
    [JsAccessor("url", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString UrlGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-readystate
    /// </summary>
    [JsAccessor("readyState", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ReadyStateGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).ReadyState);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-bufferedamount — the bytes <c>send()</c> has queued
    /// that have not reached the network. It only ever grows once the connection is closed, because a
    /// <c>send()</c> then queues bytes nothing will ever write; the standard says so explicitly.
    /// </summary>
    [JsAccessor("bufferedAmount", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber BufferedAmountGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).BufferedAmount);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-extensions — always the empty string, because the
    /// in-box transport negotiates no extensions at all; see <see cref="ClientWebSocketConnection"/>.
    /// </summary>
    [JsAccessor("extensions", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ExtensionsGet(JsValue thisObject)
    {
        Brand(thisObject);
        return JsString.Empty;
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-protocol
    /// </summary>
    [JsAccessor("protocol", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ProtocolGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Protocol);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-binarytype
    /// </summary>
    /// <remarks>
    /// Jint's default is <c>"arraybuffer"</c> where the standard's is <c>"blob"</c> — the one deliberate
    /// divergence in this interface, explained on <see cref="JsWebSocket"/>.
    /// </remarks>
    [JsAccessor("binaryType", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString BinaryTypeGet(JsValue thisObject)
        => string.Equals(Brand(thisObject).BinaryType, JsWebSocket.BinaryTypeBlob, StringComparison.Ordinal) ? _blob : _arrayBuffer;

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-binarytype, setter half. Assigning something that is
    /// not one of the two enumeration values is <i>ignored</i> rather than refused, which is what
    /// https://webidl.spec.whatwg.org/#es-enumeration says an attribute of enumeration type does.
    /// </summary>
    [JsAccessor("binaryType", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue BinaryTypeSet(JsValue thisObject, JsValue value)
    {
        var socket = Brand(thisObject);
        var requested = TypeConverter.ToString(value);

        if (requested is JsWebSocket.BinaryTypeArrayBuffer or JsWebSocket.BinaryTypeBlob)
        {
            socket.BinaryType = requested;
        }

        return Undefined;
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-send
    /// </summary>
    /// <remarks>
    /// <para>
    /// The union arms are tried in the order the IDL declares them — <c>BufferSource</c>, then <c>Blob</c>,
    /// then <c>USVString</c> — which is what makes a typed array binary rather than the string
    /// <c>"[object Uint8Array]"</c>. A <c>SharedArrayBuffer</c> is not a <c>BufferSource</c> and is therefore
    /// stringified, exactly as in a browser.
    /// </para>
    /// <para>
    /// <c>bufferedAmount</c> grows for every call that does not throw, whether or not the bytes are ever
    /// written; only a write that reaches the network brings it down again.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "send", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Send(JsValue thisObject, JsValue data)
    {
        var socket = Brand(thisObject);

        // Step 1: sending before the handshake finished is the script's own bug, and the only exception this
        // method has.
        if (socket.ReadyState == JsWebSocket.Connecting)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to execute 'send' on 'WebSocket': Still in CONNECTING state.");
        }

        var payload = Marshal(data, out var isText);
        socket.AddBufferedAmount(payload.Length);

        // "If the WebSocket connection is established and the WebSocket closing handshake has not yet
        // started" — a socket that is CLOSING or CLOSED counts the bytes and writes nothing.
        if (socket.ReadyState != JsWebSocket.Open || socket.Operation is not { } operation)
        {
            return Undefined;
        }

        if (!operation.Enqueue(payload, isText))
        {
            // The closing handshake started underneath us — the peer sent a Close frame the engine has not
            // dispatched yet. The bytes stay counted, which is what they are: queued and never transmitted.
            return Undefined;
        }

        // "If the data cannot be sent, e.g. because it would need to be buffered but the buffer is full, the
        // user agent must flag the WebSocket as full and then close the WebSocket connection." That ceiling
        // is the host's, and reaching it drops the connection with the error and close events every abnormal
        // closure fires — a script that outruns its socket is told so rather than growing the heap.
        if (socket.BufferedAmount > BufferedAmountLimit())
        {
            operation.Fail();
        }

        return Undefined;
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close
    /// </summary>
    /// <remarks>
    /// Both arguments are converted before either is validated, which is the order WebIDL puts them in and is
    /// observable through a <c>reason</c> whose <c>toString</c> has a side effect.
    /// </remarks>
    [JsFunction(Name = "close", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Close(JsValue thisObject, JsCallArguments arguments)
    {
        var socket = Brand(thisObject);

        var codeArgument = arguments.At(0);
        var reasonArgument = arguments.At(1);

        // Both arguments are optional with no default, so an explicit undefined is "not present".
        var hasCode = !codeArgument.IsUndefined();
        var code = hasCode ? WebSocketValues.ToClampedUnsignedShort(codeArgument) : 0;

        var hasReason = !reasonArgument.IsUndefined();
        var reason = hasReason ? UrlValues.ToUsvString(reasonArgument) : string.Empty;

        // Step 1: 1000 and the private range are the only codes an application may send; everything else is
        // reserved to the protocol.
        if (hasCode && code != WebSocketOperation.NormalClosure && code is < 3000 or > 4999)
        {
            ThrowDomException(
                DomExceptionNames.InvalidAccess,
                $"Failed to execute 'close' on 'WebSocket': The close code must be either 1000, or between 3000 and 4999. {code} is neither.");
        }

        // Step 2.
        if (hasReason && SystemEncoding.UTF8.GetByteCount(reason) > MaxReasonBytes)
        {
            ThrowDomException(
                DomExceptionNames.Syntax,
                $"Failed to execute 'close' on 'WebSocket': The close reason must not be greater than {MaxReasonBytes} UTF-8 bytes.");
        }

        // Step 3.1: already closing or closed, so there is nothing to do.
        if (socket.ReadyState is JsWebSocket.Closing or JsWebSocket.Closed)
        {
            return Undefined;
        }

        var operation = socket.Operation;

        if (socket.ReadyState == JsWebSocket.Connecting)
        {
            // Step 3.2: "Fail the WebSocket connection and set this's ready state to CLOSING." Failing rather
            // than handshaking is why closing during the handshake ends in error and close(1006) rather than
            // in the code the script passed — there was never a connection to close politely.
            socket.EnterClosing();
            operation?.Fail();
            return Undefined;
        }

        // Step 3.3: "Start the WebSocket closing handshake and set this's ready state to CLOSING."
        socket.EnterClosing();

        // "If neither code nor reason is present, the WebSocket Close message must not have a body." A reason
        // with no code has nowhere to live — the protocol puts the status code in front of it — so it travels
        // behind a normal closure, which is what the sending endpoint means by it.
        var sent = hasCode ? code : hasReason ? WebSocketOperation.NormalClosure : (int?) null;
        operation?.RequestClose(sent, reason);

        return Undefined;
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onopen
    /// </summary>
    [JsAccessor("onopen", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnOpenGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), JsWebSocket.OpenEventType);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onopen, setter half.
    /// </summary>
    [JsAccessor("onopen", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnOpenSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), JsWebSocket.OpenEventType, value);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onmessage
    /// </summary>
    [JsAccessor("onmessage", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), JsWebSocket.MessageEventType);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onmessage, setter half.
    /// </summary>
    [JsAccessor("onmessage", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), JsWebSocket.MessageEventType, value);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onerror
    /// </summary>
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), JsWebSocket.ErrorEventType);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onerror, setter half.
    /// </summary>
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), JsWebSocket.ErrorEventType, value);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onclose
    /// </summary>
    [JsAccessor("onclose", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnCloseGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), JsWebSocket.CloseEventType);

    /// <summary>
    /// https://websockets.spec.whatwg.org/#handler-websocket-onclose, setter half.
    /// </summary>
    [JsAccessor("onclose", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnCloseSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), JsWebSocket.CloseEventType, value);

    /// <summary>
    /// The bytes to put on the wire, and whether they are a text frame — the
    /// <c>(BufferSource or Blob or USVString)</c> union, converted on the engine's thread so that nothing the
    /// script does afterwards can change what is sent.
    /// </summary>
    private static ReadOnlyMemory<byte> Marshal(JsValue data, out bool isText)
    {
        isText = false;

        if (FileApi.TryGetBufferSourceBytes(data, out var buffered))
        {
            // A copy: the buffer stays script-writable, and a frame that changed under the socket after it
            // was queued would be a message-smuggling primitive rather than a curiosity.
            return buffered.ToArray();
        }

        if (data is JsBlob blob)
        {
            // A blob is immutable by specification, so its bytes can go on the wire as they are.
            return blob.Data;
        }

        isText = true;
        return SystemEncoding.UTF8.GetBytes(UrlValues.ToUsvString(data));
    }

    /// <summary>
    /// The ceiling on unwritten bytes, which is the same number that bounds one incoming message —
    /// <c>Options.WebApi.Fetch.MaxResponseBytes</c>. Read afresh on the engine's thread, from the settings the
    /// engine was built with.
    /// </summary>
    private long BufferedAmountLimit() => _engine._webApi?.FetchOptions?.MaxResponseBytes ?? long.MaxValue;

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    private JsWebSocket Brand(JsValue thisObject)
    {
        if (thisObject is JsWebSocket socket)
        {
            return socket;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a WebSocket");
        return null!;
    }
}
#endif
