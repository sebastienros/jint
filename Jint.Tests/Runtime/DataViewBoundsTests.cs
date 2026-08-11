namespace Jint.Tests.Runtime;

/// <summary>
/// The two abstract operations behind every <c>DataView.prototype</c> accessor,
/// <see href="https://tc39.es/ecma262/#sec-getviewvalue">GetViewValue</see> and
/// <see href="https://tc39.es/ecma262/#sec-setviewvalue">SetViewValue</see>, treat <c>getIndex</c> as a
/// mathematical integer and let one comparison — <c>getIndex + elementSize &gt; viewSize</c> — decide whether
/// the request is in range. Jint narrowed <c>getIndex</c> before that comparison, so a request past
/// <c>2^31</c> wrapped to a negative or tiny offset, passed the check, and reached the backing array: a raw
/// CLR <c>IndexOutOfRangeException</c> (or <c>ArgumentOutOfRangeException</c>, by element width) where a
/// <c>RangeError</c> belongs.
/// <para>
/// The suite's own coverage of these offsets is in <c>staging/sm/extensions/dataview.js</c>, and
/// <c>staging/</c> is not part of the generated test262 projection at all, so it lives here. These pin both
/// directions at both boundaries that matter — <c>2^31</c>, where the <c>int</c> truncation bit, and
/// <c>2^32-1</c>, where unsigned arithmetic on <c>getIndex + elementSize</c> wraps — for every element width,
/// alongside the ordinary out-of-bounds answers that were already right, so a future rearrangement cannot
/// trade one for the other.
/// </para>
/// </summary>
public class DataViewBoundsTests
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
    /// 2147483648 and 2147483649 are where narrowing <c>getIndex</c> to a signed 32-bit integer produced a
    /// negative offset. 4294967295 is the largest value <c>ToIndex</c> reports here and is where adding the
    /// element size wraps in unsigned arithmetic; 9007199254740991 is <c>ToIndex</c>'s own ceiling and lands
    /// in the same place.
    /// </summary>
    private static readonly string[] OutOfRangeIndexes =
    [
        "2147483648",
        "2147483649",
        "4294967295",
        "9007199254740991",
    ];

    public static TheoryData<string> Getters =>
    [
        "getInt8", "getUint8",
        "getInt16", "getUint16", "getFloat16",
        "getInt32", "getUint32", "getFloat32",
        "getFloat64",
        "getBigInt64", "getBigUint64",
    ];

    /// <summary>
    /// Each setter with a value it accepts. The BigInt views coerce the value with <c>ToBigInt</c> before the
    /// range check runs, so those rows have to hand over a BigInt or they would report that TypeError first.
    /// </summary>
    public static TheoryData<string, string> Setters => new()
    {
        { "setInt8", "1" },
        { "setUint8", "1" },
        { "setInt16", "1" },
        { "setUint16", "1" },
        { "setFloat16", "1" },
        { "setInt32", "1" },
        { "setUint32", "1" },
        { "setFloat32", "1" },
        { "setFloat64", "1" },
        { "setBigInt64", "1n" },
        { "setBigUint64", "1n" },
    };

    [Theory]
    [MemberData(nameof(Getters))]
    public void AReadFarPastTheEndIsARangeError(string getter)
    {
        foreach (var index in OutOfRangeIndexes)
        {
            ThrownErrorName($"new DataView(new ArrayBuffer(16)).{getter}({index})")
                .Should().Be("RangeError", $"{getter}({index}) is out of range");
        }
    }

    [Theory]
    [MemberData(nameof(Setters))]
    public void AWriteFarPastTheEndIsARangeError(string setter, string value)
    {
        foreach (var index in OutOfRangeIndexes)
        {
            ThrownErrorName($"new DataView(new ArrayBuffer(16)).{setter}({index}, {value})")
                .Should().Be("RangeError", $"{setter}({index}) is out of range");
        }
    }

    [Fact]
    public void ANonZeroViewOffsetDoesNotBringAFarIndexBackIntoRange()
    {
        // bufferIndex is getIndex + viewOffset, so a view starting partway into its buffer is where a wrapped
        // index is likeliest to land back inside the array by accident.
        foreach (var index in OutOfRangeIndexes)
        {
            ThrownErrorName($"new DataView(new ArrayBuffer(16), 8).getInt8({index})").Should().Be("RangeError");
            ThrownErrorName($"new DataView(new ArrayBuffer(16), 8).setInt8({index}, 1)").Should().Be("RangeError");
        }
    }

    [Fact]
    public void ASharedBufferAnswersTheSameWay()
    {
        foreach (var index in OutOfRangeIndexes)
        {
            ThrownErrorName($"new DataView(new SharedArrayBuffer(16)).getFloat64({index})").Should().Be("RangeError");
            ThrownErrorName($"new DataView(new SharedArrayBuffer(16)).setFloat64({index}, 1)").Should().Be("RangeError");
        }
    }

    [Fact]
    public void ALengthTrackingViewOnAResizableBufferAnswersTheSameWay()
    {
        _engine.Evaluate("""
            var buffer = new ArrayBuffer(16, { maxByteLength: 16 });
            var view = new DataView(buffer);
            buffer.resize(8);
            """);

        _engine.Evaluate("view.byteLength").AsNumber().Should().Be(8, "the view tracks the shrunk buffer");

        foreach (var index in OutOfRangeIndexes)
        {
            ThrownErrorName($"view.getInt8({index})").Should().Be("RangeError");
            ThrownErrorName($"view.setInt8({index}, 1)").Should().Be("RangeError");
        }
    }

    /// <summary>
    /// Decoding a Float16 needs <c>System.Half</c>, which the net462 asset this project binds on net472 does
    /// not have — a read there raises <c>NotImplementedException</c> from <c>JsArrayBuffer</c>. That gap is
    /// older than this file and unrelated to it, so the in-range half of the boundary test below skips
    /// Float16 where <c>Half</c> is missing. Its rejection half runs everywhere, because the range check
    /// happens before the decode.
    /// </summary>
    private static readonly bool SupportsHalf = Type.GetType("System.Half") is not null;

    [Fact]
    public void TheLastInRangeIndexOfEachWidthStillReads()
    {
        // The boundary the fix must not move: viewSize - elementSize is the last accepted index, and one past
        // it is the first rejected one.
        _engine.Evaluate("var view = new DataView(new ArrayBuffer(16));");

        foreach (var (accessor, elementSize) in new[]
                 {
                     ("Int8", 1), ("Uint8", 1),
                     ("Int16", 2), ("Uint16", 2), ("Float16", 2),
                     ("Int32", 4), ("Uint32", 4), ("Float32", 4),
                     ("Float64", 8), ("BigInt64", 8), ("BigUint64", 8),
                 })
        {
            var last = 16 - elementSize;
            ThrownErrorName($"view.get{accessor}({last + 1})").Should().Be("RangeError", $"get{accessor}({last + 1}) is past the end");

            if (accessor == "Float16" && !SupportsHalf)
            {
                continue;
            }

            _engine.Evaluate($"view.get{accessor}({last})").ToString().Should().Be("0", $"get{accessor}({last}) is in range");
        }
    }

    [Fact]
    public void AnOrdinaryReadAndWriteRoundTrip()
    {
        _engine.Evaluate("""
            var view = new DataView(new ArrayBuffer(24), 4, 16);
            view.setFloat64(0, 1.5);
            view.setInt32(8, 123456, true);
            """);

        _engine.Evaluate("view.byteLength").AsNumber().Should().Be(16);
        _engine.Evaluate("view.byteOffset").AsNumber().Should().Be(4);
        _engine.Evaluate("view.getFloat64(0)").AsNumber().Should().Be(1.5);
        _engine.Evaluate("view.getInt32(8, true)").AsNumber().Should().Be(123456);

        // The write went into the view's own window, not to the front of the buffer.
        _engine.Evaluate("new DataView(view.buffer).getInt32(0)").AsNumber().Should().Be(0);
    }

    [Fact]
    public void AShrunkBufferPutsAFixedLengthViewOutOfBounds()
    {
        // Out of bounds is a TypeError, and it is decided before the index is looked at, so it wins over the
        // RangeError an out-of-range index would otherwise produce.
        _engine.Evaluate("""
            var buffer = new ArrayBuffer(16, { maxByteLength: 16 });
            var view = new DataView(buffer, 8);
            buffer.resize(4);
            """);

        ThrownErrorName("view.byteLength").Should().Be("TypeError");
        ThrownErrorName("view.byteOffset").Should().Be("TypeError");
        ThrownErrorName("view.getInt8(0)").Should().Be("TypeError");
        ThrownErrorName("view.setInt8(0, 1)").Should().Be("TypeError");
        ThrownErrorName("view.getInt8(2147483648)").Should().Be("TypeError");
        ThrownErrorName("view.setInt8(2147483648, 1)").Should().Be("TypeError");
    }

    [Fact]
    public void ADetachedBufferIsATypeError()
    {
        _engine.Evaluate("""
            var buffer = new ArrayBuffer(16);
            var view = new DataView(buffer);
            buffer.transfer();
            """);

        ThrownErrorName("view.byteLength").Should().Be("TypeError");
        ThrownErrorName("view.byteOffset").Should().Be("TypeError");
        ThrownErrorName("view.getInt8(0)").Should().Be("TypeError");
        ThrownErrorName("view.setInt8(0, 1)").Should().Be("TypeError");
        ThrownErrorName("view.getInt8(2147483648)").Should().Be("TypeError");
    }

    [Fact]
    public void ToIndexStillRejectsWhatItAlwaysRejected()
    {
        _engine.Evaluate("var view = new DataView(new ArrayBuffer(16));");

        ThrownErrorName("view.getInt8(-1)").Should().Be("RangeError");
        ThrownErrorName("view.getInt8(9007199254740992)").Should().Be("RangeError");
        ThrownErrorName("view.getInt8(Infinity)").Should().Be("RangeError");
        ThrownErrorName("view.setInt8(-1, 1)").Should().Be("RangeError");

        // A fractional or absent request is floored by ToIntegerOrInfinity rather than rejected.
        _engine.Evaluate("view.getInt8(0.9)").AsNumber().Should().Be(0);
        _engine.Evaluate("view.getInt8(undefined)").AsNumber().Should().Be(0);
    }

    [Fact]
    public void TheValueIsStillCoercedBeforeTheRangeCheck()
    {
        // SetViewValue coerces the value between ToIndex and the bounds test, so a bad value on an
        // out-of-range write is reported as that TypeError, not as the RangeError.
        ThrownErrorName("new DataView(new ArrayBuffer(16)).setBigInt64(2147483648, 1)").Should().Be("TypeError");
        ThrownErrorName("new DataView(new ArrayBuffer(16)).setInt8(2147483648, Symbol())").Should().Be("TypeError");
    }
}
