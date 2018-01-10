using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Descriptors
{
    public class PropertyDescriptor : IPropertyDescriptor
    {
        public static readonly IPropertyDescriptor Undefined = new PropertyDescriptor();

        protected PropertyDescriptor()
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

        public PropertyDescriptor(IPropertyDescriptor descriptor)
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

        public static JsValue FromPropertyDescriptor(Engine engine, IPropertyDescriptor desc)
        {
            if (ReferenceEquals(desc, Undefined))
            {
                return Native.Undefined.Instance;
            }

            var obj = engine.Object.Construct(Arguments.Empty);

            if (desc.IsDataDescriptor())
            {
                obj.SetOwnProperty("value", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Value != null ? desc.Value : Native.Undefined.Instance));
                obj.SetOwnProperty("writable", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Writable.HasValue && desc.Writable.Value));
            }
            else
            {
                obj.SetOwnProperty("get", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Get ?? Native.Undefined.Instance));
                obj.SetOwnProperty("set", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Set ?? Native.Undefined.Instance));
            }

            obj.SetOwnProperty("enumerable", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Enumerable.HasValue && desc.Enumerable.Value));
            obj.SetOwnProperty("configurable", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Configurable.HasValue && desc.Configurable.Value));

            return obj;
        }
    }
}