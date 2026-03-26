using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asynciterator-constructor
/// </summary>
internal sealed class AsyncIteratorConstructor : Constructor
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

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["from"] = new(new PropertyDescriptor(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags)),
        };
        SetProperties(properties);
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined() || ReferenceEquals(this, newTarget))
        {
            Throw.TypeError(_realm);
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.AsyncIterator.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsObject(engine));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciterator.from
    /// </summary>
    private JsValue From(JsValue thisObject, JsValue[] arguments)
    {
        var o = arguments.At(0);

        // 1. If O is not an Object, throw a TypeError exception.
        if (o is not ObjectInstance obj)
        {
            Throw.TypeError(_realm, "AsyncIterator.from requires an object");
            return Undefined;
        }

        ObjectInstance iterator;

        // 2. Let asyncIteratorMethod be ? GetMethod(O, @@asyncIterator).
        var asyncIteratorMethod = obj.GetMethod(GlobalSymbolRegistry.AsyncIterator);

        if (asyncIteratorMethod is not null)
        {
            // 3. If asyncIteratorMethod is not undefined, then
            //    a. Let asyncIterator be ? Call(asyncIteratorMethod, O).
            var result = asyncIteratorMethod.Call(obj);
            if (result is not ObjectInstance asyncIter)
            {
                Throw.TypeError(_realm, "Async iterator result is not an object");
                return Undefined;
            }
            iterator = asyncIter;
        }
        else
        {
            // 4. Else,
            //    a. Let syncIteratorMethod be ? GetMethod(O, @@iterator).
            var syncIteratorMethod = obj.GetMethod(GlobalSymbolRegistry.Iterator);

            if (syncIteratorMethod is not null)
            {
                // b. If syncIteratorMethod is not undefined, then
                //    i. Let syncIterator be ? Call(syncIteratorMethod, O).
                var syncResult = syncIteratorMethod.Call(obj);
                if (syncResult is not ObjectInstance syncIter)
                {
                    Throw.TypeError(_realm, "Iterator result is not an object");
                    return Undefined;
                }

                // ii. Return ! CreateAsyncFromSyncIterator(syncIterator).
                var syncIteratorInstance = new IteratorInstance.ObjectIterator(syncIter);
                return new AsyncFromSyncIterator(_engine, syncIteratorInstance);
            }

            // c. Let iteratorRecord be GetIteratorDirect(O) - use object directly.
            iterator = obj;
        }

        // 5. Let hasInstance be ? OrdinaryHasInstance(%AsyncIterator%, iterator).
        var hasInstance = _engine.Intrinsics.AsyncIterator.OrdinaryHasInstance(iterator);

        // 6. If hasInstance is true, return iterator.
        if (TypeConverter.ToBoolean(hasInstance))
        {
            return iterator;
        }

        // 7. Let wrapper be WrapForValidAsyncIterator
        var iteratorRecord = new IteratorInstance.ObjectIterator(iterator);
        var wrapper = new WrapForValidAsyncIterator(_engine, iteratorRecord);
        return wrapper;
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
/// https://tc39.es/ecma262/#sec-%wrapforvalidasynciteratorprototype%-object
/// </summary>
internal sealed class WrapForValidAsyncIteratorPrototype : Prototype
{
    internal WrapForValidAsyncIteratorPrototype(
        Engine engine,
        Realm realm,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, realm)
    {
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new PropertyDescriptor(new ClrFunction(_engine, "next", Next, 0, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            [KnownKeys.Return] = new PropertyDescriptor(new ClrFunction(_engine, "return", Return, 0, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// %WrapForValidAsyncIteratorPrototype%.next()
    /// Delegates to the underlying iterator's next() and wraps in a promise.
    /// </summary>
    private JsValue Next(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not WrapForValidAsyncIterator wrapper)
        {
            Throw.TypeError(_realm, "Method WrapForValidAsyncIterator.prototype.next called on incompatible receiver");
            return Undefined;
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        try
        {
            // Call the underlying iterator's next method
            var target = wrapper.Iterated.Instance;
            var nextMethod = target.Get(CommonProperties.Next);
            if (nextMethod is not ICallable callable)
            {
                Throw.TypeError(_realm, "Iterator does not have a next method");
                return Undefined;
            }

            var result = callable.Call(target, Arguments.Empty);

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
            promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
        }

        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// %WrapForValidAsyncIteratorPrototype%.return()
    /// </summary>
    private JsValue Return(JsValue thisObject, JsValue[] arguments)
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
                promiseCapability.Resolve.Call(Undefined, new JsValue[] { doneResult });
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
            promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
        }

        return promiseCapability.PromiseInstance;
    }
}
