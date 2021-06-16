using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakSet
{
    public sealed class WeakSetConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("WeakSet");

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

        public WeakSetPrototype PrototypeObject { get; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Constructor WeakSet requires 'new'");
            return null;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var set = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.WeakSet.PrototypeObject,
                static (engine, realm, _) => new WeakSetInstance(engine));
            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var adder = set.Get("add") as ICallable;
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