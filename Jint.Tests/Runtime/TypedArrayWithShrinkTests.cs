namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://tc39.es/ecma262/#sec-%typedarray%.prototype.with">%TypedArray%.prototype.with</see>
/// reads the length before it coerces the index and the value, and either coercion can run script that shrinks
/// a resizable buffer. <c>IsValidIntegerIndex</c> then proves only that the replaced index survived, so every
/// element past the new length reads as <c>undefined</c> and <c>TypedArraySetElement</c> converts it: NaN in a
/// float array, zero in an integer one, and a <c>TypeError</c> in a BigInt one. Jint bulk-copied the original
/// length's worth of bytes out of the reallocated, shorter buffer, so a CLR <c>ArgumentException</c> escaped
/// into the host instead.
/// <para>
/// test262 covers this in <c>built-ins/TypedArray/prototype/with/*-coercion-shrinks.js</c> (tc39/test262#5136,
/// written for tc39/ecma262#3979); these pin it per element type and add the growing and still-in-bounds cases
/// the generated projection does not reach.
/// </para>
/// </summary>
public class TypedArrayWithShrinkTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// Runs <paramref name="body"/> against a three-element <paramref name="constructor"/> array over a
    /// resizable buffer, with <c>shrink()</c> cutting the buffer to one element. Written in script with its
    /// own catch, so a CLR exception leaking out of the engine fails the test instead of passing as an error.
    /// </summary>
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

    [TestCase("Int8Array", "0")]
    [TestCase("Uint8Array", "0")]
    [TestCase("Uint8ClampedArray", "0")]
    [TestCase("Int16Array", "0")]
    [TestCase("Uint16Array", "0")]
    [TestCase("Int32Array", "0")]
    [TestCase("Uint32Array", "0")]
    [TestCase("Float32Array", "NaN")]
    [TestCase("Float64Array", "NaN")]
    public void IndexCoercionThatShrinksConvertsTheMissingElements(string constructor, string missing)
    {
        Run(constructor, "1", """
            var result = sample.with({ valueOf: function () { shrink(); return 0; } }, 9);
            return Array.prototype.join.call(result, ',') + ' calls=' + calls + ' source=' + Array.prototype.join.call(sample, ',');
            """).Should().Be($"9,{missing},{missing} calls=1 source=1");
    }

    [TestCase("Int8Array", "0")]
    [TestCase("Uint8Array", "0")]
    [TestCase("Uint8ClampedArray", "0")]
    [TestCase("Int16Array", "0")]
    [TestCase("Uint16Array", "0")]
    [TestCase("Int32Array", "0")]
    [TestCase("Uint32Array", "0")]
    [TestCase("Float32Array", "NaN")]
    [TestCase("Float64Array", "NaN")]
    public void ValueCoercionThatShrinksConvertsTheMissingElements(string constructor, string missing)
    {
        Run(constructor, "1", """
            var result = sample.with(0, { valueOf: function () { shrink(); return 9; } });
            return Array.prototype.join.call(result, ',') + ' calls=' + calls + ' source=' + Array.prototype.join.call(sample, ',');
            """).Should().Be($"9,{missing},{missing} calls=1 source=1");
    }

    /// <summary>
    /// Float16 needs <c>System.Half</c>, which the net472 build lacks, so these rows run where it exists.
    /// </summary>
    private static readonly bool SupportsHalf = Type.GetType("System.Half") is not null;

    [TestCase("index")]
    [TestCase("value")]
    [IgnoreUnless(nameof(SupportsHalf), "Float16Array needs System.Half, which this target framework lacks")]
    public void AFloat16ArrayThatShrinksConvertsTheMissingElementsToNaN(string coerced)
    {
        var call = coerced == "index"
            ? "sample.with({ valueOf: function () { shrink(); return 0; } }, 9)"
            : "sample.with(0, { valueOf: function () { shrink(); return 9; } })";

        Run("Float16Array", "1", "var result = " + call + """
            ;
            return Array.prototype.join.call(result, ',') + ' calls=' + calls + ' source=' + Array.prototype.join.call(sample, ',');
            """).Should().Be("9,NaN,NaN calls=1 source=1");
    }

    /// <summary>
    /// <c>ToBigInt(undefined)</c> is a <c>TypeError</c>, and since tc39/ecma262#3979 step 12 propagates it.
    /// </summary>
    [TestCase("BigInt64Array", "index")]
    [TestCase("BigInt64Array", "value")]
    [TestCase("BigUint64Array", "index")]
    [TestCase("BigUint64Array", "value")]
    public void ABigIntArrayThatShrinksIsATypeError(string constructor, string coerced)
    {
        var call = coerced == "index"
            ? "sample.with({ valueOf: function () { shrink(); return 0; } }, 9n)"
            : "sample.with(0, { valueOf: function () { shrink(); return 9n; } })";

        Run(constructor, "1n", call + "; return 'did not throw';")
            .Should().Be("TypeError calls=1 source=1");
    }

    /// <summary>
    /// A negative index resolves against the length read before the coercion, so shrinking past it is the
    /// step 9 <c>RangeError</c>, which comes before any element is copied.
    /// </summary>
    [TestCase("Int32Array", "1", "3")]
    [TestCase("Float64Array", "1", "3")]
    [TestCase("BigInt64Array", "1n", "3n")]
    public void ANegativeIndexShrunkOutOfBoundsIsARangeError(string constructor, string one, string three)
    {
        Run(constructor, one, $"sample.with(-2, {{ valueOf: function () {{ shrink(); return {three}; }} }}); return 'did not throw';")
            .Should().Be($"RangeError calls=1 source={one.TrimEnd('n')}");
    }

    /// <summary>
    /// Growing the buffer behind a length-tracking view copies only the length read before the coercion.
    /// </summary>
    [TestCase("Int32Array")]
    [TestCase("BigInt64Array")]
    public void GrowingDuringCoercionCopiesTheOriginalLength(string constructor)
    {
        var suffix = constructor.StartsWith("Big", StringComparison.Ordinal) ? "n" : "";
        _engine.Evaluate($$"""
            (function () {
                var TA = {{constructor}};
                var buffer = new ArrayBuffer(2 * TA.BYTES_PER_ELEMENT, { maxByteLength: 4 * TA.BYTES_PER_ELEMENT });
                var sample = new TA(buffer);
                sample[0] = 1{{suffix}};
                sample[1] = 2{{suffix}};
                var result = sample.with(1, { valueOf: function () { buffer.resize(4 * TA.BYTES_PER_ELEMENT); sample[2] = 7{{suffix}}; return 5{{suffix}}; } });
                return Array.prototype.join.call(result, ',') + ' source=' + Array.prototype.join.call(sample, ',');
            })()
            """).AsString().Should().Be("1,5 source=1,2,7,0");
    }

    /// <summary>
    /// A fixed-length view over a buffer that keeps its full length copies every element as before.
    /// </summary>
    [Test]
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
