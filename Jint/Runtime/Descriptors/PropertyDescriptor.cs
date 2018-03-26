using System;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Descriptors
{
    public class PropertyDescriptor : IPropertyDescriptor
    {
        [Flags]
        private enum PropertyFlag
        {
            Enumerable = 1,
            EnumerableSet = 2,
            Writable = 4,
            WritableSet = 8,
            Configurable = 16,
            ConfigurableSet = 32
        }

        public static readonly IPropertyDescriptor Undefined = new PropertyDescriptor();

        private PropertyFlag _flags;

        protected PropertyDescriptor()
        {
        }

        public PropertyDescriptor(JsValue value, bool? writable, bool? enumerable, bool? configurable)
        {
            Value = value;
            Writable = writable;
            Enumerable = enumerable;
            Configurable = configurable;
        }

        public PropertyDescriptor(IPropertyDescriptor descriptor)
        {
            Value = descriptor.Value;
            Enumerable = descriptor.Enumerable;
            Configurable = descriptor.Configurable;
            Writable = descriptor.Writable;
        }

        public virtual JsValue Get => null;
        public virtual JsValue Set => null;

        public bool? Enumerable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & PropertyFlag.EnumerableSet) != 0 ? (_flags & PropertyFlag.Enumerable) != 0 : (bool?) null;
            set
            {
                _flags &= ~(PropertyFlag.EnumerableSet | PropertyFlag.Enumerable);
                if (value != null)
                {
                    _flags |= PropertyFlag.EnumerableSet;
                    if (value.Value)
                    {
                        _flags |= PropertyFlag.Enumerable;
                    }
                }
            }
        }

        public bool? Writable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & PropertyFlag.WritableSet) != 0 ? (_flags & PropertyFlag.Writable) != 0 : (bool?) null;
            set
            {
                _flags &= ~(PropertyFlag.WritableSet | PropertyFlag.Writable);
                if (value != null)
                {
                    _flags |= PropertyFlag.WritableSet;
                    if (value.Value)
                    {
                        _flags |= PropertyFlag.Writable;
                    }
                }
            }
        }

        public bool? Configurable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & PropertyFlag.ConfigurableSet) != 0 ? (_flags & PropertyFlag.Configurable) != 0 : (bool?) null;
            set
            {
                _flags &= ~(PropertyFlag.ConfigurableSet | PropertyFlag.Configurable);
                if (value != null)
                {
                    _flags |= PropertyFlag.ConfigurableSet;
                    if (value.Value)
                    {
                        _flags |= PropertyFlag.Configurable;
                    }
                }
            }
        }

        public virtual JsValue Value { get; set; }

        public static PropertyDescriptor ToPropertyDescriptor(Engine engine, JsValue o)
        {
            var obj = o.TryCast<ObjectInstance>();
            if (obj == null)
            {
                throw new JavaScriptException(engine.TypeError);
            }

            var hasGetProperty = obj.HasProperty("get");
            var hasSetProperty = obj.HasProperty("set");
            
            if ((obj.HasProperty("value") || obj.HasProperty("writable")) &&
                (hasGetProperty || hasSetProperty))
            {
                throw new JavaScriptException(engine.TypeError);
            }

            var desc = hasGetProperty || hasSetProperty
                ? new GetSetPropertyDescriptor(null, null, null, null)
                : new PropertyDescriptor();

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

            if (hasGetProperty)
            {
                var getter = obj.Get("get");
                if (getter != JsValue.Undefined && getter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }

                ((GetSetPropertyDescriptor) desc).SetGet(getter);
            }

            if (hasSetProperty)
            {
                var setter = obj.Get("set");
                if (setter != Native.Undefined.Instance && setter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }

                ((GetSetPropertyDescriptor) desc).SetSet(setter);
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