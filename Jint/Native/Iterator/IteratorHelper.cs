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
            CloseIterator(CompletionType.Throw);
            throw;
        }
    }

    /// <summary>
    /// Called by IteratorHelperPrototype.return() to close the helper.
    /// </summary>
    public virtual ObjectInstance Return()
    {
        // 3. Assert: O has an [[UnderlyingIterator]] internal slot.
        // 4. Let iterator be O.[[UnderlyingIterator]].[[Iterator]].
        // 5. Set O.[[GeneratorState]] to completed.
        State = GeneratorState.Completed;

        // 6. Let innerIterator be O.[[UnderlyingIterator]].
        // 7. Return ? IteratorClose(innerIterator, NormalCompletion(undefined)).
        // Only close if not already exhausted
        if (!Exhausted)
        {
            Exhausted = true; // Prevent subsequent calls from forwarding
            CloseIterator(CompletionType.Return);
        }
        return CreateIteratorResult(Undefined, done: true);
    }

    /// <summary>
    /// Override in derived classes to implement the specific iteration logic.
    /// </summary>
    protected abstract StepResult ExecuteStep();

    /// <summary>
    /// Gets the next value from the underlying iterator.
    /// Returns (value, false) if a value is available, or (undefined, true) if done.
    /// </summary>
    protected StepResult IteratorStepValue()
    {
        if (!Iterated.TryIteratorStep(out var result))
        {
            return StepResult.DoneResult;
        }

        var value = result.Get(CommonProperties.Value);
        return new StepResult(value, false);
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
    protected readonly struct StepResult
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

    /// <summary>
    /// Generator state for iterator helpers.
    /// </summary>
    protected enum GeneratorState
    {
        SuspendedStart,
        SuspendedYield,
        Executing,
        Completed
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
    /// Override Return to also close the inner iterator if present.
    /// </summary>
    public override ObjectInstance Return()
    {
        // Set state to completed first
        State = GeneratorState.Completed;

        // Close the inner iterator if there is one and it hasn't been closed yet
        if (_innerIterator is not null && !_innerIteratorClosed)
        {
            _innerIteratorClosed = true;
            _innerIterator.Close(CompletionType.Return);
        }

        // Close the outer iterator
        if (!Exhausted)
        {
            Exhausted = true;
            CloseIterator(CompletionType.Return);
        }

        return CreateIteratorResult(Undefined, done: true);
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
