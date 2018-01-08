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

        public bool? Enumerable => null;
        public bool? Writable => null;
        public bool? Configurable => null;

        public JsValue Value { get; set; }
    }
}