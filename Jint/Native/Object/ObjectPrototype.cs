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
            FastAddProperty("toString", new ClrFunctionInstance<object, string>(Engine, ToString), false, false, false);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<object, string>(Engine, ToLocaleString), false, false, false);
            FastAddProperty("valueOf", new ClrFunctionInstance<object, object>(Engine, ValueOf), false, false, false);
            FastAddProperty("hasOwnProperty", new ClrFunctionInstance<object, bool>(Engine, HasOwnProperty), false, false, false);
            FastAddProperty("isPrototypeOf", new ClrFunctionInstance<object, bool>(Engine, IsPrototypeOf), false, false, false);
            FastAddProperty("propertyIsEnumerable", new ClrFunctionInstance<object, bool>(Engine, PropertyIsEnumerable), false, false, false);
        }
        private bool PropertyIsEnumerable(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ValueOf(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private bool IsPrototypeOf(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private string ToLocaleString(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
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
