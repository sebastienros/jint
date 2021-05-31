using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakSet
{
    public sealed class WeakSetConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("WeakSet");

        private WeakSetConstructor(Engine engine)
            : base(engine, _functionName)
        {
        }

        public WeakSetPrototype PrototypeObject { get; private set; }

        public static WeakSetConstructor CreateWeakSetConstructor(Engine engine)
        {
            var obj = new WeakSetConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the WeakSet constructor is the Function prototype object
            obj.PrototypeObject = WeakSetPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);

            // The initial value of WeakSet.prototype is the WeakSet prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_engine, "Constructor WeakSet requires 'new'");
            return null;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var set = OrdinaryCreateFromConstructor(newTarget, PrototypeObject, static (engine, _) => new WeakSetInstance(engine));
            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var adderValue = set.Get("add");
                if (!(adderValue is ICallable adder))
                {
                    return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine, "add must be callable");
                }

                var iterable = arguments.At(0).GetIterator(_engine);

                try
                {
                    var args = new JsValue[1];
                    do
                    {
                        if (!iterable.TryIteratorStep(out var next))
                        {
                            return set;
                        }

                        next.TryGetValue(CommonProperties.Value, out var nextValue);
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
}