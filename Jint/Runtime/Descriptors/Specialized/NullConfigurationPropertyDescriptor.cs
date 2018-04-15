using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = null, enumerable = null, writable = null.
    /// </summary>
    internal sealed class NullConfigurationPropertyDescriptor : IPropertyDescriptor
    {
        public NullConfigurationPropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool Enumerable => false;
        public bool EnumerableSet => false;
        
        public bool Writable => false;
        public bool WritableSet => false;
        
        public bool Configurable => false;
        public bool ConfigurableSet => false;
        
        public JsValue Value { get; set; }
    }
}