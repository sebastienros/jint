#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Every array-like algorithm starts with <see href="https://tc39.es/ecma262/#sec-lengthofarraylike">
/// LengthOfArrayLike</see>, which is <i>ToLength(Get(O, "length"))</i>, and
/// <see href="https://tc39.es/ecma262/#sec-tolength">ToLength</see> clamps into <c>[0, 2^53-1]</c>. It never
/// truncates and it never wraps.
/// <para>
/// Jint carried that value in two widths and only the <c>ulong</c> one clamped: the <c>uint</c> overload cast
/// the <c>double</c> straight across. An out-of-range <c>double</c>-to-integer conversion <em>saturates</em>
/// on .NET but is <em>unspecified</em> on .NET Framework, where it keeps the low 32 bits — so a
/// <c>length</c> of 2^53 read as 4294967295 on net10.0 and as 0 on net472, from the same script. These tests
/// therefore pin one answer for both target frameworks; a row that disagrees between them is the bug.
/// </para>
/// </summary>
public class ArrayLikeLengthClampTests
{
    private readonly Engine _engine = new();

    private string Run(string body) => _engine.Evaluate("(function () {" + body + "})()").ToString();

    /// <summary>
    /// The iterator's step 1.b is <i>LengthOfArrayLike</i> performed afresh on every <c>next()</c>
    /// (https://tc39.es/ecma262/#sec-createarrayiterator). A length above the index range is still a length,
    /// so index 0 is present and the first step yields it.
    /// </summary>
    [Theory]
    [InlineData("2147483648", "a")]           // 2^31
    [InlineData("4294967295", "a")]           // 2^32 - 1
    [InlineData("4294967296", "a")]           // 2^32     -- net472 kept the low 32 bits: 0
    [InlineData("8589934592", "a")]           // 2^33     -- likewise 0
    [InlineData("9007199254740991", "a")]     // 2^53 - 1 -- low 32 bits happen to be 0xFFFFFFFF
    [InlineData("9007199254740992", "a")]     // 2^53     -- likewise 0
    [InlineData("18446744073709551616", "a")] // 2^64
    [InlineData("Infinity", "a")]
    [InlineData("'9007199254740992'", "a")]   // a string above the range coerces, then clamps
    [InlineData("-1", "")]                    // ToLength clamps a negative to +0
    [InlineData("-Infinity", "")]
    [InlineData("NaN", "")]
    [InlineData("0", "")]
    public void TheArrayIteratorSeesTheClampedLength(string length, string expected)
    {
        var result = Run(
            "var o = {length: " + length + ", 0: 'a'};" +
            "var r = Array.prototype.values.call(o).next();" +
            "return r.done ? '' : r.value;");

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// The same length through the for-of lane, which steps the iterator without materializing an
    /// IteratorResult. One step and out, so a huge length costs nothing.
    /// </summary>
    [Theory]
    [InlineData("4294967296", "a")]
    [InlineData("9007199254740992", "a")]
    [InlineData("Infinity", "a")]
    [InlineData("-1", "none")]
    public void TheForOfStepLaneSeesTheClampedLength(string length, string expected)
    {
        var result = Run(
            "var o = {length: " + length + ", 0: 'a'};" +
            "var seen = 'none';" +
            "for (var v of Array.prototype.values.call(o)) { seen = v; break; }" +
            "return seen;");

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Every <c>Array.prototype</c> generic below is specified over <i>LengthOfArrayLike</i>, so a length of
    /// 2^53 means element 0 is read. A truncated length of 0 makes each of them return early instead — the
    /// throwing accessor is how that early return is told apart from the read.
    /// </summary>
    [Theory]
    [InlineData("Array.prototype.forEach.call(o, function () {})")]
    [InlineData("Array.prototype.filter.call(o, function () { return true; })")]
    [InlineData("Array.prototype.reduce.call(o, function () {}, 0)")]
    [InlineData("Array.prototype.sort.call(o)")]
    [InlineData("Array.prototype.join.call(o)")]
    [InlineData("Array.prototype.toLocaleString.call(o)")]
    [InlineData("Array.prototype.shift.call(o)")]
    [InlineData("Array.prototype.flat.call(o)")]
    [InlineData("Array.prototype.flatMap.call(o, function (x) { return x; })")]
    [InlineData("String.raw({raw: o})")]
    public void AGenericReadsElementZeroForALengthAboveTheIndexRange(string call)
    {
        var result = Run(
            "var o = {get 0() { throw 'reached'; }, length: 9007199254740992};" +
            "try { " + call + "; return 'not-reached'; } catch (e) { return e; }");

        Assert.Equal("reached", result);
    }

    /// <summary>
    /// The same generics for a length of exactly 0, which must <em>not</em> read element 0. The pair with the
    /// theory above is what makes "the length was seen" and "the length was zero" distinguishable.
    /// </summary>
    [Theory]
    [InlineData("Array.prototype.forEach.call(o, function () {})")]
    [InlineData("Array.prototype.filter.call(o, function () { return true; })")]
    [InlineData("Array.prototype.reduce.call(o, function () {}, 0)")]
    [InlineData("Array.prototype.sort.call(o)")]
    [InlineData("Array.prototype.join.call(o)")]
    [InlineData("Array.prototype.toLocaleString.call(o)")]
    [InlineData("Array.prototype.shift.call(o)")]
    [InlineData("Array.prototype.flat.call(o)")]
    [InlineData("Array.prototype.flatMap.call(o, function (x) { return x; })")]
    [InlineData("String.raw({raw: o})")]
    public void AGenericDoesNotReadElementZeroForALengthOfZero(string call)
    {
        var result = Run(
            "var o = {get 0() { throw 'reached'; }, length: 0};" +
            "try { " + call + "; return 'not-reached'; } catch (e) { return e; }");

        Assert.Equal("not-reached", result);
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-array.from">Array.from</see> step 7.d hands the constructor
    /// the length as a Number, so a constructor receiver reads the clamped value back out exactly. This is
    /// the one place the specified value is observable without iterating it.
    /// </summary>
    [Theory]
    [InlineData("2147483648", "2147483648")]
    [InlineData("4294967295", "4294967295")]
    [InlineData("4294967296", "4294967296")]
    [InlineData("8589934592", "8589934592")]
    [InlineData("9007199254740991", "9007199254740991")]
    [InlineData("9007199254740992", "9007199254740991")]
    [InlineData("18446744073709551616", "9007199254740991")]
    [InlineData("Infinity", "9007199254740991")]
    [InlineData("'9007199254740992'", "9007199254740991")]
    [InlineData("-1", "0")]
    [InlineData("-Infinity", "0")]
    [InlineData("NaN", "0")]
    public void ArrayFromHandsTheConstructorTheClampedLength(string length, string expected)
    {
        var result = Run(
            "var seen;" +
            "function C(n) { seen = n; }" +
            "var o = {get 0() { throw 'stop'; }, length: " + length + "};" +
            "try { Array.from.call(C, o); } catch (e) { }" +
            "return String(seen);");

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// The non-constructor branch is <i>ArrayCreate(len)</i>, whose step 1 is a RangeError for anything above
    /// 2^32-1 (https://tc39.es/ecma262/#sec-arraycreate). A narrowed length either lands inside that range
    /// and creates an array, or lands on 0 and creates an empty one.
    /// </summary>
    [Theory]
    [InlineData("4294967296")]
    [InlineData("9007199254740992")]
    [InlineData("Infinity")]
    public void ArrayFromRejectsALengthAboveTheArrayIndexRange(string length)
    {
        var result = Run(
            "var o = {get 0() { throw 'element-read'; }, length: " + length + "};" +
            "try { Array.from(o); return 'no-throw'; } catch (e) { return e instanceof RangeError ? 'RangeError' : String(e); }");

        Assert.Equal("RangeError", result);
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-settypedarrayfromarraylike">SetTypedArrayFromArrayLike</see>
    /// step 5 is <i>If srcLength + targetOffset &gt; targetLength, throw a RangeError</i>. A source length
    /// that truncated to 0 passes that test and silently copies nothing.
    /// </summary>
    [Theory]
    [InlineData("4294967296")]
    [InlineData("9007199254740992")]
    [InlineData("Infinity")]
    public void TypedArraySetRejectsASourceLengthAboveTheTargetLength(string length)
    {
        var result = Run(
            "var ta = new Uint8Array(4);" +
            "try { ta.set({length: " + length + "}); return 'no-throw'; } catch (e) { return e instanceof RangeError ? 'RangeError' : String(e); }");

        Assert.Equal("RangeError", result);
    }

    /// <summary>
    /// The <em>offset</em> half of the same step. It is a Number all the way down in the specification
    /// (step 6 is <i>If targetOffset is +infinity, throw a RangeError</i>, step 7 adds it to srcLength), and
    /// narrowing it to an <c>int</c> before either step is the same unspecified conversion the length had.
    /// </summary>
    [Theory]
    [InlineData("4294967296")]
    [InlineData("1e20")]
    [InlineData("9007199254740992")]
    [InlineData("Infinity")]
    public void TypedArraySetRejectsAnOffsetAboveTheTargetLength(string offset)
    {
        var result = Run(
            "var ta = new Uint8Array(4);" +
            "try { ta.set([1], " + offset + "); return 'no-throw:' + ta.join(','); } catch (e) { return e instanceof RangeError ? 'RangeError' : String(e); }");

        Assert.Equal("RangeError", result);
    }
}
