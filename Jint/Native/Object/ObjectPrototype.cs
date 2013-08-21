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
            obj.FastAddProperty("toString", new ClrFunctionInstance<object, string>(engine, obj.ToString), false, false, false);
            obj.FastAddProperty("hasOwnProperty", new ClrFunctionInstance<object, bool>(engine, obj.HasOwnProperty), false, false, false);

            return obj;
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
