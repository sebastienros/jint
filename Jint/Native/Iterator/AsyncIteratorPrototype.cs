using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asynciteratorprototype
/// </summary>
internal sealed class AsyncIteratorPrototype : Prototype
{
    internal AsyncIteratorPrototype(
        Engine engine,
        Realm realm,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag Flags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(12, checkExistingKeys: false)
        {
            [KnownKeys.Constructor] = new GetSetPropertyDescriptor(
                get: new ClrFunction(_engine, "get constructor", (_, _) => _engine.Intrinsics.AsyncIterator, 0, PropertyFlag.Configurable),
                set: new ClrFunction(_engine, "set constructor", (thisObject, arguments) =>
                {
                    SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.AsyncIterator.PrototypeObject, CommonProperties.Constructor, arguments.At(0));
                    return Undefined;
                }, 1, PropertyFlag.Configurable),
                PropertyFlag.Configurable),
            ["map"] = new(new ClrFunction(_engine, "map", Map, 1, PropertyFlag.Configurable), Flags),
            ["filter"] = new(new ClrFunction(_engine, "filter", Filter, 1, PropertyFlag.Configurable), Flags),
            ["take"] = new(new ClrFunction(_engine, "take", Take, 1, PropertyFlag.Configurable), Flags),
            ["drop"] = new(new ClrFunction(_engine, "drop", Drop, 1, PropertyFlag.Configurable), Flags),
            ["flatMap"] = new(new ClrFunction(_engine, "flatMap", FlatMap, 1, PropertyFlag.Configurable), Flags),
            ["reduce"] = new(new ClrFunction(_engine, "reduce", Reduce, 1, PropertyFlag.Configurable), Flags),
            ["toArray"] = new(new ClrFunction(_engine, "toArray", ToArray, 0, PropertyFlag.Configurable), Flags),
            ["forEach"] = new(new ClrFunction(_engine, "forEach", ForEach, 1, PropertyFlag.Configurable), Flags),
            ["some"] = new(new ClrFunction(_engine, "some", Some, 1, PropertyFlag.Configurable), Flags),
            ["every"] = new(new ClrFunction(_engine, "every", Every, 1, PropertyFlag.Configurable), Flags),
            ["find"] = new(new ClrFunction(_engine, "find", Find, 1, PropertyFlag.Configurable), Flags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(3)
        {
            [GlobalSymbolRegistry.AsyncIterator] = new(new ClrFunction(Engine, "[Symbol.asyncIterator]", AsyncIterator, 0, PropertyFlag.Configurable), Flags),
            [GlobalSymbolRegistry.AsyncDispose] = new(new ClrFunction(Engine, "[Symbol.asyncDispose]", AsyncDispose, 0, PropertyFlag.Configurable), Flags),
            [GlobalSymbolRegistry.ToStringTag] = new GetSetPropertyDescriptor(
                get: new ClrFunction(_engine, "get [Symbol.toStringTag]", (_, _) => "AsyncIterator", 0, PropertyFlag.Configurable),
                set: new ClrFunction(_engine, "set [Symbol.toStringTag]", (thisObject, arguments) =>
                {
                    SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.AsyncIterator.PrototypeObject, GlobalSymbolRegistry.ToStringTag, arguments.At(0));
                    return Undefined;
                }, 0, PropertyFlag.Configurable),
                PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-SetterThatIgnoresPrototypeProperties
    /// </summary>
    private void SetterThatIgnoresPrototypeProperties(JsValue thisValue, ObjectInstance home, JsValue p, JsValue v)
    {
        if (thisValue is not ObjectInstance objectInstance)
        {
            Throw.TypeError(_realm);
            return;
        }

        if (SameValue(thisValue, home))
        {
            Throw.TypeError(_realm);
            return;
        }

        var desc = objectInstance.GetOwnProperty(p);
        if (desc == PropertyDescriptor.Undefined)
        {
            objectInstance.CreateDataPropertyOrThrow(p, v);
        }
        else
        {
            objectInstance.Set(p, v, throwOnError: true);
        }
    }

    private static IteratorInstance.ObjectIterator GetIteratorDirect(ObjectInstance objectInstance) => new(objectInstance);

    /// <summary>
    /// Close an iterator by calling its return() method directly.
    /// </summary>
    private static void IteratorClose(ObjectInstance obj, CompletionType completionType)
    {
        var returnMethod = obj.GetMethod(CommonProperties.Return);
        if (returnMethod is null)
        {
            return;
        }

        try
        {
            returnMethod.Call(obj, Arguments.Empty);
        }
        catch when (completionType == CompletionType.Throw)
        {
            // Ignore errors from return when completion is throw
        }
    }

    /// <summary>
    /// Validates thisObject is an ObjectInstance and extracts a callable argument.
    /// Closes the iterator on validation failure.
    /// </summary>
    private ObjectInstance ValidateThisAndGetCallable(JsValue thisObject, JsValue[] arguments, string methodName, out ICallable callable)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, $"AsyncIterator.prototype.{methodName} called on non-object");
            callable = null!;
            return null!;
        }

        try
        {
            callable = GetCallable(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        return o;
    }

    /// <summary>
    /// Validates thisObject and extracts a numeric limit argument.
    /// Closes the iterator on validation failure.
    /// </summary>
    private ObjectInstance ValidateThisAndGetLimit(JsValue thisObject, JsValue[] arguments, string methodName, out long limit)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, $"AsyncIterator.prototype.{methodName} called on non-object");
            limit = 0;
            return null!;
        }

