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

            obj.FastAddProperty("constructor", objectConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToObjectString), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, "valueOF", ValueOf), true, false, true);
            FastAddProperty("hasOwnProperty", new ClrFunctionInstance(Engine, "hasOwnProperty", HasOwnProperty, 1), true, false, true);
            FastAddProperty("isPrototypeOf", new ClrFunctionInstance(Engine, "isPrototypeOf", IsPrototypeOf, 1), true, false, true);
            FastAddProperty("propertyIsEnumerable", new ClrFunctionInstance(Engine, "propertyIsEnumerable", PropertyIsEnumerable, 1), true, false, true);
        }

        private JsValue PropertyIsEnumerable(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            if (desc == PropertyDescriptor.Undefined)
            {
                return false;
            }
            return desc.Enumerable;
        }

        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            return o;
        }

        private JsValue IsPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments[0];
            if (!arg.IsObject())
            {
                return false;
            }

            var v = arg.AsObject();

            var o = TypeConverter.ToObject(Engine, thisObject);
            while (true)
            {
                v = v.Prototype;

                if (ReferenceEquals(v, null))
                {
                    return false;
                }

                if (o == v)
                {
                    return true;
                }

            }
        }

        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            var toString = o.Get("toString").TryCast<ICallable>(x =>
            {
                ExceptionHelper.ThrowTypeError(Engine);
            });

            return toString.Call(o, Arguments.Empty);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.2
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public JsValue ToObjectString(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                return "[object Undefined]";
            }

            if (thisObject.IsNull())
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
        public JsValue HasOwnProperty(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }
    }
}
