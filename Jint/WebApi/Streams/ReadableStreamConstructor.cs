#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStream</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#rs-class
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ReadableStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("ReadableStream");

    internal ReadableStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ReadableStreamPrototype(engine, realm, this, objectPrototype);

        // Both constructor arguments are optional, so the interface object's length is 0 —
        // https://webidl.spec.whatwg.org/#es-interface-call.
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ReadableStreamPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-constructor
    /// </summary>
    /// <remarks>
    /// The argument order matters and is observable: <c>strategy</c> is a WebIDL dictionary and is therefore
    /// converted before the constructor's own steps run, while <c>underlyingSource</c> is converted inside
    /// them — so a strategy whose <c>size</c> getter throws wins over a source whose <c>start</c> getter
    /// does.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var underlyingSource = arguments.At(0);
        if (!underlyingSource.IsUndefined() && underlyingSource is not ObjectInstance)
        {
            // `optional object underlyingSource`: the IDL object type is not nullable, so an explicit null
            // is a TypeError while an omitted (or explicitly undefined) argument is simply missing.
            Throw.TypeError(_realm, "Failed to construct 'ReadableStream': the underlying source is not an object");
        }

        var strategy = StreamDictionaries.ReadQueuingStrategy(_realm, arguments.At(1), "The queuing strategy");

        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.ReadableStream.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsReadableStream(engine, realm));

        var source = StreamDictionaries.ReadUnderlyingSource(_realm, underlyingSource);

        if (source.TypeExists)
        {
            // `type: "bytes"` asks for a readable byte stream. Its queuing strategy may not carry a size()
            // at all — a chunk's size is its byte length — and its default high water mark is 0 rather
            // than 1, so a byte stream pulls only when a consumer asks.
            if (strategy.Size is not null)
            {
                Throw.RangeError(_realm, "A readable byte stream cannot have a queuing strategy with a size function");
            }

            var byteHighWaterMark = StreamDictionaries.ExtractHighWaterMark(_realm, in strategy, 0);

            ReadableByteStreamControllerOperations.SetUpFromUnderlyingSource(
                stream, underlyingSource, in source, byteHighWaterMark);

            return stream;
        }

        var sizeAlgorithm = StreamDictionaries.ExtractSizeAlgorithm(in strategy);
        var highWaterMark = StreamDictionaries.ExtractHighWaterMark(_realm, in strategy, 1);

        ReadableStreamOperations.SetUpDefaultControllerFromUnderlyingSource(
            stream, underlyingSource, in source, highWaterMark, sizeAlgorithm);

        return stream;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-from, which is
    /// https://streams.spec.whatwg.org/#readable-stream-from-iterable.
    /// </summary>
    /// <remarks>
    /// The argument is an <c>async_sequence&lt;any&gt;</c>, so anything that is not an object — a string
    /// included — is a <c>TypeError</c>, and an object carrying <c>@@asyncIterator</c> never has its
    /// <c>@@iterator</c> looked at. A synchronous iterable is adapted through the same
    /// <c>CreateAsyncFromSyncIterator</c> the language uses for <c>for await…of</c>, which is what makes
    /// <c>ReadableStream.from([Promise.resolve('a')])</c> yield <c>'a'</c> rather than the promise.
    /// </remarks>
    [JsFunction(Name = "from", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsReadableStream From(JsValue thisObject, JsValue asyncIterable)
    {
        var iterator = OpenAsyncSequence(asyncIterable);

        JsReadableStream stream = null!;

        JsPromise PullAlgorithm()
        {
            var nextPromise = GetNextValue(iterator);

            return StreamPromises.TransformPromiseWith(
                _engine,
                _realm,
                nextPromise,
                iterationResult =>
                {
                    if (iterationResult is not ObjectInstance iterationObject)
                    {
                        Throw.TypeError(_realm, "The iterator result is not an object");
                        return Undefined;
                    }

                    var controller = stream.DefaultController;
                    if (TypeConverter.ToBoolean(iterationObject.Get(CommonProperties.Done)))
                    {
                        ReadableStreamDefaultControllerOperations.Close(controller);
                    }
                    else
                    {
                        ReadableStreamDefaultControllerOperations.Enqueue(controller, iterationObject.Get(CommonProperties.Value));
                    }

                    return Undefined;
                },
                onRejected: null);
        }

        JsPromise CancelAlgorithm(JsValue reason) => CloseAsyncIterator(iterator, reason);

        // A high water mark of 0 means the iterator is only advanced when a consumer actually asks.
        stream = ReadableStreamOperations.CreateReadableStream(
            _engine, _realm, static () => Undefined, PullAlgorithm, CancelAlgorithm, highWaterMark: 0);

        return stream;
    }

    /// <summary>
    /// The <c>async_sequence</c> conversion followed by "opening" it —
    /// https://webidl.spec.whatwg.org/#js-to-async-iterable and
    /// https://webidl.spec.whatwg.org/#async-sequence-open, which the specification performs as one step
    /// because <c>ReadableStream.from</c> opens its argument immediately.
    /// </summary>
    private IteratorInstance OpenAsyncSequence(JsValue asyncIterable)
    {
        if (asyncIterable is not ObjectInstance objectInstance)
        {
            Throw.TypeError(_realm, "Failed to execute 'from' on 'ReadableStream': the value is not an object");
            return null!;
        }

        var method = objectInstance.GetMethod(GlobalSymbolRegistry.AsyncIterator);
        if (method is not null)
        {
            return asyncIterable.GetIteratorFromMethod(_realm, method);
        }

        var syncMethod = objectInstance.GetMethod(GlobalSymbolRegistry.Iterator);
        if (syncMethod is null)
        {
            Throw.TypeError(_realm, "Failed to execute 'from' on 'ReadableStream': the value is not async iterable");
            return null!;
        }

        // CreateAsyncFromSyncIterator, which is also what `for await…of` uses for a synchronous iterable:
        // the wrapper's next() awaits each value, so a sequence of promises yields the values they settle to.
        var syncIterator = asyncIterable.GetIteratorFromMethod(_realm, syncMethod);
        return new IteratorInstance.ObjectIterator(new AsyncFromSyncIterator(_engine, syncIterator));
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#async-iterator-get-next-value. A <c>next()</c> that throws becomes a
    /// rejected promise rather than escaping, because the pull algorithm's contract is to return a promise.
    /// The <c>next</c> method itself is the one read when the sequence was opened, exactly as an Iterator
    /// Record's <c>[[NextMethod]]</c> is — it is not re-read per step.
    /// </summary>
    private JsPromise GetNextValue(IteratorInstance iterator)
    {
        try
        {
            if (iterator.NextMethod is not { } nextMethod)
            {
                Throw.TypeError(_realm, "The iterator has no callable next method");
                return null!;
            }

            var nextResult = nextMethod.Call(iterator.Instance, Arguments.Empty);
            if (nextResult is not ObjectInstance)
            {
                Throw.TypeError(_realm, "The iterator's next method did not return an object");
            }

            return StreamPromises.ResolvedWith(_engine, _realm, nextResult);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#async-iterator-close — the iterator's <c>return</c> is called with the
    /// cancellation reason, and its result must be an object.
    /// </summary>
    private JsPromise CloseAsyncIterator(IteratorInstance iterator, JsValue reason)
    {
        var iteratorObject = iterator.Instance;

        JsValue returnResult;
        try
        {
            var returnMethod = iteratorObject.GetMethod(CommonProperties.Return);
            if (returnMethod is null)
            {
                return StreamPromises.ResolvedWithUndefined(_engine, _realm);
            }

            returnResult = returnMethod.Call(iteratorObject, reason);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }

        var returnPromise = StreamPromises.ResolvedWith(_engine, _realm, returnResult);

        return StreamPromises.TransformPromiseWith(
            _engine,
            _realm,
            returnPromise,
            result =>
            {
                if (result is not ObjectInstance)
                {
                    Throw.TypeError(_realm, "The iterator's return method did not return an object");
                }

                return Undefined;
            },
            onRejected: null);
    }
}
#endif
