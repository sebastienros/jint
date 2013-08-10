using Jint.Native.Function;

namespace Jint.Runtime.Descriptors
{
    public class AccessorDescriptor : PropertyDescriptor
    {
        public AccessorDescriptor(FunctionInstance get, FunctionInstance set = null)
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
        public virtual FunctionInstance Get { get; set; }

        /// <summary>
        /// The setter function
        /// </summary>
        /// <returns></returns>
        public virtual FunctionInstance Set { get; set; }

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