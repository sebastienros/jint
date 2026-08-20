#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>%ReadableStreamAsyncIteratorPrototype%</c> — WebIDL's "asynchronous iterator prototype object" for
/// <c>ReadableStream</c>'s <c>async_iterable&lt;any&gt;</c> declaration.
/// <para>
/// https://webidl.spec.whatwg.org/#js-asynchronous-iterator-prototype-object
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>%AsyncIteratorPrototype%</c>, which is what gives an iterator obtained from
/// a stream the whole async-iterator-helper surface (<c>map</c>, <c>filter</c>, <c>toArray</c>, …) and its
/// <c>@@asyncIterator</c> method. It is reachable only through
/// <c>Object.getPrototypeOf(stream.values())</c>; no global names it.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class ReadableStreamAsyncIteratorPrototype : Prototype
{
    /// <summary>
    /// The value the <i>get the next iteration result</i> promise fulfils with when the stream has closed:
    /// WebIDL's "end of iteration". A symbol nothing can reach — it is never handed to a callback and is
    /// compared only by reference — so it can never be mistaken for a chunk.
    /// </summary>
    private static readonly JsSymbol _endOfIteration = new("[[end of iteration]]");

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString AsyncIteratorToStringTag = new("ReadableStream AsyncIterator");

    internal ReadableStreamAsyncIteratorPrototype(
        Engine engine,
        Realm realm,
        ObjectInstance asyncIteratorPrototype) : base(engine, realm)
    {
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// Creates the iterator <c>ReadableStream.prototype.values()</c> hands out, running the specification's
    /// asynchronous iterator initialization steps for <c>ReadableStream</c>:
    /// https://streams.spec.whatwg.org/#rs-get-iterator.
    /// </summary>
    internal ReadableStreamAsyncIterator Construct(JsReadableStream stream, bool preventCancel)
    {
        var iterator = new ReadableStreamAsyncIterator(_engine, _realm)
        {
            _prototype = this,
        };

        // Acquiring the reader is what locks the stream, and what raises the TypeError for a stream that is
        // already locked.
        iterator.Reader = ReadableStreamOperations.AcquireDefaultReader(stream);
        iterator.PreventCancel = preventCancel;
        return iterator;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#js-asynchronous-iterator-prototype-object, the <c>next</c> data
    /// property's steps.
    /// </summary>
    [JsFunction(Name = "next", Length = 0)]
    private JsPromise Next(JsValue thisObject)
    {
        if (thisObject is not ReadableStreamAsyncIterator iterator)
        {
            // The brand check is reported as a rejection, not a throw: the operation's return type is a
            // promise type — https://webidl.spec.whatwg.org/#js-operations.
            return StreamPromises.RejectedWith(
                _engine,
                _realm,
                _realm.Intrinsics.TypeError.Construct("Illegal invocation: receiver is not a ReadableStream async iterator"));
        }

        if (iterator.OngoingPromise is { } ongoing)
        {
            // A next() while another is outstanding queues behind it, whichever way that one settles.
            iterator.OngoingPromise = StreamPromises.TransformPromiseWith(
                _engine, _realm, ongoing, _ => NextSteps(iterator), _ => NextSteps(iterator));
        }
        else
        {
            iterator.OngoingPromise = NextSteps(iterator);
        }

        return iterator.OngoingPromise;
    }

    private JsPromise NextSteps(ReadableStreamAsyncIterator iterator)
    {
        if (iterator.IsFinished)
        {
            return StreamPromises.ResolvedWith(_realm, IteratorResult.CreateValueIteratorPosition(_engine, Undefined, JsBoolean.True));
        }

        var nextPromise = GetNextIterationResult(iterator);

        return StreamPromises.TransformPromiseWith(
            _engine,
            _realm,
            nextPromise,
            next =>
            {
                iterator.OngoingPromise = null;

                if (ReferenceEquals(next, _endOfIteration))
                {
                    iterator.IsFinished = true;
                    return IteratorResult.CreateValueIteratorPosition(_engine, Undefined, JsBoolean.True);
                }

                return IteratorResult.CreateValueIteratorPosition(_engine, next, JsBoolean.False);
            },
            reason =>
            {
                iterator.OngoingPromise = null;
                iterator.IsFinished = true;
                throw new JavaScriptException(reason);
            });
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-asynciterator-prototype-next — the <i>get the next iteration
    /// result</i> steps for a <c>ReadableStream</c>. Both terminal outcomes release the reader's lock, which
    /// is what lets an iteration that ran to completion leave the stream usable again.
    /// </summary>
    private JsPromise GetNextIterationResult(ReadableStreamAsyncIterator iterator)
    {
        var reader = iterator.Reader;
        var capability = StreamPromises.NewPromise(_engine, _realm);

        ReadableStreamOperations.DefaultReaderRead(reader, new IterationReadRequest(reader, capability));
        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#js-asynchronous-iterator-prototype-object, the <c>return</c> data
    /// property's steps.
    /// </summary>
    [JsFunction(Name = "return", Length = 1)]
    private JsPromise Return(JsValue thisObject, JsValue value)
    {
        if (thisObject is not ReadableStreamAsyncIterator iterator)
        {
            return StreamPromises.RejectedWith(
                _engine,
                _realm,
                _realm.Intrinsics.TypeError.Construct("Illegal invocation: receiver is not a ReadableStream async iterator"));
        }

        if (iterator.OngoingPromise is { } ongoing)
        {
            iterator.OngoingPromise = StreamPromises.TransformPromiseWith(
                _engine, _realm, ongoing, _ => ReturnSteps(iterator, value), _ => ReturnSteps(iterator, value));
        }
        else
        {
            iterator.OngoingPromise = ReturnSteps(iterator, value);
        }

        // Whatever the return steps produced, the iterator result the caller sees is { value, done: true }.
        return StreamPromises.TransformPromiseWith(
            _engine,
            _realm,
            iterator.OngoingPromise,
            _ => IteratorResult.CreateValueIteratorPosition(_engine, value, JsBoolean.True),
            onRejected: null);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-asynciterator-prototype-return — the <i>asynchronous iterator
    /// return</i> steps for a <c>ReadableStream</c>.
    /// </summary>
    private JsPromise ReturnSteps(ReadableStreamAsyncIterator iterator, JsValue value)
    {
        if (iterator.IsFinished)
        {
            return StreamPromises.ResolvedWith(_realm, IteratorResult.CreateValueIteratorPosition(_engine, value, JsBoolean.True));
        }

        iterator.IsFinished = true;

        var reader = iterator.Reader;

        if (!iterator.PreventCancel)
        {
            // Leaving the loop cancels the stream, and the promise the loop awaits is the cancel's — so a
            // source whose cancel() rejects makes `for await…of` throw on its way out.
            var result = ReadableStreamOperations.ReaderGenericCancel(reader, value);
            ReadableStreamOperations.DefaultReaderRelease(reader);
            return result;
        }

        ReadableStreamOperations.DefaultReaderRelease(reader);
        return StreamPromises.ResolvedWithUndefined(_engine, _realm);
    }

    /// <summary>
    /// The read request one <c>next()</c> makes. Closing and erroring both release the lock before settling,
    /// so the stream is free again by the time the consumer observes the end of the iteration.
    /// </summary>
    private sealed class IterationReadRequest : ReadRequest
    {
        private readonly JsReadableStreamDefaultReader _reader;
        private readonly PromiseCapability _capability;

        internal IterationReadRequest(JsReadableStreamDefaultReader reader, PromiseCapability capability)
        {
            _reader = reader;
            _capability = capability;
        }

        internal override void ChunkSteps(JsValue chunk) => _capability.Resolve(chunk);

        internal override void CloseSteps()
        {
            ReadableStreamOperations.DefaultReaderRelease(_reader);
            _capability.Resolve(_endOfIteration);
        }

        internal override void ErrorSteps(JsValue error)
        {
            ReadableStreamOperations.DefaultReaderRelease(_reader);
            _capability.Reject(error);
        }
    }
}
#endif
