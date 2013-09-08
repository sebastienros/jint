using System;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectPrototype : ObjectInstance
    {
        private ObjectPrototype(Engine engine) : base(engine)
        {
        }

        public static ObjectPrototype CreatePrototypeObject(Engine engine, ObjectConstructor objectConstructor)
        {
            var obj = new ObjectPrototype(engine) { Extensible = true };

            obj.FastAddProperty("constructor", objectConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, string>(Engine, ToString), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<object, object>(Engine, ToLocaleString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance<object, object>(Engine, ValueOf), true, false, true);
            FastAddProperty("hasOwnProperty", new ClrFunctionInstance<object, bool>(Engine, HasOwnProperty), true, false, true);
            FastAddProperty("isPrototypeOf", new ClrFunctionInstance<object, bool>(Engine, IsPrototypeOf), true, false, true);
            FastAddProperty("propertyIsEnumerable", new ClrFunctionInstance<object, bool>(Engine, PropertyIsEnumerable), true, false, true);
        }

        private bool PropertyIsEnumerable(object thisObject, object[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            if (desc == PropertyDescriptor.Undefined)
            {
                return false;
            }
            return desc.EnumerableIsSet;
        }

        private object ValueOf(object thisObject, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            return o;
        }

        private bool IsPrototypeOf(object thisObject, object[] arguments)
        {
            var v = arguments[0] as ObjectInstance;
            if (v == null)
            {
                return false;
            }

            var o = TypeConverter.ToObject(Engine, thisObject);
            while (true)
            {
                v = v.Prototype;
                if (v == null)
                {
                    return false;
                }
                if (o == v)
                {
                    return true;
                }

            }
        }

        private object ToLocaleString(object thisObject, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            var toString = o.Get("toString") as ICallable;
            if (toString == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }
            return toString.Call(o, Arguments.Empty);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.2
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public string ToString(object thisObject, object[] arguments)
        {
            if (thisObject == Undefined.Instance)
            {
                return "[object Undefined]";
            }

            if (thisObject == Null.Instance)
            {
                return "[object Null]";
            }

            var o = TypeConverter.ToObject(Engine, thisObject);
            return "[object " + o.Class + "]";
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.5
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public bool HasOwnProperty(object thisObject, object[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }
    }
}
