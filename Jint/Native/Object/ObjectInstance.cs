using System;
using System.Collections.Generic;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object
{
    public class ObjectInstance
    {
        public ObjectInstance(Engine engine)
        {
            Engine = engine;
            Properties = new Dictionary<string, PropertyDescriptor>();
        }

        public Engine Engine { get; set; }

        public IDictionary<string, PropertyDescriptor> Properties { get; private set; }

        /// <summary>
        /// The prototype of this object.
        /// </summary>
        public ObjectInstance Prototype { get; set; }
        
        /// <summary>
        /// If true, own properties may be added to the 
        /// object.
        /// </summary>
        public bool Extensible { get; set; }

        /// <summary>
        /// A String value indicating a specification defined 
        /// classification of objects.
        /// </summary>
        public virtual string Class
        {
            get { return "Object"; }
        }

        /// <summary>
        /// Returns the value of the named property.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual object Get(string propertyName)
        {
            var desc = GetProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return Undefined.Instance;
            }

            if (desc.IsDataDescriptor())
            {
                return desc.As<DataDescriptor>().Value;
            }

            var getter = desc.As<AccessorDescriptor>().Get;

            if (getter == null)
            {
                return Undefined.Instance;
            }

            return getter.Call(this, Arguments.Empty);
        }

        public void Set(string name, object value)
        {

            if (!HasProperty(name))
            {
                DefineOwnProperty(name, new DataDescriptor(value) { Configurable = true, Enumerable = true, Writable = true }, false);
            }
            else
            {
                Put(name, value, false);
            }
        }

        /// <summary>
        /// Returns the Property Descriptor of the named 
        /// own property of this object, or undefined if 
        /// absent.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.1
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual PropertyDescriptor GetOwnProperty(string propertyName)
        {
            PropertyDescriptor x;
            if (Properties.TryGetValue(propertyName, out x))
            {
                /* Spec implementation
                PropertyDescriptor d;
                if (x.IsDataDescriptor())
                {
                    d = new DataDescriptor(x.As<DataDescriptor>());
                }
                else
                {
                    d = new AccessorDescriptor(x.As<AccessorDescriptor>());
                }

                return d;
                */

                // optimmized implementation
                return x;
            }
            
            return PropertyDescriptor.Undefined;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public PropertyDescriptor GetProperty(string propertyName)
        {
            var prop = GetOwnProperty(propertyName);

            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }
            
            if(Prototype == null)
            {
                return PropertyDescriptor.Undefined;
            }

            return Prototype.GetProperty(propertyName);
        }

        /// <summary>
        /// Sets the specified named property to the value 
        /// of the second parameter. The flag controls 
        /// failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <param name="throwOnError"></param>
        public void Put(string propertyName, object value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                var valueDesc = new DataDescriptor(value);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.As<AccessorDescriptor>().Set;
                setter.Call(this, new [] {value});
            }
            else
            {
                var newDesc = new DataDescriptor(value) {Writable = true, Enumerable = true, Configurable = true};
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether a 
        /// [[Put]] operation with PropertyName can be 
        /// performed.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.4
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool CanPut(string propertyName)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    if (desc.As<AccessorDescriptor>().Set == null)
                    {
                        return false;
                    }

                    return true;
                }

                return desc.As<DataDescriptor>().WritableIsSet;
            }

            if (Prototype == null)
            {
                return Extensible;
            }

            var inherited = Prototype.GetProperty(propertyName);

            if (inherited == PropertyDescriptor.Undefined)
            {
                return Extensible;
            }

            if (inherited.IsAccessorDescriptor())
            {
                if (inherited.As<AccessorDescriptor>().Set == null)
                {
                    return false;
                }

                return true;
            }

            if (!Extensible)
            {
                return false;
            }
            else
            {
                return inherited.As<DataDescriptor>().WritableIsSet;
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the 
        /// object already has a property with the given 
        /// name.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool HasProperty(string propertyName)
        {
            return GetProperty(propertyName) != PropertyDescriptor.Undefined;
        }

        /// <summary>
        /// Removes the specified named own property 
        /// from the object. The flag controls failure 
        /// handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public bool Delete(string propertyName, bool throwOnError)
        {
            var desc = GetOwnProperty(propertyName);
            
            if (desc == PropertyDescriptor.Undefined)
            {
                return true;
            }

            if (desc.ConfigurableIsSet)
            {
                Properties.Remove(propertyName);
                return true;
            }
            else
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return false;
            }
        }

        /// <summary>
        /// Hint is a String. Returns a default value for the 
        /// object.
        /// </summary>
        /// <param name="hint"></param>
        /// <returns></returns>
        public object DefaultValue(Types hint)
        {
            if ((hint == Types.String) || (hint == Types.None && this is StringInstance))
            {
                var toString = Get("toString");
                var callable = toString as ICallable;
                if (callable != null)
                {
                    var str = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(str))
                    {
                        return str;
                    }
                }

                var valueOf = Get("valueOf");
                callable = valueOf as ICallable;
                if (callable != null)
                {
                    var val = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(val))
                    {
                        return val;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
            }

            if ((hint == Types.Number) || (hint == Types.None))
            {
                var valueOf = Get("valueOf");
                var callable = valueOf as ICallable;
                if (callable != null)
                {
                    var val = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(val))
                    {
                        return val;
                    }
                }

                var toString = Get("toString");
                callable = toString as ICallable;
                if (callable != null)
                {
                    var str = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(str))
                    {
                        return str;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
            }

            return ToString();
        }

        /// <summary>
        /// Creates or alters the named own property to 
        /// have the state described by a Property 
        /// Descriptor. The flag controls failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="desc"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public virtual bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var current = GetOwnProperty(propertyName);
            
            if (current == PropertyDescriptor.Undefined)
            {
                if (!Extensible)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }
                else
                {
                    if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
                    {
                        Properties[propertyName] = new DataDescriptor(desc.As<DataDescriptor>());
                    }
                    else
                    {
                        Properties[propertyName] = new AccessorDescriptor(desc.As<AccessorDescriptor>());
                    }
                }

                return true;
            }

            // todo: if desc and current are the same, return true

            if (!current.ConfigurableIsSet)
            {
                if (desc.ConfigurableIsSet)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }

                if (desc.EnumerableIsSet != current.EnumerableIsSet)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }
            }

            if (desc.IsGenericDescriptor())
            {
                // ????
            }

            if (current.IsDataDescriptor() != desc.IsDataDescriptor())
            {
                if (!current.ConfigurableIsSet)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }

                if (current.IsDataDescriptor())
                {
                    // todo: convert to accessor
                }
            }
            else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
            {
                var cd = current.As<DataDescriptor>();
                var dd = current.As<DataDescriptor>();

                if (!current.ConfigurableIsSet)
                {
                    if (!cd.WritableIsSet && dd.WritableIsSet)
                    {
                        if (throwOnError)
                        {
                            throw new JavaScriptException(Engine.TypeError);
                        }

                        return false;
                    }
                }

                if (!dd.Writable.HasValue && cd.Writable.HasValue)
                {
                    dd.Enumerable = cd.Enumerable;
                }
            }
            else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
            {
                var ca = current.As<AccessorDescriptor>();
                var da = desc.As<AccessorDescriptor>();

                if (!current.ConfigurableIsSet)
                {
                    if ( (da.Set != null && da.Set != ca.Set)
                        || (da.Get != null && da.Get != ca.Get))
                    {
                        if (throwOnError)
                        {
                            throw new JavaScriptException(Engine.TypeError);
                        }

                        return false;
                    }
                }
            }

            Properties[propertyName] = desc;

            if (!desc.Configurable.HasValue && current.Configurable.HasValue)
            {
                desc.Configurable = current.Configurable;
            }

            if (!desc.Enumerable.HasValue && current.Enumerable.HasValue)
            {
                desc.Enumerable = current.Enumerable;
            }

            return true;
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be undeclared already
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="writable"></param>
        /// <param name="configurable"></param>
        /// <param name="enumerable"></param>
        public void FastAddProperty(string name, object value, bool writable, bool enumerable, bool configurable)
        {
            Properties.Add(name, new DataDescriptor(value) { Writable = writable, Enumerable = enumerable, Configurable = configurable });
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be already declared 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void FastSetProperty(string name, PropertyDescriptor value)
        {
            Properties[name] = value;
        }

        public override string ToString()
        {
            return TypeConverter.ToString(this);
        }
    }
}
