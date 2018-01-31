using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    /// <summary>
    /// configurable = false, enumerable = false, writable = false.
    /// </summary>
    internal sealed class AllForbiddenPropertyDescriptor : IPropertyDescriptor
    {
        public AllForbiddenPropertyDescriptor(JsValue value)
        {
            Value = value;
        }

        public JsValue Get => null;
        public JsValue Set => null;

        public bool? Enumerable => false;
        public bool? Writable => false;
        public bool? Configurable => false;

        public JsValue Value { get; set; }
    }
}