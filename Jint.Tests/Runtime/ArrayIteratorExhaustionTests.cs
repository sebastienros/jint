namespace Jint.Tests.Runtime;

/// <summary>
/// The abstract closure behind <see href="https://tc39.es/ecma262/#sec-createarrayiterator">
/// CreateArrayIterator</see> is a generator body: once it has run off the end and returned, the generator is
/// <em>completed</em>, and every later <c>next</c> answers <c>{ value: undefined, done: true }</c> without
/// executing a single step of it again. So an exhausted iterator observes nothing about the array it came
/// from — not <c>ValidateTypedArray</c>, which would raise a <c>TypeError</c> for a buffer detached in the
/// meantime, and not the <c>length</c> read an array-like's step begins with.
/// <para>
/// Jint kept stepping the closure. The detached-buffer assertion in particular ran unconditionally, so
/// detaching a buffer after its typed array had been iterated to the end turned the next <c>next</c> into a
/// <c>TypeError</c> where the spec requires another <c>{ done: true }</c>. test262 covers it in
/// <c>staging/sm/TypedArray/iterator-next-with-detached.js</c>.
/// </para>
/// </summary>
public class ArrayIteratorExhaustionTests
{
    private readonly Engine _engine = new();

    [Test]
    public void AnExhaustedTypedArrayIteratorIgnoresALaterDetach()
    {
        const string Script = """
            var buffer = new ArrayBuffer(2);
            var array = new Uint8Array(buffer);
            array[0] = 1;
            array[1] = 2;

            var iterator = array[Symbol.iterator]();
            var seen = [iterator.next().value, iterator.next().value];
            iterator.next().done;

            buffer.transfer();
            var after = iterator.next();
            seen.join(',') + ' ' + after.value + ' ' + after.done;
            """;

        _engine.Evaluate(Script).AsString().Should().Be("1,2 undefined true");
    }

    /// <summary>
    /// The control, and the half that was already right: an iterator that has <em>not</em> yet reported
    /// <c>done</c> still validates the array, so a detach it has not stepped past is a <c>TypeError</c>.
    /// That includes the all-but-exhausted case, where the elements are gone but the closure has not
    /// returned yet.
    /// </summary>
    [TestCase(1)]
    [TestCase(2)]
    public void AnUnfinishedTypedArrayIteratorStillReportsADetach(int stepsBeforeDetaching)
    {
        var script = $$"""
            var buffer = new ArrayBuffer(2);
            var array = new Uint8Array(buffer);
            var iterator = array[Symbol.iterator]();
            for (var i = 0; i < {{stepsBeforeDetaching}}; i++) { iterator.next(); }

            buffer.transfer();
            try { iterator.next(); return 'did not throw'; }
            catch (e) { return e.constructor.name; }
            """;

        _engine.Evaluate($"(function () {{ {script} }})()").AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// The same rule for an ordinary array-like: once the closure has returned, nothing re-reads
    /// <c>length</c>, so a getter that could grow the collection never runs again. The assertion is that the
    /// count stops moving, not what it reached - how many times the iterator consults <c>length</c> on its
    /// way to <c>done</c> is a separate question this fix does not touch.
    /// </summary>
    [Test]
    public void AnExhaustedArrayLikeIteratorDoesNotReReadLength()
    {
        const string Script = """
            var reads = 0;
            var arrayLike = { 0: 'a', get length() { reads++; return 1; } };
            var iterator = Array.prototype[Symbol.iterator].call(arrayLike);

            var values = [iterator.next().value, iterator.next().value];
            var readsWhenDone = reads;
            iterator.next();
            iterator.next();

            values.join(',') + ' ' + (reads === readsWhenDone);
            """;

        _engine.Evaluate(Script).AsString().Should().Be("a, true");
    }
}
