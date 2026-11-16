#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The built-ins that make a script-sized array or list (issue #4520) are held to <c>Options.Constraints.MaxArraySize</c> where
/// the list is built, with the exception and message of <c>ArrayConstructor.ValidateElementCount</c>
/// (<see cref="MemoryLimitExceededException"/>, "The array size N is larger than maximum allowed (M)"). The ones that
/// pull values from an iterator stop at the first value past the limit and close the iterator, as an abrupt completion
/// of the built-in's own does (https://tc39.es/ecma262/#sec-iteratorclose,
/// https://tc39.es/ecma262/#sec-asynciteratorclose).
/// </summary>
public class ArrayBuiltinMaxArraySizeTests
{
    private static class Caught
    {
        public static Exception? Exception(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }

    private const uint Limit = 4;

    /// <summary>
    /// <c>ten</c> is a ten-element array-like, <c>gen(n)</c> a generator of <c>n</c> values that records, in
    /// <c>pulled</c> and <c>closed</c>, how far it was driven and whether its <c>finally</c> ran, and <c>asyncGen(n)</c>
    /// is its async twin.
    /// </summary>
    private const string Prelude = """
        var ten = { length: 10, 0: 'a', 1: 'b', 2: 'c', 3: 'd', 4: 'e', 5: 'f', 6: 'g', 7: 'h', 8: 'i', 9: 'j' };
        var pulled = 0, closed = false;
        function* gen(n) { try { for (let i = 0; i < n; i++) { pulled++; yield i; } } finally { closed = true; } }
        async function* asyncGen(n) { try { for (let i = 0; i < n; i++) { pulled++; yield i; } } finally { closed = true; } }
        function manyKeys(n) { const p = new Proxy({}, { has: () => true, get: (t, k) => k === 'length' ? n : Number(k) }); return p; }
        """;

