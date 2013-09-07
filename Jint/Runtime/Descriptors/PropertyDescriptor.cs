using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Descriptors
{
    /// <summary>
    /// An element of a javascript object
    /// </summary>
    public abstract class PropertyDescriptor
    {
        public static PropertyDescriptor Undefined = new UndefinedPropertyDescriptor();

        /// <summary>
        /// If true, the property will be enumerated by a for-in 
        /// enumeration (see 12.6.4). Otherwise, the property is said 
        /// to be non-enumerable.
        /// </summary>
        public bool Enumerable { get; set; }

        /// <summary>
        /// If false, attempts to delete the property, change the 
        /// property to be a data property, or change its attributes will 
        /// fail.
        /// </summary>
        public bool Configurable { get; set; }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.1
        /// </summary>
        /// <returns></returns>
        public abstract bool IsAccessorDescriptor();

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.2
        /// </summary>
        /// <returns></returns>
        public abstract bool IsDataDescriptor();

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.3
        /// </summary>
        /// <returns></returns>
        public bool IsGenericDescriptor()
        {
            return !IsDataDescriptor() && !IsAccessorDescriptor();
        }

        public T As<T>() where T : PropertyDescriptor
        {
            return (T)this;
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
            
            bool writable = TypeConverter.ToBoolean(obj.Get("writable"));
            bool enumerable = TypeConverter.ToBoolean(obj.Get("enumerable"));
            bool configurable = TypeConverter.ToBoolean(obj.Get("configurable"));

            if (obj.HasProperty("value"))
            {
                var value = TypeConverter.ToBoolean(obj.Get("value"));
                return new DataDescriptor(value) { Configurable = configurable, Enumerable = enumerable, Writable = writable};
            }
            else
            {
                object getter = null, setter = null;
                if (obj.HasProperty("get"))
                {
                    getter = obj.Get("get");
                    if (getter != Native.Undefined.Instance && !(getter is ICallable))
                    {
                        throw new JavaScriptException(engine.TypeError);
                    }
                }

                if (obj.HasProperty("set"))
                {
                    setter = obj.Get("set");
                    if (setter != Native.Undefined.Instance && !(setter is ICallable))
                    {
                        throw new JavaScriptException(engine.TypeError);
                    }
                }

                return new AccessorDescriptor(getter as ICallable, setter as ICallable);
            }
        }

        public static object FromPropertyDescriptor(Engine engine, PropertyDescriptor desc)
        {
            if (desc == PropertyDescriptor.Undefined)
            {
                return Native.Undefined.Instance;
            }

            var obj = engine.Object.Construct(Arguments.Empty);

            if (desc.IsDataDescriptor())
            {
                var datadesc = desc.As<DataDescriptor>();
                obj.DefineOwnProperty("value", new DataDescriptor(datadesc.Value) { Writable = true, Enumerable = true, Configurable = true }, false);
                obj.DefineOwnProperty("writable", new DataDescriptor(datadesc.Writable) { Writable = true, Enumerable = true, Configurable = true }, false);
            }
            else
            {
                var accdesc = desc.As<AccessorDescriptor>();
                obj.DefineOwnProperty("get", new DataDescriptor(accdesc.Get ?? Native.Undefined.Instance) { Writable = true, Enumerable = true, Configurable = true }, false);
                obj.DefineOwnProperty("set", new DataDescriptor(accdesc.Set ?? Native.Undefined.Instance) { Writable = true, Enumerable = true, Configurable = true }, false);
            }

            obj.DefineOwnProperty("enumerable", new DataDescriptor(desc.Enumerable) { Writable = true, Enumerable = true, Configurable = true }, false);
            obj.DefineOwnProperty("configurable", new DataDescriptor(desc.Configurable) { Writable = true, Enumerable = true, Configurable = true }, false);

            return obj;
        }

        /// <summary>
        /// Local implementation used to create a singleton representing 
        /// an undefined result of a PropertyDescriptor. This prevents the rest
        /// of the code to return 'object' in order to be able to return
        /// Undefined.Instance
        /// </summary>
        internal sealed class UndefinedPropertyDescriptor : PropertyDescriptor
        {
            public override bool IsAccessorDescriptor()
            {
                return false;
            }

            public override bool IsDataDescriptor()
            {
                return false;
            }
        }


    }
}