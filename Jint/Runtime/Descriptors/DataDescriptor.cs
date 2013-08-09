namespace Jint.Runtime.Descriptors
{
    public sealed class DataDescriptor : PropertyDescriptor
    {
        private object _value;

        public DataDescriptor(object value)
        {
            _value = value;
            Writable = true;
        }

        public override object Get()
        {
            return _value;
        }

        public override void Set(object value)
        {
            _value = value;
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