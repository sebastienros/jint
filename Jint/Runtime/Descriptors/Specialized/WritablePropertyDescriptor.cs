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

        public bool? Enumerable => false;
        public bool? Writable => true;
        public bool? Configurable => false;

        public JsValue Value { get; set; }
    }
}