using System.Diagnostics.CodeAnalysis;
using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// Base class for async iterator helper instances (map, filter, take, drop, flatMap).
/// https://tc39.es/proposal-async-iterator-helpers/#sec-async-iterator-helper-objects
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
            // A step that fails synchronously is one IfAbruptCloseAsyncIterator guards, flatMap's inner IteratorNext: the
            // source's own step rejects through CallIteratorNext without throwing, and leaves the source open.
            State = GeneratorState.Completed;
            if (!Exhausted)
            {
                Exhausted = true;
                try { Iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
            }
            promiseCapability.Reject(ex.Error);
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
                        promiseCapability.Resolve(CreateIteratorResult(Undefined, done: true));
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    var onRejected = new ClrFunction(_engine, "", (_, args) =>
                    {
                        promiseCapability.Reject(args.At(0));
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    PromiseOperations.PerformPromiseThen(_engine, returnPromise, onFulfilled, onRejected, null!);
                    return promiseCapability.PromiseInstance;
                }
            }
            catch (JavaScriptException ex)
            {
                promiseCapability.Reject(ex.Error);
                return promiseCapability.PromiseInstance;
            }
        }

        var doneResult = CreateIteratorResult(Undefined, done: true);
        promiseCapability.Resolve(doneResult);
        return promiseCapability.PromiseInstance;
    }

    protected abstract void ExecuteStep(PromiseCapability promiseCapability);

    /// <summary>
    /// The helper's <c>? Await(? IteratorNext(iterated))</c>
    /// (https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.map): calls the [[NextMethod]] that
    /// GetIteratorDirect read once (https://tc39.es/ecma262/#sec-getiteratordirect) and normalizes the result as a
    /// promise. It never reads <c>next</c> again, so a getter on it runs once and a <c>next</c> replaced mid-iteration is
    /// not called (sebastienros/jint#4419). Answers null when the call threw, having rejected the capability through
    /// <see cref="RejectSourceStep"/>.
    /// </summary>
    protected JsPromise? CallIteratorNext(PromiseCapability promiseCapability)
    {
        try
        {
            var result = Iterated.CallNextMethod();
            return (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(result);
        }
        catch (JavaScriptException ex)
        {
            RejectSourceStep(promiseCapability, ex.Error);
            return null;
        }
    }

    /// <summary>
    /// The helper's <c>? IteratorComplete(next)</c> on what the source's step settled to. Answers true, with the result,
    /// when the source has a value. Otherwise the helper is over: a done result completes it, and one that is not an
    /// object, or whose <c>done</c> throws, rejects it through <see cref="RejectSourceStep"/>.
    /// </summary>
    protected bool TryCompleteStep(JsValue next, PromiseCapability promiseCapability, [NotNullWhen(true)] out ObjectInstance? result)
    {
        result = next as ObjectInstance;
        if (result is null)
        {
            RejectSourceStep(promiseCapability, _engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
            return false;
        }

        bool done;
        try
        {
            done = TypeConverter.ToBoolean(result.Get(CommonProperties.Done));
        }
        catch (JavaScriptException ex)
        {
            RejectSourceStep(promiseCapability, ex.Error);
            result = null;
            return false;
        }

        if (done)
        {
            ResolveDone(promiseCapability);
            result = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// The helper's <c>? IteratorValue(next)</c>. Answers false when the <c>value</c> getter threw, having rejected the
    /// capability through <see cref="RejectSourceStep"/>.
    /// </summary>
    protected bool TryGetStepValue(ObjectInstance result, PromiseCapability promiseCapability, out JsValue value)
    {
        try
        {
            value = result.Get(CommonProperties.Value);
            return true;
        }
        catch (JavaScriptException ex)
        {
            RejectSourceStep(promiseCapability, ex.Error);
            value = Undefined;
            return false;
        }
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
        promiseCapability.Resolve(result);
        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// Helper to resolve promiseCapability with an iterator result {value, done: false},
    /// setting state to SuspendedYield.
    /// </summary>
    protected void ResolveYield(PromiseCapability promiseCapability, JsValue value)
    {
        State = GeneratorState.SuspendedYield;
        promiseCapability.Resolve(CreateIteratorResult(value, done: false));
    }

    /// <summary>
    /// Helper to resolve promiseCapability with {value: undefined, done: true},
    /// setting state to Completed.
    /// </summary>
    protected void ResolveDone(PromiseCapability promiseCapability)
    {
        State = GeneratorState.Completed;
        Exhausted = true;
        promiseCapability.Resolve(CreateIteratorResult(Undefined, done: true));
    }

    /// <summary>
    /// IfAbruptCloseAsyncIterator(value, iterated)
    /// (https://tc39.es/proposal-async-iterator-helpers/#sec-ifabruptcloseasynciterator), for a failure of what the
    /// helper calls with a value — the mapper or predicate, the await of what it answered, flatMap's inner iterator:
    /// closes the source and rejects with the error, completing the helper.
    /// </summary>
    protected void RejectWithError(PromiseCapability promiseCapability, JsValue error)
    {
        State = GeneratorState.Completed;
        Exhausted = true;
        try { Iterated.Close(CompletionType.Throw); } catch { /* ignore */ }
        promiseCapability.Reject(error);
    }

    /// <summary>
    /// A failure of the source's own step — its <c>next</c> throwing, the promise it answered rejecting or settling to
    /// something other than an object, its <c>done</c> or <c>value</c> throwing. Those are <c>?</c> steps, so the error
    /// completes the helper as it is, and the source is not closed, then or by a later <c>return()</c>
    /// (sebastienros/jint#4439).
    /// </summary>
    protected void RejectSourceStep(PromiseCapability promiseCapability, JsValue error)
    {
        State = GeneratorState.Completed;
        Exhausted = true;
        promiseCapability.Reject(error);
    }

    /// <summary>
    /// The reaction to the source's step promise rejecting: <see cref="RejectSourceStep"/>.
    /// </summary>
    protected ClrFunction CreateSourceStepRejectedHandler(PromiseCapability promiseCapability)
    {
        return new ClrFunction(_engine, "", (_, args) =>
        {
            RejectSourceStep(promiseCapability, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);
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
        var nextPromise = CallIteratorNext(promiseCapability);
        if (nextPromise is null)
        {
            return;
        }

        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            if (!self.TryCompleteStep(args.At(0), promiseCapability, out var iterResultObj)
                || !self.TryGetStepValue(iterResultObj, promiseCapability, out var value))
            {
                return Undefined;
            }

            try
            {
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

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, CreateSourceStepRejectedHandler(promiseCapability), null!);
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
        var nextPromise = CallIteratorNext(promiseCapability);
        if (nextPromise is null)
        {
            return;
        }

        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            if (!self.TryCompleteStep(args.At(0), promiseCapability, out var iterResultObj)
                || !self.TryGetStepValue(iterResultObj, promiseCapability, out var value))
            {
                return Undefined;
            }

            try
            {
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

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, CreateSourceStepRejectedHandler(promiseCapability), null!);
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
            promiseCapability.Resolve(CreateIteratorResult(Undefined, done: true));
            return;
        }

        var nextPromise = CallIteratorNext(promiseCapability);
        if (nextPromise is null)
        {
            return;
        }

        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            if (self.TryCompleteStep(args.At(0), promiseCapability, out var iterResultObj)
                && self.TryGetStepValue(iterResultObj, promiseCapability, out var value))
            {
                self._taken++;
                self.ResolveYield(promiseCapability, value);
            }

            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, CreateSourceStepRejectedHandler(promiseCapability), null!);
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
        var nextPromise = CallIteratorNext(promiseCapability);
        if (nextPromise is null)
        {
            return;
        }

        var self = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            if (!self.TryCompleteStep(args.At(0), promiseCapability, out var iterResultObj))
            {
                return Undefined;
            }

            if (self._dropping)
            {
                self._remaining--;
                if (self._remaining <= 0)
                {
                    self._dropping = false;
                }

                // Skip this value, continue dropping: a dropped step reads only `done`.
                self.DropStep(promiseCapability);
                return Undefined;
            }

            if (self.TryGetStepValue(iterResultObj, promiseCapability, out var value))
            {
                self.ResolveYield(promiseCapability, value);
            }

            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, CreateSourceStepRejectedHandler(promiseCapability), null!);
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
                        promiseCapability.Resolve(CreateIteratorResult(Undefined, done: true));
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    var onRejected = new ClrFunction(_engine, "", (_, args) =>
                    {
                        promiseCapability.Reject(args.At(0));
                        return Undefined;
                    }, 1, PropertyFlag.Configurable);

                    PromiseOperations.PerformPromiseThen(_engine, returnPromise, onFulfilled, onRejected, null!);
                    return promiseCapability.PromiseInstance;
                }
            }
        }
        catch (JavaScriptException ex)
        {
            promiseCapability.Reject(ex.Error);
            return promiseCapability.PromiseInstance;
        }

        promiseCapability.Resolve(CreateIteratorResult(Undefined, done: true));
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
            // IteratorNext(innerIterator) (https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.flatmap)
            // calls the [[NextMethod]] GetIteratorFlattenable read once, never `next` read again; one that was not
            // callable fails the step with a TypeError (sebastienros/jint#4419).
            JsValue innerResult;
            try
            {
                innerResult = _innerIterator.CallNextMethod();
            }
            catch (JavaScriptException)
            {
                // IfAbruptCloseAsyncIterator(innerNextPromise, iterated): whoever catches this closes the outer iterator
                // and completes the helper. The inner iterator is not closed, then or by a later return().
                _innerIterator = null;
                throw;
            }

            var innerPromise = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(innerResult);
            var self = this;

            var onInnerFulfilled = new ClrFunction(_engine, "", (_, args) =>
            {
                try
                {
                    var iterResult = args.At(0);
                    if (iterResult is not ObjectInstance iterResultObj)
                    {
                        // IteratorComplete(innerNext) on something other than an object fails the step, as for await's
                        // check does, and IfAbruptCloseAsyncIterator closes the outer iterator. It used to count as the
                        // inner iterator's end (sebastienros/jint#4439).
                        self._innerIterator = null;
                        self.RejectWithError(promiseCapability, self._engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
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
        var nextPromise = CallIteratorNext(promiseCapability);
        if (nextPromise is null)
        {
            return;
        }

        var outerSelf = this;

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            if (!outerSelf.TryCompleteStep(args.At(0), promiseCapability, out var iterResultObj)
                || !outerSelf.TryGetStepValue(iterResultObj, promiseCapability, out var value))
            {
                return Undefined;
            }

            try
            {
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
                        // GetIteratorFlattenable(mapped, async): a TypeError for anything that is not an object, a
                        // string included; then @@asyncIterator, else @@iterator behind CreateAsyncFromSyncIterator, so
                        // that each value a sync iterable answers is awaited (sebastienros/jint#4439), else the object
                        // itself. IfAbruptCloseAsyncIterator(innerIterator, iterated) is the catch below.
                        outerSelf._innerIterator = AsyncIteratorConstructor.GetIteratorFlattenable(
                            outerSelf._engine,
                            outerSelf._engine.Realm,
                            innerArgs.At(0),
                            "flatMap mapper must return an iterable or iterator");
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

        PromiseOperations.PerformPromiseThen(_engine, nextPromise, onFulfilled, CreateSourceStepRejectedHandler(promiseCapability), null!);
    }
}
