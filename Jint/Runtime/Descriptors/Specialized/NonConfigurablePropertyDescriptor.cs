using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = false, enumerable = true, writable = true.
    /// </summary>
    internal sealed class NonConfigurablePropertyDescriptor : IPropertyDescriptor
    {
        public NonConfigurablePropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool? Enumerable => true;
        public bool? Writable => true;
        public bool? Configurable => false;

        public JsValue Value { get; set; }
    }
}