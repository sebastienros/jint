using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = true, enumerable = true, writable = true.
    /// </summary>
    internal sealed class ConfigurableEnumerableWritablePropertyDescriptor : IPropertyDescriptor
    {
        public ConfigurableEnumerableWritablePropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool Enumerable => true;
        public bool EnumerableSet => true;
        
        public bool Writable => true;
        public bool WritableSet => true;

        public bool Configurable => true;
        public bool ConfigurableSet => true;

        public JsValue Value { get; set; }
    }
}