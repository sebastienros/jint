using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Native.Iterator;

/// <summary>
/// Base class for iterator helper instances (map, filter, take, drop, flatMap).
/// https://tc39.es/ecma262/#sec-iterator-helper-objects
/// </summary>
internal abstract class IteratorHelper : ObjectInstance
{
    protected readonly IteratorInstance.ObjectIterator Iterated;
    protected GeneratorState State;
    protected int Counter;
    protected bool Exhausted;

    protected IteratorHelper(Engine engine, IteratorInstance.ObjectIterator iterated) : base(engine)
    {
        Iterated = iterated;
        State = GeneratorState.SuspendedStart;
        Counter = 0;
        Exhausted = false;
        _prototype = engine.Realm.Intrinsics.IteratorHelperPrototype;
    }

    /// <summary>
    /// Called by IteratorHelperPrototype.next() to get the next value.
    /// </summary>
    public ObjectInstance Next()
    {
        // Check for re-entrant calls
        if (State == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
            return null!;
        }

        // If completed, return done
        if (State == GeneratorState.Completed)
        {
            return CreateIteratorResult(Undefined, done: true);
        }

        State = GeneratorState.Executing;

        try
        {
            var result = ExecuteStep();

            if (result.Done)
            {
                State = GeneratorState.Completed;
                return CreateIteratorResult(Undefined, done: true);
            }

            State = GeneratorState.SuspendedYield;
            return CreateIteratorResult(result.Value, done: false);
        }
        catch
        {
            State = GeneratorState.Completed;

            // IfAbruptCloseIterator covers the mapper/predicate call, not the step that fed it, and
            // only applies while the underlying record is still open. Exhausted says it is not: the
            // step reported DONE, the step itself completed abruptly (which marks the record done and
            // closes nothing), or the helper already ran an IteratorClose of its own - take's
            // `Return ? IteratorClose(iterated, ReturnCompletion(undefined))` for a spent limit.
            // Closing again would invoke "return" where the spec invokes nothing, or a second time,
            // swallowing whatever the first invocation raised.
            if (!Exhausted)
            {
                Exhausted = true;
                CloseIterator(CompletionType.Throw);
            }

            throw;
        }
    }

    /// <summary>
    /// Called by IteratorHelperPrototype.return() to close the helper.
    /// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%.return
    /// </summary>
    /// <remarks>
    /// The three reachable [[GeneratorState]]s answer differently, and each difference is observable:
    /// <list type="bullet">
    /// <item>executing - step 6 hands the helper to GeneratorResumeAbrupt, whose GeneratorValidate rejects a
    /// running generator. That is a return re-entering the helper from inside its own mapper or predicate;
    /// closing the underlying iterator there would invoke "return" where the spec invokes nothing, and would
    /// leave the helper going on to yield from an iterator it had just closed.</item>
    /// <item>completed - GeneratorResumeAbrupt answers a return completion with a done result and resumes no
    /// body, so nothing is left to close. This is not the same question as <see cref="Exhausted"/>, which is
    /// about the underlying record: a flatMap whose inner iterator stepped abruptly is completed while still
    /// holding that inner iterator, and closing it here is precisely what the spec does not do.</item>
    /// <item>suspended-start / suspended-yield - close, which is step 4c for the former and the body's own
    /// IfAbruptCloseIterator for the latter. Both are an IteratorClose with a non-throw completion, so the
    /// difference between the spec's normal and return completions is not observable here.</item>
    /// </list>
    /// The state moves to completed <em>before</em> the close, so a return that re-enters from inside the
    /// underlying iterator's own "return" lands in the completed arm rather than the executing one.
    /// </remarks>
    public ObjectInstance Return()
    {
        if (State == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
        }

        if (State != GeneratorState.Completed)
        {
            State = GeneratorState.Completed;
            CloseOnReturn();
        }

        return CreateIteratorResult(Undefined, done: true);
    }

    /// <summary>
    /// Closes whatever this helper still holds open, for a <see cref="Return"/> that reached a suspended
    /// helper. <see cref="Exhausted"/> is what says the underlying record is already done - the step reported
    /// DONE, the step itself completed abruptly, or the helper ran an IteratorClose of its own - and closing
    /// again would invoke "return" a second time.
    /// </summary>
    protected virtual void CloseOnReturn()
    {
        if (!Exhausted)
        {
            Exhausted = true;
            CloseIterator(CompletionType.Return);
        }
    }