        double numLimit;
        try
        {
            numLimit = TypeConverter.ToNumber(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "Invalid limit");
            limit = 0;
            return null!;
        }

        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "Invalid limit");
            limit = 0;
            return null!;
        }

        limit = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return o;
    }

    // ---- Shared async iteration infrastructure ----

    /// <summary>
    /// Calls the underlying iterator's next() and normalizes the result as a promise.
    /// Returns null and rejects the capability on failure.
    /// </summary>
    private JsPromise? CallIteratorNext(IteratorInstance.ObjectIterator iterated, PromiseCapability promiseCapability)
    {
        try
        {
            var target = iterated.Instance;
            var nextMethod = target.Get(CommonProperties.Next);
            if (nextMethod is not ICallable callable)
            {
                Throw.TypeError(_realm, "Iterator does not have a next method");
                return null;
            }

            var result = callable.Call(target, Arguments.Empty);
            return (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(result);
        }
        catch (JavaScriptException ex)
        {
            promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
            return null;
        }
    }

    /// <summary>
    /// Shared async iteration loop. Calls next() on the iterator, extracts done/value,
    /// and invokes the callback for each value. The callback handles method-specific logic
    /// and calls continueLoop() to iterate further, or resolves/rejects the capability to finish.
    /// </summary>
    private void AsyncIterateLoop(
        IteratorInstance.ObjectIterator iterated,
        PromiseCapability promiseCapability,
        Action<JsValue, PromiseCapability> onDone,
        Action<JsValue, int, PromiseCapability> onValue,
        int counter = 0)
    {
        var nextPromise = CallIteratorNext(iterated, promiseCapability);
        if (nextPromise is null)
        {
            return; // Already rejected
        }

        var capturedCounter = counter;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            try
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    promiseCapability.Reject.Call(Undefined, new JsValue[] { _engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object") });
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                if (done)
                {
                    onDone(Undefined, promiseCapability);
                    return Undefined;
                }

                var value = iterResultObj.Get(CommonProperties.Value);
                onValue(value, capturedCounter, promiseCapability);
                return Undefined;
            }
            catch (JavaScriptException ex)
            {
                promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
                return Undefined;
            }
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            promiseCapability.Reject.Call(Undefined, new[] { args.At(0) });
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, onRejected, null!);
    }

    /// <summary>
    /// Chains on a promise result from a callback (predicate/reducer/procedure), then invokes
    /// the handler with the resolved value. Rejects and closes the iterator on failure.
    /// </summary>
    private void ChainCallbackResult(
        IteratorInstance.ObjectIterator iterated,
        JsValue callbackResult,
        PromiseCapability promiseCapability,
        Action<JsValue> onResolved)
    {
        var resultPromise = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(callbackResult);

        var onFulfilled = new ClrFunction(_engine, "", (_, innerArgs) =>
        {
            onResolved(innerArgs.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, innerArgs) =>
        {
            try { iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
            promiseCapability.Reject.Call(Undefined, new[] { innerArgs.At(0) });
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, resultPromise, onFulfilled, onRejected, null!);
    }

    // ---- Symbol methods ----

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciteratorprototype-asynciterator
    /// </summary>
    private static JsValue AsyncIterator(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%asynciteratorprototype%-@@asyncDispose
    /// </summary>
    private JsValue AsyncDispose(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject;
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        ICallable? returnMethod;
        try
        {
            returnMethod = o.IsObject() ? o.AsObject().GetMethod(CommonProperties.Return) : null;
        }
        catch (JavaScriptException ex)
        {
            promiseCapability.Reject.Call(Undefined, ex.Error);
            return promiseCapability.PromiseInstance;
        }

        if (returnMethod is null)
        {
            promiseCapability.Resolve.Call(Undefined, Undefined);
        }
        else
        {
            JsValue result;
            try
            {
                result = returnMethod.Call(o, Undefined);
            }
            catch (JavaScriptException ex)
            {
                promiseCapability.Reject.Call(Undefined, ex.Error);
                return promiseCapability.PromiseInstance;
            }

            JsPromise resultWrapper;
            try
            {
                resultWrapper = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(result);
            }
            catch (JavaScriptException ex)
            {
                promiseCapability.Reject.Call(Undefined, ex.Error);
                return promiseCapability.PromiseInstance;
            }

            var onFulfilled = new ClrFunction(_engine, "", (_, _) => Undefined, 1, PropertyFlag.Configurable);
            PromiseOperations.PerformPromiseThen(_engine, resultWrapper, onFulfilled, null!, promiseCapability);
        }

        return promiseCapability.PromiseInstance;
    }

    // ---- Helper-returning methods ----

    private AsyncMapIterator Map(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "map", out var mapper);
        return new AsyncMapIterator(_engine, GetIteratorDirect(o), mapper);
    }

    private AsyncFilterIterator Filter(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "filter", out var predicate);
        return new AsyncFilterIterator(_engine, GetIteratorDirect(o), predicate);
    }

    private AsyncTakeIterator Take(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetLimit(thisObject, arguments, "take", out var limit);
        return new AsyncTakeIterator(_engine, GetIteratorDirect(o), limit);
    }

    private AsyncDropIterator Drop(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetLimit(thisObject, arguments, "drop", out var limit);
        return new AsyncDropIterator(_engine, GetIteratorDirect(o), limit);
    }

    private AsyncFlatMapIterator FlatMap(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "flatMap", out var mapper);
        return new AsyncFlatMapIterator(_engine, GetIteratorDirect(o), mapper);
    }

    // ---- Consuming methods (return promises) ----

    private JsValue Reduce(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "reduce", out var reducer);
        var iterated = GetIteratorDirect(o);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        var hasInitialValue = arguments.Length >= 2;
        var accumulator = hasInitialValue ? arguments.At(1) : Undefined;

        AsyncReduceLoop(iterated, reducer, accumulator, hasInitialValue, 0, promiseCapability);
        return promiseCapability.PromiseInstance;
    }

    private void AsyncReduceLoop(
        IteratorInstance.ObjectIterator iterated,
        ICallable reducer,
        JsValue accumulator,
        bool hasAccumulator,
        int counter,
        PromiseCapability promiseCapability)
    {
        var capturedAccumulator = accumulator;
        var capturedHasAccumulator = hasAccumulator;

        AsyncIterateLoop(iterated, promiseCapability,
            onDone: (_, cap) =>
            {
                if (!capturedHasAccumulator)
                {
                    cap.Reject.Call(Undefined, new JsValue[] { _engine.Realm.Intrinsics.TypeError.Construct("Reduce of empty iterator with no initial value") });
                }
                else
                {
                    cap.Resolve.Call(Undefined, new[] { capturedAccumulator });
                }
            },
            onValue: (value, idx, cap) =>
            {
                if (!capturedHasAccumulator)
                {
                    AsyncReduceLoop(iterated, reducer, value, true, idx + 1, cap);
                    return;
                }

                JsValue newAccumulator;
                try
                {
                    newAccumulator = reducer.Call(Undefined, new[] { capturedAccumulator, value, (JsValue) idx });
                }
                catch (JavaScriptException ex)
                {
                    try { iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
                    cap.Reject.Call(Undefined, new[] { ex.Error });
                    return;
                }

                ChainCallbackResult(iterated, newAccumulator, cap, resolved =>
                {
                    AsyncReduceLoop(iterated, reducer, resolved, true, idx + 1, cap);
                });
            },
            counter: counter);
    }

    private JsValue ToArray(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "AsyncIterator.prototype.toArray called on non-object");
            return Undefined;
        }

        var iterated = GetIteratorDirect(o);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        var items = new List<JsValue>();

        AsyncCollectLoop(iterated, items, promiseCapability);
        return promiseCapability.PromiseInstance;
    }

    private void AsyncCollectLoop(
        IteratorInstance.ObjectIterator iterated,
        List<JsValue> items,
        PromiseCapability promiseCapability)
    {
        AsyncIterateLoop(iterated, promiseCapability,
            onDone: (_, cap) =>
            {
                var array = new JsArray(_engine, items.ToArray());
                cap.Resolve.Call(Undefined, new JsValue[] { array });
            },
            onValue: (value, _, _) =>
            {
                items.Add(value);
                AsyncCollectLoop(iterated, items, promiseCapability);
            });
    }

    private JsValue ForEach(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "forEach", out var procedure);
        var iterated = GetIteratorDirect(o);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        AsyncConsumeLoop(iterated, procedure, promiseCapability,
            onDone: cap => cap.Resolve.Call(Undefined, Undefined),
            shouldStop: null);

        return promiseCapability.PromiseInstance;
    }

    private JsValue Some(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "some", out var predicate);
        var iterated = GetIteratorDirect(o);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        AsyncConsumeLoop(iterated, predicate, promiseCapability,
            onDone: cap => cap.Resolve.Call(Undefined, new JsValue[] { JsBoolean.False }),
            shouldStop: (resolved, value, cap) =>
            {
                if (!TypeConverter.ToBoolean(resolved)) return false;
                try { iterated.Close(CompletionType.Normal); } catch { /* ignore */ }
                cap.Resolve.Call(Undefined, new JsValue[] { JsBoolean.True });
                return true;
            });

        return promiseCapability.PromiseInstance;
    }

    private JsValue Every(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "every", out var predicate);
        var iterated = GetIteratorDirect(o);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        AsyncConsumeLoop(iterated, predicate, promiseCapability,
            onDone: cap => cap.Resolve.Call(Undefined, new JsValue[] { JsBoolean.True }),
            shouldStop: (resolved, value, cap) =>
            {
                if (TypeConverter.ToBoolean(resolved)) return false;
                try { iterated.Close(CompletionType.Normal); } catch { /* ignore */ }
                cap.Resolve.Call(Undefined, new JsValue[] { JsBoolean.False });
                return true;
            });

        return promiseCapability.PromiseInstance;
    }

    private JsValue Find(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateThisAndGetCallable(thisObject, arguments, "find", out var predicate);
        var iterated = GetIteratorDirect(o);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        AsyncConsumeLoop(iterated, predicate, promiseCapability,
            onDone: cap => cap.Resolve.Call(Undefined, Undefined),
            shouldStop: (resolved, value, cap) =>
            {
                if (!TypeConverter.ToBoolean(resolved)) return false;
                try { iterated.Close(CompletionType.Normal); } catch { /* ignore */ }
                cap.Resolve.Call(Undefined, new[] { value });
                return true;
            });

        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// Shared async consumption loop for forEach/some/every/find.
    /// Calls the callback for each value, chains on its result. If shouldStop returns true,
    /// the loop terminates (shouldStop is responsible for resolving). Otherwise continues.
    /// </summary>
    private void AsyncConsumeLoop(
        IteratorInstance.ObjectIterator iterated,
        ICallable callback,
        PromiseCapability promiseCapability,
        Action<PromiseCapability> onDone,
        Func<JsValue, JsValue, PromiseCapability, bool>? shouldStop,
        int counter = 0)
    {
        AsyncIterateLoop(iterated, promiseCapability,
            onDone: (_, cap) => onDone(cap),
            onValue: (value, idx, cap) =>
            {
                JsValue callResult;
                try
                {
                    callResult = callback.Call(Undefined, new[] { value, (JsValue) idx });
                }
                catch (JavaScriptException ex)
                {
                    try { iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
                    cap.Reject.Call(Undefined, new[] { ex.Error });
                    return;
                }

                ChainCallbackResult(iterated, callResult, cap, resolved =>
                {
                    if (shouldStop is not null && shouldStop(resolved, value, cap))
                    {
                        return;
                    }

                    AsyncConsumeLoop(iterated, callback, cap, onDone, shouldStop, idx + 1);
                });
            },
            counter: counter);
    }
}
