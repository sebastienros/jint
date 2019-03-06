using System;
using Jint.Native.Array;
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
            var obj = new FunctionPrototype(engine)
            {
                Extensible = true,
                // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
                Prototype = engine.Object.PrototypeObject
            };

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
            var f = new BindFunctionInstance(Engine)
            {
                Extensible = true,
                TargetFunction = thisObj,
                BoundThis = thisArg,
                BoundArgs = arguments.Skip(1),
                Prototype = Engine.Function.PrototypeObject
            };

            if (target is FunctionInstance functionInstance)
            {
                var l = TypeConverter.ToNumber(functionInstance.Get("length")) - (arguments.Length - 1);
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
            if (!(thisObj is FunctionInstance))
            {
                return ExceptionHelper.ThrowTypeError<FunctionInstance>(_engine, "Function object expected.");
            }

            return "function() {{ ... }}";
        }

        private JsValue Apply(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(Engine);
            var thisArg = arguments.At(0);
            var argArray = arguments.At(1);

            if (argArray.IsNullOrUndefined())
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray as ObjectInstance ?? ExceptionHelper.ThrowTypeError<ObjectInstance>(Engine);
            var operations = ArrayPrototype.ArrayOperations.For(argArrayObj);

            uint n = operations.GetLength();
            var argList = _engine._jsValueArrayPool.RentArray((int) n);
            for (uint i = 0; i < n; i++)
            {
                var nextArg = operations.Get(i);
                argList[i] = nextArg;
            }

            var result = func.Call(thisArg, argList);
            _engine._jsValueArrayPool.ReturnArray(argList);

            return result;
        }

        private JsValue CallImpl(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(Engine);
            JsValue[] values = ArrayExt.Empty<JsValue>();
            if (arguments.Length > 1)
            {
                values = new JsValue[arguments.Length - 1];
                System.Array.Copy(arguments, 1, values, 0, arguments.Length - 1);
            }

            var result = func.Call(arguments.At(0), values);

            return result;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Undefined;
        }
    }
}