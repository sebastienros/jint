namespace Jint.Runtime.Descriptors
{
    public class DataDescriptor : PropertyDescriptor
    {
        public DataDescriptor(object value)
        {
            Value = value;
            Writable = true;
        }

        public DataDescriptor(DataDescriptor d)
        {
            Value = d.Value;
            Writable = d.Writable;
            Configurable = d.Configurable;
            Enumerable = d.Enumerable;
        }

        public object Value { get; set; }
        /// <summary>
        /// If false, attempts by ECMAScript code to change the 
        /// property‘s [[Value]] attribute using [[Put]] will not succeed.
        /// </summary>
        public bool? Writable { get; set; }

        public bool WritableIsSet
        {
            get { return Writable.HasValue && Writable.Value; }
        }

        public override bool IsAccessorDescriptor()
        {
            return false;
        }

        public override bool IsDataDescriptor()
        {
            return true;
        }
    }
}