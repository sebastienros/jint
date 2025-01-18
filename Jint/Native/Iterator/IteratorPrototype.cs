using Jint.Collections;
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
                new ClrFunction(_engine, "Iterator.prototype.constructor", (_, _) => _engine.Intrinsics.Iterator),
                new ClrFunction(_engine, "Iterator.prototype.constructor", (_, _) =>
                {
                    ExceptionHelper.ThrowTypeError(_realm);
                    return Undefined;
                }),
                PropertyFlag.Configurable),
            ["map"] = new(new ClrFunction(_engine, "map", Map, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["filter"] = new(new ClrFunction(_engine, "filter", Filter, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["take"] = new(new ClrFunction(_engine, "take", Take, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["drop"] = new(new ClrFunction(_engine, "drop", Drop, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["flatMap"] = new(new ClrFunction(_engine, "flatMap", FlatMap, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["reduce"] = new(new ClrFunction(_engine, "reduce", Reduce, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["toArray"] = new(new ClrFunction(_engine, "toArray", ToArray, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["forEach"] = new(new ClrFunction(_engine, "forEach", ForEach, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["some"] = new(new ClrFunction(_engine, "some", Some, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["evey"] = new(new ClrFunction(_engine, "every", Every, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["find"] = new(new ClrFunction(_engine, "find", Find, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
        };

        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.Iterator] = new(new ClrFunction(Engine, "[Symbol.iterator]", ToIterator, 0, PropertyFlag.Configurable), true, false, true), };
        SetSymbols(symbols);
    }

    private JsValue Map(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is not ObjectInstance o)
        {
            ExceptionHelper.ThrowTypeError(_realm, "object must be an Object");
            return Undefined;
        }

        var callable = GetCallable(arguments.At(0));
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

    private JsValue Filter(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue Take(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue Drop(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue FlatMap(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue Reduce(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue ToArray(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue ForEach(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue Some(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue Every(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private JsValue Find(JsValue thisObject, JsValue[] arguments)
    {
        return Undefined;
    }

    private static JsValue ToIterator(JsValue thisObject, JsValue[] arguments)
    {
        return thisObject;
    }

    internal JsValue Next(JsValue thisObject, JsValue[] arguments)
    {
        var iterator = thisObject as IteratorInstance;
        if (iterator is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        iterator.TryIteratorStep(out var result);
        return result;
    }
}
