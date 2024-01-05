using Jint.Runtime;

namespace Jint.Native;

public interface IJsPrimitive
{
    Types Type { get; }
    JsValue PrimitiveValue { get; }
}
