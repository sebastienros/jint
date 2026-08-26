using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Order-sensitive corners of the array exotic object and of the generics that write through it: the
/// downward walk <c>ArraySetLength</c> performs when it truncates, the throwing <c>Set</c> that
/// <c>shift</c> and <c>splice</c> use to relocate an element, and <c>LengthOfArrayLike</c> being a real
/// <c>Get</c> when the receiver of an <c>Array.prototype</c> generic is a typed array.
/// </summary>
public class ArrayExoticOrderingTests
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraysetlength step 17 walks downwards from <c>oldLen - 1</c> and stops
    /// at the first index whose <c>[[Delete]]</c> fails, reporting that index + 1 as the length. Everything
    /// below the survivor is shielded by it. Jint deleted the concrete elements in ascending order, so the
    /// low ones were gone by the time the non-configurable one refused.
    /// </summary>
    [Test]
    public void TruncatingLengthStopsAtTheHighestNonConfigurableElement()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [];
            arr[10] = 'a';
            arr[20] = 'b';
            Object.defineProperty(arr, 30, { value: 'keep', configurable: false, writable: true, enumerable: true });
            arr[40] = 'c';
            arr.length = 1;
            [arr.length, 10 in arr, 20 in arr, 30 in arr, 40 in arr].join(',');
            """).AsString();

        result.Should().Be("31,true,true,true,false");
    }

    [Test]
    public void TruncatingADenseArrayStopsAtTheHighestNonConfigurableElement()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [0, 1, 2, 3, 4];
            Object.defineProperty(arr, 2, { value: 2, configurable: false, writable: true, enumerable: true });
            arr.length = 0;
            [arr.length, 0 in arr, 1 in arr, 2 in arr, 3 in arr, 4 in arr].join(',');
            """).AsString();

        result.Should().Be("3,true,true,true,false,false");
    }

    /// <summary>
    /// Truncation in strict mode reports the same length and the same surviving set, but the failed
    /// <c>[[DefineOwnProperty]]</c> becomes a TypeError.
    /// </summary>
    [Test]
    public void TruncatingPastANonConfigurableElementThrowsInStrictMode()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            'use strict';
            var arr = [];
            arr[10] = 'a';
            Object.defineProperty(arr, 30, { value: 'keep', configurable: false, writable: true, enumerable: true });
            arr[40] = 'c';
            var threw = false;
            try { arr.length = 1; } catch (e) { threw = e instanceof TypeError; }
            [threw, arr.length, 10 in arr, 30 in arr, 40 in arr].join(',');
            """).AsString();

        result.Should().Be("true,31,true,true,false");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.shift step 6.d.ii is <c>Set(O, to, fromValue, true)</c>:
    /// a non-writable destination is a TypeError in sloppy mode too.
    /// </summary>
    [TestCase("var a = [10, 20, 30]; Object.defineProperty(a, 0, { writable: false }); a.shift();")]
    [TestCase("var o = { 0: 10, 1: 20, 2: 30, length: 3 }; Object.defineProperty(o, 0, { writable: false }); Array.prototype.shift.call(o);")]
    [TestCase("var a = [1, 2, 3]; Object.defineProperty(a, 0, { writable: false }); a.splice(0, 1);")]
    [TestCase("var o = { 0: 1, 1: 2, 2: 3, length: 3 }; Object.defineProperty(o, 0, { writable: false }); Array.prototype.splice.call(o, 0, 1);")]
    public void RelocatingOverANonWritableElementThrows(string source)
    {
        foreach (var prefix in new[] { "", "'use strict';\n" })
        {
            var engine = new Engine();
            var act = () => engine.Evaluate(prefix + source);
            act.Should().Throw<JavaScriptException>().Which.Error.ToString().Should().Contain("read only");
        }
    }

    /// <summary>
    /// A hole in the source means <c>DeletePropertyOrThrow</c> on the destination, which a
    /// non-configurable element refuses.
    /// </summary>
    [TestCase("var a = [1, 2, , 4]; Object.defineProperty(a, 1, { configurable: false }); a.shift();")]
    [TestCase("var a = [1, 2, , 4]; Object.defineProperty(a, 1, { configurable: false }); a.splice(0, 1);")]
    public void DeletingANonConfigurableDestinationThrows(string source)
    {
        foreach (var prefix in new[] { "", "'use strict';\n" })
        {
            var engine = new Engine();
            var act = () => engine.Evaluate(prefix + source);
            act.Should().Throw<JavaScriptException>();
        }
    }

    /// <summary>
    /// An <c>Array.prototype</c> generic takes its length from <c>LengthOfArrayLike</c>, which is
    /// <c>ToLength(? Get(O, "length"))</c> — so an own <c>"length"</c> on a typed array shadows the
    /// <c>%TypedArray%.prototype</c> accessor and narrows what the generic touches.
    /// </summary>
    [Test]
    public void ArrayGenericOnATypedArrayHonoursAnOwnLength()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var ta = new Int8Array([3, 2, 1]);
            Object.defineProperty(ta, 'length', { value: 2 });
            Array.prototype.sort.call(ta, function (a, b) { return a - b; });
            ta.toString();
            """).AsString();

        result.Should().Be("2,3,1");
    }

    /// <summary>
    /// The typed array's <em>own</em> sort is specified on <c>TypedArrayLength</c>
    /// (https://tc39.es/ecma262/#sec-%typedarray%.prototype.sort step 4), so the same shadowing property
    /// must not narrow it.
    /// </summary>
    [Test]
    public void TypedArraySortIgnoresAnOwnLength()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var ta = new Int8Array([3, 2, 1]);
            Object.defineProperty(ta, 'length', { value: 2 });
            ta.sort(function (a, b) { return a - b; });
            ta.toString();
            """).AsString();

        result.Should().Be("1,2,3");
    }

    [Test]
    public void ArrayGenericOnATypedArrayWithoutAnOwnLengthIsUnchanged()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var ta = new Int8Array([3, 2, 1]);
            Array.prototype.sort.call(ta, function (a, b) { return a - b; });
            ta.toString();
            """).AsString();

        result.Should().Be("1,2,3");
    }
}
