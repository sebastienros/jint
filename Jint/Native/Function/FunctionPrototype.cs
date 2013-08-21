using System;
using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Function
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.4
    /// </summary>
    public sealed class FunctionPrototype : FunctionInstance
    {
        private FunctionPrototype(Engine engine):base(engine, null, null, false)
        {

        }

        public static FunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new FunctionPrototype(engine);

            // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
            obj.Prototype = engine.Object.PrototypeObject;

            obj.FastAddProperty("length", 0, false, false, false);

            obj.FastAddProperty("apply", new ClrFunctionInstance<object, object>(engine, obj.Apply), false, false, false);

            return obj;
        }

        public object Apply(object thisObject, object[] arguments)
        {
            if (arguments.Length != 2)
            {
                throw new ArgumentException("Apply has to be called with two arguments.");
            }

            var func = thisObject as ICallable;
            var thisArg = arguments[0];
            var argArray = arguments[1];

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            if (argArray == Null.Instance || argArray == Undefined.Instance)
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray as ObjectInstance;
            if (argArrayObj == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var len = argArrayObj.Get("length");
            var n = TypeConverter.ToUint32(len);
            var argList = new List<object>();
            for (var index = 0; index < n; index++)
            {
                var indexName = index.ToString();
                var nextArg = argArrayObj.Get(indexName);
                argList.Add(nextArg);
            }
            return func.Call(thisArg, argList.ToArray());
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Undefined.Instance;
        }
    }
}