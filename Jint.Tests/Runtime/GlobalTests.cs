namespace Jint.Tests.Runtime;

public class GlobalTests
{
    [Fact]
    public void CanDisableSelectedGlobalBuiltIns()
    {
        var engine = new Engine(options => options.DisableGlobalBuiltIns(
            GlobalBuiltIn.ArrayBuffer
            | GlobalBuiltIn.SharedArrayBuffer
            | GlobalBuiltIn.DataView
            | GlobalBuiltIn.TypedArray
            | GlobalBuiltIn.Atomics
            | GlobalBuiltIn.ShadowRealm));

        var result = engine.Evaluate("""
            [
                typeof ArrayBuffer,
                typeof SharedArrayBuffer,
                typeof DataView,
                typeof TypedArray,
                typeof BigInt64Array,
                typeof BigUint64Array,
                typeof Float16Array,
                typeof Float32Array,
                typeof Float64Array,
                typeof Int8Array,
                typeof Int16Array,
                typeof Int32Array,
                typeof Uint8Array,
                typeof Uint8ClampedArray,
                typeof Uint16Array,
                typeof Uint32Array,
                typeof Atomics,
                typeof ShadowRealm,
                typeof Array,
                typeof Object
            ].join(',')
            """).AsString();

        Assert.Equal(
            "undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,undefined,function,function",
            result);
    }

    [Fact]
    public void DisablingOneGlobalBuiltInDoesNotDisableRelatedBuiltIns()
    {
        var engine = new Engine(options => options.DisableGlobalBuiltIns(GlobalBuiltIn.ArrayBuffer));

        var result = engine.Evaluate("""
            [
                typeof ArrayBuffer,
                typeof DataView,
                typeof Uint8Array,
                typeof SharedArrayBuffer
            ].join(',')
            """).AsString();

        Assert.Equal("undefined,function,function,function", result);
    }

    [Fact]
    public void UnescapeAtEndOfString()
    {
        var e = new Engine();

        Assert.Equal("@", e.Evaluate("unescape('%40');").AsString());
        Assert.Equal("@_", e.Evaluate("unescape('%40_');").AsString());
        Assert.Equal("@@", e.Evaluate("unescape('%40%40');").AsString());
        Assert.Equal("@", e.Evaluate("unescape('%u0040');").AsString());
        Assert.Equal("@@", e.Evaluate("unescape('%u0040%u0040');").AsString());
    }
}
