using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorprototype%-object
/// </summary>
internal class IteratorPrototype : Prototype
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
        var properties = new PropertyDictionary(12, checkExistingKeys: false)
        {
            [KnownKeys.Constructor] = new GetSetPropertyDescriptor(
                get: new ClrFunction(_engine, "get constructor", (_, _) => _engine.Intrinsics.Iterator, 0, PropertyFlag.Configurable),
                set: new ClrFunction(_engine, "set constructor", (thisObject, arguments) =>
                {
                    SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, CommonProperties.Constructor, arguments.At(0));
                    return Undefined;
                }, 1, PropertyFlag.Configurable),
                PropertyFlag.Configurable),
            ["map"] = new(new ClrFunction(_engine, "map", Map, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["filter"] = new(new ClrFunction(_engine, "filter", Filter, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["take"] = new(new ClrFunction(_engine, "take", Take, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["drop"] = new(new ClrFunction(_engine, "drop", Drop, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["flatMap"] = new(new ClrFunction(_engine, "flatMap", FlatMap, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["reduce"] = new(new ClrFunction(_engine, "reduce", Reduce, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["toArray"] = new(new ClrFunction(_engine, "toArray", ToArray, 0, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["forEach"] = new(new ClrFunction(_engine, "forEach", ForEach, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["some"] = new(new ClrFunction(_engine, "some", Some, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["every"] = new(new ClrFunction(_engine, "every", Every, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["find"] = new(new ClrFunction(_engine, "find", Find, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
        };

        SetProperties(properties);

        var symbols = new SymbolDictionary(3)
        {
            [GlobalSymbolRegistry.Iterator] = new(new ClrFunction(Engine, "[Symbol.iterator]", ToIterator, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            [GlobalSymbolRegistry.Dispose] = new(new ClrFunction(Engine, "[Symbol.dispose]", Dispose, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            [GlobalSymbolRegistry.ToStringTag] = new GetSetPropertyDescriptor(
                get: new ClrFunction(_engine, "get [Symbol.toStringTag]", (_, _) => "Iterator", 0, PropertyFlag.Configurable),
                set: new ClrFunction(_engine, "set [Symbol.toStringTag]", (thisObject, arguments) =>
                {
                    SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, GlobalSymbolRegistry.ToStringTag, arguments.At(0));
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.map
    /// </summary>
    private JsValue Map(JsValue thisObject, JsValue[] arguments)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.map called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(mapper) is false, throw a TypeError exception.
        ICallable mapper;
        try
        {
            mapper = GetCallable(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return map iterator
        return new MapIterator(_engine, iterated, mapper);
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
    private JsValue Filter(JsValue thisObject, JsValue[] arguments)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.filter called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(predicate) is false, throw a TypeError exception.
        ICallable predicate;
        try
        {
            predicate = GetCallable(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return filter iterator
        return new FilterIterator(_engine, iterated, predicate);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.take
    /// </summary>
    private JsValue Take(JsValue thisObject, JsValue[] arguments)
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
            numLimit = TypeConverter.ToNumber(arguments.At(0));
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
            Throw.RangeError(_realm, "Invalid limit");
            return Undefined;
        }

        // 5. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 6. If integerLimit < 0, throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "Invalid limit");
            return Undefined;
        }

        // 7. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return take iterator
        var limit = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new TakeIterator(_engine, iterated, limit);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.drop
    /// </summary>
    private JsValue Drop(JsValue thisObject, JsValue[] arguments)
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
            numLimit = TypeConverter.ToNumber(arguments.At(0));
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
            Throw.RangeError(_realm, "Invalid limit");
            return Undefined;
        }

        // 5. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 6. If integerLimit < 0, throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "Invalid limit");
            return Undefined;
        }

        // 7. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return drop iterator
        var limit = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new DropIterator(_engine, iterated, limit);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.flatmap
    /// </summary>
    private JsValue FlatMap(JsValue thisObject, JsValue[] arguments)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.flatMap called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(mapper) is false, throw a TypeError exception.
        ICallable mapper;
        try
        {
            mapper = GetCallable(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return flatMap iterator
        return new FlatMapIterator(_engine, iterated, mapper);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.reduce
    /// </summary>
    private JsValue Reduce(JsValue thisObject, JsValue[] arguments)
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
    private JsValue ToArray(JsValue thisObject, JsValue[] arguments)
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
    private JsValue ForEach(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable procedure;
        try
        {
            procedure = GetCallable(arguments.At(0));
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
                procedure.Call(Undefined, [value, counter]);
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
    private JsValue Some(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicate;
        try
        {
            predicate = GetCallable(arguments.At(0));
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
                var result = predicate.Call(Undefined, [value, counter]);
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
    private JsValue Every(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicate;
        try
        {
            predicate = GetCallable(arguments.At(0));
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
                var result = predicate.Call(Undefined, [value, counter]);
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
    private JsValue Find(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicate;
        try
        {
            predicate = GetCallable(arguments.At(0));
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
                var result = predicate.Call(Undefined, [value, counter]);
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

    private static JsValue ToIterator(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    private static JsValue Dispose(JsValue thisObject, JsCallArguments arguments)
    {
        var method = thisObject.AsObject().GetMethod(CommonProperties.Return);
        if (method is not null)
        {
            method.Call(thisObject, arguments);
        }

        return Undefined;
    }

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