    private static Engine EngineWith(uint maxArraySize = Limit)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = maxArraySize);
        engine.Execute(Prelude);
        return engine;
    }

    private static void ShouldBeRefused(Exception? thrown, int size)
    {
        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Contain("The array size " + size + " is larger than maximum allowed (" + Limit + ")");
    }

    /// <summary>
    /// Each of these makes an array of the array-like's <c>length</c> (https://tc39.es/ecma262/#sec-array.prototype.tosorted,
    /// https://tc39.es/ecma262/#sec-array.prototype.toreversed, https://tc39.es/ecma262/#sec-array.prototype.with
    /// and https://tc39.es/ecma262/#sec-array.prototype.tospliced); none of the specification's own steps throws first.
    /// </summary>
    [Theory]
    [InlineData("Array.prototype.toSorted.call(ten)")]
    [InlineData("Array.prototype.toReversed.call(ten)")]
    [InlineData("Array.prototype.with.call(ten, 0, 'x')")]
    [InlineData("Array.prototype.toSpliced.call(ten, 0, 0)")]
    [InlineData("Array.prototype.toSpliced.call(ten, 0, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)")]
    public void AnArrayLikePastTheLimitIsRefused(string expression)
    {
        var engine = EngineWith();

        ShouldBeRefused(Caught.Exception(() => engine.Evaluate(expression)), 10);
    }

    /// <summary>
    /// The source is within the limit and the result is not: <c>toSpliced</c> sizes its result from the inserted items.
    /// </summary>
    [Fact]
    public void ToSplicedRefusesAResultPastTheLimitMadeOfASmallSource()
    {
        var engine = EngineWith();

        ShouldBeRefused(Caught.Exception(() => engine.Evaluate("[1, 2, 3].toSpliced(0, 0, 4, 5, 6)")), 6);
    }

    /// <summary>
    /// <c>sort</c> gathers the array-like's present elements into a list (SortIndexedProperties, holes skipped); a proxy
    /// that reports every index present makes the list as long as the <c>length</c> it answers.
    /// </summary>
    [Fact]
    public void SortRefusesAListPastTheLimit()
    {
        var engine = EngineWith();

        Caught.Exception(() => engine.Evaluate("Array.prototype.sort.call(manyKeys(10))")).Should().BeOfType<MemoryLimitExceededException>();
    }

    /// <summary>
    /// The sparse case is no list past the limit: a huge <c>length</c> with a few present elements sorts as it did.
    /// </summary>
    [Fact]
    public void SortOfASparseArrayLikeStillSucceeds()
    {
        var engine = EngineWith();

        engine.Evaluate("var s = { length: 1000000, 5: 'b', 900: 'a' }; Array.prototype.sort.call(s); s[0] + s[1]")
            .AsString().Should().Be("ab");
    }

    [Fact]
    public void SortFromARealArrayWithinTheLimitIsUnchanged()
    {
        var engine = EngineWith();

        engine.Evaluate("[3, 1, 2, 0].sort().join()").AsString().Should().Be("0,1,2,3");
        engine.Evaluate("[3, 1, 2, 0].toSorted().join()").AsString().Should().Be("0,1,2,3");
    }

    /// <summary>
    /// The control: an array-like of exactly <c>MaxArraySize</c> elements is allowed, so the check is a strict "past".
    /// </summary>
    [Theory]
    [InlineData("Array.prototype.toSorted.call({ length: 4, 0: 3, 1: 2, 2: 1, 3: 0 }).join()", "0,1,2,3")]
    [InlineData("Array.prototype.toReversed.call({ length: 4, 0: 0, 1: 1, 2: 2, 3: 3 }).join()", "3,2,1,0")]
    [InlineData("Array.prototype.with.call({ length: 4, 0: 0, 1: 1, 2: 2, 3: 3 }, 0, 'x').join()", "x,1,2,3")]
    [InlineData("Array.prototype.toSpliced.call({ length: 4, 0: 0, 1: 1, 2: 2, 3: 3 }, 0, 0).join()", "0,1,2,3")]
    [InlineData("[1, 2, 3].toSpliced(0, 0, 4).join()", "4,1,2,3")]
    [InlineData("Array.from(gen(4)).join()", "0,1,2,3")]
    [InlineData("gen(4).toArray().join()", "0,1,2,3")]
    [InlineData("Object.groupBy(gen(4), () => 'a').a.join()", "0,1,2,3")]
    [InlineData("Map.groupBy(gen(4), () => 'a').get('a').join()", "0,1,2,3")]
    [InlineData("Array.from({ length: 4, 0: 0, 1: 1, 2: 2, 3: 3 }).join()", "0,1,2,3")]
    public void AnArrayOfExactlyTheLimitIsAllowed(string expression, string expected)
    {
        var engine = EngineWith();

        engine.Evaluate(expression).AsString().Should().Be(expected);
    }

    /// <summary>
    /// A synchronous collection stops at the first value past the limit, closes the iterator and leaves the generator
    /// where it was: <c>pulled</c> is the limit plus the one value that did not fit.
    /// </summary>
    [Theory]
    [InlineData("Array.from(gen(10))")]
    [InlineData("Array.from(gen(10), x => x)")]
    [InlineData("gen(10).toArray()")]
    public void AnIteratorCollectionStopsAtTheLimitAndClosesTheIterator(string expression)
    {
        var engine = EngineWith();

        ShouldBeRefused(Caught.Exception(() => engine.Evaluate(expression)), (int) Limit + 1);

        engine.Evaluate("pulled").AsNumber().Should().Be(Limit + 1);
        engine.Evaluate("closed").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>Array.from</c> over an array-like and over a list-backed iterator that is not a generator.
    /// </summary>
    [Theory]
    [InlineData("Array.from(ten)")]
    [InlineData("Array.from(new Set([1, 2, 3, 4, 5]))")]
    [InlineData("Array.from(new Set([1, 2, 3, 4, 5]).values().map(x => x))")]
    public void ArrayFromPastTheLimitIsRefused(string expression)
    {
        var engine = EngineWith();

        Caught.Exception(() => engine.Evaluate(expression)).Should().BeOfType<MemoryLimitExceededException>();
    }

    /// <summary>
    /// <c>Object.groupBy</c> and <c>Map.groupBy</c> build one list per key, each an array (GroupByHelper); a key's list
    /// is held to the limit like any array.
    /// </summary>
    [Theory]
    [InlineData("Object.groupBy(gen(10), () => 'a')")]
    [InlineData("Map.groupBy(gen(10), () => 'a')")]
    public void AGroupListPastTheLimitIsRefused(string expression)
    {
        var engine = EngineWith();

        Caught.Exception(() => engine.Evaluate(expression)).Should().BeOfType<MemoryLimitExceededException>();

        engine.Evaluate("pulled").AsNumber().Should().BeLessThanOrEqualTo(Limit + 1);
        engine.Evaluate("closed").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Groups of one element each are lists within the limit, however many there are.
    /// </summary>
    [Fact]
    public void ManyGroupsOfOneAreNotAnArrayPastTheLimit()
    {
        var engine = EngineWith();

        engine.Evaluate("Map.groupBy(gen(10), x => x).size").AsNumber().Should().Be(10);
    }

    /// <summary>
    /// The asynchronous twin of <c>Iterator.prototype.toArray</c>: the run ends in the exception when the promise
    /// jobs run, and the source async iterator has been asked to <c>return</c>.
    /// </summary>
    [Fact]
    public void AsyncIteratorToArrayStopsAtTheLimitAndClosesTheIterator()
    {
        var engine = EngineWith();

        var thrown = Caught.Exception(() => engine.Evaluate("asyncGen(10).toArray()").UnwrapIfPromise());

        ShouldBeRefused(thrown, (int) Limit + 1);
        engine.Evaluate("pulled").AsNumber().Should().Be(Limit + 1);
        engine.RunAvailableContinuations();
        engine.Evaluate("closed").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AsyncIteratorToArrayOfExactlyTheLimitResolves()
    {
        var engine = EngineWith();

        engine.Evaluate("asyncGen(4).toArray().then(a => a.join())").UnwrapIfPromise().AsString().Should().Be("0,1,2,3");
    }

    /// <summary>
    /// A host that set no limit gets the arrays it always did.
    /// </summary>
    [Fact]
    public void WithoutALimitNothingIsRefused()
    {
        var engine = new Engine();
        engine.Execute(Prelude);

        engine.Evaluate("Array.prototype.toSorted.call(ten).length").AsNumber().Should().Be(10);
        engine.Evaluate("Array.prototype.toReversed.call(ten).length").AsNumber().Should().Be(10);
        engine.Evaluate("Array.prototype.with.call(ten, 0, 'x').length").AsNumber().Should().Be(10);
        engine.Evaluate("Array.prototype.toSpliced.call(ten, 0, 0).length").AsNumber().Should().Be(10);
        engine.Evaluate("Array.prototype.sort.call(manyKeys(10)).length").AsNumber().Should().Be(10);
        engine.Evaluate("Array.from(gen(10)).length").AsNumber().Should().Be(10);
        engine.Evaluate("gen(10).toArray().length").AsNumber().Should().Be(10);
        engine.Evaluate("Object.groupBy(gen(10), () => 'a').a.length").AsNumber().Should().Be(10);
        engine.Evaluate("asyncGen(10).toArray().then(a => a.length)").UnwrapIfPromise().AsNumber().Should().Be(10);
    }
}
