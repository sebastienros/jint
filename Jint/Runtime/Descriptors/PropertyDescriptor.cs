using Jint.Native;
using Jint.Native.Object;
using System;

namespace Jint.Runtime.Descriptors
{
    [Flags]
    public enum DescriptorAttributes
    {
        None = 0,
        Enumerable = 1,
        Writable = 2,
        Configurable = 4,
        All = Enumerable | Writable | Configurable
    }

    public class PropertyDescriptor {
        public static PropertyDescriptor Undefined = new PropertyDescriptor();

        public PropertyDescriptor() {
        }

        public PropertyDescriptor(JsValue? value) {
            Value = value;
        }

        public PropertyDescriptor(JsValue? value, DescriptorAttributes attributes) 
        {
            Value = value;
            Attributes = attributes;
        }

        public PropertyDescriptor(JsValue? get, JsValue? set) {
            Get = get;
            Set = set;
        }

        public PropertyDescriptor(JsValue? get, JsValue? set, DescriptorAttributes attributes) {
            Get = get;
            Set = set;
            Attributes = attributes;
        }

        public PropertyDescriptor(PropertyDescriptor descriptor) {
            Get = descriptor.Get;
            Set = descriptor.Set;
            Value = descriptor.Value;
            Attributes = descriptor.Attributes;
        }

        public JsValue? Get { get; set; }
        public JsValue? Set { get; set; }
        public DescriptorAttributes Attributes { get; set; }
        public virtual JsValue? Value { get; set; }
        
        public bool IsAccessorDescriptor()
        {
            if (!Get.HasValue && !Set.HasValue)
            {
                return false;
            }

            return true;
        }

        public bool Enumerable { get { return (Attributes & DescriptorAttributes.Enumerable) == DescriptorAttributes.Enumerable; } }
        public bool Writable { get { return (Attributes & DescriptorAttributes.Writable) == DescriptorAttributes.Writable; } }
        public bool Configurable { get { return (Attributes & DescriptorAttributes.Configurable) == DescriptorAttributes.Configurable; } }

        public bool IsDataDescriptor()
        {
            if (!Writable && !Value.HasValue)
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
                if(TypeConverter.ToBoolean(obj.Get("enumerable"))) 
                    desc.Attributes |= DescriptorAttributes.Enumerable;
                else
                    desc.Attributes &= ~DescriptorAttributes.Enumerable;
            }

            if (obj.HasProperty("writable")) {
                if (TypeConverter.ToBoolean(obj.Get("writable")))
                    desc.Attributes |= DescriptorAttributes.Writable;
                else
                    desc.Attributes &= ~DescriptorAttributes.Writable;
            }

            if (obj.HasProperty("configurable")) {
                if (TypeConverter.ToBoolean(obj.Get("configurable")))
                    desc.Attributes |= DescriptorAttributes.Configurable;
                else
                    desc.Attributes &= ~DescriptorAttributes.Configurable;
            }

            if (obj.HasProperty("value"))
            {
                var value = obj.Get("value");
                desc.Value = value;
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

            if (desc.Get.HasValue || desc.Get.HasValue)
            {
                if (desc.Value.HasValue || desc.Writable)
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
                obj.DefineOwnProperty("value", new PropertyDescriptor(desc.Value.HasValue ? desc.Value.Value : Native.Undefined.Instance, DescriptorAttributes.All ), false);
                obj.DefineOwnProperty("writable", new PropertyDescriptor(desc.Writable, DescriptorAttributes.All), false);
            }
            else
            {
                obj.DefineOwnProperty("get", new PropertyDescriptor(desc.Get ?? Native.Undefined.Instance, DescriptorAttributes.All), false);
                obj.DefineOwnProperty("set", new PropertyDescriptor(desc.Set ?? Native.Undefined.Instance, DescriptorAttributes.All), false);
            }

            obj.DefineOwnProperty("enumerable", new PropertyDescriptor(desc.Enumerable, DescriptorAttributes.All), false);
            obj.DefineOwnProperty("configurable", new PropertyDescriptor(desc.Configurable, DescriptorAttributes.All), false);

            return obj;
        }

        public PropertyDescriptor WithAttributes(bool? writable, bool? enumerable, bool? configurable) {

            if (writable.HasValue && writable.Value) {
                Attributes |= DescriptorAttributes.Writable;
            }

            if (enumerable.HasValue && enumerable.Value) {
                Attributes |= DescriptorAttributes.Enumerable;
            }

            if (configurable.HasValue && configurable.Value) {
                Attributes |= DescriptorAttributes.Configurable;
            }

            return this;
        }

        public PropertyDescriptor WithWritable() {
            Attributes |= DescriptorAttributes.Writable;
            return this;
        }

        public PropertyDescriptor WithEnumerable() {
            Attributes |= DescriptorAttributes.Enumerable;
            return this;
        }

        public PropertyDescriptor WithConfigurable() {
            Attributes |= DescriptorAttributes.Configurable;
            return this;
        }

        public PropertyDescriptor WithNotWritable() {
            Attributes &= ~DescriptorAttributes.Writable;
            return this;
        }

        public PropertyDescriptor WithNotEnumerable() {
            Attributes &= ~DescriptorAttributes.Enumerable;
            return this;
        }

        public PropertyDescriptor WithNotConfigurable() {
            Attributes &= ~DescriptorAttributes.Configurable;
            return this;
        }

    }
}