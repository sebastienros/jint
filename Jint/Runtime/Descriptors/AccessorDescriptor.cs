using System;
using Jint.Native;

namespace Jint.Runtime.Descriptors
{
    public class AccessorDescriptor : PropertyDescriptor
    {
        public AccessorDescriptor(object getter) : this(getter, Native.Undefined.Instance)
        {
        }

        public AccessorDescriptor(object getter, object setter)
        {
            if (getter == null)
            {
                throw new ArgumentNullException("getter", "get can only be undefined");
            }

            if (setter == null)
            {
                throw new ArgumentNullException("setter", "set can only be undefined");
            }

            Get = getter;
            Set = setter;
        }

        public AccessorDescriptor(AccessorDescriptor a)
        {
            Get = a.Get;
            Set = a.Set;
            Configurable = a.Configurable;
            Enumerable = a.Enumerable;
        }

        /// <summary>
        /// The getter function
        /// </summary>
        /// <returns></returns>
        public virtual object Get { get; set; }

        /// <summary>
        /// The setter function
        /// </summary>
        /// <returns></returns>
        public virtual object Set { get; set; }

        public override bool IsAccessorDescriptor()
        {
            return true;
        }

        public override bool IsDataDescriptor()
        {
            return false;
        }
    }
}