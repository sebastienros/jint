using Jint.Native;
using Jint.Native.ArrayBuffer;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Converts a byte array to an ArrayBuffer.
    /// </summary>
    public class BytesToArrayBufferConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is byte[] bytes)
            {
                result = new ArrayBufferInstance(engine, bytes);
                return true;
            }
            
            // TODO: provide similar implementation for Memory<byte> that will affect how ArrayBufferInstance works (offset)

            result = JsValue.Null;
            return false;
        }
    }
}
