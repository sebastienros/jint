using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Descriptors
{
    public enum Fields
    {
        Get,
        Set,
        Enumerable,
        Configurable,
        Writable,
        Value
    }

    public interface IPropertyField
    {
        bool IsPresent { get; }

        bool IsAbsent { get; }

        object Value { get; }
    }

    public struct Field<T> : IPropertyField
    {
        private readonly T _field;
        private readonly bool _present;

        public Field(T value)
        {
            _field = value;
            _present = true;
        }

        public bool IsPresent
        {
            get { return _present; }
        }

        public bool IsAbsent
        {
            get { return !_present; }
        }

        public T Value
        {
            get
            {
                return _field;
            }
        }

        bool IPropertyField.IsPresent
        {
            get { return IsPresent; }
        }

        bool IPropertyField.IsAbsent
        {
            get { return IsAbsent; }
        }

        object IPropertyField.Value
        {
            get
            {
                return Value;
            }
        }
    }

    public class PropertyDescriptor
    {
        public static PropertyDescriptor Undefined = new PropertyDescriptor();

        public PropertyDescriptor()
        {
            Get = new Field<object>();
            Set = new Field<object>();
            Value = new Field<object>();
            Enumerable = new Field<bool>();
            Configurable = new Field<bool>();
            Writable = new Field<bool>();
        }

        public PropertyDescriptor(object value, bool? writable, bool? enumerable, bool? configurable)
        {
            Value = value == null ? new Field<object>() : new Field<object>(value);
            Writable = writable == null ? new Field<bool>() : new Field<bool>(writable.Value);
            Enumerable = enumerable == null ? new Field<bool>() : new Field<bool>(enumerable.Value);
            Configurable = configurable == null ? new Field<bool>() : new Field<bool>(configurable.Value);

            Get = new Field<object>();
            Set = new Field<object>();
        }

        public PropertyDescriptor(object get, object set, bool? enumerable = null, bool? configurable = null)
        {
            Get = get == null ? new Field<object>() : new Field<object>(get);
            Set = set == null ? new Field<object>() : new Field<object>(set);

            Enumerable = enumerable == null ? new Field<bool>() : new Field<bool>(enumerable.Value);
            Configurable = configurable == null ? new Field<bool>() : new Field<bool>(configurable.Value);

            Value = new Field<object>();
            Writable = new Field<bool>();
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

        public Field<object> Get { get; set; }
        public Field<object> Set { get; set; }
        public Field<bool> Enumerable { get; set; }
        public Field<bool> Writable { get; set; }
        public Field<bool> Configurable { get; set; }
        public Field<object> Value { get; set; }

        public IDictionary<string, IPropertyField> AllFields
        {
            get
            {
                return new Dictionary<string, IPropertyField>
                {
                    {"Get", Get},
                    {"Set", Set},
                    {"Enumerable", Enumerable},
                    {"Configurable", Configurable},
                    {"Writable", Writable},
                    {"Value", Value},
                };
            }
        }

        public bool IsAccessorDescriptor()
        {
            if (Get.IsAbsent && Set.IsAbsent)
            {
                return false;
            }

            return true;
        }

        public bool IsDataDescriptor()
        {
            if (Writable.IsAbsent && Value.IsAbsent)
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

        public static PropertyDescriptor ToPropertyDescriptor(Engine engine, object o)
        {
            var obj = o as ObjectInstance;

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
                desc.Enumerable = new Field<bool>(TypeConverter.ToBoolean(obj.Get("enumerable")));
            }

            if (obj.HasProperty("configurable"))
            {
                desc.Configurable = new Field<bool>(TypeConverter.ToBoolean(obj.Get("configurable")));
            }

            if (obj.HasProperty("value"))
            {
                var value = obj.Get("value");
                desc.Value = new Field<object>(value);
            }

            if (obj.HasProperty("writable"))
            {
                desc.Writable = new Field<bool>(TypeConverter.ToBoolean(obj.Get("writable")));
            }

            if (obj.HasProperty("get"))
            {
                var getter = obj.Get("get");
                if (getter != Native.Undefined.Instance && !(getter is ICallable))
                {
                    throw new JavaScriptException(engine.TypeError);
                }
                desc.Get = new Field<object>(getter);
            }

            if (obj.HasProperty("set"))
            {
                var setter = obj.Get("set");
                if (setter != Native.Undefined.Instance && !(setter is ICallable))
                {
                    throw new JavaScriptException(engine.TypeError);
                }
                desc.Set = new Field<object>(setter);
            }

            if (desc.Get.IsPresent || desc.Get.IsPresent)
            {
                if (desc.Value.IsPresent || desc.Writable.IsPresent)
                {
                    throw new JavaScriptException(engine.TypeError);
                }
            }

            return desc;
        }

        public static object FromPropertyDescriptor(Engine engine, PropertyDescriptor desc)
        {
            if (desc == Undefined)
            {
                return Native.Undefined.Instance;
            }

            var obj = engine.Object.Construct(Arguments.Empty);

            if (desc.IsDataDescriptor())
            {
                obj.DefineOwnProperty("value", new PropertyDescriptor(value: desc.Value.Value, writable: true, enumerable: true, configurable: true ), false);
                obj.DefineOwnProperty("writable", new PropertyDescriptor(value: desc.Writable.IsPresent && desc.Writable.Value, writable: true, enumerable: true, configurable: true ), false);
            }
            else
            {
                obj.DefineOwnProperty("get", new PropertyDescriptor(desc.Get.Value ?? Native.Undefined.Instance, writable: true, enumerable: true, configurable: true ), false);
                obj.DefineOwnProperty("set", new PropertyDescriptor(desc.Set.Value ?? Native.Undefined.Instance, writable: true, enumerable: true, configurable: true), false);
            }

            obj.DefineOwnProperty("enumerable", new PropertyDescriptor(value: desc.Enumerable.IsPresent && desc.Enumerable.Value, writable: true, enumerable: true, configurable: true), false);
            obj.DefineOwnProperty("configurable", new PropertyDescriptor(value: desc.Configurable.IsPresent && desc.Configurable.Value, writable: true, enumerable: true, configurable: true), false);

            return obj;
        }
    }
}