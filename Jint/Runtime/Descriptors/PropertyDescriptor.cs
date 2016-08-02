using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Descriptors
{
    public class PropertyDescriptor
    {
        public static PropertyDescriptor Undefined = new PropertyDescriptor();

        public PropertyDescriptor()
        {
        }

        public PropertyDescriptor(JsValue value, bool? writable, bool? enumerable, bool? configurable)
        {
            Value = value;

            if (writable.HasValue)
            {
                Writable = writable.Value;
            }

            if (enumerable.HasValue)
            {
                Enumerable = enumerable.Value;
            }

            if (configurable.HasValue)
            {
                Configurable = configurable.Value;
            }
        }

        public PropertyDescriptor(JsValue get, JsValue set, bool? enumerable = null, bool? configurable = null)
        {
            Get = get;
            Set = set;

            if (enumerable.HasValue)
            {
                Enumerable = enumerable.Value;
            }

            if (configurable.HasValue)
            {
                Configurable = configurable.Value;
            }
        }

        public PropertyDescriptor(PropertyDescriptor descriptor)
        {
            Get = descriptor.Get;
            Set = descriptor.Set;
            Value = descriptor.Value;
            Enumerable = descriptor.Enumerable;
            Configurable = descriptor.Configurable;
            Writable = descriptor.Writable;
        }

        public JsValue Get { get; set; }
        public JsValue Set { get; set; }
        public bool? Enumerable { get; set; }
        public bool? Writable { get; set; }
        public bool? Configurable { get; set; }
        public virtual JsValue Value { get; set; }

        public bool IsAccessorDescriptor()
        {
            if (Get ==null && Set == null)
            {
                return false;
            }

            return true;
        }

        public bool IsDataDescriptor()
        {
            if (!Writable.HasValue && Value == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.3
        /// </summary>
        /// <returns></returns>
        public bool IsGenericDescriptor()
        {
            return !IsDataDescriptor() && !IsAccessorDescriptor();
        }

        public static PropertyDescriptor ToPropertyDescriptor(Engine engine, JsValue o)
        {
            var obj = o.TryCast<ObjectInstance>();
            if (obj == null)
            {
                throw new JavaScriptException(engine.TypeError);
            }

            if ((obj.HasProperty("value") || obj.HasProperty("writable")) &&
                (obj.HasProperty("get") || obj.HasProperty("set")))
            {
                throw new JavaScriptException(engine.TypeError);
            }

            var desc = new PropertyDescriptor();

            if (obj.HasProperty("enumerable"))
            {
                desc.Enumerable = TypeConverter.ToBoolean(obj.Get("enumerable"));
            }

            if (obj.HasProperty("configurable"))
            {
                desc.Configurable = TypeConverter.ToBoolean(obj.Get("configurable"));
            }

            if (obj.HasProperty("value"))
            {
                var value = obj.Get("value");
                desc.Value = value;
            }

            if (obj.HasProperty("writable"))
            {
                desc.Writable = TypeConverter.ToBoolean(obj.Get("writable"));
            }

            if (obj.HasProperty("get"))
            {
                var getter = obj.Get("get");
                if (getter != JsValue.Undefined && getter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }
                desc.Get = getter;
            }

            if (obj.HasProperty("set"))
            {
                var setter = obj.Get("set");
                if (setter != Native.Undefined.Instance && setter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }
                desc.Set = setter;
            }

            if (desc.Get != null || desc.Get != null)
            {
                if (desc.Value != null || desc.Writable.HasValue)
                {
                    throw new JavaScriptException(engine.TypeError);
                }
            }

            return desc;
        }

        public static JsValue FromPropertyDescriptor(Engine engine, PropertyDescriptor desc)
        {
            if (desc == Undefined)
            {
                return Native.Undefined.Instance;
            }

            var obj = engine.Object.Construct(Arguments.Empty);

            if (desc.IsDataDescriptor())
            {
                obj.DefineOwnProperty("value", new PropertyDescriptor(value: desc.Value != null ? desc.Value : Native.Undefined.Instance, writable: true, enumerable: true, configurable: true ), false);
                obj.DefineOwnProperty("writable", new PropertyDescriptor(value: desc.Writable.HasValue && desc.Writable.Value, writable: true, enumerable: true, configurable: true), false);
            }
            else
            {
                obj.DefineOwnProperty("get", new PropertyDescriptor(desc.Get ?? Native.Undefined.Instance, writable: true, enumerable: true, configurable: true ), false);
                obj.DefineOwnProperty("set", new PropertyDescriptor(desc.Set ?? Native.Undefined.Instance, writable: true, enumerable: true, configurable: true), false);
            }

            obj.DefineOwnProperty("enumerable", new PropertyDescriptor(value: desc.Enumerable.HasValue && desc.Enumerable.Value, writable: true, enumerable: true, configurable: true), false);
            obj.DefineOwnProperty("configurable", new PropertyDescriptor(value: desc.Configurable.HasValue && desc.Configurable.Value, writable: true, enumerable: true, configurable: true), false);

            return obj;
        }
    }
}