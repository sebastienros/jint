using Jint.Native;

namespace Jint.Runtime.Descriptors
{
    public interface IPropertyDescriptor
    {
        JsValue Get { get; }
        JsValue Set { get; }

        bool Enumerable { get; }
        bool EnumerableSet { get; }
        
        bool Writable { get; }
        bool WritableSet { get; }

        bool Configurable { get; }
        bool ConfigurableSet { get; }

        JsValue Value { get; set; }
    }
}