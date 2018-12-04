using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Set
{
    public sealed class SetConstructor : FunctionInstance, IConstructor
    {
        private SetConstructor(Engine engine, string name) : base(engine, name, null, null, false)
        {
        }

        public SetPrototype PrototypeObject { get; private set; }

        public static SetConstructor CreateSetConstructor(Engine engine)
        {
            SetConstructor CreateSetConstructorTemplate(string name)
            {
                var ctr = new SetConstructor(engine, name);
                ctr.Extensible = true;

                // The value of the [[Prototype]] internal property of the Set constructor is the Function prototype object
                ctr.Prototype = engine.Function.PrototypeObject;
                ctr.PrototypeObject = SetPrototype.CreatePrototypeObject(engine, ctr);

                ctr.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Configurable));
                return ctr;
            }

            var obj = CreateSetConstructorTemplate("Set");

            // The initial value of Set.prototype is the Set prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            obj.SetOwnProperty(GlobalSymbolRegistry.Species._value,
                new GetSetPropertyDescriptor(
                    get: new ClrFunctionInstance(engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable),
                    set: Undefined,
                    PropertyFlag.Configurable));

            return obj;
        }

        public void Configure()
        {
        }

        private static JsValue Species(JsValue thisObject, JsValue[] arguments)
        {
            return thisObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Constructor Set requires 'new'");
            }

            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            var instance = new SetInstance(Engine)
            {
                Prototype = PrototypeObject,
                Extensible = true
            };
            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var iterator = arguments.At(0).GetIterator(_engine);
                var protocol = new SetProtocol(_engine, instance, iterator);
                protocol.Execute();
            }

            return instance;
        }

        private sealed class SetProtocol : IteratorProtocol
        {
            private readonly SetInstance _instance;
            private readonly ICallable _adder;

            public SetProtocol(
                Engine engine,
                SetInstance instance,
                IIterator iterator) : base(engine, iterator, 1)
            {
                _instance = instance;
                var setterProperty = instance.GetProperty("add");

                if (setterProperty is null
                    || !setterProperty.TryGetValue(instance, out var setterValue)
                    || (_adder = setterValue as ICallable) is null)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "add must be callable");
                }
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                args[0] = ExtractValueFromIteratorInstance(currentValue);
                _adder.Call(_instance, args);
            }
        }
    }
}