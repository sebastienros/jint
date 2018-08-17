using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Set
{
    public sealed class SetConstructor : FunctionInstance, IConstructor
    {
        private SetConstructor(Engine engine, string name) :  base(engine, name, null, null, false)
        {
        }

        public SetPrototype PrototypeObject { get; private set; }

        public static SetConstructor CreateSetConstructor(Engine engine)
        {
            SetConstructor CreateSetConstructorTemplate(string name)
            {
                var mapConstructor = new SetConstructor(engine, name);
                mapConstructor.Extensible = true;

                // The value of the [[Prototype]] internal property of the Set constructor is the Function prototype object
                mapConstructor.Prototype = engine.Function.PrototypeObject;
                mapConstructor.PrototypeObject = SetPrototype.CreatePrototypeObject(engine, mapConstructor);

                mapConstructor.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Configurable));
                return mapConstructor;
            }

            var obj = CreateSetConstructorTemplate("Set");

            // The initial value of Set.prototype is the Set prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            // TODO fix
            obj.SetOwnProperty(JsSymbol.species._value, new GetSetPropertyDescriptor(
                get: CreateSetConstructorTemplate("get [Symbol.species]"),
                set: Undefined,
                PropertyFlag.Configurable));

            return obj;
        }

        public void Configure()
        {
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            var instance = _engine.Set.Construct(System.Array.Empty<JsValue>());

            if (arguments.Length > 0)
            {
                if (arguments.At(0) is IIterable it)
                {
                    var adder = instance.GetProperty("add")?.Value as ICallable
                                ?? ExceptionHelper.ThrowTypeError<FunctionInstance>(_engine);

                    var iterator = it.Iterator();
                    var array = _engine._jsValueArrayPool.RentArray(1);
                    do
                    {
                        var item = iterator.Next();
                        if (item.TryGetValue("done", out var done) && done.AsBoolean())
                        {
                            break;
                        }

                        if (!item.TryGetValue("value", out var currentValue))
                        {
                            break;
                        }

                        array[0] = currentValue;
                        adder.Call(instance, array);
                    } while (true);
                    _engine._jsValueArrayPool.ReturnArray(array);
                }
            }

            return instance;
        }
    }
}
