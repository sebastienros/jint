#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The global <c>fetch(input, init)</c> function.
/// <para>
/// https://fetch.spec.whatwg.org/#fetch-method
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A WebIDL operation whose return type is a promise, which is what makes it total: every failure — a
/// malformed URL, a body the engine cannot serialize, a refused scheme, a DNS failure, a blown size cap —
/// becomes a rejection of the promise it hands back, and the call itself never throws. The algorithm lives in
/// <see cref="FetchOperation"/>.
/// </para>
/// <para>
/// <b>Nothing runs until the engine is pumped.</b> The request is started on the calling thread and completes
/// on a thread pool thread, but the promise settles from an event-loop job, so the script's continuation runs
/// where every other continuation runs: inside a blocking <c>UnwrapIfPromise</c>, an <c>await</c> of
/// <c>EvaluateAsync</c>, or the host's own <c>engine.Tasks.ProcessTasks()</c> loop. The deadline is the
/// one exception, and deliberately so — it is enforced CLR-side, so an engine nobody pumps still lets go of
/// its socket.
/// </para>
/// </remarks>
internal sealed class FetchFunction : Jint.Native.Function.Function
{
    private static readonly JsString _functionName = new("fetch");

    private readonly WebApiEngineState _state;

    private FetchFunction(Engine engine, Realm realm, FunctionPrototype functionPrototype, WebApiEngineState state)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        _state = state;

        // A WebIDL operation's length counts the required arguments only, and is configurable but neither
        // writable nor enumerable — https://webidl.spec.whatwg.org/#dfn-create-operation-function.
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    internal static FetchFunction Create(Engine engine, Realm realm, FunctionPrototype functionPrototype)
    {
        var state = engine._webApi;
        if (state?.FetchOptions is null)
        {
            // Unreachable: the global that reaches this is installed only where the state was created, in the
            // same block of WebApiRegistration.
            Throw.InvalidOperationException("The fetch global was reached on an engine that has no fetch configuration.");
        }

        return new FetchFunction(engine, realm, functionPrototype, state);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return FetchOperation.Start(_engine, _realm, _state, arguments);
    }
}
#endif
