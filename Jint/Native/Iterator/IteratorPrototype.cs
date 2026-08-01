using System.Text;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorprototype%-object
/// </summary>
[JsObject(UseShape = true)]
internal partial class IteratorPrototype : Prototype
{
    internal IteratorPrototype(
        Engine engine,
        Realm realm,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-iteratorprototype-constructor
    /// </summary>
    [JsAccessor("constructor")]
    private IteratorConstructor ConstructorGet(JsValue thisObject) => _engine.Intrinsics.Iterator;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-iteratorprototype-constructor
    /// </summary>
    [JsAccessor("constructor", AccessorKind.Set)]
    private JsValue ConstructorSet(JsValue thisObject, JsValue value)
    {
        SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, CommonProperties.Constructor, value);
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-iteratorprototype-@@tostringtag
    /// </summary>
    private static readonly JsString IteratorToStringTag = new("Iterator");

    [JsSymbolAccessor("ToStringTag")]
    private static JsString ToStringTagGet(JsValue thisObject) => IteratorToStringTag;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-iteratorprototype-@@tostringtag
    /// </summary>
    [JsSymbolAccessor("ToStringTag", AccessorKind.Set)]
    private JsValue ToStringTagSet(JsValue thisObject, JsValue value)
    {
        SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, GlobalSymbolRegistry.ToStringTag, value);
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-SetterThatIgnoresPrototypeProperties
    /// </summary>
    private void SetterThatIgnoresPrototypeProperties(JsValue thisValue, ObjectInstance home, JsValue p, JsValue v)
    {
        if (thisValue is not ObjectInstance objectInstance)
        {
            Throw.TypeError(_realm, "Iterator prototype setter called on non-object");
            return;
        }

        if (SameValue(thisValue, home))
        {
            Throw.TypeError(_realm, "Cannot set property on Iterator prototype directly");
            return;
        }

        // step 4's `desc is undefined` is the only thing the descriptor was ever consulted for
        if (!objectInstance.HasOwnProperty(p))
        {
            objectInstance.CreateDataPropertyOrThrow(p, v);
        }
        else
        {
            objectInstance.Set(p, v, throwOnError: true);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.map
    /// </summary>
    [JsFunction]
    private JsValue Map(JsValue thisObject, JsValue mapper)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.map called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(mapper) is false, throw a TypeError exception.
        ICallable mapperCallable;
        try
        {
            mapperCallable = GetCallable(mapper);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return map iterator
        return new MapIterator(_engine, iterated, mapperCallable);
    }

    private static IteratorInstance.ObjectIterator GetIteratorDirect(ObjectInstance objectInstance) => new(objectInstance);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorclose
    /// Close an iterator by calling its return() method directly (without reading next).
    /// Used when validation fails before GetIteratorDirect is called.
    /// </summary>
    private static void IteratorClose(ObjectInstance obj, CompletionType completionType)
    {
        // Step 4 - "If completion is a throw completion, return ? completion" - is reached before
        // innerResult is inspected, so nothing raised while closing may replace the error that
        // triggered the close. That covers the GetMethod in step 2 as much as the call in step 3.c:
        // a throwing "return" accessor, or a "return" that is present but not callable, must leave
        // the original TypeError/RangeError/coercion error standing.
        ICallable? returnMethod;
        try
        {
            returnMethod = obj.GetMethod(CommonProperties.Return);
        }
        catch when (completionType == CompletionType.Throw)
        {
            return;
        }

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
    /// https://tc39.es/ecma262/#sec-iterator.prototype.filter
    /// </summary>
    [JsFunction]
    private JsValue Filter(JsValue thisObject, JsValue predicate)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.filter called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(predicate) is false, throw a TypeError exception.
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return filter iterator
        return new FilterIterator(_engine, iterated, predicateCallable);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.take
    /// </summary>
    [JsFunction]
    private JsValue Take(JsValue thisObject, JsValue limit)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.take called on non-object");
            return Undefined;
        }

        // 3. Let iterated be the Iterator Record { [[Iterator]]: O, [[NextMethod]]: undefined, [[Done]]: false }.
        //    The record is modelled by the receiver alone, because every close below happens before
        //    GetIteratorDirect and so must read "return" without ever touching "next".
        // 4. Let numLimit be Completion(ToNumber(limit)).
        // 5. IfAbruptCloseIterator(numLimit, iterated).
        double numLimit;
        try
        {
            numLimit = TypeConverter.ToNumber(limit);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 6. If numLimit is NaN, close the iterator and throw a RangeError exception.
        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "NaN must be positive");
            return Undefined;
        }

        // 7. If numLimit is finite and numLimit > 𝔽(2**53 - 1), close the iterator and throw a
        //    RangeError exception. Infinity stays valid and means an unbounded limit.
        if (double.IsFinite(numLimit) && numLimit > NumberConstructor.MaxSafeInteger)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{numLimit} exceeds the maximum safe integer");
            return Undefined;
        }

        // 8. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 9. If integerLimit < 0, close the iterator and throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{integerLimit} must be positive");
            return Undefined;
        }

