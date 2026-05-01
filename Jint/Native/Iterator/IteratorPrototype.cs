using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorprototype%-object
/// </summary>
[JsObject]
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.map
    /// </summary>
    [JsFunction(Length = 1)]
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
    /// Close an iterator by calling its return() method directly (without reading next).
    /// Used when validation fails before GetIteratorDirect is called.
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
    /// https://tc39.es/ecma262/#sec-iterator.prototype.filter
    /// </summary>
    [JsFunction(Length = 1)]
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
    [JsFunction(Length = 1)]
    private JsValue Take(JsValue thisObject, JsValue limit)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.take called on non-object");
            return Undefined;
        }

        // 3. Let numLimit be ? ToNumber(limit).
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

        // 4. If numLimit is NaN, throw a RangeError exception.
        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "NaN must be positive");
            return Undefined;
        }

        // 5. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 6. If integerLimit < 0, throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{integerLimit} must be positive");
            return Undefined;
        }

        // 7. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return take iterator
        var lim = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new TakeIterator(_engine, iterated, lim);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.drop
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Drop(JsValue thisObject, JsValue limit)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.drop called on non-object");
            return Undefined;
        }

        // 3. Let numLimit be ? ToNumber(limit).
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

        // 4. If numLimit is NaN, throw a RangeError exception.
        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "NaN must be positive");
            return Undefined;
        }

        // 5. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 6. If integerLimit < 0, throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{integerLimit} must be positive");
            return Undefined;
        }

        // 7. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return drop iterator
        var lim = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new DropIterator(_engine, iterated, lim);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.flatmap
    /// </summary>
    [JsFunction(Length = 1)]
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
    [JsFunction(Length = 0)]
    private JsValue ToArray(JsValue thisObject)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        var iterated = GetIteratorDirect(o);
        var items = new JsArray(_engine);
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            try
            {
                var value = iteratorResult.Get(CommonProperties.Value);
                items.Push(value);
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return items;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.foreach
    /// </summary>
    [JsFunction(Length = 1)]
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
    [JsFunction(Length = 1)]
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
    [JsFunction(Length = 1)]
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
    [JsFunction(Length = 1)]
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

    [JsFunction(Length = 0, Name = "next")]
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