    /// <summary>
    /// Override in derived classes to implement the specific iteration logic.
    /// </summary>
    protected abstract StepResult ExecuteStep();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorstepvalue
    /// Gets the next value from the underlying iterator.
    /// Returns (value, false) if a value is available, or (undefined, true) if done.
    /// </summary>
    /// <remarks>
    /// Every abrupt completion here - a throwing next(), a result that is not an Object, a throwing
    /// "done" getter, a throwing "value" getter - sets the record's [[Done]] to true and is returned
    /// as-is, with no IteratorClose. Every helper spells the step `? IteratorStepValue(iterated)`,
    /// outside the IfAbruptCloseIterator that guards the call it feeds, so marking the record
    /// Exhausted is what keeps the close in Next from invoking "return" on it.
    /// </remarks>
    protected StepResult IteratorStepValue()
    {
        try
        {
            if (!Iterated.TryIteratorStep(out var result))
            {
                return StepResult.DoneResult;
            }

            return new StepResult(result.Get(CommonProperties.Value), false);
        }
        catch
        {
            Exhausted = true;
            throw;
        }
    }

    /// <summary>
    /// Close the underlying iterator.
    /// </summary>
    protected void CloseIterator(CompletionType completionType)
    {
        Iterated.Close(completionType);
    }

    /// <summary>
    /// Create an iterator result object.
    /// </summary>
    protected ObjectInstance CreateIteratorResult(JsValue value, bool done)
    {
        return IteratorResult.CreateValueIteratorPosition(_engine, value, JsBoolean.Create(done));
    }

    /// <summary>
    /// Represents the result of an iteration step.
    /// </summary>
    internal readonly struct StepResult
    {
        public static readonly StepResult DoneResult = new(JsValue.Undefined, true);

        public readonly JsValue Value;
        public readonly bool Done;

        public StepResult(JsValue value, bool done)
        {
            Value = value;
            Done = done;
        }
    }
}

/// <summary>
/// Iterator helper for drop(limit) - skips the first N elements.
/// https://tc39.es/ecma262/#sec-iterator.prototype.drop
/// </summary>
internal sealed class DropIterator : IteratorHelper
{
    private long _remaining;

    public DropIterator(Engine engine, IteratorInstance.ObjectIterator iterated, long limit) : base(engine, iterated)
    {
        _remaining = limit;
    }

    protected override StepResult ExecuteStep()
    {
        // Skip elements while remaining > 0
        while (_remaining > 0)
        {
            var step = IteratorStepValue();
            if (step.Done)
            {
                Exhausted = true;
                return StepResult.DoneResult;
            }
            _remaining--;
        }

        // Return subsequent elements
        var result = IteratorStepValue();
        if (result.Done)
        {
            Exhausted = true;
        }
        return result;
    }
}

/// <summary>
/// Iterator helper for take(limit) - returns the first N elements.
/// https://tc39.es/ecma262/#sec-iterator.prototype.take
/// </summary>
internal sealed class TakeIterator : IteratorHelper
{
    private readonly long _limit;
    private long _taken;

    public TakeIterator(Engine engine, IteratorInstance.ObjectIterator iterated, long limit) : base(engine, iterated)
    {
        _limit = limit;
        _taken = 0;
    }

    protected override StepResult ExecuteStep()
    {
        // If we've taken enough, close and return done
        if (_taken >= _limit)
        {
            Exhausted = true;
            CloseIterator(CompletionType.Normal);
            return StepResult.DoneResult;
        }

        var result = IteratorStepValue();
        if (result.Done)
        {
            Exhausted = true;
            return StepResult.DoneResult;
        }

        _taken++;
        return result;
    }
}

/// <summary>
/// Iterator helper for filter(predicate) - returns elements matching the predicate.
/// https://tc39.es/ecma262/#sec-iterator.prototype.filter
/// </summary>
internal sealed class FilterIterator : IteratorHelper
{
    private readonly ICallable _predicate;

    public FilterIterator(Engine engine, IteratorInstance.ObjectIterator iterated, ICallable predicate) : base(engine, iterated)
    {
        _predicate = predicate;
    }