        // 10. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return take iterator
        var lim = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new TakeIterator(_engine, iterated, lim);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.drop
    /// </summary>
    [JsFunction]
    private JsValue Drop(JsValue thisObject, JsValue limit)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.drop called on non-object");
            return Undefined;
        }

        // 3. Let iterated be the Iterator Record { [[Iterator]]: O, [[NextMethod]]: undefined, [[Done]]: false }.
        //    The record is modelled by the receiver alone, because every close below happens before
        //    GetIteratorDirect and so must read "return" without ever touching "next".
        // 4. Let numLimit be Completion(ToNumber(limit)).
        // 5. IfAbruptCloseIterator(numLimit, iterated).
        double numLimit;
        try
        {
            numLimit = TypeConverter.ToNumber(limit);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 6. If numLimit is NaN, close the iterator and throw a RangeError exception.
        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "NaN must be positive");
            return Undefined;
        }

        // 7. If numLimit is finite and numLimit > 𝔽(2**53 - 1), close the iterator and throw a
        //    RangeError exception. Infinity stays valid and means an unbounded limit.
        if (double.IsFinite(numLimit) && numLimit > NumberConstructor.MaxSafeInteger)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{numLimit} exceeds the maximum safe integer");
            return Undefined;
        }

        // 8. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 9. If integerLimit < 0, close the iterator and throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{integerLimit} must be positive");
            return Undefined;
        }

        // 10. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return drop iterator
        var lim = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new DropIterator(_engine, iterated, lim);
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.chunks
    /// </summary>
    [JsFunction]
    private JsValue Chunks(JsValue thisObject, JsValue chunkSize)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.chunks called on non-object");
            return Undefined;
        }

        // 3-6. Validate chunkSize against the pre-GetIteratorDirect Iterator Record.
        var size = ValidateChunkOrWindowSize(o, chunkSize, "chunkSize");

        // 7. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        return new ChunksIterator(_engine, iterated, size);
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.windows
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Windows(JsValue thisObject, JsValue windowSize, JsValue undersized)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.windows called on non-object");
            return Undefined;
        }

        // 3-6. Validate windowSize first - an invalid size is a RangeError even when undersized is
        //      also invalid, so the two checks may not be reordered.
        var size = ValidateChunkOrWindowSize(o, windowSize, "windowSize");

        // 7. If undersized is undefined, set undersized to "only-full".
        // 8. If undersized is neither "only-full" nor "allow-partial", close the iterator and throw
        //    a TypeError exception. The value is compared as-is: there is no ToString coercion, so
        //    a String object or anything else non-string is a TypeError rather than a match.
        var allowPartial = false;
        if (!undersized.IsUndefined())
        {
            if (undersized is not JsString mode)
            {
                IteratorClose(o, CompletionType.Throw);
                Throw.TypeError(_realm, "undersized must be either \"only-full\" or \"allow-partial\"");
                return Undefined;
            }

            switch (mode.ToString())
            {
                case "only-full":
                    break;
                case "allow-partial":
                    allowPartial = true;
                    break;
                default:
                    IteratorClose(o, CompletionType.Throw);
                    Throw.TypeError(_realm, "undersized must be either \"only-full\" or \"allow-partial\"");
                    return Undefined;
            }
        }

        // 9. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        return new WindowsIterator(_engine, iterated, size, allowPartial);
    }

    /// <summary>
    /// Shared steps 4-6 of Iterator.prototype.chunks and Iterator.prototype.windows.
    /// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.chunks
    /// </summary>
    /// <remarks>
    /// The size is taken verbatim, with no ToNumber coercion: anything that is not already an
    /// integral Number is a TypeError, which is why NaN and ±Infinity land there rather than in the
    /// range check below. Every failure closes the receiver through "return" without reading "next",
    /// because the spec's Iterator Record still carries an undefined [[NextMethod]] at this point.
    /// </remarks>
    private uint ValidateChunkOrWindowSize(ObjectInstance o, JsValue size, string parameterName)
    {
        // 4. If size is not a Number, close the iterator and throw a TypeError exception.
        // 5. If size is not an integral Number, close the iterator and throw a TypeError exception.
        if (size is not JsNumber number || !TypeConverter.IsIntegralNumber(number._value))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.TypeError(_realm, $"{parameterName} must be an integral Number");
            return 0;
        }

        // 6. If size is not in the inclusive interval from 1𝔽 to 𝔽(2**32 - 1), close the iterator
        //    and throw a RangeError exception. -0 is integral but below 1, so it lands here.
        var value = number._value;
        if (value < 1 || value > uint.MaxValue)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{parameterName} must be in the inclusive interval from 1 to 2**32 - 1");
            return 0;
        }

        return (uint) value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.flatmap
    /// </summary>
    [JsFunction]
    private JsValue FlatMap(JsValue thisObject, JsValue mapper)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.flatMap called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(mapper) is false, throw a TypeError exception.
        ICallable mapperCallable;
        try
        {
            mapperCallable = GetCallable(mapper);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return flatMap iterator
        return new FlatMapIterator(_engine, iterated, mapperCallable);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.reduce
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Reduce(JsValue thisObject, JsCallArguments arguments)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.reduce called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(reducer) is false, throw a TypeError exception.
        ICallable reducer;
        try
        {
            reducer = GetCallable(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        JsValue accumulator;
        var counter = 0;

        // 5. If initialValue is not present, then
        if (arguments.Length < 2)
        {
            // a. Let next be ? IteratorStep(iterated).
            // b. If next is done, throw a TypeError exception.
            if (!iterated.TryIteratorStep(out var firstResult))
            {
                Throw.TypeError(_realm, "Reduce of empty iterator with no initial value");
                return Undefined;
            }
            // c. Let accumulator be ? IteratorValue(next).
            accumulator = firstResult.Get(CommonProperties.Value);
            counter = 1;
        }
        else
        {
            // 6. Else, let accumulator be initialValue.
            accumulator = arguments.At(1);
        }

        // 7. Repeat,
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            try
            {
                var value = iteratorResult.Get(CommonProperties.Value);
                // a. Let value be ? IteratorStepValue(iterated).
                // b. If value is done, return accumulator.
                // c. Set accumulator to ? Call(reducer, undefined, « accumulator, value, 𝔽(counter) »).
                accumulator = reducer.Call(Undefined, [accumulator, value, counter]);
                // d. Set counter to counter + 1.
                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return accumulator;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.toarray
    /// </summary>
    [JsFunction]
    private JsValue ToArray(JsValue thisObject)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        var iterated = GetIteratorDirect(o);
        var builder = new JsValueListBuilder(16);
        try
        {
            var iterations = 0;
            while (iterated.TryIteratorStep(out var iteratorResult))
            {
                try
                {
                    // Check constraints periodically so a huge (or native-backed) iterator
                    // cannot run uninterrupted; the catch closes the iterator.
                    if (++iterations % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    var value = iteratorResult.Get(CommonProperties.Value);
                    builder.Add(value);
                }
                catch
                {
                    iterated.Close(CompletionType.Throw);
                    throw;
                }
            }

            return _engine.Realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.foreach
    /// </summary>
    [JsFunction]
    private JsValue ForEach(JsValue thisObject, JsValue procedure)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable procedureCallable;
        try
        {
            procedureCallable = GetCallable(procedure);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            try
            {
                var value = iteratorResult.Get(CommonProperties.Value);
                procedureCallable.Call(Undefined, [value, counter]);
                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.some
    /// </summary>
    [JsFunction]
    private JsValue Some(JsValue thisObject, JsValue predicate)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            try
            {
                var value = iteratorResult.Get(CommonProperties.Value);
                var result = predicateCallable.Call(Undefined, [value, counter]);
                if (TypeConverter.ToBoolean(result))
                {
                    iterated.Close(CompletionType.Normal);
                    return JsBoolean.True;
                }

                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.every
    /// </summary>
    [JsFunction]
    private JsValue Every(JsValue thisObject, JsValue predicate)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            try
            {
                var value = iteratorResult.Get(CommonProperties.Value);
                var result = predicateCallable.Call(Undefined, [value, counter]);
                if (!TypeConverter.ToBoolean(result))
                {
                    iterated.Close(CompletionType.Normal);
                    return JsBoolean.False;
                }

                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return JsBoolean.True;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.find
    /// </summary>
    [JsFunction]
    private JsValue Find(JsValue thisObject, JsValue predicate)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            try
            {
                var value = iteratorResult.Get(CommonProperties.Value);
                var result = predicateCallable.Call(Undefined, [value, counter]);
                if (TypeConverter.ToBoolean(result))
                {
                    iterated.Close(CompletionType.Normal);
                    return value;
                }

                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-join/#sec-iterator.prototype.join
    /// </summary>
    [JsFunction]
    private JsValue Join(JsValue thisObject, JsValue separator)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.join called on non-object");
            return Undefined;
        }

        string sep;
        if (separator.IsUndefined())
        {
            // 3. If separator is undefined, let sep be ",".
            sep = ",";
        }
        else
        {
            // 4. Else, let sep be Completion(ToString(separator)), closing O if that is abrupt.
            try
            {
                sep = TypeConverter.ToString(separator);
            }
            catch
            {
                IteratorClose(o, CompletionType.Throw);
                throw;
            }
        }

        // 5. Let iterated be ? GetIteratorDirect(O). Reading "next" deliberately happens only after
        // the separator has been coerced, and an abrupt lookup here does NOT close the receiver.
        var iterated = GetIteratorDirect(o);

        // 6. Let R be the empty String. 7. Let first be true.
        using var sb = new ValueStringBuilder();
        var first = true;
        var iterations = 0;

        // 8. Repeat,
        //    a. Let next be ? IteratorStepValue(iterated). b. If next is DONE, return R.
        // An abrupt step — a throwing next(), a non-object result, or a throwing "value" getter —
        // propagates without closing, per IteratorStepValue marking the record done.
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            var value = iteratorResult.Get(CommonProperties.Value);

            // c. If first is false, set R to the string-concatenation of R and sep.
            if (!first)
            {
                sb.Append(sep);
            }

            // d. Set first to false.
            first = false;

            try
            {
                // e. If next is neither undefined nor null, set R to the string-concatenation of R
                //    and ? ToString(next), closing the iterator if the coercion is abrupt.
                if (!value.IsNullOrUndefined())
                {
                    sb.Append(TypeConverter.ToString(value));
                }

                // Check constraints periodically so a huge (or native-backed) iterator cannot run
                // uninterrupted; the catch closes the iterator.
                if (++iterations % Engine.ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return sb.Length == 0 ? JsString.Empty : JsString.Create(sb.ToString());
    }

    [JsSymbolFunction("Iterator", Length = 0, Flags = PropertyFlag.NonEnumerable)]
    private static JsValue ToIterator(JsValue thisObject) => thisObject;

    [JsSymbolFunction("Dispose", Length = 0, Flags = PropertyFlag.NonEnumerable)]
    private static JsValue Dispose(JsValue thisObject)
    {
        var method = thisObject.AsObject().GetMethod(CommonProperties.Return);
        if (method is not null)
        {
            method.Call(thisObject, Arguments.Empty);
        }

        return Undefined;
    }

    [JsFunction(Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    // Kept with the JsCallArguments signature so still-hand-written subclasses (Array/RegExpString/String
    // IteratorPrototype) can pass it as a ClrFunction delegate from their Initialize bodies.
    internal JsValue Next(JsValue thisObject, JsCallArguments arguments)
    {
        var iterator = thisObject as IteratorInstance;
        if (iterator is null)
        {
            Throw.TypeError(_engine.Realm);
        }

        iterator.TryIteratorStep(out var result);
        return result;
    }
}
