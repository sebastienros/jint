#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ReadableStream.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#rs-prototype
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Which failures throw and which reject is not a matter of taste: a WebIDL operation whose return type is
/// a promise type turns <i>every</i> exception — the brand check, the argument conversion, the body — into a
/// rejected promise, while one that returns anything else lets it propagate
/// (https://webidl.spec.whatwg.org/#js-operations). So <c>cancel()</c> and <c>pipeTo()</c> reject and
/// <c>getReader()</c>, <c>pipeThrough()</c>, <c>tee()</c> and <c>values()</c> throw, and the difference is
/// pinned by tests.
/// </para>
/// <para>
/// <c>@@asyncIterator</c> is the very same function object as <c>values</c>, which is what a WebIDL
/// <c>async_iterable&lt;&gt;</c> declaration produces and what
/// <c>ReadableStream.prototype[Symbol.asyncIterator] === ReadableStream.prototype.values</c> asserts.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
[JsSymbolAlias("AsyncIterator", "values")]
internal sealed partial class ReadableStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ReadableStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ReadableStreamToStringTag = new("ReadableStream");

    internal ReadableStreamPrototype(
        Engine engine,
        Realm realm,
        ReadableStreamConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
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
    /// https://streams.spec.whatwg.org/#rs-locked
    /// </summary>
    [JsAccessor("locked", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean LockedGet(JsValue thisObject)
        => ReadableStreamOperations.IsLocked(Brand(thisObject)) ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-cancel
    /// </summary>
    [JsFunction(Name = "cancel", Length = 0)]
    private JsPromise Cancel(JsValue thisObject, JsValue reason)
    {
        try
        {
            var stream = Brand(thisObject);

            if (ReadableStreamOperations.IsLocked(stream))
            {
                Throw.TypeError(_realm, "Cannot cancel a readable stream that is locked to a reader");
            }

            return ReadableStreamOperations.Cancel(stream, reason);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-get-reader
    /// </summary>
    [JsFunction(Name = "getReader", Length = 0)]
    private JsReadableStreamReader GetReader(JsValue thisObject, JsValue options)
    {
        var stream = Brand(thisObject);

        // A BYOB reader can only be acquired from a readable byte stream; asking for one from an ordinary
        // stream is the TypeError SetUpReadableStreamBYOBReader raises.
        if (StreamDictionaries.ReadByobModeRequested(_realm, options))
        {
            return ReadableStreamOperations.AcquireBYOBReader(stream);
        }

        return ReadableStreamOperations.AcquireDefaultReader(stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-pipe-through
    /// </summary>
    [JsFunction(Name = "pipeThrough", Length = 1)]
    private JsReadableStream PipeThrough(JsValue thisObject, JsValue transform, JsValue options)
    {
        var stream = Brand(thisObject);
        var (readable, writable) = StreamDictionaries.ReadReadableWritablePair(_realm, transform);
        var pipeOptions = StreamDictionaries.ReadStreamPipeOptions(_realm, options);

        if (ReadableStreamOperations.IsLocked(stream))
        {
            Throw.TypeError(_realm, "Cannot pipe from a readable stream that is locked to a reader");
        }

        if (WritableStreamOperations.IsLocked(writable))
        {
            Throw.TypeError(_realm, "Cannot pipe to a writable stream that is locked to a writer");
        }

        var promise = ReadableStreamPipe.PipeTo(
            stream, writable, pipeOptions.PreventClose, pipeOptions.PreventAbort, pipeOptions.PreventCancel, pipeOptions.Signal);

        // The pipe's promise is not handed back — the readable side is — so it is marked handled and a
        // failure surfaces through that stream instead of as an unhandled rejection.
        StreamPromises.MarkHandled(promise);

        return readable;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-pipe-to
    /// </summary>
    [JsFunction(Name = "pipeTo", Length = 1)]
    private JsPromise PipeTo(JsValue thisObject, JsValue destination, JsValue options)
    {
        try
        {
            var stream = Brand(thisObject);

            if (destination is not JsWritableStream writable)
            {
                Throw.TypeError(_realm, "Failed to execute 'pipeTo' on 'ReadableStream': the destination is not a WritableStream");
                return null!;
            }

            var pipeOptions = StreamDictionaries.ReadStreamPipeOptions(_realm, options);

            if (ReadableStreamOperations.IsLocked(stream))
            {
                Throw.TypeError(_realm, "Cannot pipe from a readable stream that is locked to a reader");
            }

            if (WritableStreamOperations.IsLocked(writable))
            {
                Throw.TypeError(_realm, "Cannot pipe to a writable stream that is locked to a writer");
            }

            return ReadableStreamPipe.PipeTo(
                stream, writable, pipeOptions.PreventClose, pipeOptions.PreventAbort, pipeOptions.PreventCancel, pipeOptions.Signal);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-tee
    /// </summary>
    [JsFunction(Name = "tee", Length = 0)]
    private JsArray Tee(JsValue thisObject)
    {
        var (branch1, branch2) = ReadableStreamOperations.Tee(Brand(thisObject));
        return new JsArray(_engine, [branch1, branch2]);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-asynciterator — the <c>values</c> method a WebIDL
    /// <c>async_iterable&lt;any&gt;</c> declaration produces, which is also <c>@@asyncIterator</c>.
    /// </summary>
    [JsFunction(Name = "values", Length = 0)]
    private ReadableStreamAsyncIterator Values(JsValue thisObject, JsValue options)
    {
        var stream = Brand(thisObject);

        // The options dictionary is converted before the iterator exists, so a preventCancel getter that
        // throws is reported before the stream is locked.
        var preventCancel = StreamDictionaries.ReadPreventCancel(_realm, options);

        return _realm.Intrinsics.ReadableStreamAsyncIteratorPrototype.Construct(stream, preventCancel);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsReadableStream Brand(JsValue thisObject)
    {
        if (thisObject is JsReadableStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ReadableStream");
        return null!;
    }
}
#endif