    protected override StepResult ExecuteStep()
    {
        while (true)
        {
            var result = IteratorStepValue();
            if (result.Done)
            {
                Exhausted = true;
                return StepResult.DoneResult;
            }

            var value = result.Value;
            var selected = _predicate.Call(Undefined, [value, Counter]);
            Counter++;

            if (TypeConverter.ToBoolean(selected))
            {
                return new StepResult(value, false);
            }
        }
    }
}

/// <summary>
/// Iterator helper for map(mapper) - transforms each element.
/// https://tc39.es/ecma262/#sec-iterator.prototype.map
/// </summary>
internal sealed class MapIterator : IteratorHelper
{
    private readonly ICallable _mapper;

    public MapIterator(Engine engine, IteratorInstance.ObjectIterator iterated, ICallable mapper) : base(engine, iterated)
    {
        _mapper = mapper;
    }

    protected override StepResult ExecuteStep()
    {
        var result = IteratorStepValue();
        if (result.Done)
        {
            Exhausted = true;
            return StepResult.DoneResult;
        }

        var mapped = _mapper.Call(Undefined, [result.Value, Counter]);
        Counter++;
        return new StepResult(mapped, false);
    }
}

/// <summary>
/// Iterator helper for chunks(chunkSize) - yields consecutive non-overlapping arrays.
/// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.chunks
/// </summary>
internal sealed class ChunksIterator : IteratorHelper
{
    private readonly uint _chunkSize;
    private readonly List<JsValue> _buffer;
    private bool _underlyingDone;

    public ChunksIterator(Engine engine, IteratorInstance.ObjectIterator iterated, uint chunkSize) : base(engine, iterated)
    {
        _chunkSize = chunkSize;
        // Deliberately not sized to chunkSize: it can be as large as 2**32 - 1, and the buffer only
        // ever needs to hold what the underlying iterator actually produced.
        _buffer = new List<JsValue>();
    }

    protected override StepResult ExecuteStep()
    {
        // Once the underlying iterator has reported done, never step it again - a trailing partial
        // chunk is yielded from the buffer, and the call after it must not re-enter next().
        if (_underlyingDone)
        {
            return StepResult.DoneResult;
        }

        var iterations = 0;
        while (true)
        {
            var step = IteratorStepValue();
            if (step.Done)
            {
                _underlyingDone = true;
                Exhausted = true;

                // If buffer is not empty, yield it as the final, shorter-than-chunkSize chunk.
                if (_buffer.Count > 0)
                {
                    return new StepResult(TakeBufferAsArray(), false);
                }

                return StepResult.DoneResult;
            }

            _buffer.Add(step.Value);

            if ((uint) _buffer.Count == _chunkSize)
            {
                return new StepResult(TakeBufferAsArray(), false);
            }

            // A large chunkSize means many next() calls inside one step; keep the engine
            // interruptible, and let the catch in the base class close the iterator.
            if (++iterations % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }
    }

    private JsArray TakeBufferAsArray()
    {
        var array = _engine.Realm.Intrinsics.Array.ConstructFast(_buffer);
        _buffer.Clear();
        return array;
    }
}

/// <summary>
/// Iterator helper for windows(windowSize [, undersized]) - yields overlapping sliding windows.
/// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.windows
/// </summary>
internal sealed class WindowsIterator : IteratorHelper
{
    private readonly uint _windowSize;
    private readonly bool _allowPartial;
    private readonly List<JsValue> _buffer;
    private bool _underlyingDone;

    public WindowsIterator(Engine engine, IteratorInstance.ObjectIterator iterated, uint windowSize, bool allowPartial) : base(engine, iterated)
    {
        _windowSize = windowSize;
        _allowPartial = allowPartial;
        // See ChunksIterator: windowSize can be 2**32 - 1, so the buffer grows with arrivals.
        _buffer = new List<JsValue>();
    }

