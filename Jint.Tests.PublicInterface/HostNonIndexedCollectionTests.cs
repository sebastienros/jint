#nullable enable

using System.Collections;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what the <c>Array.prototype</c> generics do to a wrapped CLR collection that has a count but no
/// index — <see cref="Queue{T}"/>, <see cref="Stack{T}"/>, <see cref="LinkedList{T}"/>,
/// <see cref="SortedSet{T}"/>, <see cref="HashSet{T}"/>, and an embedder's own
/// <see cref="System.Collections.ICollection"/>.
///
/// <para>
/// Every one of those is array-like — it has a <c>Count</c>, so the wrapper carries <c>Array.prototype</c>
/// and a live <c>length</c> — and none of them has an element at index 0. <c>ICollection</c> is a
/// count-and-copy contract; the index is not in it. So the generics honour <c>length</c> and read
/// <see langword="undefined"/> at every index, which is what an array-like with no index properties means in
/// JavaScript, and iteration keeps yielding the real elements because that goes through the wrapper's
/// <c>Symbol.iterator</c> instead. That split is the specification's, not an interop compromise:
/// <c>Array.prototype.join</c> is <i>LengthOfArrayLike</i> plus <i>Get(O, ToString(k))</i>, while spread,
/// <c>for..of</c>, <c>Array.from</c> and array destructuring are all <i>GetIterator</i>.
/// </para>
///
/// <para>
/// Before #3302 the five that implement the <em>non-generic</em> <c>ICollection</c> reached an indexed lane
/// that cast their target to <c>IList</c>, so every generic above threw a raw
/// <see cref="InvalidCastException"/> out of <c>Engine.Evaluate</c> — not a
/// <see cref="Jint.Runtime.JavaScriptException"/>, so neither a host <c>catch</c> nor a script
/// <c>try</c>/<c>catch</c> could see it. <see cref="HashSet{T}"/> is carried along here because it is a
/// collection through the generic interface only and so was never admitted to that lane: it is the shape the
/// other five now match.
/// </para>
/// </summary>
public class HostNonIndexedCollectionTests
{
    public static TestCases<string> CountedButNotIndexed => new TestCases<string>
    {
        nameof(Queue<int>),
        nameof(Stack<int>),
        nameof(LinkedList<int>),
        nameof(SortedSet<int>),
        nameof(HashSet<int>),
        nameof(CountOnlyCollection),
    };

    private static object CreateCollection(string kind) => kind switch
    {
        nameof(Queue<int>) => new Queue<int>(new[] { 1, 2, 3 }),
        // enumerates top-first, so push 3, 2, 1 to iterate 1, 2, 3
        nameof(Stack<int>) => new Stack<int>(new[] { 3, 2, 1 }),
        nameof(LinkedList<int>) => new LinkedList<int>(new[] { 1, 2, 3 }),
        nameof(SortedSet<int>) => new SortedSet<int> { 1, 2, 3 },
        nameof(CountOnlyCollection) => new CountOnlyCollection(),
        _ => new HashSet<int> { 1, 2, 3 },
    };

