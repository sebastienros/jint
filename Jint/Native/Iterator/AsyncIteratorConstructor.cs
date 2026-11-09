using Jint.Native.Function;
using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/proposal-async-iterator-helpers/#sec-asynciterator-constructor
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class AsyncIteratorConstructor : Constructor
{
    private static readonly JsString _functionName = new("AsyncIterator");

    internal AsyncIteratorConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        AsyncIteratorPrototype asyncIteratorPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = asyncIteratorPrototype;
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal AsyncIteratorPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined() || ReferenceEquals(this, newTarget))
        {
            Throw.TypeError(_realm, "Abstract class AsyncIterator not directly constructable");
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.AsyncIterator.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsObject(engine));
    }

    /// <summary>
    /// https://tc39.es/proposal-async-iterator-helpers/#sec-asynciterator.from
    /// </summary>
    [JsFunction]
    private JsValue From(JsValue thisObject, JsValue o)
    {
        // 1. If O is a String, set O to ! ToObject(O). The String object is then the receiver of the iterator method,
        //    where Iterator.from calls it on the primitive (sebastienros/jint#4439).
        if (o.IsString())
        {
            o = TypeConverter.ToObject(_realm, o);
        }

        // 2. Let iteratorRecord be ? GetIteratorFlattenable(O, async).
        var iteratorRecord = GetIteratorFlattenable(_engine, _realm, o, "AsyncIterator.from called on non-object");
        var iterator = iteratorRecord.Instance;

        // 3. Let hasInstance be ? OrdinaryHasInstance(%AsyncIterator%, iteratorRecord.[[Iterator]]).
        var hasInstance = _engine.Intrinsics.AsyncIterator.OrdinaryHasInstance(iterator);

        // 4. If hasInstance is true, return iteratorRecord.[[Iterator]]. A CreateAsyncFromSyncIterator wrapper is one,
        //    through %AsyncFromSyncIteratorPrototype%.
        if (TypeConverter.ToBoolean(hasInstance))
        {
            return iterator;
        }

        // 5-7. Let wrapper be OrdinaryObjectCreate(%WrapForValidAsyncIteratorPrototype%, « [[AsyncIterated]] »), set
        //      wrapper.[[AsyncIterated]] to iteratorRecord, and return wrapper.
        return new WrapForValidAsyncIterator(_engine, iteratorRecord);
    }

    /// <summary>
    /// GetIteratorFlattenable(obj, async), the operation <c>AsyncIterator.from</c> and the inner iterator of
    /// <c>AsyncIterator.prototype.flatMap</c> build their record with. The proposal links the version that had a hint,
    /// from before the async helpers moved out of tc39/proposal-iterator-helpers
    /// (https://github.com/tc39/proposal-iterator-helpers/blob/ef4471f0709f37e8f02045b9d5f2d7f3d5e8fc18/spec.html);
    /// ECMA-262's is sync only. An object with no
    /// <c>@@asyncIterator</c> but an <c>@@iterator</c> is wrapped by CreateAsyncFromSyncIterator
    /// (https://tc39.es/ecma262/#sec-createasyncfromsynciterator), so each value it answers is awaited; one with
    /// neither is the iterator itself (sebastienros/jint#4439).
    /// </summary>
    /// <remarks>
    /// <c>next</c> is read once, for the record, before the caller's OrdinaryHasInstance (sebastienros/jint#4419). A
    /// <c>next</c> that is not callable is not rejected here, although that version says so: like GetIteratorDirect
    /// (tc39/ecma262#274) and core-js, the TypeError comes from the first step's call. The iterator methods are read
    /// with GetMethod, as core-js does, so one that is neither callable nor undefined throws.
    /// </remarks>
    internal static IteratorInstance.ObjectIterator GetIteratorFlattenable(Engine engine, Realm realm, JsValue value, string notAnObjectMessage)
    {
        // If obj is not an Object, throw a TypeError exception.
        if (value is not ObjectInstance obj)
        {
            Throw.TypeError(realm, notAnObjectMessage);
            return null!;
        }

        var asyncIteratorMethod = obj.GetMethod(GlobalSymbolRegistry.AsyncIterator);
        if (asyncIteratorMethod is not null)
        {
            if (asyncIteratorMethod.Call(obj) is not ObjectInstance asyncIterator)
            {
                Throw.TypeError(realm, "Result of the Symbol.asyncIterator method is not an object");
                return null!;
            }

            return new IteratorInstance.ObjectIterator(asyncIterator);
        }

        var syncIteratorMethod = obj.GetMethod(GlobalSymbolRegistry.Iterator);
        if (syncIteratorMethod is not null)
        {
            // The sync record is GetIteratorFromMethod's, which reads `next` once and adopts a built-in iterator so that
            // the wrapper's return can release its native state. The wrapper's own record reads its `next`, as
            // CreateAsyncFromSyncIterator's ! Get does.
            var syncIteratorRecord = obj.GetIterator(realm, GeneratorKind.Sync, syncIteratorMethod);
            return new IteratorInstance.ObjectIterator(new AsyncFromSyncIterator(engine, syncIteratorRecord));
        }

        return new IteratorInstance.ObjectIterator(obj);
    }
}

