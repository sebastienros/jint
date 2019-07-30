using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public sealed class PromiseConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Promise");

        private PromiseConstructor(Engine engine)
            : base(engine, _functionName, false)
        {
        }

        public PromisePrototype PrototypeObject { get; private set; }

        public static PromiseConstructor CreatePromiseConstructor(Engine engine)
        {
            var obj = new PromiseConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Set constructor is the Function prototype object
            obj.PrototypeObject = PromisePrototype.CreatePrototypeObject(engine, obj);
            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);
            obj._prototype = obj.PrototypeObject;

            return obj;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, checkExistingKeys: false)
            {
                ["resolve"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "resolve", Resolve, 1), PropertyFlag.NonEnumerable)),
                ["reject"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "reject", Reject, 1), PropertyFlag.NonEnumerable)),
            };
            SetProperties(properties);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Constructor Set requires 'new'");
            }

            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue receiver)
        {
            FunctionInstance promiseResolver = null;

            if (arguments.Length == 0 || (promiseResolver = arguments[0] as FunctionInstance) == null)
                ExceptionHelper.ThrowTypeError(_engine, $"Promise resolver {(arguments.Length >= 1 ? arguments[0].Type.ToString() : Undefined.ToString())} is not a function");

            var instance = new PromiseInstance(Engine, promiseResolver)
            {
                _prototype = PrototypeObject
            };

            instance.InvokePromiseResolver();

            return instance;
        }

        public PromiseInstance Resolve(JsValue thisRef, JsValue[] args) => PromiseInstance.CreateResolved(Engine, args.Length >= 1 ? args[0] : Undefined);
        public PromiseInstance Reject(JsValue thisRef, JsValue[] args) => PromiseInstance.CreateRejected(Engine, args.Length >= 1 ? args[0] : Undefined);
    }
}