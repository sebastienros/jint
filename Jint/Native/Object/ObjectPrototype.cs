using Jint.Collections;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectPrototype : ObjectInstance
    {
        private readonly Realm _realm;
        private readonly ObjectConstructor _constructor;

        internal ObjectPrototype(
            Engine engine,
            Realm realm,
            ObjectConstructor constructor) : base(engine)
        {
            _realm = realm;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(8, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, propertyFlags),
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
            var o = TypeConverter.ToObject(_realm, thisObject);
            var desc = o.GetOwnProperty(p);
            if (desc == PropertyDescriptor.Undefined)
            {
                return JsBoolean.False;
            }
            return desc.Enumerable;
        }

        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            return o;
        }

        private JsValue IsPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments[0];
            if (!arg.IsObject())
            {
                return JsBoolean.False;
            }

            var v = arg.AsObject();

            var o = TypeConverter.ToObject(_realm, thisObject);
            while (true)
            {
                v = v.Prototype;

                if (ReferenceEquals(v, null))
                {
                    return JsBoolean.False;
                }

                if (ReferenceEquals(o, v))
                {
                    return JsBoolean.True;
                }
            }
        }

        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            var func = o.Get("toString");
            var callable = func as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Can only invoke functions");
            }
            return TypeConverter.ToJsString(callable.Call(thisObject, arguments));
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

            var o = TypeConverter.ToObject(_realm, thisObject);

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
            var o = TypeConverter.ToObject(_realm, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }
    }
}