/// <summary>
/// Wrapper instance for AsyncIterator.from when the input is not already an AsyncIterator instance.
/// </summary>
internal sealed class WrapForValidAsyncIterator : ObjectInstance
{
    internal readonly IteratorInstance.ObjectIterator Iterated;

    public WrapForValidAsyncIterator(Engine engine, IteratorInstance.ObjectIterator iterated) : base(engine)
    {
        Iterated = iterated;
        _prototype = engine.Realm.Intrinsics.WrapForValidAsyncIteratorPrototype;
    }
}

/// <summary>
/// https://tc39.es/proposal-async-iterator-helpers/#sec-wrapforvalidasynciteratorprototype-object
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class WrapForValidAsyncIteratorPrototype : Prototype
{
    internal WrapForValidAsyncIteratorPrototype(
        Engine engine,
        Realm realm,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, realm)
    {
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://tc39.es/proposal-async-iterator-helpers/#sec-wrapforvalidasynciteratorprototype.next
    /// IteratorNext on the wrapped record, wrapped in a promise: the call is to the [[NextMethod]] that AsyncIterator.from
    /// read once, never <c>next</c> read again (sebastienros/jint#4419).
    /// </summary>
    [JsFunction]
    private JsValue Next(JsValue thisObject)
    {
        if (thisObject is not WrapForValidAsyncIterator wrapper)
        {
            Throw.TypeError(_realm, "Method WrapForValidAsyncIterator.prototype.next called on incompatible receiver");
            return Undefined;
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        try
        {
            // Call(iteratorRecord.[[NextMethod]], iteratorRecord.[[Iterator]])
            var result = wrapper.Iterated.CallNextMethod();

            // Normalize the result to a promise
            var resultPromise = (JsPromise) _realm.Intrinsics.Promise.PromiseResolve(result);

            // Chain to extract the iterator result
            var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    throw new JavaScriptException(_realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                }
                return iterResultObj;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(_engine, resultPromise, onFulfilled, Undefined, promiseCapability);
        }
        catch (JavaScriptException ex)
        {
            promiseCapability.Reject(ex.Error);
        }

        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// %WrapForValidAsyncIteratorPrototype%.return()
    /// </summary>
    [JsFunction]
    private JsValue Return(JsValue thisObject)
    {
        if (thisObject is not WrapForValidAsyncIterator wrapper)
        {
            Throw.TypeError(_realm, "Method WrapForValidAsyncIterator.prototype.return called on incompatible receiver");
            return Undefined;
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        try
        {
            var iterator = wrapper.Iterated.Instance;
            var returnMethod = iterator.GetMethod(CommonProperties.Return);

            if (returnMethod is null)
            {
                // Return a resolved promise with {value: undefined, done: true}
                var doneResult = IteratorResult.CreateValueIteratorPosition(_engine, Undefined, JsBoolean.True);
                promiseCapability.Resolve(doneResult);
            }
            else
            {
                var result = returnMethod.Call(iterator, Arguments.Empty);

                // Normalize the result to a promise
                var resultPromise = (JsPromise) _realm.Intrinsics.Promise.PromiseResolve(result);

                var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
                {
                    var iterResult = args.At(0);
                    if (iterResult is not ObjectInstance iterResultObj)
                    {
                        throw new JavaScriptException(_realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    }
                    return iterResultObj;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(_engine, resultPromise, onFulfilled, Undefined, promiseCapability);
            }
        }
        catch (JavaScriptException ex)
        {
            promiseCapability.Reject(ex.Error);
        }

        return promiseCapability.PromiseInstance;
    }
}