    protected override StepResult ExecuteStep()
    {
        if (_underlyingDone)
        {
            return StepResult.DoneResult;
        }

        var iterations = 0;
        while (true)
        {
            var step = IteratorStepValue();
            if (step.Done)
            {
                _underlyingDone = true;
                Exhausted = true;

                // Only "allow-partial" yields a trailing window, and only one that never reached
                // full size - a buffer already at windowSize was yielded as a full window already.
                if (_allowPartial && _buffer.Count > 0 && (uint) _buffer.Count < _windowSize)
                {
                    var partial = _engine.Realm.Intrinsics.Array.ConstructFast(_buffer);
                    _buffer.Clear();
                    return new StepResult(partial, false);
                }

                return StepResult.DoneResult;
            }

            // Slide before appending, and only once the window is full, so window N+1 drops exactly
            // one element of window N.
            if ((uint) _buffer.Count == _windowSize)
            {
                _buffer.RemoveAt(0);
            }

            _buffer.Add(step.Value);

            if ((uint) _buffer.Count == _windowSize)
            {
                // The buffer is retained for the next window, so the yielded array must be a copy.
                return new StepResult(_engine.Realm.Intrinsics.Array.ConstructFast(_buffer), false);
            }

            if (++iterations % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }
    }
}

/// <summary>
/// Iterator helper for flatMap(mapper) - maps and flattens one level.
/// https://tc39.es/ecma262/#sec-iterator.prototype.flatmap
/// </summary>
internal sealed class FlatMapIterator : IteratorHelper
{
    private readonly ICallable _mapper;
    private IteratorInstance.ObjectIterator? _innerIterator;
    private bool _innerIteratorClosed;

    public FlatMapIterator(Engine engine, IteratorInstance.ObjectIterator iterated, ICallable mapper) : base(engine, iterated)
    {
        _mapper = mapper;
    }

    /// <summary>
    /// A flatMap suspended inside an inner iterator holds two open records, and the body's
    /// IfAbruptCloseIterator closes the inner one before the outer.
    /// </summary>
    protected override void CloseOnReturn()
    {
        if (_innerIterator is not null && !_innerIteratorClosed)
        {
            _innerIteratorClosed = true;
            _innerIterator.Close(CompletionType.Return);
        }

        base.CloseOnReturn();
    }

    protected override StepResult ExecuteStep()
    {
        while (true)
        {
            // If we have an inner iterator, consume from it first
            if (_innerIterator is not null)
            {
                if (_innerIterator.TryIteratorStep(out var innerResult))
                {
                    var innerValue = innerResult.Get(CommonProperties.Value);
                    return new StepResult(innerValue, false);
                }
                // Inner iterator exhausted
                _innerIterator = null;
                _innerIteratorClosed = false;
            }

            // Get next value from outer iterator
            var result = IteratorStepValue();
            if (result.Done)
            {
                Exhausted = true;
                return StepResult.DoneResult;
            }

            // Map the value
            var mapped = _mapper.Call(Undefined, [result.Value, Counter]);
            Counter++;

            // Get iterator from mapped value (GetIteratorFlattenable)
            if (mapped is not ObjectInstance mappedObj)
            {
                Throw.TypeError(_engine.Realm, "flatMap mapper must return an iterable or iterator");
                return StepResult.DoneResult;
            }

            var method = mappedObj.GetMethod(GlobalSymbolRegistry.Iterator);
            ObjectInstance innerIteratorObj;
            if (method is null)
            {
                // Use the object itself as an iterator
                innerIteratorObj = mappedObj;
            }
            else
            {
                var iterResult = method.Call(mappedObj);
                if (iterResult is not ObjectInstance iterObj)
                {
                    Throw.TypeError(_engine.Realm, "Iterator result is not an object");
                    return StepResult.DoneResult;
                }
                innerIteratorObj = iterObj;
            }

            _innerIterator = new IteratorInstance.ObjectIterator(innerIteratorObj);
            _innerIteratorClosed = false;
        }
    }
}

/// <summary>
/// Iterator helper for Iterator.concat(...items) - sequences multiple iterables.
/// https://tc39.es/proposal-iterator-sequencing/
/// </summary>
internal sealed class ConcatIterator : ObjectInstance
{
    private readonly List<IterableRecord> _iterables;
    private int _currentIndex;
    private IteratorInstance.ObjectIterator? _currentIterator;
    private GeneratorState _state;
    private bool _exhausted;

    /// <summary>
    /// Record for storing captured iterable and its iterator method.
    /// </summary>
    internal readonly struct IterableRecord
    {
        public readonly ICallable Method;
        public readonly ObjectInstance Iterable;

        public IterableRecord(ICallable method, ObjectInstance iterable)
        {
            Method = method;
            Iterable = iterable;
        }
    }

    public ConcatIterator(Engine engine, List<IterableRecord> iterables) : base(engine)
    {
        _iterables = iterables;
        _currentIndex = 0;
        _currentIterator = null;
        _state = GeneratorState.SuspendedStart;
        _exhausted = false;
        _prototype = engine.Realm.Intrinsics.IteratorHelperPrototype;
    }

