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
        typedArray.Length.Should().Be((uint) 1);
        typedArray[0].Should().Be(17);
        typedArray[1].Should().BeUndefined();

        engine.Evaluate("a.length").Should().Be(1);
        engine.Evaluate("a[0]").Should().Be(17);
        engine.Evaluate("a[1]").Should().BeUndefined();

        bytes[0] = 42;
        engine.Evaluate("a[0]").Should().Be(42);
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
        typedArray.Length.Should().Be((uint) 1);
        typedArray[0].Should().Be(17);
        typedArray[1].Should().BeUndefined();

        engine.Evaluate("a.length").Should().Be(1);
        engine.Evaluate("a[0]").Should().Be(17);
        engine.Evaluate("a[1]").Should().BeUndefined();
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
