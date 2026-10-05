namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://tc39.es/ecma262/#sec-%typedarray%.prototype.with">%TypedArray%.prototype.with</see>
/// reads the length before it coerces the index and the value, and either coercion can run script that shrinks
/// a resizable buffer. Every element past the new length then reads as <c>undefined</c> and
/// <c>TypedArraySetElement</c> converts it: NaN in a float array, zero in an integer one, and a
/// <c>TypeError</c> in a BigInt one (tc39/ecma262#3979, tc39/test262#5136). Jint bulk-copied the original
/// length's worth of bytes out of the reallocated, shorter buffer, so a CLR <c>ArgumentException</c> escaped
/// into the host instead.
/// </summary>
public class TypedArrayWithShrinkTests
{
    private readonly Engine _engine = new();

    private static readonly bool SupportsHalf = Type.GetType("System.Half") is not null;

    // Script-side catch, so a CLR exception leaking out of the engine fails the test instead of passing as an error.
    private string Run(string constructor, string one, string body) => _engine.Evaluate($$"""
        (function () {
            var TA = {{constructor}};
            var buffer = new ArrayBuffer(3 * TA.BYTES_PER_ELEMENT, { maxByteLength: 3 * TA.BYTES_PER_ELEMENT });
            var sample = new TA(buffer);
            sample[0] = {{one}};
            sample[1] = {{one}} + {{one}};
            sample[2] = {{one}} + {{one}} + {{one}};
            var calls = 0;
            function shrink() { calls++; buffer.resize(TA.BYTES_PER_ELEMENT); }
            try {
                {{body}}
            } catch (e) {
                return e.constructor.name + ' calls=' + calls + ' source=' + Array.prototype.join.call(sample, ',');
            }
        })()
        """).AsString();

    [Theory]
    [InlineData("Int8Array", "0", "index")]
    [InlineData("Uint8Array", "0", "index")]
    [InlineData("Uint8ClampedArray", "0", "index")]
    [InlineData("Int16Array", "0", "index")]
    [InlineData("Uint16Array", "0", "index")]
    [InlineData("Int32Array", "0", "index")]
    [InlineData("Uint32Array", "0", "index")]
    [InlineData("Float32Array", "NaN", "index")]
    [InlineData("Float64Array", "NaN", "index")]
    [InlineData("Int8Array", "0", "value")]
    [InlineData("Uint8Array", "0", "value")]
    [InlineData("Uint8ClampedArray", "0", "value")]
    [InlineData("Int16Array", "0", "value")]
    [InlineData("Uint16Array", "0", "value")]
    [InlineData("Int32Array", "0", "value")]
    [InlineData("Uint32Array", "0", "value")]
    [InlineData("Float32Array", "NaN", "value")]
    [InlineData("Float64Array", "NaN", "value")]
    public void CoercionThatShrinksConvertsTheMissingElements(string constructor, string missing, string coerced)
    {
        var call = coerced == "index"
            ? "sample.with({ valueOf: function () { shrink(); return 0; } }, 9)"
            : "sample.with(0, { valueOf: function () { shrink(); return 9; } })";

        Run(constructor, "1", "var result = " + call + """
            ;
            return Array.prototype.join.call(result, ',') + ' calls=' + calls + ' source=' + Array.prototype.join.call(sample, ',');
            """).Should().Be($"9,{missing},{missing} calls=1 source=1");
    }

    [Theory]
    [InlineData("index")]
    [InlineData("value")]
    public void AFloat16ArrayThatShrinksConvertsTheMissingElementsToNaN(string coerced)
    {
        if (!SupportsHalf)
        {
            return;
        }

        var call = coerced == "index"
            ? "sample.with({ valueOf: function () { shrink(); return 0; } }, 9)"
            : "sample.with(0, { valueOf: function () { shrink(); return 9; } })";

        Run("Float16Array", "1", "var result = " + call + """
            ;
            return Array.prototype.join.call(result, ',') + ' calls=' + calls + ' source=' + Array.prototype.join.call(sample, ',');
            """).Should().Be("9,NaN,NaN calls=1 source=1");
    }

    [Theory]
    [InlineData("BigInt64Array", "index")]
    [InlineData("BigInt64Array", "value")]
    [InlineData("BigUint64Array", "index")]
    [InlineData("BigUint64Array", "value")]
    public void ABigIntArrayThatShrinksIsATypeError(string constructor, string coerced)
    {
        var call = coerced == "index"
            ? "sample.with({ valueOf: function () { shrink(); return 0; } }, 9n)"
            : "sample.with(0, { valueOf: function () { shrink(); return 9n; } })";

        Run(constructor, "1n", call + "; return 'did not throw';")
            .Should().Be("TypeError calls=1 source=1");
    }

    [Theory]
    [InlineData("Int32Array", "1", "3")]
    [InlineData("Float64Array", "1", "3")]
    [InlineData("BigInt64Array", "1n", "3n")]
    public void ANegativeIndexShrunkOutOfBoundsIsARangeError(string constructor, string one, string three)
    {
        Run(constructor, one, $"sample.with(-2, {{ valueOf: function () {{ shrink(); return {three}; }} }}); return 'did not throw';")
            .Should().Be($"RangeError calls=1 source={one.TrimEnd('n')}");
    }

    [Fact]
    public void AnUnshrunkBufferCopiesEveryElement()
    {
        _engine.Evaluate("""
            (function () {
                var buffer = new ArrayBuffer(16, { maxByteLength: 32 });
                var sample = new Int32Array(buffer, 4, 3);
                sample.set([1, 2, 3]);
                return Array.prototype.join.call(sample.with(-1, 9), ',');
            })()
            """).AsString().Should().Be("1,2,9");
    }
}
