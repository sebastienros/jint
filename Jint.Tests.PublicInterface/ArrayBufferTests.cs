using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class ArrayBufferTests
{
    [Fact]
    public void CanConvertByteArrayToArrayBuffer()
    {
        var engine = new Engine(o => o.AddObjectConverter(new BytesToArrayBufferConverter()));

        var bytes = new byte[] { 17 };
        engine.SetValue("buffer", bytes);

        engine.Evaluate("var a = new Uint8Array(buffer)");

        var typedArray = (JsTypedArray) engine.GetValue("a");
        Assert.Equal((uint) 1, typedArray.Length);
        Assert.Equal(17, typedArray[0]);
        Assert.Equal(JsValue.Undefined, typedArray[1]);

        Assert.Equal(1, engine.Evaluate("a.length"));
        Assert.Equal(17, engine.Evaluate("a[0]"));
        Assert.Equal(JsValue.Undefined, engine.Evaluate("a[1]"));

        bytes[0] = 42;
        Assert.Equal(42, engine.Evaluate("a[0]"));
    }

    [Fact]
    public void CanCreateArrayBufferAndTypedArrayUsingCode()
    {
        var engine = new Engine();

        var jsArrayBuffer = engine.Intrinsics.ArrayBuffer.Construct(1);
        var jsTypedArray = engine.Intrinsics.Uint8Array.Construct(jsArrayBuffer);
        jsTypedArray[0] = 17;

        engine.SetValue("buffer", jsArrayBuffer);
        engine.SetValue("a", jsTypedArray);

        var typedArray = (JsTypedArray) engine.GetValue("a");
        Assert.Equal((uint) 1, typedArray.Length);
        Assert.Equal(17, typedArray[0]);
        Assert.Equal(JsValue.Undefined, typedArray[1]);

        Assert.Equal(1, engine.Evaluate("a.length"));
        Assert.Equal(17, engine.Evaluate("a[0]"));
        Assert.Equal(JsValue.Undefined, engine.Evaluate("a[1]"));
    }

    /// <summary>
    /// Converts a byte array to an ArrayBuffer.
    /// </summary>
    private sealed class BytesToArrayBufferConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is byte[] bytes)
            {
                var buffer = engine.Intrinsics.ArrayBuffer.Construct(bytes);
                result = buffer;
                return true;
            }

            // TODO: provide similar implementation for Memory<byte> that will affect how ArrayBufferInstance works (offset)

            result = JsValue.Null;
            return false;
        }
    }
}
