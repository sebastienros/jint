#nullable enable

using System.Collections;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the array-like treatment a wrapped CLR collection gets when it implements only the <b>generic</b>
/// collection interfaces — <see cref="ICollection{T}"/> or <see cref="IReadOnlyCollection{T}"/> — and not the
/// non-generic <see cref="ICollection"/>. <see cref="HashSet{T}"/> is the everyday example, and an embedder's
/// own read-only view types usually are too.
///
/// <para>
/// The engine decides this by analysing the wrapped type <em>and every interface it implements</em>, folding
/// the results together, so a type that is a collection only through a generic interface still counts. That
/// fold is what these tests protect: it is easy to "simplify" the analysis back to the non-generic
/// <see cref="ICollection"/> check alone, which would silently drop <c>Array.prototype</c> and <c>length</c>
/// from every <see cref="HashSet{T}"/> an embedder hands to script.
/// </para>
///
/// <para>
/// Array-like treatment means three things, all pinned below: the wrapper's prototype is
/// <c>Array.prototype</c>, <c>length</c> forwards to the live <c>Count</c>, and the object iterates. It does
/// <em>not</em> mean the object is indexable — that needs an integer indexer, which a set does not have — so
/// the <c>Array.prototype</c> methods that address elements by index find nothing. <see cref="List{T}"/> is
/// carried along as the contrast case that does have one.
/// </para>
/// </summary>
public class ArrayLikeCollectionInteropTests
{
    private static Engine CreateEngine(object host, Action<Options>? configure = null)
    {
        var engine = new Engine(options => configure?.Invoke(options));
        engine.SetValue("host", host);
        return engine;
    }

    public static TestCases<string> GenericCollections => new TestCases<string>
    {
        nameof(HashSet<int>),
        nameof(ReadOnlyBag),
        nameof(GenericOnlyCollection),
    };

    private static object CreateGenericCollection(string kind) => kind switch
    {
        nameof(ReadOnlyBag) => new ReadOnlyBag(),
        nameof(GenericOnlyCollection) => new GenericOnlyCollection(),
        _ => new HashSet<int> { 1, 2, 3 },
    };

    [TestCaseSource(nameof(GenericCollections))]
    public void AGenericOnlyCollectionGetsTheArrayPrototype(string kind)
    {
        var engine = CreateEngine(CreateGenericCollection(kind));

        engine.Evaluate("Object.getPrototypeOf(host) === Array.prototype").Should().BeTrue();

        // ...so every Array.prototype member is reachable off it
        engine.Evaluate("typeof host.map").Should().Be("function");
        engine.Evaluate("typeof host.join").Should().Be("function");
        engine.Evaluate("typeof host.forEach").Should().Be("function");

        // but it is still not an Array exotic object, and must not claim to be one
        engine.Evaluate("Array.isArray(host)").Should().BeFalse();
    }

    [TestCaseSource(nameof(GenericCollections))]
    public void AGenericOnlyCollectionExposesItsCountAsLength(string kind)
    {
        var engine = CreateEngine(CreateGenericCollection(kind));

        engine.Evaluate("host.length").Should().Be(3);
    }

    [Test]
    public void LengthFollowsTheLiveCollection()
    {
        // length forwards to Count on every read rather than snapshotting it at wrap time
        var set = new HashSet<int> { 1, 2, 3 };
        var engine = CreateEngine(set);

        engine.Evaluate("host.length").Should().Be(3);

        set.Add(4);
        engine.Evaluate("host.length").Should().Be(4);

        set.Clear();
        engine.Evaluate("host.length").Should().Be(0);
    }

    [TestCaseSource(nameof(GenericCollections))]
    public void AGenericOnlyCollectionIterates(string kind)
    {
        var engine = CreateEngine(CreateGenericCollection(kind));

        var expected = kind switch
        {
            nameof(ReadOnlyBag) => "10|20|30",
            nameof(GenericOnlyCollection) => "a|b|c",
            _ => "1|2|3",
        };

        engine.Evaluate("[...host].join('|')").Should().Be(expected);
        engine.Evaluate("Array.from(host).join('|')").Should().Be(expected);
        engine.Evaluate("(function () { var r = []; for (var x of host) { r.push(x); } return r.join('|'); })()")
            .Should().Be(expected);
    }

