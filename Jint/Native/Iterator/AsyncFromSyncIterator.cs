using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-async-from-sync-iterator-objects
/// </summary>
internal sealed class AsyncFromSyncIterator : ObjectInstance
{
    private readonly IteratorInstance _syncIterator;

    public AsyncFromSyncIterator(Engine engine, IteratorInstance syncIterator) : base(engine)
    {
        _syncIterator = syncIterator;
        _prototype = engine.Realm.Intrinsics.AsyncFromSyncIteratorPrototype;
    }

    /// <summary>
    /// Gets the underlying sync iterator.
    /// </summary>
    internal IteratorInstance SyncIterator => _syncIterator;
}

/// <summary>
/// https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%-object
/// </summary>
internal sealed class AsyncFromSyncIteratorPrototype : ObjectInstance
{
    private readonly Realm _realm;

    internal AsyncFromSyncIteratorPrototype(Engine engine, Realm realm, AsyncIteratorPrototype asyncIteratorPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new PropertyDescriptor(new ClrFunction(_engine, "next", Next, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            [KnownKeys.Return] = new PropertyDescriptor(new ClrFunction(_engine, "return", Return, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            [KnownKeys.Throw] = new PropertyDescriptor(new ClrFunction(_engine, "throw", Throw, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.next
    /// </summary>
    private JsValue Next(JsValue thisObject, JsCallArguments arguments)
    {
        var asyncIterator = thisObject as AsyncFromSyncIterator;
        if (asyncIterator is null)
        {
            Runtime.Throw.TypeError(_realm, "Method called on incompatible receiver");
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var syncIterator = asyncIterator!.SyncIterator;

        ObjectInstance result;
        try
        {
            if (!syncIterator.TryIteratorStep(out result))
            {
                // Iterator is done
                result = IteratorResult.CreateValueIteratorPosition(_engine, Undefined, done: JsBoolean.True);
            }
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, [e.Error]);
            return promiseCapability.PromiseInstance;
        }

        return AsyncFromSyncIteratorContinuation(result, promiseCapability, syncIterator, closeOnRejection: true);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return
    /// </summary>
    private JsValue Return(JsValue thisObject, JsCallArguments arguments)
    {
        var asyncIterator = thisObject as AsyncFromSyncIterator;
        if (asyncIterator is null)
        {
            Runtime.Throw.TypeError(_realm, "Method called on incompatible receiver");
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var syncIterator = asyncIterator!.SyncIterator;
        var value = arguments.At(0);

        // Get the return method from the sync iterator
        var returnMethod = syncIterator.Instance.GetMethod(CommonProperties.Return);
        if (returnMethod is null)
        {
            // If return is undefined, create a done iterator result
            var iterResult = IteratorResult.CreateValueIteratorPosition(_engine, value, done: JsBoolean.True);
            promiseCapability.Resolve.Call(Undefined, new[] { iterResult });
            return promiseCapability.PromiseInstance;
        }

        ObjectInstance result;
        try
        {
            var jsResult = returnMethod.Call(syncIterator.Instance, arguments.Length > 0 ? [value] : Arguments.Empty);
            if (jsResult is not ObjectInstance oi)
            {
                Runtime.Throw.TypeError(_realm, "Iterator result is not an object");
            }
            result = (ObjectInstance) jsResult;
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, [e.Error]);
            return promiseCapability.PromiseInstance;
        }

        return AsyncFromSyncIteratorContinuation(result, promiseCapability, syncIterator, closeOnRejection: false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.throw
    /// </summary>
    private JsValue Throw(JsValue thisObject, JsCallArguments arguments)
    {
        var asyncIterator = thisObject as AsyncFromSyncIterator;
        if (asyncIterator is null)
        {
            Runtime.Throw.TypeError(_realm, "Method called on incompatible receiver");
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var syncIterator = asyncIterator!.SyncIterator;
        var value = arguments.At(0);

        // Get the throw method from the sync iterator
        var throwMethod = syncIterator.Instance.GetMethod(CommonProperties.Throw);
        if (throwMethod is null)
        {
            // If throw is undefined, reject the promise
            promiseCapability.Reject.Call(Undefined, [value]);
            return promiseCapability.PromiseInstance;
        }

        ObjectInstance result;
        try
        {
            var jsResult = throwMethod.Call(syncIterator.Instance, arguments.Length > 0 ? [value] : Arguments.Empty);
            if (jsResult is not ObjectInstance oi)
            {
                Runtime.Throw.TypeError(_realm, "Iterator result is not an object");
            }
            result = (ObjectInstance) jsResult;
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, [e.Error]);
            return promiseCapability.PromiseInstance;
        }

        return AsyncFromSyncIteratorContinuation(result, promiseCapability, syncIterator, closeOnRejection: false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncfromsynciteratorcontinuation
    /// </summary>
    private JsValue AsyncFromSyncIteratorContinuation(
        ObjectInstance result,
        PromiseCapability promiseCapability,
        IteratorInstance syncIteratorRecord,
        bool closeOnRejection)
    {
        // 1. Let done be IteratorComplete(result)
        var done = TypeConverter.ToBoolean(result.Get(CommonProperties.Done));

        // 2. Let value be IteratorValue(result)
        var value = result.Get(CommonProperties.Value);

        // 3. Let valueWrapper be PromiseResolve(%Promise%, value)
        JsPromise valueWrapper;
        try
        {
            valueWrapper = (JsPromise) _engine.Intrinsics.Promise.PromiseResolve(value);
        }
        catch (JavaScriptException e)
        {
            // 6. If valueWrapper is an abrupt completion, done is false, and closeOnRejection is true, then
            //    a. Set valueWrapper to IteratorClose(syncIteratorRecord, valueWrapper).
            if (!done && closeOnRejection)
            {
                try
                {
                    syncIteratorRecord.Close(CompletionType.Throw);
                }
                catch
                {
                    // Ignore errors from close
                }
            }
            // 7. IfAbruptRejectPromise(valueWrapper, promiseCapability).
            promiseCapability.Reject.Call(Undefined, [e.Error]);
            return promiseCapability.PromiseInstance;
        }

        // 8-9. Let onFulfilled be CreateBuiltinFunction(unwrap, 1, "", « »).
        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            var resolvedValue = args.At(0);
            return IteratorResult.CreateValueIteratorPosition(_engine, resolvedValue, done ? JsBoolean.True : JsBoolean.False);
        }, 1, PropertyFlag.Configurable);

        // 11-13. Determine onRejected based on done and closeOnRejection
        JsValue onRejected;
        if (done || !closeOnRejection)
        {
            // 11. If done is true, or if closeOnRejection is false, then
            //     a. Let onRejected be undefined.
            onRejected = Undefined;
        }
        else
        {
            // 12. Else,
            //     a. Let closeIterator be a new Abstract Closure that closes the iterator
            //     b. Let onRejected be CreateBuiltinFunction(closeIterator, 1, "", « »).
            var iteratorToClose = syncIteratorRecord;
            onRejected = new ClrFunction(_engine, "", (thisObj, args) =>
            {
                var error = args.At(0);
                // Close the iterator and re-throw the error
                try
                {
                    iteratorToClose.Close(CompletionType.Throw);
                }
                catch
                {
                    // If close throws, we still want to propagate the original error
                }
                // Re-throw to propagate to the promise chain
                throw new JavaScriptException(error);
            }, 1, PropertyFlag.Configurable);
        }

        // 14. Perform PerformPromiseThen(valueWrapper, onFulfilled, onRejected, promiseCapability).
        PromiseOperations.PerformPromiseThen(_engine, valueWrapper, onFulfilled, onRejected, promiseCapability);

        // 15. Return promiseCapability.[[Promise]]
        return promiseCapability.PromiseInstance;
    }
}
