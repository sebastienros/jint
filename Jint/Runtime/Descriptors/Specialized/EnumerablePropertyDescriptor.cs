using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = true, enumerable = true, writable = true.
    /// </summary>
    internal sealed class EnumerablePropertyDescriptor : IPropertyDescriptor
    {
        public EnumerablePropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool? Enumerable => true;
        public bool? Writable => false;
        public bool? Configurable => false;

        public JsValue Value { get; set; }
    }
}