    /// <summary>
    /// Called by IteratorHelperPrototype.next() to get the next value.
    /// </summary>
    public ObjectInstance Next()
    {
        // Check for re-entrant calls
        if (_state == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
            return null!;
        }

        // If completed, return done
        if (_state == GeneratorState.Completed)
        {
            return CreateIteratorResult(Undefined, done: true);
        }

        _state = GeneratorState.Executing;

        try
        {
            while (true)
            {
                // If we have an active inner iterator, get next from it
                if (_currentIterator is not null)
                {
                    // Per spec, use IteratorStepValue, which reads the value property (may throw) and
                    // Yield creates a fresh iterator result object. The step is a bare
                    // `? IteratorStepValue(iteratorRecord)`, so an abrupt one marks the record done
                    // and propagates without an IteratorClose - hence dropping the record here rather
                    // than letting the catch below close it.
                    bool alive;
                    JsValue innerValue;
                    try
                    {
                        alive = _currentIterator.TryIteratorStep(out var result);
                        innerValue = alive ? result.Get(CommonProperties.Value) : Undefined;
                    }
                    catch
                    {
                        _currentIterator = null;
                        _exhausted = true;
                        throw;
                    }

                    if (alive)
                    {
                        _state = GeneratorState.SuspendedYield;
                        return CreateIteratorResult(innerValue, done: false);
                    }

                    // Current iterator exhausted, move to next
                    _currentIterator = null;
                }

                // Check if we have more iterables
                if (_currentIndex >= _iterables.Count)
                {
                    _exhausted = true;
                    _state = GeneratorState.Completed;
                    return CreateIteratorResult(Undefined, done: true);
                }

                // Create new inner iterator from next iterable
                var record = _iterables[_currentIndex++];
                var iterResult = record.Method.Call(record.Iterable);

                if (iterResult is not ObjectInstance iterObj)
                {
                    Throw.TypeError(_engine.Realm, "Iterator is not an object");
                    return null!;
                }

                _currentIterator = new IteratorInstance.ObjectIterator(iterObj);
            }
        }
        catch
        {
            _state = GeneratorState.Completed;
            _currentIterator?.Close(CompletionType.Throw);
            throw;
        }
    }

    /// <summary>
    /// Called by IteratorHelperPrototype.return() to close the helper.
    /// </summary>
    public ObjectInstance Return()
    {
        // Check for re-entrant calls
        if (_state == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
            return null!;
        }

        // Set state to Executing during the close operation to detect re-entrancy
        var previousState = _state;
        _state = GeneratorState.Executing;

        try
        {
            // Only close if we have an active inner iterator and haven't exhausted
            // Capture and clear _currentIterator to prevent double-close
            var iteratorToClose = _currentIterator;
            _currentIterator = null;

            if (iteratorToClose is not null && !_exhausted)
            {
                _exhausted = true;
                iteratorToClose.Close(CompletionType.Return);
            }
            else
            {
                _exhausted = true;
            }

            return CreateIteratorResult(Undefined, done: true);
        }
        finally
        {
            _state = GeneratorState.Completed;
        }
    }

    private IteratorResult CreateIteratorResult(JsValue value, bool done)
    {
        return IteratorResult.CreateValueIteratorPosition(_engine, value, JsBoolean.Create(done));
    }
}

/// <summary>
/// Iterator helper for Iterator.zip and Iterator.zipKeyed.
/// https://tc39.es/proposal-joint-iteration/#sec-iteratorzip
/// </summary>
internal sealed class ZipIterator : ObjectInstance
{
    private readonly List<IteratorInstance.ObjectIterator> _iters;
    private readonly List<IteratorInstance.ObjectIterator> _openIters;
    private readonly ZipMode _mode;
    private readonly IteratorInstance.ObjectIterator? _paddingIterator;
    private readonly JsValue[]? _paddingValues;
    private readonly Func<JsValue[], JsValue> _finishResults;
    private GeneratorState _state;
    private bool _exhausted;