    [Test]
    public void LengthDrivenPrototypeMethodsRunButFindNoIndexedElements()
    {
        // The boundary of the treatment, stated explicitly so a change in either direction is visible: a set
        // has no integer indexer, so Array.prototype methods honour its length and then read undefined at
        // every index. Turning this into real elements would need an indexed lane, not a wider IsArrayLike.
        var engine = CreateEngine(new HashSet<int> { 1, 2, 3 });

        engine.Evaluate("host[0]").Should().BeUndefined();
        engine.Evaluate("'0' in host").Should().BeFalse();

        // join walks 0..length-1 and stringifies each absent slot as empty, so the separators alone show the
        // length was honoured
        engine.Evaluate("Array.prototype.join.call(host, '|')").Should().Be("||");

        // forEach skips absent indices entirely, which is the same fact from the other side
        engine.Evaluate("""
            var visited = 0;
            Array.prototype.forEach.call(host, function () { visited++; });
            visited;
            """).Should().Be(0);
    }

    [Test]
    public void AnIndexableCollectionGetsTheElementsToo()
    {
        // The contrast case: List<T> has an integer indexer, so it takes the indexable lane on top of
        // everything above.
        var engine = CreateEngine(new List<int> { 1, 2, 3 });

        engine.Evaluate("Object.getPrototypeOf(host) === Array.prototype").Should().BeTrue();
        engine.Evaluate("host.length").Should().Be(3);
        engine.Evaluate("host[0]").Should().Be(1);
        engine.Evaluate("Array.prototype.join.call(host, '|')").Should().Be("1|2|3");
        engine.Evaluate("Array.prototype.indexOf.call(host, 2)").Should().Be(1);
    }

    [Test]
    public void APlainEnumerableIsNotArrayLike()
    {
        // The other side of the fold: being enumerable is not being a collection. No length, no
        // Array.prototype - but iteration still works, which is what IEnumerable alone buys.
        var engine = CreateEngine(new EnumerableOnly());

        engine.Evaluate("Object.getPrototypeOf(host) === Array.prototype").Should().BeFalse();
        engine.Evaluate("host.length").Should().BeUndefined();
        engine.Evaluate("[...host].join('|')").Should().Be("7|8");
    }

    [Test]
    public void AGenericDictionaryIsNotArrayLike()
    {
        // Dictionary<K,V> implements ICollection<KeyValuePair<K,V>>, so the fold sees a collection - but
        // dictionary-shaped targets are deliberately treated as ordinary objects instead.
        var engine = CreateEngine(new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 });

        engine.Evaluate("Object.getPrototypeOf(host) === Array.prototype").Should().BeFalse();
        engine.Evaluate("host.length").Should().BeUndefined();
        engine.Evaluate("host.a").Should().Be(1);
        engine.Evaluate("JSON.stringify(host)").Should().Be("""{"a":1}""");
    }

    [Test]
    public void AttachArrayPrototypeCanBeTurnedOffWithoutLosingLengthOrIteration()
    {
        var engine = CreateEngine(new HashSet<int> { 1, 2, 3 }, options => options.Interop.AttachArrayPrototype = false);

        engine.Evaluate("Object.getPrototypeOf(host) === Array.prototype").Should().BeFalse();

        // the option only decides the prototype; the collection is still recognized as one
        engine.Evaluate("host.length").Should().Be(3);
        engine.Evaluate("[...host].join('|')").Should().Be("1|2|3");
    }

    /// <summary>
    /// A read-only view over host data: <see cref="IReadOnlyCollection{T}"/> and nothing else. Not even
    /// <see cref="ICollection{T}"/>, so the generic-collection fold is the only thing that can recognize it.
    /// </summary>
    private sealed class ReadOnlyBag : IReadOnlyCollection<int>
    {
        private readonly List<int> _items = new List<int> { 10, 20, 30 };

        public int Count => _items.Count;

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A mutable collection reachable only through <see cref="ICollection{T}"/> — the other half of the fold.
    /// </summary>
    private sealed class GenericOnlyCollection : ICollection<string>
    {
        private readonly List<string> _items = new List<string> { "a", "b", "c" };

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(string item) => _items.Add(item);

        public void Clear() => _items.Clear();

        public bool Contains(string item) => _items.Contains(item);

        public void CopyTo(string[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public bool Remove(string item) => _items.Remove(item);

        public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Enumerable, but not a collection: no <c>Count</c> contract at all.</summary>
    private sealed class EnumerableOnly : IEnumerable<int>
    {
        private readonly List<int> _items = new List<int> { 7, 8 };

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
