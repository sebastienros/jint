using Jint.Native.Object;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-dataview-instances
/// </summary>
internal sealed class JsDataView : ObjectInstance
{
    internal JsArrayBuffer? _viewedArrayBuffer;
    internal uint _byteLength;
    internal uint _byteOffset;

    internal JsDataView(Engine engine) : base(engine)
    {
    }
}
