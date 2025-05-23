using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

internal sealed class IteratorConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Iterator");

    internal IteratorConstructor(
        Engine engine,
        Realm realm,
        ObjectInstance functionPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["concat"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "concat", Concat, 1), PropertyFlag.NonEnumerable)),
        };
        SetProperties(properties);
    }


    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        ExceptionHelper.ThrowTypeError(_realm);
        return null!;
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-sequencing/
    /// </summary>
    private static JsValue Concat(JsValue? thisObj, JsValue[] arguments)
    {
        // this needs iterator helpers!

        var iterables = new List<(JsValue OpenMethod, JsValue Iterable)>();

        foreach (var item in arguments)
        {
            if (!item.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Item is not an object");
            }

            var method = item.GetMethod(GlobalSymbolRegistry.Iterator);
            if (method.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Method is undefined");
            }

            iterables.Add((method, item));
        }

        JsValue closure()
        {
            foreach (var iterable in iterables)
            {
                var iter = iterable.OpenMethod.Call(iterable.Iterable);
                if (!iter.IsObject())
                {
                    ExceptionHelper.ThrowTypeError(_realm, "Iterator is not an object");
                }

                var iteratorRecord = iter.GetIteratorDirect();
                var innerAlive = true;

                while (innerAlive)
                {
                    var iteratorResult = iteratorRecord.IteratorStep();
                    if (iteratorResult.IsDone())
                    {
                        iteratorResult.IteratorValue();
                        innerAlive = false;
                    }
                    else
                    {
                        var completion = iteratorResult.GeneratorYield();
                        if (completion.IsAbrupt())
                        {
                            return iteratorRecord.IteratorClose(completion);
                        }
                    }
                }
            }

            return JsValue.Undefined;
        }

        var gen = CreateIteratorFromClosure(closure, "Iterator Helper", _realm.Intrinsics.IteratorHelperPrototype, new List<string> { "UnderlyingIterators" });
        gen.SetUnderlyingIterators(new List<JsValue>());
        return gen;
    }
}