    public ZipIterator(
        Engine engine,
        List<IteratorInstance.ObjectIterator> iters,
        ZipMode mode,
        IteratorInstance.ObjectIterator? paddingIterator,
        JsValue[]? paddingValues,
        Func<JsValue[], JsValue> finishResults) : base(engine)
    {
        _iters = iters;
        _openIters = new List<IteratorInstance.ObjectIterator>(iters);
        _mode = mode;
        _paddingIterator = paddingIterator;
        _paddingValues = paddingValues;
        _finishResults = finishResults;
        _state = GeneratorState.SuspendedStart;
        _exhausted = false;
        _prototype = engine.Realm.Intrinsics.IteratorHelperPrototype;
    }

    public ObjectInstance Next()
    {
        // Check for re-entrant calls
        if (_state == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
            return null!;
        }

        // If completed, return done
        if (_state == GeneratorState.Completed)
        {
            return CreateIteratorResult(Undefined, done: true);
        }

        _state = GeneratorState.Executing;

        try
        {
            var result = ExecuteStep();

            if (result.Done)
            {
                _state = GeneratorState.Completed;
                return CreateIteratorResult(Undefined, done: true);
            }

            _state = GeneratorState.SuspendedYield;
            return CreateIteratorResult(result.Value, done: false);
        }
        catch
        {
            _state = GeneratorState.Completed;
            CloseAllIterators(CompletionType.Throw);
            throw;
        }
    }

    public ObjectInstance Return()
    {
        // Check for re-entrant calls
        if (_state == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator is already executing");
            return null!;
        }

        // Per spec: If in suspended-start, set to completed first
        // This allows re-entrant .next() calls during close to return done properly
        if (_state == GeneratorState.SuspendedStart)
        {
            _state = GeneratorState.Completed;
            if (!_exhausted)
            {
                _exhausted = true;
                CloseAllIterators(CompletionType.Return);
            }
            return CreateIteratorResult(Undefined, done: true);
        }

        _state = GeneratorState.Executing;

        try
        {
            if (!_exhausted)
            {
                _exhausted = true;
                CloseAllIterators(CompletionType.Return);
            }
            return CreateIteratorResult(Undefined, done: true);
        }
        finally
        {
            _state = GeneratorState.Completed;
        }
    }

    private IteratorHelper.StepResult ExecuteStep()
    {
        var iterCount = _iters.Count;

        // If no iterators, we're done immediately
        if (iterCount == 0)
        {
            _exhausted = true;
            return IteratorHelper.StepResult.DoneResult;
        }

        var results = new JsValue[iterCount];
        var allDone = true;
        var anyDone = false;
        var firstDoneIndex = -1;

        // Step through each iterator
        for (var i = 0; i < iterCount; i++)
        {
            var iter = _iters[i];

            // Check if this iterator is still open
            if (!_openIters.Contains(iter))
            {
                // Already closed, use padding
                anyDone = true;
                if (firstDoneIndex < 0) firstDoneIndex = i;
                results[i] = GetPaddingValue(i);
                continue;
            }

            bool stepDone;
            ObjectInstance? stepResult = null;
            try
            {
                stepDone = !iter.TryIteratorStep(out stepResult);
            }
            catch
            {
                // Per spec: If IteratorStepValue throws, remove iter from openIters
                // then call IteratorCloseAll on remaining
                _openIters.Remove(iter);
                throw;
            }

            if (stepDone)
            {
                // Iterator is done
                anyDone = true;
                if (firstDoneIndex < 0) firstDoneIndex = i;
                _openIters.Remove(iter);

                if (_mode == ZipMode.Shortest)
                {
                    // Close all other open iterators in reverse order
                    CloseOpenIteratorsInReverse(i);
                    _exhausted = true;
                    return IteratorHelper.StepResult.DoneResult;
                }

                if (_mode == ZipMode.Strict)
                {
                    if (i == 0)
                    {
                        // Per spec: When first iterator (i=0) is done in strict mode,
                        // check remaining iterators one by one with IteratorStep
                        // If any is NOT done, close openIters and throw
                        for (var k = 1; k < iterCount; k++)
                        {
                            var otherIter = _iters[k];
                            if (!_openIters.Contains(otherIter))
                            {
                                continue;
                            }

                            bool otherDone;
                            try
                            {
                                otherDone = !otherIter.TryIteratorStep(out _);
                            }
                            catch
                            {
                                _openIters.Remove(otherIter);
                                throw;
                            }

                            if (otherDone)
                            {
                                _openIters.Remove(otherIter);
                            }
                            else
                            {
                                // Not done - iterators have different lengths
                                CloseAllIterators(CompletionType.Throw);
                                _exhausted = true;
                                Throw.TypeError(_engine.Realm, "Iterators have different lengths");
                                return IteratorHelper.StepResult.DoneResult;
                            }
                        }
                        // All remaining iterators were also done
                        _exhausted = true;
                        return IteratorHelper.StepResult.DoneResult;
                    }
                    else
                    {
                        // Per spec: When i!=0 and iterator is done in strict mode,
                        // first iterator wasn't done (or we'd have returned), so lengths differ
                        CloseAllIterators(CompletionType.Throw);
                        _exhausted = true;
                        Throw.TypeError(_engine.Realm, "Iterators have different lengths");
                        return IteratorHelper.StepResult.DoneResult;
                    }
                }

                results[i] = GetPaddingValue(i);
            }
            else
            {
                allDone = false;
                JsValue value;
                try
                {
                    value = stepResult!.Get(CommonProperties.Value);
                }
                catch
                {
                    // If getting the value throws, remove iter and close others
                    _openIters.Remove(iter);
                    throw;
                }
                results[i] = value;
            }
        }

        // Handle mode-specific termination after checking all iterators
        if (_mode == ZipMode.Strict)
        {
            // In strict mode, if some are done and some aren't, throw
            // Per spec: IteratorCloseAll(openIters, ThrowCompletion(TypeError))
            // This means close exceptions are ignored because completion is already Throw
            if (anyDone && !allDone)
            {
                CloseAllIterators(CompletionType.Throw);
                _exhausted = true;
                Throw.TypeError(_engine.Realm, "Iterators have different lengths");
                return IteratorHelper.StepResult.DoneResult;
            }
        }

        if (allDone)
        {
            _exhausted = true;
            return IteratorHelper.StepResult.DoneResult;
        }

        var resultValue = _finishResults(results);
        return new IteratorHelper.StepResult(resultValue, false);
    }

