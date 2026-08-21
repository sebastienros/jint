#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.FetchEvents;

/// <summary>
/// <c>FetchEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/ServiceWorker/#fetchevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, so a <c>fetch</c> event has every <c>Event</c> member
/// and <c>event instanceof Event</c> holds inside the listener. The IDL puts <c>ExtendableEvent.prototype</c>
/// between the two and <c>waitUntil()</c> on it; that interface is not materialized here, so
/// <c>waitUntil</c> is a member of this object instead — see <see cref="JsFetchEvent"/>.
/// </para>
/// <para>
/// The two operations are non-enumerable, where a WebIDL interface prototype object's operations are
/// enumerable — the same documented simplification <c>Event.prototype</c> and <c>EventTarget.prototype</c>
/// carry.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class FetchEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly FetchEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString FetchEventToStringTag = new("FetchEvent");

    internal FetchEventPrototype(
        Engine engine,
        Realm realm,
        FetchEventConstructor constructor,
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
    /// https://w3c.github.io/ServiceWorker/#dom-fetchevent-request
    /// </summary>
    [JsAccessor("request", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsRequest RequestGet(JsValue thisObject) => Brand(thisObject).Request;

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#fetch-event-respondwith
    /// </summary>
    [JsFunction(Name = "respondWith", Length = 1)]
    private JsValue RespondWith(JsValue thisObject, JsCallArguments arguments)
    {
        var ev = Brand(thisObject);
        RequireArguments(arguments, 1, "respondWith");
        ev.RespondWith(arguments.At(0));
        return Undefined;
    }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-extendableevent-waituntil
    /// </summary>
    [JsFunction(Name = "waitUntil", Length = 1)]
    private JsValue WaitUntil(JsValue thisObject, JsCallArguments arguments)
    {
        var ev = Brand(thisObject);
        RequireArguments(arguments, 1, "waitUntil");
        ev.WaitUntil(arguments.At(0));
        return Undefined;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing the
    /// interface raises a <c>TypeError</c> — <c>FetchEvent.prototype</c> itself included, which is not one.
    /// </summary>
    private JsFetchEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsFetchEvent ev)
        {
            return ev;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a FetchEvent");
        return null!;
    }

    /// <summary>
    /// WebIDL's arity check, https://webidl.spec.whatwg.org/#dfn-create-operation-function: an operation whose
    /// required arguments were not all supplied raises a <c>TypeError</c> before its arguments are converted.
    /// It matters here rather than being pedantry — <c>Promise&lt;T&gt;</c> converts <i>anything</i>, so a bare
    /// <c>respondWith()</c> would otherwise resolve with <c>undefined</c> and fail the request one turn later
    /// with a message about the answer not being a <c>Response</c>.
    /// </summary>
    private void RequireArguments(JsCallArguments arguments, int required, string operationName)
    {
        if (arguments.Length < required)
        {
            Throw.TypeError(
                _realm,
                $"Failed to execute '{operationName}' on 'FetchEvent': {required} argument required, but only {arguments.Length} present.");
        }
    }
}
#endif
