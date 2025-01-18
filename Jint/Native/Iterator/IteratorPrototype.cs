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
                get: new ClrFunction(_engine, "Iterator.prototype.constructor", (_, _) => _engine.Intrinsics.Iterator),
                set: new ClrFunction(_engine, "Iterator.prototype.constructor", (thisObject, arguments) =>
                {
                    SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, CommonProperties.Constructor, arguments.At(0));
                    return Undefined;
                }),
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
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        var mapper = GetCallable(arguments.At(0));
        var iterated = GetIteratorDirect(o);
        //var iterator = new iterao

        var closure = () =>
        {
            //a. Let counter be 0.
            //    b. Repeat,
            //i. Let value be ? IteratorStepValue(iterated).
            //    ii. If value is done, return undefined.
            //    iii. Let mapped be Completion(Call(mapper, undefined, « value, 𝔽(counter) »)).
            //iv. IfAbruptCloseIterator(mapped, iterated).
            //    v. Let completion be Completion(Yield(mapped)).
            //    vi. IfAbruptCloseIterator(completion, iterated).
            //    vii. Set counter to counter + 1.
        };

        var result = new SuperFoo(_engine, closure, iterated);
        return result;
    }

    private static IteratorInstance.ObjectIterator GetIteratorDirect(ObjectInstance objectInstance) => new(objectInstance);

    private sealed class SuperFoo : IteratorInstance
    {
        public SuperFoo(Engine engine, Action closure, IteratorInstance iterated) : base(engine)
        {
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.filter
    /// </summary>
    private JsValue Filter(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.take
    /// </summary>
    private JsValue Take(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.drop
    /// </summary>
    private JsValue Drop(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.flatmap
    /// </summary>
    private JsValue FlatMap(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.reduce
    /// </summary>
    private JsValue Reduce(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
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

        var procedure = GetCallable(arguments.At(0));
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

        var predicate = GetCallable(arguments.At(0));
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

        var predicate = GetCallable(arguments.At(0));
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

        var predicate = GetCallable(arguments.At(0));
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