    private JsValue GetPaddingValue(int index)
    {
        if (_paddingValues != null && index < _paddingValues.Length)
        {
            return _paddingValues[index];
        }
        return Undefined;
    }

    private void CloseOpenIteratorsInReverse(int upToIndex)
    {
        // Per IteratorCloseAll spec with Return completion:
        // Close all open iterators in reverse order, accumulating the first exception
        Exception? caughtException = null;
        var isThrowCompletion = false;

        for (var j = _openIters.Count - 1; j >= 0; j--)
        {
            var openIter = _openIters[j];
            // Find the index of this iterator in the original list
            var originalIndex = _iters.IndexOf(openIter);
            if (originalIndex != upToIndex && originalIndex != -1)
            {
                try
                {
                    openIter.Close(isThrowCompletion ? CompletionType.Throw : CompletionType.Normal);
                }
                catch (Exception ex)
                {
                    // Capture the first exception, subsequent ones are ignored
                    if (caughtException is null)
                    {
                        caughtException = ex;
                        isThrowCompletion = true;
                    }
                }
            }
        }
        _openIters.Clear();

        // Rethrow the first captured exception
        if (caughtException is not null)
        {
            throw caughtException;
        }
    }

    private void CloseAllIterators(CompletionType completionType)
    {
        // Per IteratorCloseAll spec:
        // 1. For each iterator in reverse, call IteratorClose
        // 2. If any close throws and we don't already have an exception, capture it
        // 3. Throw the first captured exception at the end
        Exception? caughtException = null;
        var isThrowCompletion = completionType == CompletionType.Throw;

        for (var i = _openIters.Count - 1; i >= 0; i--)
        {
            try
            {
                _openIters[i].Close(isThrowCompletion ? CompletionType.Throw : CompletionType.Normal);
            }
            catch (Exception ex)
            {
                // Per IteratorClose step 5: if completion is throw, ignore new exceptions
                // Otherwise, capture the first exception
                if (!isThrowCompletion && caughtException is null)
                {
                    caughtException = ex;
                    isThrowCompletion = true; // Subsequent closes will ignore their exceptions
                }
            }
        }
        _openIters.Clear();

        // Rethrow the first captured exception
        if (caughtException is not null)
        {
            throw caughtException;
        }
    }

    private IteratorResult CreateIteratorResult(JsValue value, bool done)
    {
        return IteratorResult.CreateValueIteratorPosition(_engine, value, JsBoolean.Create(done));
    }
}

internal enum ZipMode
{
    Shortest,
    Longest,
    Strict
}
