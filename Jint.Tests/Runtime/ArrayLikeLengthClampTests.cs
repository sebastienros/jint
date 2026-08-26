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
    [TestCase("2147483648", "a")]           // 2^31
    [TestCase("4294967295", "a")]           // 2^32 - 1
    [TestCase("4294967296", "a")]           // 2^32     -- net472 kept the low 32 bits: 0
    [TestCase("8589934592", "a")]           // 2^33     -- likewise 0
    [TestCase("9007199254740991", "a")]     // 2^53 - 1 -- low 32 bits happen to be 0xFFFFFFFF
    [TestCase("9007199254740992", "a")]     // 2^53     -- likewise 0
    [TestCase("18446744073709551616", "a")] // 2^64
    [TestCase("Infinity", "a")]
    [TestCase("'9007199254740992'", "a")]   // a string above the range coerces, then clamps
    [TestCase("-1", "")]                    // ToLength clamps a negative to +0
    [TestCase("-Infinity", "")]
    [TestCase("NaN", "")]
    [TestCase("0", "")]
    public void TheArrayIteratorSeesTheClampedLength(string length, string expected)
    {
        var result = Run(
            "var o = {length: " + length + ", 0: 'a'};" +
            "var r = Array.prototype.values.call(o).next();" +
            "return r.done ? '' : r.value;");

        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// The same length through the for-of lane, which steps the iterator without materializing an
    /// IteratorResult. One step and out, so a huge length costs nothing.
    /// </summary>
    [TestCase("4294967296", "a")]
    [TestCase("9007199254740992", "a")]
    [TestCase("Infinity", "a")]
    [TestCase("-1", "none")]
    public void TheForOfStepLaneSeesTheClampedLength(string length, string expected)
    {
        var result = Run(
            "var o = {length: " + length + ", 0: 'a'};" +
            "var seen = 'none';" +
            "for (var v of Array.prototype.values.call(o)) { seen = v; break; }" +
            "return seen;");

        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Every <c>Array.prototype</c> generic below is specified over <i>LengthOfArrayLike</i>, so a length of
    /// 2^53 means element 0 is read. A truncated length of 0 makes each of them return early instead — the
    /// throwing accessor is how that early return is told apart from the read.
    /// </summary>
    [TestCase("Array.prototype.forEach.call(o, function () {})")]
    [TestCase("Array.prototype.filter.call(o, function () { return true; })")]
    [TestCase("Array.prototype.reduce.call(o, function () {}, 0)")]
    [TestCase("Array.prototype.sort.call(o)")]
    [TestCase("Array.prototype.join.call(o)")]
    [TestCase("Array.prototype.toLocaleString.call(o)")]
    [TestCase("Array.prototype.shift.call(o)")]
    [TestCase("Array.prototype.flat.call(o)")]
    [TestCase("Array.prototype.flatMap.call(o, function (x) { return x; })")]
    [TestCase("String.raw({raw: o})")]
    public void AGenericReadsElementZeroForALengthAboveTheIndexRange(string call)
    {
        var result = Run(
            "var o = {get 0() { throw 'reached'; }, length: 9007199254740992};" +
            "try { " + call + "; return 'not-reached'; } catch (e) { return e; }");

        Assert.That(result, Is.EqualTo("reached"));
    }

    /// <summary>
    /// The same generics for a length of exactly 0, which must <em>not</em> read element 0. The pair with the
    /// theory above is what makes "the length was seen" and "the length was zero" distinguishable.
    /// </summary>
    [TestCase("Array.prototype.forEach.call(o, function () {})")]
    [TestCase("Array.prototype.filter.call(o, function () { return true; })")]
    [TestCase("Array.prototype.reduce.call(o, function () {}, 0)")]
    [TestCase("Array.prototype.sort.call(o)")]
    [TestCase("Array.prototype.join.call(o)")]
    [TestCase("Array.prototype.toLocaleString.call(o)")]
    [TestCase("Array.prototype.shift.call(o)")]
    [TestCase("Array.prototype.flat.call(o)")]
    [TestCase("Array.prototype.flatMap.call(o, function (x) { return x; })")]
    [TestCase("String.raw({raw: o})")]
    public void AGenericDoesNotReadElementZeroForALengthOfZero(string call)
    {
        var result = Run(
            "var o = {get 0() { throw 'reached'; }, length: 0};" +
            "try { " + call + "; return 'not-reached'; } catch (e) { return e; }");

        Assert.That(result, Is.EqualTo("not-reached"));
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-array.from">Array.from</see> step 7.d hands the constructor
    /// the length as a Number, so a constructor receiver reads the clamped value back out exactly. This is
    /// the one place the specified value is observable without iterating it.
    /// </summary>
    [TestCase("2147483648", "2147483648")]
    [TestCase("4294967295", "4294967295")]
    [TestCase("4294967296", "4294967296")]
    [TestCase("8589934592", "8589934592")]
    [TestCase("9007199254740991", "9007199254740991")]
    [TestCase("9007199254740992", "9007199254740991")]
    [TestCase("18446744073709551616", "9007199254740991")]
    [TestCase("Infinity", "9007199254740991")]
    [TestCase("'9007199254740992'", "9007199254740991")]
    [TestCase("-1", "0")]
    [TestCase("-Infinity", "0")]
    [TestCase("NaN", "0")]
    public void ArrayFromHandsTheConstructorTheClampedLength(string length, string expected)
    {
        var result = Run(
            "var seen;" +
            "function C(n) { seen = n; }" +
            "var o = {get 0() { throw 'stop'; }, length: " + length + "};" +
            "try { Array.from.call(C, o); } catch (e) { }" +
            "return String(seen);");

        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// The non-constructor branch is <i>ArrayCreate(len)</i>, whose step 1 is a RangeError for anything above
    /// 2^32-1 (https://tc39.es/ecma262/#sec-arraycreate). A narrowed length either lands inside that range
    /// and creates an array, or lands on 0 and creates an empty one.
    /// </summary>
    [TestCase("4294967296")]
    [TestCase("9007199254740992")]
    [TestCase("Infinity")]
    public void ArrayFromRejectsALengthAboveTheArrayIndexRange(string length)
    {
        var result = Run(
            "var o = {get 0() { throw 'element-read'; }, length: " + length + "};" +
            "try { Array.from(o); return 'no-throw'; } catch (e) { return e instanceof RangeError ? 'RangeError' : String(e); }");

        Assert.That(result, Is.EqualTo("RangeError"));
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-settypedarrayfromarraylike">SetTypedArrayFromArrayLike</see>
    /// step 5 is <i>If srcLength + targetOffset &gt; targetLength, throw a RangeError</i>. A source length
    /// that truncated to 0 passes that test and silently copies nothing.
    /// </summary>
    [TestCase("4294967296")]
    [TestCase("9007199254740992")]
    [TestCase("Infinity")]
    public void TypedArraySetRejectsASourceLengthAboveTheTargetLength(string length)
    {
        var result = Run(
            "var ta = new Uint8Array(4);" +
            "try { ta.set({length: " + length + "}); return 'no-throw'; } catch (e) { return e instanceof RangeError ? 'RangeError' : String(e); }");

        Assert.That(result, Is.EqualTo("RangeError"));
    }

    /// <summary>
    /// The <em>offset</em> half of the same step. It is a Number all the way down in the specification
    /// (step 6 is <i>If targetOffset is +infinity, throw a RangeError</i>, step 7 adds it to srcLength), and
    /// narrowing it to an <c>int</c> before either step is the same unspecified conversion the length had.
    /// </summary>
    [TestCase("4294967296")]
    [TestCase("1e20")]
    [TestCase("9007199254740992")]
    [TestCase("Infinity")]
    public void TypedArraySetRejectsAnOffsetAboveTheTargetLength(string offset)
    {
        var result = Run(
            "var ta = new Uint8Array(4);" +
            "try { ta.set([1], " + offset + "); return 'no-throw:' + ta.join(','); } catch (e) { return e instanceof RangeError ? 'RangeError' : String(e); }");

        Assert.That(result, Is.EqualTo("RangeError"));
    }
}
