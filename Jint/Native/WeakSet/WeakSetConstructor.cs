using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakSet;

internal sealed class WeakSetConstructor : Constructor
{
    private static readonly JsString _functionName = new("WeakSet");

    internal WeakSetConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new WeakSetPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private WeakSetPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var set = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WeakSet.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsWeakSet(engine));

        var arg1 = arguments.At(0);
        if (!arg1.IsNullOrUndefined())
        {
            var adder = set.Get("add") as ICallable;

            // check fast path
            if (arg1 is JsArray array && ReferenceEquals(adder, _engine.Realm.Intrinsics.WeakSet.PrototypeObject._originalAddFunction))
            {
                foreach (var value in array)
                {
                    set.WeakSetAdd(value);
                }

                return set;
            }

            if (adder is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "add must be callable");
            }

            var iterable = arguments.At(0).GetIterator(_realm);

            try
            {
                var args = new JsValue[1];
                do
                {
                    if (!iterable.TryIteratorStep(out var next))
                    {
                        return set;
                    }

                    var nextValue = next.Get(CommonProperties.Value);
                    args[0] = nextValue;
                    adder.Call(set, args);
                } while (true);
            }
            catch
            {
                iterable.Close(CompletionType.Throw);
                throw;
            }
        }

        return set;
    }
}
