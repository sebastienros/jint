using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = true, enumerable = false, writable  = true.
    /// </summary>
    internal sealed class NonEnumerablePropertyDescriptor : IPropertyDescriptor
    {
        public NonEnumerablePropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool? Enumerable => false;
        public bool? Writable => true;
        public bool? Configurable => true;

        public JsValue Value { get; set; }
    }
}