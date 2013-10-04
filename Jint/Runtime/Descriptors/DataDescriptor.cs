namespace Jint.Runtime.Descriptors
{
    public class DataDescriptor : PropertyDescriptor
    {
        public DataDescriptor(object value) : this(value, true, null, null)
        {
        }

        public DataDescriptor(DataDescriptor d) : this(d.Value, d.Writable, d.Enumerable, d.Configurable)
        {
        }

        public DataDescriptor(object value, bool? writable, bool? enumerable, bool? configurable)
        {
            Value = value;
            Writable = writable;
            Enumerable = enumerable;
            Configurable = configurable;
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