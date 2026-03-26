using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// Base class for async iterator helper instances (map, filter, take, drop, flatMap).
/// https://tc39.es/ecma262/#sec-asynciteratorhelper-objects
/// </summary>
internal abstract class AsyncIteratorHelper : ObjectInstance
{
    protected readonly IteratorInstance.ObjectIterator Iterated;
    protected GeneratorState State;
    protected int Counter;
    protected bool Exhausted;

    protected AsyncIteratorHelper(Engine engine, IteratorInstance.ObjectIterator iterated) : base(engine)
    {
        Iterated = iterated;
        State = GeneratorState.SuspendedStart;
        Counter = 0;
        Exhausted = false;
        _prototype = engine.Realm.Intrinsics.AsyncIteratorHelperPrototype;
    }

    /// <summary>
    /// Called by AsyncIteratorHelperPrototype.next() to get a promise of the next value.
    /// </summary>
    public JsValue Next()
    {
        if (State == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
            return Undefined;
        }

        if (State == GeneratorState.Completed)
        {
            return CreateResolvedIteratorResultPromise(Undefined, done: true);
        }

        State = GeneratorState.Executing;

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        try
        {
            ExecuteStep(promiseCapability);
        }
        catch (JavaScriptException ex)
        {
            State = GeneratorState.Completed;
            if (!Exhausted)
            {
                Exhausted = true;
                try { Iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
            }
            promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
        }

        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// Called by AsyncIteratorHelperPrototype.return() to close the helper.
    /// </summary>
    public virtual JsValue Return()
    {
        State = GeneratorState.Completed;

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        if (!Exhausted)
        {
            Exhausted = true;
            try
            {
                var iterator = Iterated.Instance;
                var returnMethod = iterator.GetMethod(CommonProperties.Return);
                if (returnMethod is not null)
                {
                    var returnResult = returnMethod.Call(iterator, Arguments.Empty);
                    var returnPromise = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(returnResult);

                    var onFulfilled = new ClrFunction(_engine, "", (_, _) =>
                    {
                        promiseCapability.Resolve.Call(Undefined, new JsValue[] { CreateIteratorResult(Undefined, done: true) });
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    var onRejected = new ClrFunction(_engine, "", (_, args) =>
                    {
                        promiseCapability.Reject.Call(Undefined, new[] { args.At(0) });
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    PromiseOperations.PerformPromiseThen(_engine, returnPromise, onFulfilled, onRejected, null!);
                    return promiseCapability.PromiseInstance;
                }
            }
            catch (JavaScriptException ex)
            {
                promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
                return promiseCapability.PromiseInstance;
            }
        }

        var doneResult = CreateIteratorResult(Undefined, done: true);
        promiseCapability.Resolve.Call(Undefined, new JsValue[] { doneResult });
        return promiseCapability.PromiseInstance;
    }

    protected abstract void ExecuteStep(PromiseCapability promiseCapability);

    /// <summary>
    /// Calls the underlying iterator's next() and normalizes the result as a promise.
    /// </summary>
    protected JsPromise CallIteratorNext()
    {
        var target = Iterated.Instance;
        var nextMethod = target.Get(CommonProperties.Next);
        if (nextMethod is not ICallable callable)
        {
            Throw.TypeError(_engine.Realm, "Iterator does not have a next method");
            return null!;
        }

        var result = callable.Call(target, Arguments.Empty);
        return (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(result);
    }

    protected void CloseIterator(CompletionType completionType)
    {
        Iterated.Close(completionType);
    }

    protected ObjectInstance CreateIteratorResult(JsValue value, bool done)
    {
        return IteratorResult.CreateValueIteratorPosition(_engine, value, JsBoolean.Create(done));
    }

    protected JsValue CreateResolvedIteratorResultPromise(JsValue value, bool done)
    {
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        var result = CreateIteratorResult(value, done);
        promiseCapability.Resolve.Call(Undefined, new JsValue[] { result });
        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// Helper to resolve promiseCapability with an iterator result {value, done: false},
    /// setting state to SuspendedYield.
    /// </summary>
    protected void ResolveYield(PromiseCapability promiseCapability, JsValue value)
    {
        State = GeneratorState.SuspendedYield;
        promiseCapability.Resolve.Call(Undefined, new JsValue[] { CreateIteratorResult(value, done: false) });
    }

    /// <summary>
    /// Helper to resolve promiseCapability with {value: undefined, done: true},
    /// setting state to Completed.
    /// </summary>
    protected void ResolveDone(PromiseCapability promiseCapability)
    {
        State = GeneratorState.Completed;
        Exhausted = true;
        promiseCapability.Resolve.Call(Undefined, new JsValue[] { CreateIteratorResult(Undefined, done: true) });
    }

    /// <summary>
    /// Helper to reject promiseCapability with an error.
    /// </summary>
    protected void RejectWithError(PromiseCapability promiseCapability, JsValue error)
    {
        State = GeneratorState.Completed;
        Exhausted = true;
        try { Iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
        promiseCapability.Reject.Call(Undefined, new[] { error });
    }
}

/// <summary>
/// Async iterator helper for map(mapper) - transforms each element.
/// </summary>
internal sealed class AsyncMapIterator : AsyncIteratorHelper
{
    private readonly ICallable _mapper;

    public AsyncMapIterator(Engine engine, IteratorInstance.ObjectIterator iterated, ICallable mapper) : base(engine, iterated)
    {
        _mapper = mapper;
    }

    protected override void ExecuteStep(PromiseCapability promiseCapability)
    {
        var nextPromise = CallIteratorNext();
        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            try
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    self.RejectWithError(promiseCapability, self._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                if (done)
                {
                    self.ResolveDone(promiseCapability);
                    return Undefined;
                }

                var value = iterResultObj.Get(CommonProperties.Value);
                var counter = self.Counter;
                self.Counter++;

                JsValue mapped;
                try
                {
                    mapped = self._mapper.Call(Undefined, new[] { value, (JsValue) counter });
                }
                catch (JavaScriptException ex)
                {
                    self.RejectWithError(promiseCapability, ex.Error);
                    return Undefined;
                }

                // The mapped value could be a promise, so resolve it
                var mappedPromise = (JsPromise) self._engine.Realm.Intrinsics.Promise.PromiseResolve(mapped);

                var onMappedFulfilled = new ClrFunction(self._engine, "", (_, innerArgs) =>
                {
                    self.ResolveYield(promiseCapability, innerArgs.At(0));
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                var onMappedRejected = new ClrFunction(self._engine, "", (_, innerArgs) =>
                {
                    self.RejectWithError(promiseCapability, innerArgs.At(0));
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(self._engine, mappedPromise, onMappedFulfilled, onMappedRejected, null!);
                return Undefined;
            }
            catch (JavaScriptException ex)
            {
                self.RejectWithError(promiseCapability, ex.Error);
                return Undefined;
            }
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            self.RejectWithError(promiseCapability, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, onRejected, null!);
    }
}

/// <summary>
/// Async iterator helper for filter(predicate) - returns elements matching the predicate.
/// </summary>
internal sealed class AsyncFilterIterator : AsyncIteratorHelper
{
    private readonly ICallable _predicate;

    public AsyncFilterIterator(Engine engine, IteratorInstance.ObjectIterator iterated, ICallable predicate) : base(engine, iterated)
    {
        _predicate = predicate;
    }

    protected override void ExecuteStep(PromiseCapability promiseCapability)
    {
        FilterNextStep(promiseCapability);
    }

    private void FilterNextStep(PromiseCapability promiseCapability)
    {
        var nextPromise = CallIteratorNext();
        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            try
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    self.RejectWithError(promiseCapability, self._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                if (done)
                {
                    self.ResolveDone(promiseCapability);
                    return Undefined;
                }

                var value = iterResultObj.Get(CommonProperties.Value);
                var counter = self.Counter;
                self.Counter++;

                JsValue selected;
                try
                {
                    selected = self._predicate.Call(Undefined, new[] { value, (JsValue) counter });
                }
                catch (JavaScriptException ex)
                {
                    self.RejectWithError(promiseCapability, ex.Error);
                    return Undefined;
                }

                var selectedPromise = (JsPromise) self._engine.Realm.Intrinsics.Promise.PromiseResolve(selected);

                var onSelectedFulfilled = new ClrFunction(self._engine, "", (_, innerArgs) =>
                {
                    var resolvedSelected = innerArgs.At(0);
                    if (TypeConverter.ToBoolean(resolvedSelected))
                    {
                        self.ResolveYield(promiseCapability, value);
                    }
                    else
                    {
                        // Value didn't pass filter, try the next one
                        self.FilterNextStep(promiseCapability);
                    }
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                var onSelectedRejected = new ClrFunction(self._engine, "", (_, innerArgs) =>
                {
                    self.RejectWithError(promiseCapability, innerArgs.At(0));
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(self._engine, selectedPromise, onSelectedFulfilled, onSelectedRejected, null!);
                return Undefined;
            }
            catch (JavaScriptException ex)
            {
                self.RejectWithError(promiseCapability, ex.Error);
                return Undefined;
            }
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            self.RejectWithError(promiseCapability, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, onRejected, null!);
    }
}

/// <summary>
/// Async iterator helper for take(limit) - returns the first N elements.
/// </summary>
internal sealed class AsyncTakeIterator : AsyncIteratorHelper
{
    private readonly long _limit;
    private long _taken;

    public AsyncTakeIterator(Engine engine, IteratorInstance.ObjectIterator iterated, long limit) : base(engine, iterated)
    {
        _limit = limit;
        _taken = 0;
    }

    protected override void ExecuteStep(PromiseCapability promiseCapability)
    {
        if (_taken >= _limit)
        {
            State = GeneratorState.Completed;
            Exhausted = true;
            try { Iterated.Close(CompletionType.Normal); } catch { /* ignore */ }
            promiseCapability.Resolve.Call(Undefined, new JsValue[] { CreateIteratorResult(Undefined, done: true) });
            return;
        }

        var nextPromise = CallIteratorNext();
        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            try
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    self.RejectWithError(promiseCapability, self._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                if (done)
                {
                    self.ResolveDone(promiseCapability);
                    return Undefined;
                }

                var value = iterResultObj.Get(CommonProperties.Value);
                self._taken++;
                self.ResolveYield(promiseCapability, value);
                return Undefined;
            }
            catch (JavaScriptException ex)
            {
                self.RejectWithError(promiseCapability, ex.Error);
                return Undefined;
            }
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            self.RejectWithError(promiseCapability, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, onRejected, null!);
    }
}

/// <summary>
/// Async iterator helper for drop(limit) - skips the first N elements.
/// </summary>
internal sealed class AsyncDropIterator : AsyncIteratorHelper
{
    private long _remaining;
    private bool _dropping;

    public AsyncDropIterator(Engine engine, IteratorInstance.ObjectIterator iterated, long limit) : base(engine, iterated)
    {
        _remaining = limit;
        _dropping = limit > 0;
    }

    protected override void ExecuteStep(PromiseCapability promiseCapability)
    {
        DropStep(promiseCapability);
    }

    private void DropStep(PromiseCapability promiseCapability)
    {
        var nextPromise = CallIteratorNext();
        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            try
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    self.RejectWithError(promiseCapability, self._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                if (done)
                {
                    self.ResolveDone(promiseCapability);
                    return Undefined;
                }

                if (self._dropping)
                {
                    self._remaining--;
                    if (self._remaining <= 0)
                    {
                        self._dropping = false;
                    }
                    // Skip this value, continue dropping
                    self.DropStep(promiseCapability);
                    return Undefined;
                }

                var value = iterResultObj.Get(CommonProperties.Value);
                self.ResolveYield(promiseCapability, value);
                return Undefined;
            }
            catch (JavaScriptException ex)
            {
                self.RejectWithError(promiseCapability, ex.Error);
                return Undefined;
            }
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            self.RejectWithError(promiseCapability, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, onRejected, null!);
    }
}

/// <summary>
/// Async iterator helper for flatMap(mapper) - maps and flattens one level.
/// </summary>
internal sealed class AsyncFlatMapIterator : AsyncIteratorHelper
{
    private readonly ICallable _mapper;
    private IteratorInstance.ObjectIterator? _innerIterator;

    public AsyncFlatMapIterator(Engine engine, IteratorInstance.ObjectIterator iterated, ICallable mapper) : base(engine, iterated)
    {
        _mapper = mapper;
    }

    public override JsValue Return()
    {
        State = GeneratorState.Completed;

        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        try
        {
            if (_innerIterator is not null)
            {
                var inner = _innerIterator;
                _innerIterator = null;
                inner.Close(CompletionType.Return);
            }

            if (!Exhausted)
            {
                Exhausted = true;
                var iterator = Iterated.Instance;
                var returnMethod = iterator.GetMethod(CommonProperties.Return);
                if (returnMethod is not null)
                {
                    var returnResult = returnMethod.Call(iterator, Arguments.Empty);
                    var returnPromise = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(returnResult);

                    var onFulfilled = new ClrFunction(_engine, "", (_, _) =>
                    {
                        promiseCapability.Resolve.Call(Undefined, new JsValue[] { CreateIteratorResult(Undefined, done: true) });
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    var onRejected = new ClrFunction(_engine, "", (_, args) =>
                    {
                        promiseCapability.Reject.Call(Undefined, new[] { args.At(0) });
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    PromiseOperations.PerformPromiseThen(_engine, returnPromise, onFulfilled, onRejected, null!);
                    return promiseCapability.PromiseInstance;
                }
            }
        }
        catch (JavaScriptException ex)
        {
            promiseCapability.Reject.Call(Undefined, new[] { ex.Error });
            return promiseCapability.PromiseInstance;
        }

        promiseCapability.Resolve.Call(Undefined, new JsValue[] { CreateIteratorResult(Undefined, done: true) });
        return promiseCapability.PromiseInstance;
    }

    protected override void ExecuteStep(PromiseCapability promiseCapability)
    {
        FlatMapStep(promiseCapability);
    }

    private void FlatMapStep(PromiseCapability promiseCapability)
    {
        // If we have an inner iterator, consume from it first
        if (_innerIterator is not null)
        {
            var innerTarget = _innerIterator.Instance;
            var innerNextMethod = innerTarget.Get(CommonProperties.Next);
            if (innerNextMethod is not ICallable innerCallable)
            {
                _innerIterator = null;
                FlatMapStep(promiseCapability);
                return;
            }

            var innerResult = innerCallable.Call(innerTarget, Arguments.Empty);
            var innerPromise = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(innerResult);
            var self = this;

            var onInnerFulfilled = new ClrFunction(_engine, "", (_, args) =>
            {
                try
                {
                    var iterResult = args.At(0);
                    if (iterResult is not ObjectInstance iterResultObj)
                    {
                        self._innerIterator = null;
                        self.FlatMapStep(promiseCapability);
                        return Undefined;
                    }

                    var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                    if (done)
                    {
                        self._innerIterator = null;
                        self.FlatMapStep(promiseCapability);
                        return Undefined;
                    }

                    var value = iterResultObj.Get(CommonProperties.Value);
                    self.ResolveYield(promiseCapability, value);
                    return Undefined;
                }
                catch (JavaScriptException ex)
                {
                    self._innerIterator = null;
                    self.RejectWithError(promiseCapability, ex.Error);
                    return Undefined;
                }
            }, 1, PropertyFlag.Configurable);

            var onInnerRejected = new ClrFunction(_engine, "", (_, args) =>
            {
                self._innerIterator = null;
                self.RejectWithError(promiseCapability, args.At(0));
                return Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(_engine, innerPromise, onInnerFulfilled, onInnerRejected, null!);
            return;
        }

        // Get next from outer iterator
        var nextPromise = CallIteratorNext();
        var outerSelf = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            try
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    outerSelf.RejectWithError(promiseCapability, outerSelf._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                if (done)
                {
                    outerSelf.ResolveDone(promiseCapability);
                    return Undefined;
                }

                var value = iterResultObj.Get(CommonProperties.Value);
                var counter = outerSelf.Counter;
                outerSelf.Counter++;

                JsValue mapped;
                try
                {
                    mapped = outerSelf._mapper.Call(Undefined, new[] { value, (JsValue) counter });
                }
                catch (JavaScriptException ex)
                {
                    outerSelf.RejectWithError(promiseCapability, ex.Error);
                    return Undefined;
                }

                var mappedPromise = (JsPromise) outerSelf._engine.Realm.Intrinsics.Promise.PromiseResolve(mapped);

                var onMappedFulfilled = new ClrFunction(outerSelf._engine, "", (_, innerArgs) =>
                {
                    try
                    {
                        var resolvedMapped = innerArgs.At(0);

                        if (resolvedMapped is not ObjectInstance mappedObj)
                        {
                            outerSelf.RejectWithError(promiseCapability, outerSelf._engine.Realm.Intrinsics.TypeError.Construct("flatMap mapper must return an iterable or iterator"));
                            return Undefined;
                        }

                        // Try @@asyncIterator first, then @@iterator, then use directly
                        ObjectInstance innerIteratorObj;
                        var asyncMethod = mappedObj.GetMethod(Symbol.GlobalSymbolRegistry.AsyncIterator);
                        if (asyncMethod is not null)
                        {
                            var asyncResult = asyncMethod.Call(mappedObj);
                            if (asyncResult is not ObjectInstance asyncObj)
                            {
                                outerSelf.RejectWithError(promiseCapability, outerSelf._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                                return Undefined;
                            }
                            innerIteratorObj = asyncObj;
                        }
                        else
                        {
                            var syncMethod = mappedObj.GetMethod(Symbol.GlobalSymbolRegistry.Iterator);
                            if (syncMethod is not null)
                            {
                                var syncResult = syncMethod.Call(mappedObj);
                                if (syncResult is not ObjectInstance syncObj)
                                {
                                    outerSelf.RejectWithError(promiseCapability, outerSelf._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                                    return Undefined;
                                }
                                innerIteratorObj = syncObj;
                            }
                            else
                            {
                                innerIteratorObj = mappedObj;
                            }
                        }

                        outerSelf._innerIterator = new IteratorInstance.ObjectIterator(innerIteratorObj);
                        outerSelf.FlatMapStep(promiseCapability);
                        return Undefined;
                    }
                    catch (JavaScriptException ex)
                    {
                        outerSelf.RejectWithError(promiseCapability, ex.Error);
                        return Undefined;
                    }
                }, 1, PropertyFlag.Configurable);

                var onMappedRejected = new ClrFunction(outerSelf._engine, "", (_, innerArgs) =>
                {
                    outerSelf.RejectWithError(promiseCapability, innerArgs.At(0));
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(outerSelf._engine, mappedPromise, onMappedFulfilled, onMappedRejected, null!);
                return Undefined;
            }
            catch (JavaScriptException ex)
            {
                outerSelf.RejectWithError(promiseCapability, ex.Error);
                return Undefined;
            }
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            outerSelf.RejectWithError(promiseCapability, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, onRejected, null!);
    }
}