    private static Engine CreateEngine(object host, Action<Options>? configure = null)
    {
        var engine = new Engine(options => configure?.Invoke(options));
        engine.SetValue("host", host);
        return engine;
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void TheIndexReadingGenericsHonourLengthAndFindNoElements(string kind)
    {
        var engine = CreateEngine(CreateCollection(kind));

        // the count is there, and so is the prototype the generics are read off
        engine.Evaluate("host.length").Should().Be(3);
        engine.Evaluate("Object.getPrototypeOf(host) === Array.prototype").Should().BeTrue();

        // ...but there is nothing at any index, so the separators alone show length was honoured
        engine.Evaluate("host[0]").Should().BeUndefined();
        engine.Evaluate("'0' in host").Should().BeFalse();
        engine.Evaluate("Array.prototype.join.call(host, '-')").Should().Be("--");
        engine.Evaluate("Array.prototype.map.call(host, function (x) { return x; }).join('-')").Should().Be("--");
        engine.Evaluate("Array.prototype.slice.call(host).length").Should().Be(3);

        // and the generics that skip absent indices see nothing at all
        engine.Evaluate("Array.prototype.filter.call(host, function () { return true; }).length").Should().Be(0);
        engine.Evaluate("Array.prototype.indexOf.call(host, 2)").Should().Be(-1);
        engine.Evaluate("Array.prototype.includes.call(host, 2)").Should().BeFalse();
        engine.Evaluate("""
            var visited = 0;
            Array.prototype.forEach.call(host, function () { visited++; });
            visited;
            """).Should().Be(0);
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void EveryIteratingFormYieldsTheRealElements(string kind)
    {
        var engine = CreateEngine(CreateCollection(kind));

        engine.Evaluate("[...host].join('-')").Should().Be("1-2-3");
        engine.Evaluate("Array.from(host).join('-')").Should().Be("1-2-3");
        engine.Evaluate("(function () { var r = []; for (var x of host) { r.push(x); } return r.join('-'); })()")
            .Should().Be("1-2-3");
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void ArrayDestructuringAgreesWithSpread(string kind)
    {
        // Both are GetIterator over the same value (https://tc39.es/ecma262/#sec-runtime-semantics-bindinginitialization),
        // so an index-reading fast path is only allowed to stand in for the iterator where the two agree.
        // Over a collection with no index they do not, and destructuring used to produce holes for a
        // HashSet<T> and an InvalidCastException for the rest.
        var engine = CreateEngine(CreateCollection(kind));

        engine.Evaluate("(function () { var [...r] = host; return r.join('-'); })()").Should().Be("1-2-3");
        engine.Evaluate("(function () { var [first] = host; return first; })()").Should().Be(1);
        engine.Evaluate("(function () { var [, second] = host; return second; })()").Should().Be(2);
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void NoClrExceptionEscapesIntoScript(string kind)
    {
        // The unambiguous half of #3302: whatever the answer is, it must be a JavaScript one. Every mutating
        // generic is in here too, because the faulting lane was reached for writes as well as reads.
        string[] scripts =
        [
            "Array.prototype.join.call(host, '-')",
            "Array.prototype.map.call(host, function (x) { return x; })",
            "Array.prototype.filter.call(host, function () { return true; })",
            "Array.prototype.forEach.call(host, function () { })",
            "Array.prototype.indexOf.call(host, 2)",
            "Array.prototype.lastIndexOf.call(host, 2)",
            "Array.prototype.includes.call(host, 2)",
            "Array.prototype.slice.call(host)",
            "Array.prototype.every.call(host, function () { return true; })",
            "Array.prototype.some.call(host, function () { return true; })",
            "Array.prototype.reduce.call(host, function (a, b) { return a + b; }, 0)",
            "Array.prototype.flat.call(host)",
            "Array.prototype.toString.call(host)",
            "[...host]",
            "Array.from(host)",
            "(function () { var [...r] = host; return r; })()",
            "Math.max.apply(null, host)",
            "JSON.stringify(host)",
            "try { Array.prototype.sort.call(host); } catch (e) { }",
            "try { Array.prototype.reverse.call(host); } catch (e) { }",
            "try { Array.prototype.push.call(host, 4); } catch (e) { }",
            "try { Array.prototype.pop.call(host); } catch (e) { }",
            "try { Array.prototype.fill.call(host, 0); } catch (e) { }",
            "try { Array.prototype.splice.call(host, 0, 1); } catch (e) { }",
        ];

        foreach (var script in scripts)
        {
            var engine = CreateEngine(CreateCollection(kind));
            var run = () => engine.Evaluate(script);
            run.Should().NotThrow($"'{script}' must not leak a CLR exception for a {kind}");
        }
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void AGrowingGenericFailsAsACatchableTypeError(string kind)
    {
        // What still throws, and why that is right: push writes "length", which forwards to a read-only
        // Count. That is an ordinary JavaScript refusal, visible to script and to a host catch.
        var engine = CreateEngine(CreateCollection(kind));

        engine.Evaluate("""
            try {
                Array.prototype.push.call(host, 4);
                'no throw';
            } catch (e) {
                e instanceof TypeError ? 'TypeError' : 'other: ' + e;
            }
            """).Should().Be("TypeError");

        var throwsFromHost = () => engine.Evaluate("Array.prototype.push.call(host, 4)");
        throwsFromHost.Should().Throw<JavaScriptException>();
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void ASortingGenericIsANoOp(string kind)
    {
        // The other half of the write side, and equally specified: sort collects the present elements
        // (none), sorts them, and writes nothing back. The collection is untouched.
        var collection = CreateCollection(kind);
        var engine = CreateEngine(collection);

        engine.Evaluate("Array.prototype.sort.call(host) === host").Should().BeTrue();
        engine.Evaluate("Array.prototype.reverse.call(host) === host").Should().BeTrue();
        engine.Evaluate("[...host].join('-')").Should().Be("1-2-3");
    }

    [Test]
    public void AnIndexerIsWhatDecidesWhetherTheElementsAreFound()
    {
        // The boundary from the other side: the same non-generic ICollection, with an integer indexer added.
        // Nothing about it is an IList, so it takes the very same route — and the generics find every element
        // through the reflected indexer. Before the fix this shape threw too, even though host[0] worked.
        var engine = CreateEngine(new IndexedCountOnlyCollection());

        engine.Evaluate("host.length").Should().Be(3);
        engine.Evaluate("host[0]").Should().Be(1);
        engine.Evaluate("Array.prototype.join.call(host, '-')").Should().Be("1-2-3");
        engine.Evaluate("Array.prototype.indexOf.call(host, 2)").Should().Be(1);
        engine.Evaluate("Array.prototype.filter.call(host, function (x) { return x > 1; }).join('-')").Should().Be("2-3");
        engine.Evaluate("(function () { var [...r] = host; return r.join('-'); })()").Should().Be("1-2-3");
    }

    [Test]
    public void AnIndexableCollectionIsUnchanged()
    {
        // The control: List<T> has an indexer and keeps the indexed lane untouched.
        var engine = CreateEngine(new List<int> { 1, 2, 3 });

        engine.Evaluate("host.length").Should().Be(3);
        engine.Evaluate("host[0]").Should().Be(1);
        engine.Evaluate("'0' in host").Should().BeTrue();
        engine.Evaluate("Array.prototype.join.call(host, '-')").Should().Be("1-2-3");
        engine.Evaluate("Array.prototype.indexOf.call(host, 2)").Should().Be(1);
        engine.Evaluate("Array.prototype.map.call(host, function (x) { return x * 2; }).join('-')").Should().Be("2-4-6");
        engine.Evaluate("(function () { var [...r] = host; return r.join('-'); })()").Should().Be("1-2-3");
    }

    [TestCaseSource(nameof(CountedButNotIndexed))]
    public void TheConversionModesDoNotChangeAnyOfIt(string kind)
    {
        // ArrayConversionMode is about CLR arrays and EnumerableConversionMode about sequences with no count,
        // so neither reaches a counted, non-indexed collection: it stays a live wrapper under all of them.
        foreach (var configure in new Action<Options>[]
                 {
                     o => o.Interop.ArrayConversion = ArrayConversionMode.LiveView,
                     o => o.Interop.EnumerableConversion = EnumerableConversionMode.Snapshot,
                 })
        {
            var engine = CreateEngine(CreateCollection(kind), configure);

            engine.Evaluate("host.length").Should().Be(3);
            engine.Evaluate("Array.prototype.join.call(host, '-')").Should().Be("--");
            engine.Evaluate("[...host].join('-')").Should().Be("1-2-3");
        }
    }

    [Test]
    public void LengthStaysLive()
    {
        var queue = new Queue<int>(new[] { 1, 2, 3 });
        var engine = CreateEngine(queue);

        engine.Evaluate("host.length").Should().Be(3);

        queue.Dequeue();
        engine.Evaluate("host.length").Should().Be(2);
        engine.Evaluate("[...host].join('-')").Should().Be("2-3");

        queue.Clear();
        engine.Evaluate("host.length").Should().Be(0);
        engine.Evaluate("Array.prototype.join.call(host, '-')").Should().Be("");
    }

    /// <summary>
    /// An embedder's own collection: a count and an enumerator, reachable through the non-generic
    /// <see cref="System.Collections.ICollection"/> and nothing else. No indexer of any kind.
    /// </summary>
    private sealed class CountOnlyCollection : ICollection
    {
        private readonly int[] _items = [1, 2, 3];

        public int Count => _items.Length;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public void CopyTo(Array array, int index) => _items.CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }

    /// <summary>
    /// The same shape with an integer indexer, which is not an <see cref="IList"/> either — the members are
    /// simply there to be reflected over.
    /// </summary>
    private sealed class IndexedCountOnlyCollection : ICollection
    {
        private readonly int[] _items = [1, 2, 3];

        public int Count => _items.Length;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public int this[int index] => _items[index];

        public void CopyTo(Array array, int index) => _items.CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}
