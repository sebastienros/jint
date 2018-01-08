using Jint.Native;

namespace Jint.Runtime.Descriptors
{
    public interface IPropertyDescriptor
    {
        JsValue Get { get; }
        JsValue Set { get; }

        bool? Enumerable { get; }
        bool? Writable { get; }
        bool? Configurable { get; }

        JsValue Value { get; set; }
    }
}