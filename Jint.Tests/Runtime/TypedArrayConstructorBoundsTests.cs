namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://tc39.es/ecma262/#sec-initializetypedarrayfromarraybuffer">
/// InitializeTypedArrayFromArrayBuffer</see> treats <c>byteOffset</c> and <c>length</c> as mathematical
/// integers anywhere in <c>ToIndex</c>'s range (0 to 2^53-1) and lets three comparisons decide whether the
/// request fits the buffer. Jint narrowed both to <c>int</c> before those comparisons, so a value past
/// <c>2^31</c> wrapped into something small enough to pass them: <c>new Int32Array(new ArrayBuffer(0),
/// 2**31 + 4)</c> built a typed array with byte offset <c>-2147483644</c> and length <c>536870911</c>
/// instead of raising a <c>RangeError</c>.
/// <para>
/// The same algorithm also fixes the order of its two observable coercions. <c>ToIndex(byteOffset)</c> is
/// step 2, the offset-must-be-a-multiple-of-the-element-size <c>RangeError</c> is step 3, and
/// <c>ToIndex(length)</c> only arrives at step 5 — so a misaligned offset is reported even when the length
/// argument would itself have thrown. Jint ran both coercions up front and reported the length's error.
/// </para>
/// <para>
/// test262's own coverage of all of this is <c>staging/sm/TypedArray/constructor-byteoffsets-bounds.js</c>
/// and <c>staging/sm/TypedArray/constructor-buffer-sequence.js</c>; these pin it outside the generated
/// projection so a future rearrangement cannot trade one half for the other.
/// </para>
/// </summary>
public class TypedArrayConstructorBoundsTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// The constructor name of whatever <paramref name="source"/> throws, or <c>did not throw</c>. Written in
    /// script rather than with <c>Assert.Throws</c> so that a CLR exception leaking out of the engine fails
    /// the test as an escaping exception instead of being caught and reported as a JavaScript error.
    /// </summary>
    private string ThrownErrorName(string source) => _engine
        .Evaluate($"(function () {{ try {{ {source}; return 'did not throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
        .AsString();

    /// <summary>
    /// 2147483648 and 2147483652 are where narrowing to a signed 32-bit integer produced a negative offset;
    /// 4294967292 and 4294967296 are where it wrapped back to a small non-negative one. 2147483647 and
    /// 4294967295 are the odd values on either side, which happened to be rejected already because they are
    /// not multiples of the element size — they are here so the alignment check cannot start standing in for
    /// the range check.
    /// </summary>
    [TestCase("2147483644")]
    [TestCase("2147483647")]
    [TestCase("2147483648")]
    [TestCase("2147483649")]
    [TestCase("2147483652")]
    [TestCase("4294967292")]
    [TestCase("4294967295")]
    [TestCase("4294967296")]
    [TestCase("4294967297")]
    [TestCase("4294967300")]
    [TestCase("9007199254740991")]
    public void ByteOffsetPastTheBufferIsARangeError(string byteOffset)
    {
        ThrownErrorName($"new Int32Array(new ArrayBuffer(0), {byteOffset})").Should().Be("RangeError");
    }

    /// <summary>
    /// The same for the length argument, whose product with the element size is what used to overflow.
    /// </summary>
    [TestCase("2147483647")]
    [TestCase("2147483648")]
    [TestCase("2147483649")]
    [TestCase("4294967295")]
    [TestCase("4294967296")]
    [TestCase("4294967297")]
    [TestCase("9007199254740991")]
    public void LengthPastTheBufferIsARangeError(string length)
    {
        ThrownErrorName($"new Int32Array(new ArrayBuffer(0), 0, {length})").Should().Be("RangeError");
    }

    /// <summary>
    /// The control: offsets and lengths the buffer really can hold still build the array they always did.
    /// </summary>
    [Test]
    public void OffsetsAndLengthsWithinTheBufferStillWork()
    {
        _engine.Evaluate("new Int32Array(new ArrayBuffer(16), 4).length").AsNumber().Should().Be(3);
        _engine.Evaluate("new Int32Array(new ArrayBuffer(16), 4).byteOffset").AsNumber().Should().Be(4);
        _engine.Evaluate("new Int32Array(new ArrayBuffer(16), 8, 2).length").AsNumber().Should().Be(2);
        _engine.Evaluate("new Int32Array(new ArrayBuffer(16)).length").AsNumber().Should().Be(4);
    }

    /// <summary>
    /// Step 3 precedes step 5: the misaligned offset is reported, not whatever evaluating the length would
    /// have raised.
    /// </summary>
    [Test]
    public void MisalignedByteOffsetIsReportedBeforeTheLengthIsCoerced()
    {
        const string Script = """
            var lengthWasCoerced = false;
            var poisoned = { valueOf: function () { lengthWasCoerced = true; throw new Error('length'); } };
            var name;
            try { new Int32Array(new ArrayBuffer(8), 1, poisoned); name = 'did not throw'; }
            catch (e) { name = e.constructor.name; }
            name + ' ' + lengthWasCoerced;
            """;

        _engine.Evaluate(Script).AsString().Should().Be("RangeError false");
    }

    /// <summary>
    /// And step 2 precedes step 3: an offset that throws while being coerced does so before anything can
    /// decide it is misaligned.
    /// </summary>
    [Test]
    public void ByteOffsetIsCoercedBeforeItIsCheckedForAlignment()
    {
        const string Script = """
            var poisoned = { valueOf: function () { throw new RangeError('offset'); } };
            try { new Int32Array(new ArrayBuffer(8), poisoned, 0); return 'did not throw'; }
            catch (e) { return e.message; }
            """;

        _engine.Evaluate($"(function () {{ {Script} }})()").AsString().Should().Be("offset");
    }

    /// <summary>
    /// Step 6 (the detached-buffer <c>TypeError</c>) comes after both coercions, so a buffer detached from
    /// inside the length's <c>valueOf</c> is still seen.
    /// </summary>
    [Test]
    public void ADetachedBufferIsNoticedAfterBothArgumentsAreCoerced()
    {
        const string Script = """
            var buffer = new ArrayBuffer(8);
            var detaching = { valueOf: function () { buffer.transfer(); return 0; } };
            try { new Int32Array(buffer, 0, detaching); return 'did not throw'; }
            catch (e) { return e.constructor.name; }
            """;

        _engine.Evaluate($"(function () {{ {Script} }})()").AsString().Should().Be("TypeError");
    }
}
