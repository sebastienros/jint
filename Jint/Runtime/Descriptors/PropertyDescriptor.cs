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

            Writable = writable.GetValueOrDefault();
            WritableSet = writable != null;

            Enumerable = enumerable.GetValueOrDefault();
            EnumerableSet = enumerable != null;

            Configurable = configurable.GetValueOrDefault();
            ConfigurableSet = configurable != null;
        }

        public PropertyDescriptor(IPropertyDescriptor descriptor)
        {
            Value = descriptor.Value;

            Enumerable = descriptor.Enumerable;
            EnumerableSet = descriptor.EnumerableSet;
            
            Configurable = descriptor.Configurable;
            ConfigurableSet = descriptor.ConfigurableSet;

            Writable = descriptor.Writable;
            WritableSet = descriptor.WritableSet;
        }

        public virtual JsValue Get => null;
        public virtual JsValue Set => null;

        public bool Enumerable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & PropertyFlag.Enumerable) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _flags |= PropertyFlag.EnumerableSet;
                if (value)
                {
                    _flags |= PropertyFlag.Enumerable;
                }
                else
                {
                    _flags &= ~(PropertyFlag.Enumerable);
                }
            }
        }
                
        public bool EnumerableSet
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & (PropertyFlag.EnumerableSet | PropertyFlag.Enumerable)) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set
            {
                if (value)
                {
                    _flags |= PropertyFlag.EnumerableSet;
                }
                else
                {
                    _flags &= ~(PropertyFlag.EnumerableSet);
                }
            }
        }

        public bool Writable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & PropertyFlag.Writable) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _flags |= PropertyFlag.WritableSet;
                if (value)
                {
                    _flags |= PropertyFlag.Writable;
                }
                else
                {
                    _flags &= ~(PropertyFlag.Writable);
                }
            }
        }

        public bool WritableSet
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set
            {
                if (value)
                {
                    _flags |= PropertyFlag.WritableSet;
                }
                else
                {
                    _flags &= ~(PropertyFlag.WritableSet);
                }
            }
        }

        public bool Configurable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & PropertyFlag.Configurable) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _flags |= PropertyFlag.ConfigurableSet;
                if (value)
                {
                    _flags |= PropertyFlag.Configurable;
                }
                else
                {
                    _flags &= ~(PropertyFlag.Configurable);
                }
            }
        }
        
        public bool ConfigurableSet
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & (PropertyFlag.ConfigurableSet | PropertyFlag.Configurable)) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set
            {
                if (value)
                {
                    _flags |= PropertyFlag.ConfigurableSet;
                }
                else
                {
                    _flags &= ~(PropertyFlag.ConfigurableSet);
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

            var getProperty = obj.GetProperty("get");
            var hasGetProperty = getProperty != Undefined;
            var setProperty = obj.GetProperty("set");
            var hasSetProperty = setProperty != Undefined;
            
            if ((obj.HasProperty("value") || obj.HasProperty("writable")) &&
                (hasGetProperty || hasSetProperty))
            {
                throw new JavaScriptException(engine.TypeError);
            }

            var desc = hasGetProperty || hasSetProperty
                ? new GetSetPropertyDescriptor(null, null, null, null)
                : new PropertyDescriptor();

            var enumerableProperty = obj.GetProperty("enumerable");
            if (enumerableProperty != Undefined)
            {
                desc.Enumerable = TypeConverter.ToBoolean(obj.UnwrapJsValue(enumerableProperty));
                desc.EnumerableSet = true;
            }

            var configurableProperty = obj.GetProperty("configurable");
            if (configurableProperty != Undefined)
            {
                desc.Configurable = TypeConverter.ToBoolean(obj.UnwrapJsValue(configurableProperty));
                desc.ConfigurableSet = true;
            }

            var valueProperty = obj.GetProperty("value");
            if (valueProperty != Undefined)
            {
                desc.Value = obj.UnwrapJsValue(valueProperty);
            }

            var writableProperty = obj.GetProperty("writable");
            if (writableProperty != Undefined)
            {
                desc.Writable = TypeConverter.ToBoolean(obj.UnwrapJsValue(writableProperty));
                desc.WritableSet = true;
            }

            if (hasGetProperty)
            {
                var getter = obj.UnwrapJsValue(getProperty);
                if (getter != JsValue.Undefined && getter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }

                ((GetSetPropertyDescriptor) desc).SetGet(getter);
            }

            if (hasSetProperty)
            {
                var setter = obj.UnwrapJsValue(setProperty);
                if (setter != Native.Undefined.Instance && setter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }

                ((GetSetPropertyDescriptor) desc).SetSet(setter);
            }

            if (desc.Get != null || desc.Get != null)
            {
                if (desc.Value != null || desc.WritableSet)
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
                obj.SetOwnProperty("writable", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Writable));
            }
            else
            {
                obj.SetOwnProperty("get", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Get ?? Native.Undefined.Instance));
                obj.SetOwnProperty("set", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Set ?? Native.Undefined.Instance));
            }

            obj.SetOwnProperty("enumerable", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Enumerable));
            obj.SetOwnProperty("configurable", new ConfigurableEnumerableWritablePropertyDescriptor(desc.Configurable));

            return obj;
        }
    }
}