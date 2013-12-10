using Jint.Runtime;

namespace Jint.Native
{
    public interface IPrimitiveInstance
    {
        Types Type { get; } 
        JsValue PrimitiveValue { get; }
    }
}
