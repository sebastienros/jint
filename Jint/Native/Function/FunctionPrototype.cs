using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Function
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.4
    /// </summary>
    public sealed class FunctionPrototype : FunctionInstance
    {
        private FunctionPrototype(Engine engine)
            : base(engine, "Function", null, null, false)
        {
        }

        public static FunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new FunctionPrototype(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
            obj.Prototype = engine.Object.PrototypeObject;

            obj.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.AllForbidden));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("constructor", new PropertyDescriptor(Engine.Function, PropertyFlag.NonEnumerable));
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToString), true, false, true);
            FastAddProperty("apply", new ClrFunctionInstance(Engine, "apply", Apply, 2), true, false, true);
            FastAddProperty("call", new ClrFunctionInstance(Engine, "call", CallImpl, 1), true, false, true);
            FastAddProperty("bind", new ClrFunctionInstance(Engine, "bind", Bind, 1), true, false, true);
        }

        private JsValue Bind(JsValue thisObj, JsValue[] arguments)
        {
            var target = thisObj.TryCast<ICallable>(x =>
            {
                ExceptionHelper.ThrowTypeError(Engine);
            });

            var thisArg = arguments.At(0);
            var f = new BindFunctionInstance(Engine) {Extensible = true};
            f.TargetFunction = thisObj;
            f.BoundThis = thisArg;
            f.BoundArgs = arguments.Skip(1);
            f.Prototype = Engine.Function.PrototypeObject;

            var o = target as FunctionInstance;
            if (!ReferenceEquals(o, null))
            {
                var l = TypeConverter.ToNumber(o.Get("length")) - (arguments.Length - 1);
                f.SetOwnProperty("length", new PropertyDescriptor(System.Math.Max(l, 0), PropertyFlag.AllForbidden));
            }
            else
            {
                f.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.AllForbidden));
            }


            var thrower = Engine.Function.ThrowTypeError;
            const PropertyFlag flags = PropertyFlag.EnumerableSet | PropertyFlag.ConfigurableSet;
            f.DefineOwnProperty("caller", new GetSetPropertyDescriptor(thrower, thrower, flags), false);
            f.DefineOwnProperty("arguments", new GetSetPropertyDescriptor(thrower, thrower, flags), false);

            return f;
        }

        private JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            var func = thisObj.TryCast<FunctionInstance>();

            if (ReferenceEquals(func, null))
            {
                ExceptionHelper.ThrowTypeError(_engine, "Function object expected.");
            }

            return "function() {{ ... }}";
        }

        public JsValue Apply(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject.TryCast<ICallable>();
            var thisArg = arguments.At(0);
            var argArray = arguments.At(1);

            if (func == null)
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            if (argArray.IsNull() || argArray.IsUndefined())
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray.TryCast<ObjectInstance>();
            if (ReferenceEquals(argArrayObj, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var len = ((JsNumber) argArrayObj.Get("length"))._value;
            uint n = TypeConverter.ToUint32(len);

            var argList = _engine._jsValueArrayPool.RentArray((int) n);
            for (int index = 0; index < n; index++)
            {
                string indexName = TypeConverter.ToString(index);
                var nextArg = argArrayObj.Get(indexName);
                argList[index] = nextArg;
            }

            var result = func.Call(thisArg, argList);
            _engine._jsValueArrayPool.ReturnArray(argList);

            return result;
        }

        public JsValue CallImpl(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject.TryCast<ICallable>();
            if (func == null)
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return func.Call(arguments.At(0), arguments.Length == 0 ? arguments : arguments.Skip(1));
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Undefined;
        }
    }
}