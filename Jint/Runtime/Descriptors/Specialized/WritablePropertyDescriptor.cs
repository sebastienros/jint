using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = false, enumerable = false, writable  = true.
    /// </summary>
    internal sealed class WritablePropertyDescriptor : IPropertyDescriptor
    {
        public WritablePropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool Enumerable => false;
        public bool EnumerableSet => true;
        
        public bool Writable => true;
        public bool WritableSet => true;
        
        public bool Configurable => false;
        public bool ConfigurableSet => true;

        public JsValue Value { get; set; }
    }
}