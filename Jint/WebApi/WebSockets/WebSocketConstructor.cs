#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// The <c>WebSocket</c> interface object.
/// <para>
/// https://websockets.spec.whatwg.org/#the-websocket-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>WebSocket</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the <c>EventTarget</c>
/// interface object — https://webidl.spec.whatwg.org/#interface-object.
/// </para>
/// <para>
/// The four ready-state constants appear here as well as on the prototype, per
/// https://webidl.spec.whatwg.org/#es-constants, with the attributes constants are given there:
/// <c>{ writable: false, enumerable: true, configurable: false }</c>. That section defines them one
/// after another in the order the IDL declares them, and that order is observable, so they are declared
/// below in it and <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </para>
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class WebSocketConstructor : Constructor
{
    private static readonly JsString _functionName = new("WebSocket");

    [JsProperty(Name = "CONNECTING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Connecting = JsNumber.Create(JsWebSocket.Connecting);
    [JsProperty(Name = "OPEN", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Open = JsNumber.Create(JsWebSocket.Open);
    [JsProperty(Name = "CLOSING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Closing = JsNumber.Create(JsWebSocket.Closing);
    [JsProperty(Name = "CLOSED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Closed = JsNumber.Create(JsWebSocket.Closed);

    internal WebSocketConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new WebSocketPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal WebSocketPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-websocket — the constructor steps, followed by
    /// "establish a WebSocket connection … in parallel".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The base URL is <c>Options.WebApi.Fetch.BaseUrl</c>.</b> The specification parses <c>url</c>
    /// against "the relevant settings object's API base URL", which is a document's URL, and an embedded
    /// engine's stand-in for that is the one setting <c>fetch</c> and <c>new Request()</c> already resolve
    /// against — a browsing position is one position, not one per interface. An engine whose host named none
    /// has no document at all, and a relative URL then does not parse and is the <c>SyntaxError</c> step 3
    /// asks for, exactly as before.
    /// </para>
    /// <para>
    /// Steps 4 and 5 — <c>http</c> becomes <c>ws</c> and <c>https</c> becomes <c>wss</c> — are the
    /// specification's current text and are implemented as written, so <c>new WebSocket('https://x/')</c> is
    /// a <c>wss</c> socket rather than the <c>SyntaxError</c> older text produced.
    /// </para>
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to construct 'WebSocket': 1 argument required, but only 0 present.");
        }

        var href = UrlValues.ToUsvString(arguments[0]);

        // Steps 2 and 3. The base is read off the engine's parsed network context rather than from Options,
        // so a relative URL costs no re-parse of the base and reaches the same record fetch resolves against.
        var url = UrlParser.Parse(href, _engine._webApi?.FetchNetwork.BaseUrl);
        if (url is null)
        {
            ThrowSyntaxError($"Failed to construct 'WebSocket': The URL '{href}' is invalid.");
        }

        // Steps 4 and 5: the two HTTP schemes name the same endpoints as the two WebSocket ones, and the
        // handshake is an HTTP request anyway, so the standard maps rather than refuses.
        if (string.Equals(url!.Scheme, "http", StringComparison.Ordinal))
        {
            url.Scheme = "ws";
        }
        else if (string.Equals(url.Scheme, "https", StringComparison.Ordinal))
        {
            url.Scheme = "wss";
        }

        // Step 6.
        if (url.Scheme is not ("ws" or "wss"))
        {
            ThrowSyntaxError($"Failed to construct 'WebSocket': The URL's scheme must be either 'http', 'https', 'ws', or 'wss'. '{url.Scheme}' is not allowed.");
        }

        // Step 7: a fragment is meaningless to a socket, and silently dropping one would make two different
        // strings name the same connection.
        if (url.Fragment is not null)
        {
            ThrowSyntaxError("Failed to construct 'WebSocket': The URL contains a fragment identifier. Fragment identifiers are not allowed in WebSocket URLs.");
        }

        // Steps 8 and 9.
        var protocols = ReadProtocols(arguments.At(1));
        ValidateProtocols(protocols);

        var socket = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WebSocket.PrototypeObject,
            static (Engine engine, Realm realm, (string Url, string Origin) state) => new JsWebSocket(engine, realm, state.Url, state.Origin),
            (Url: url.Serialize(), Origin: url.SerializeOrigin()));

        Connect(socket, url, protocols);
        return socket;
    }

    /// <summary>
    /// The "establish a WebSocket connection" half, https://websockets.spec.whatwg.org/#concept-websocket-establish,
    /// which the standard runs <i>in parallel</i> — so nothing below blocks and nothing below throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A URL the host's policy refuses opens no connection at all and fails on the next event-loop turn, with
    /// the same <c>error</c> and <c>close(1006)</c> pair a refused connection produces. That is not an
    /// oversight: https://websockets.spec.whatwg.org/#feedback-from-the-protocol <b>requires</b> that a script
    /// cannot tell a blocked request from a DNS failure from a refused port, precisely so that it cannot map
    /// the network it is running in by probing it.
    /// </para>
    /// <para>
    /// The concurrency ceiling is the one exception, and it is a synchronous throw: it is not about the
    /// network at all but about this engine's own resources, and a script that has hit it has a bug worth
    /// reporting rather than a host to probe.
    /// </para>
    /// </remarks>
    private void Connect(JsWebSocket socket, UrlRecord url, List<string> protocols)
    {
        var state = _engine._webApi;
        if (state?.FetchOptions is not { } options)
        {
            // Unreachable: the global that reaches this constructor is installed only where the state and the
            // network settings were created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The WebSocket global was reached on an engine that has no network configuration.");
            return;
        }

        if (state.ActiveWebSocketCount >= options.MaxConcurrentRequests)
        {
            // https://webidl.spec.whatwg.org/#quotaexceedederror, with the ceiling and the count the
            // connection would have taken the engine to. The quota is clamped at zero for the same reason the
            // timer queue's is: a non-positive ceiling permits no socket at all, and a negative `quota` is
            // something the interface's own constructor refuses.
            ThrowQuotaExceededError(
                $"Failed to construct 'WebSocket': the engine already has {options.MaxConcurrentRequests} sockets open, which is its Options.WebApi.Fetch.MaxConcurrentRequests limit.",
                quota: Math.Max(0, options.MaxConcurrentRequests),
                requested: (double) state.ActiveWebSocketCount + 1);
        }

        // The socket exists whether or not the policy admits it, and an observer is told about it either
        // way: a refused URL is a socket that closes at once, not a socket that never was.
        var observation = WebSocketObservation.Create(options.WebSocketObserver);

        IWebSocketConnection? connection = null;
        if (WebSocketPolicy.Allows(options, url, out var uri))
        {
            observation?.Created(uri);

            var factory = state.WebSocketConnections ?? ClientWebSocketConnectionFactory.For(observation is not null);
            connection = factory.Create(uri, protocols, options.MaxResponseBytes, options.UserAgent);
        }
        else if (observation is not null && Uri.TryCreate(url.Serialize(), UriKind.Absolute, out var refused))
        {
            observation.Created(refused);
        }

        var operation = new WebSocketOperation(_engine, _realm, socket, connection, options.Timeout, observation, protocols);
        socket.Operation = operation;
        state.RegisterWebSocket(socket);
        operation.Start();
    }

    /// <summary>
    /// The <c>(DOMString or sequence&lt;DOMString&gt;)</c> conversion, https://webidl.spec.whatwg.org/#es-union:
    /// an object with an iterator is the sequence arm, and everything else — including an object without one —
    /// is stringified into a one-element list. An omitted argument is the IDL default, the empty sequence.
    /// </summary>
    private List<string> ReadProtocols(JsValue protocols)
    {
        var result = new List<string>();

        if (protocols.IsUndefined())
        {
            return result;
        }

        if (protocols is not ObjectInstance candidate || candidate.GetMethod(GlobalSymbolRegistry.Iterator) is null)
        {
            result.Add(TypeConverter.ToString(protocols));
            return result;
        }

        var iterator = protocols.GetIterator(_realm);
        try
        {
            while (iterator.TryIteratorStepValue(out var value))
            {
                result.Add(TypeConverter.ToString(value));
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return result;
    }

    /// <summary>
    /// Step 10: "If any of the values in protocols occur more than once, or otherwise fail to match the
    /// requirements for elements that comprise the value of <c>Sec-WebSocket-Protocol</c> fields as defined by
    /// The WebSocket Protocol, then throw a <c>SyntaxError</c> DOMException."
    /// </summary>
    private void ValidateProtocols(List<string> protocols)
    {
        for (var i = 0; i < protocols.Count; i++)
        {
            var protocol = protocols[i];

            if (!WebSocketValues.IsToken(protocol))
            {
                ThrowSyntaxError($"Failed to construct 'WebSocket': The subprotocol '{protocol}' is invalid.");
            }

            // The list is bounded by what a script wrote out, so the quadratic scan is the cheaper answer
            // than a set — and it keeps the comparison the byte-for-byte one the protocol asks for.
            for (var j = 0; j < i; j++)
            {
                if (string.Equals(protocols[j], protocol, StringComparison.Ordinal))
                {
                    ThrowSyntaxError($"Failed to construct 'WebSocket': The subprotocol '{protocol}' is duplicated.");
                }
            }
        }
    }

    private void ThrowSyntaxError(string message) => ThrowDomException(DomExceptionNames.Syntax, message);

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    private void ThrowQuotaExceededError(string message, double quota, double requested)
    {
        var exception = _realm.Intrinsics.QuotaExceededError.CreateException(message, quota, requested);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}
#endif
