using System.Collections.Generic;
using System.Linq;
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
        private FunctionPrototype(Engine engine) : base(engine, null, null, false)
        {
        }

        public static FunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new FunctionPrototype(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
            obj.Prototype = engine.Object.PrototypeObject;

            obj.FastAddProperty("length", 0, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("constructor", Engine.Function, true, false, true);
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToString), true, false, true);
            FastAddProperty("apply", new ClrFunctionInstance(Engine, Apply, 2), true, false, true);
            FastAddProperty("call", new ClrFunctionInstance(Engine, CallImpl, 1), true, false, true);
            FastAddProperty("bind", new ClrFunctionInstance(Engine, Bind, 1), true, false, true);
        }

        private JsValue Bind(JsValue thisObj, JsValue[] arguments)
        {
            var target = thisObj.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError);
            });
            
            var thisArg = arguments.At(0);
            var f = new BindFunctionInstance(Engine) {Extensible = true};
            f.TargetFunction = thisObj;
            f.BoundThis = thisArg;
            f.BoundArgs = arguments.Skip(1).ToArray();
            f.Prototype = Engine.Function.PrototypeObject;

            var o = target as FunctionInstance;
            if (o != null)
            {
                var l = TypeConverter.ToNumber(o.Get("length")) - (arguments.Length - 1);
                f.FastAddProperty("length", System.Math.Max(l, 0), false, false, false); 
            }
            else
            {
                f.FastAddProperty("length", 0, false, false, false); 
            }
            

            var thrower = Engine.Function.ThrowTypeError;
            f.DefineOwnProperty("caller", new PropertyDescriptor(thrower, thrower, false, false), false);
            f.DefineOwnProperty("arguments", new PropertyDescriptor(thrower, thrower, false, false), false);


            return f;
        }

        private JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            var func = thisObj.TryCast<FunctionInstance>();

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Function object expected.");       
            }

            return System.String.Format("function() {{ ... }}");
        }

        public JsValue Apply(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject.TryCast<ICallable>();
            var thisArg = arguments.At(0);
            var argArray = arguments.At(1);

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            if (argArray == Null.Instance || argArray == Undefined.Instance)
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray.TryCast<ObjectInstance>();
            if (argArrayObj == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var len = argArrayObj.Get("length").AsNumber();
            uint n = TypeConverter.ToUint32(len);
            var argList = new List<JsValue>();
            for (int index = 0; index < n; index++)
            {
                string indexName = index.ToString();
                var nextArg = argArrayObj.Get(indexName);
                argList.Add(nextArg);
            }
            return func.Call(thisArg, argList.ToArray());
        }

        public JsValue CallImpl(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject.TryCast<ICallable>();
            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return func.Call(arguments.At(0), arguments.Length == 0 ? arguments : arguments.Skip(1).ToArray());
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Undefined.Instance;
        }
    }
}