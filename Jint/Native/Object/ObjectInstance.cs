using System;
using System.Collections.Generic;
using Jint.Native.Errors;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object
{
    public class ObjectInstance
    {
        public ObjectInstance(ObjectInstance prototype)
        {
            Properties = new SortedList<string, PropertyDescriptor>();
            Extensible = true;
            Prototype = prototype;
            DefineOwnProperty("prototype", new DataDescriptor(prototype), false);
        }

        public IDictionary<string, PropertyDescriptor> Properties { get; private set; }

        /// <summary>
        /// The prototype of this object.
        /// </summary>
        public ObjectInstance Prototype { get; private set; }

        
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
        public object Get(string propertyName)
        {
            var desc = GetProperty(propertyName);

            if (desc == Undefined.Instance)
            {
                return Undefined.Instance;
            }

            return desc.Get();
        }

        public void Set(string name, object value)
        {

            if (!HasProperty(name))
            {
                DefineOwnProperty(name, new DataDescriptor(value), false);
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
        public dynamic GetOwnProperty(string propertyName)
        {
            PropertyDescriptor value;
            if (Properties.TryGetValue(propertyName, out value))
            {
                return value;
            }
            
            return Undefined.Instance;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public dynamic GetProperty(string propertyName)
        {
            var prop = GetOwnProperty(propertyName);

            if (prop != Undefined.Instance)
            {
                return prop;
            }
            
            if(Prototype == null)
            {
                return Undefined.Instance;
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
                    throw new TypeError();
                }

                return;
            }

            PropertyDescriptor ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                ownDesc.Set(value);
                return;
            }

            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                desc.Set(value);
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

            if (desc != Undefined.Instance)
            {
                if (desc.IsAccessorDescriptor())
                {
                    return true;
                }
                else
                {
                    return desc.Writable;
                }
            }

            if (Prototype == null)
            {
                return Extensible;
            }

            var inherited = Prototype.GetProperty(propertyName);

            if (inherited == Undefined.Instance)
            {
                return Prototype.Extensible;
            }

            if (inherited.IsAccessorDescriptor())
            {
                return true;
            }

            if (desc.IsAccessorDescriptor())
            {
                return true;
            }
            else
            {
                if (!Extensible)
                {
                    return false;
                }
                else
                {
                    return inherited.Writable;
                }
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
            return GetProperty(propertyName) != Undefined.Instance;
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

            if (desc == Undefined.Instance)
            {
                return true;
            }

            if (desc.Configurable)
            {
                Properties.Remove(propertyName);
                return true;
            }
            else
            {
                if (throwOnError)
                {
                    throw new TypeError();
                }

                return false;
            }
        }

        /// <summary>
        /// Hint is a String. Returns a default value for the 
        /// object.
        /// </summary>
        /// <param name="hintError"></param>
        /// <returns></returns>
        public object DefaultValue(string hintError)
        {
            return ToString();
        }

        /// <summary>
        /// Creates or alters the named own property to 
        /// have the state described by a Property 
        /// Descriptor. The flag controls failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="property"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var property = GetOwnProperty(propertyName);

            if (property == Undefined.Instance)
            {
                if (!Extensible)
                {
                    if (throwOnError)
                    {
                        throw new TypeError();
                    }

                    return false;
                }
                else
                {
                    Properties.Add(propertyName, desc);
                }

                return true;
            }

            PropertyDescriptor current = property;

            if (!current.Configurable)
            {
                if (desc.Configurable)
                {
                    if (throwOnError)
                    {
                        throw new TypeError();
                    }

                    return false;
                }

                if (desc.Enumerable != current.Enumerable)
                {
                    if (throwOnError)
                    {
                        throw new TypeError();
                    }

                    return false;
                }
            }

            /// todo: complete this implementation

            Properties[propertyName] = desc;

            return true;
        }

    }
}
