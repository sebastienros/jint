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

        /// <summary>
        /// Local implementation used to create a singleton representing 
        /// an undefined result of a PropertyDescriptor. This prevents the rest
        /// of the code to return objects in order to be able to return
        /// Undefined.Instance
        /// </summary>
        internal sealed class UndefinedPropertyDescriptor : PropertyDescriptor
        {
            public override bool IsAccessorDescriptor()
            {
                throw new System.NotImplementedException();
            }

            public override bool IsDataDescriptor()
            {
                throw new System.NotImplementedException();
            }
        }


    }
}