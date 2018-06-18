using System;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Descriptors
{
    public class PropertyDescriptor
    {
        public static readonly PropertyDescriptor Undefined = new PropertyDescriptor(PropertyFlag.None);

        private PropertyFlag _flags;
        private JsValue _value;

        protected PropertyDescriptor(PropertyFlag flags)
        {
            _flags = flags;
        }
        
        
        protected internal PropertyDescriptor(JsValue value, PropertyFlag flags) : this(flags)
        {
            Value = value;
        }

        public PropertyDescriptor(JsValue value, bool? writable, bool? enumerable, bool? configurable)
        {
            Value = value;

            if (writable != null)
            {
                Writable = writable.Value;
                WritableSet = true;
            }

            if (enumerable != null)
            {
                Enumerable = enumerable.Value;
                EnumerableSet = true;
            }

            if (configurable != null)
            {
                Configurable = configurable.Value;
                ConfigurableSet = true;
            }
        }

        public PropertyDescriptor(PropertyDescriptor descriptor)
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

        public JsValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((_flags & PropertyFlag.CustomJsValue) != 0)
                {
                    return CustomValue;
                }
                
                return _value;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if ((_flags & PropertyFlag.CustomJsValue) != 0)
                {
                    CustomValue = value;
                }
                _value = value;
            }
        }

        protected virtual JsValue CustomValue
        {
            get => null;
            set => throw new NotImplementedException();
        }

        internal PropertyFlag Flags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _flags; }
        }

        public static PropertyDescriptor ToPropertyDescriptor(Engine engine, JsValue o)
        {
            var obj = o.TryCast<ObjectInstance>();
            if (ReferenceEquals(obj, null))
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
                ? new GetSetPropertyDescriptor(null, null, PropertyFlag.None)
                : new PropertyDescriptor(PropertyFlag.None);

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
                if (!getter.IsUndefined() && getter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }

                ((GetSetPropertyDescriptor) desc).SetGet(getter);
            }

            if (hasSetProperty)
            {
                var setter = obj.UnwrapJsValue(setProperty);
                if (!setter.IsUndefined() && setter.TryCast<ICallable>() == null)
                {
                    throw new JavaScriptException(engine.TypeError);
                }

                ((GetSetPropertyDescriptor) desc).SetSet(setter);
            }

            if (!ReferenceEquals(desc.Get, null))
            {
                if (!ReferenceEquals(desc.Value, null) || desc.WritableSet)
                {
                    throw new JavaScriptException(engine.TypeError);
                }
            }

            return desc;
        }

        public static JsValue FromPropertyDescriptor(Engine engine, PropertyDescriptor desc)
        {
            if (ReferenceEquals(desc, Undefined))
            {
                return Native.Undefined.Instance;
            }
            
            var obj = engine.Object.Construct(Arguments.Empty);

            if (desc.IsDataDescriptor())
            {
                obj.SetOwnProperty("value", new PropertyDescriptor(desc.Value ?? Native.Undefined.Instance, PropertyFlag.ConfigurableEnumerableWritable));
                obj.SetOwnProperty("writable", new PropertyDescriptor(desc.Writable, PropertyFlag.ConfigurableEnumerableWritable));
            }
            else
            {
                obj.SetOwnProperty("get", new PropertyDescriptor(desc.Get ?? Native.Undefined.Instance, PropertyFlag.ConfigurableEnumerableWritable));
                obj.SetOwnProperty("set", new PropertyDescriptor(desc.Set ?? Native.Undefined.Instance, PropertyFlag.ConfigurableEnumerableWritable));
            }

            obj.SetOwnProperty("enumerable", new PropertyDescriptor(desc.Enumerable, PropertyFlag.ConfigurableEnumerableWritable));
            obj.SetOwnProperty("configurable", new PropertyDescriptor(desc.Configurable, PropertyFlag.ConfigurableEnumerableWritable));

            return obj;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAccessorDescriptor()
        {
            return !ReferenceEquals(Get, null) || !ReferenceEquals(Set, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDataDescriptor()
        {
            return WritableSet || !ReferenceEquals(Value, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.3
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsGenericDescriptor()
        {
            return !IsDataDescriptor() && !IsAccessorDescriptor();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetValue(ObjectInstance thisArg, out JsValue value)
        {
            value = JsValue.Undefined;

            if (this == Undefined)
            {
                value = JsValue.Undefined;
                return false;
            }

            if (IsDataDescriptor())
            {
                var val = Value;
                if (!ReferenceEquals(val, null))
                {
                    value = val;
                    return true;
                }
            }

            var getter = Get;
            if (!ReferenceEquals(getter, null) && !getter.IsUndefined())
            {
                // if getter is not undefined it must be ICallable
                var callable = getter.TryCast<ICallable>();
                value = callable.Call(thisArg, Arguments.Empty);
            }

            return true;
        }
    }
}