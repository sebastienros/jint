using Jint.Native;

namespace Jint.Runtime.Descriptors
{
    public class AccessorDescriptor : PropertyDescriptor
    {
        public AccessorDescriptor(ICallable get, ICallable set = null)
        {
            Get = get;
            Set = set;
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
        public virtual ICallable Get { get; set; }

        /// <summary>
        /// The setter function
        /// </summary>
        /// <returns></returns>
        public virtual ICallable Set { get; set; }

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