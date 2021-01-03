using Jint.Collections;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectPrototype : ObjectInstance
    {
        private ObjectConstructor _objectConstructor;

        private ObjectPrototype(Engine engine) : base(engine)
        {
        }

        public static ObjectPrototype CreatePrototypeObject(Engine engine, ObjectConstructor objectConstructor)
        {
            var obj = new ObjectPrototype(engine)
            {
                _objectConstructor = objectConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(8, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_objectConstructor, propertyFlags),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToObjectString, 0, lengthFlags), propertyFlags),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, lengthFlags), propertyFlags),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, lengthFlags), propertyFlags),
                ["hasOwnProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "hasOwnProperty", HasOwnProperty, 1, lengthFlags), propertyFlags),
                ["isPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isPrototypeOf", IsPrototypeOf, 1, lengthFlags), propertyFlags),
                ["propertyIsEnumerable"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "propertyIsEnumerable", PropertyIsEnumerable, 1, lengthFlags), propertyFlags)
            };
            SetProperties(properties);
        }

        private JsValue PropertyIsEnumerable(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToPropertyKey(arguments[0]);
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

                if (ReferenceEquals(o, v))
                {
                    return true;
                }

            }
        }

        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            var func = o.Get("toString");
            var callable = func as ICallable ?? ExceptionHelper.ThrowTypeErrorNoEngine<ICallable>("Can only invoke functions");
            return TypeConverter.ToString(callable.Call(thisObject, arguments));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.tostring
        /// </summary>
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

            var tag = o.Get(GlobalSymbolRegistry.ToStringTag);
            if (!tag.IsString())
            {
                tag = o.Class.ToString();
            }

            return "[object " + tag + "]";
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.5
        /// </summary>
        public JsValue HasOwnProperty(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToPropertyKey(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }
    }
}
