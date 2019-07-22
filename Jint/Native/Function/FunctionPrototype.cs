using System;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Function
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.4
    /// </summary>
    public sealed class FunctionPrototype : FunctionInstance
    {
        private static readonly JsString _functionName = new JsString("Function");

        private FunctionPrototype(Engine engine)
            : base(engine, _functionName, strict: false)
        {
        }

        public static FunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new FunctionPrototype(engine)
            {
                Extensible = true,
                // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
                Prototype = engine.Object.PrototypeObject,
                _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero
            };

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(5)
            {
                ["constructor"] = new PropertyDescriptor(Engine.Function, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString), true, false, true),
                ["apply"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "apply", Apply, 2), true, false, true),
                ["call"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "call", CallImpl, 1), true, false, true),
                ["bind"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "bind", Bind, 1), true, false, true)
            };

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
                f.SetOwnProperty(KnownKeys.Length, new PropertyDescriptor(System.Math.Max(l, 0), PropertyFlag.AllForbidden));
            }
            else
            {
                f.SetOwnProperty(KnownKeys.Length, PropertyDescriptor.AllForbiddenDescriptor.NumberZero);
            }

            f.DefineOwnProperty(KnownKeys.Caller, _engine._getSetThrower, false);
            f.DefineOwnProperty(KnownKeys.Arguments, _engine._getSetThrower, false);

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
            var argList = operations.GetAll();

            var result = func.Call(thisArg, argList);

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