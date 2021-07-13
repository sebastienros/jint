using Jint.Native.ArrayBuffer;
using Jint.Native.Object;

namespace Jint.Native.DataView
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-dataview-instances
    /// </summary>
    public sealed class DataViewInstance : ObjectInstance
    {
        internal ArrayBufferInstance _viewedArrayBuffer;
        internal uint _byteLength;
        internal uint _byteOffset;

        internal DataViewInstance(Engine engine) : base(engine)
        {
        }
    }
}