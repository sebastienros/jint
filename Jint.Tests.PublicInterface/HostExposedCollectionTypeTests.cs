#nullable enable

using System.Collections;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host object exposed to script under a collection <em>contract</em> rather than under its own type.
/// <c>ObjectWrapper.Create(engine, target, typeof(IList&lt;int&gt;))</c> is public API, is what a
/// <see cref="Options.WrapObjectDelegate"/> writes, and is what the member lane hands over for a property
/// whose <em>declared</em> type is a collection interface — so everything below runs on every target
/// framework and every leg, under a plain JIT with no trimming and no Native AOT anywhere near it.
/// </summary>
/// <remarks>
/// <para>
/// The exposure decides which wrapper is built, and the wrapper is what decides what script may do: whether
/// elements can be written, whether the collection can grow, and which lane <c>Array.prototype</c> takes.
/// <c>ResolveArrayLikeWrapperFactoryType</c> looked for <c>IList&lt;&gt;</c> and <c>IReadOnlyList&lt;&gt;</c>
/// among the exposed type's <c>GetInterfaces()</c> and nowhere else. An interface is not among its own —
/// <c>typeof(IList&lt;int&gt;)</c> yields <c>ICollection&lt;int&gt;</c>, <c>IEnumerable&lt;int&gt;</c> and
/// <c>IEnumerable</c> — so exposing a collection <em>as</em> one of the two contracts the method is written to
/// recognize found nothing, and the engine fell back to a wrapper the exposure had not named: a plain
/// <see cref="ObjectWrapper"/> when the target is not a non-generic <see cref="IList"/>, and a
/// target-writable <c>ListWrapper</c> when it is (#3421).
/// </para>
/// </remarks>
public class HostExposedCollectionTypeTests
{
    /// <summary>
    /// Which wrapper each exposure produces. Asserted by name because the wrapper types are internal, and
    /// asserted at all because it is the fact every behaviour below follows from: a matrix that only checked
    /// answers would go on passing while the engine quietly served them from the wrong view.
    /// </summary>
    [TestCase("IndexedItems", "IList<int>", "GenericListWrapper`1")]
    [TestCase("IndexedItems", null, "GenericListWrapper`1")]
    [TestCase("ReadOnlyItems", "IReadOnlyList<int>", "ReadOnlyListWrapper`1")]
    [TestCase("ReadOnlyItems", null, "ReadOnlyListWrapper`1")]
    [TestCase("List<int>", "IList<int>", "GenericListWrapper`1")]
    [TestCase("List<int>", "IReadOnlyList<int>", "ReadOnlyListWrapper`1")]
    [TestCase("int[]", "IReadOnlyList<int>", "ReadOnlyListWrapper`1")]
    [TestCase("int[]", "IList<int>", "GenericListWrapper`1")]
    [TestCase("int[]", null, "ArrayWrapper`1")]
    public void TheExposedContractDecidesTheWrapper(string target, string? exposedAs, string expected)
    {
        var engine = new Engine(options => options.AllowClr());

        var wrapper = ObjectWrapper.Create(engine, Target(target), ExposedType(exposedAs));

        wrapper.GetType().Name.Should().Be(expected,
            "{0} exposed as {1} names that contract, and the wrapper is what honours it", target, exposedAs ?? "its own type");
    }

    /// <summary>
    /// A contract with no index in it names no view: <see cref="ICollection{T}"/> and
    /// <see cref="IEnumerable{T}"/> are count-and-enumerate, so a target reachable only through them keeps the
    /// plain wrapper. The check considers the exposed type itself, not every generic interface it can see.
    /// </summary>
    [TestCase("ICollection<int>")]
    [TestCase("IEnumerable<int>")]
    public void AContractWithNoIndexProducesNoView(string exposedAs)
    {
        var engine = new Engine(options => options.AllowClr());

        var wrapper = ObjectWrapper.Create(engine, new IndexedItems(1, 2, 3), ExposedType(exposedAs));

        wrapper.GetType().Should().Be<ObjectWrapper>();
    }

    /// <summary>
    /// A host may hand <see cref="ObjectWrapper.Create(Engine, object, Type?)"/> a type its target does not
    /// implement. Every typed wrapper casts the target to the contract the exposure named, and until the
    /// exposed type itself was consulted such a factory could only be found by scanning the target's own
    /// interfaces — so the cast could not fail. It keeps the answer it has always had rather than becoming an
    /// <see cref="InvalidCastException"/> out of a wrapper creation.
    /// </summary>
    [Test]
    public void AnExposedTypeTheTargetDoesNotImplementIsNotCastTo()
    {
        var engine = new Engine(options => options.AllowClr());

        var wrapper = ObjectWrapper.Create(engine, new IndexedItems(1, 2, 3), typeof(IReadOnlyList<int>));

        wrapper.GetType().Should().Be<ObjectWrapper>("IndexedItems is not an IReadOnlyList<int>");
    }

    /// <summary>
    /// Every read an <c>Array.prototype</c> generic makes over the exposed collection, plus the element read
    /// and the two iteration forms. These answered correctly before too — through the plain wrapper's
    /// reflection lane — so they are the half that must not change.
    /// </summary>
    [TestCase("Array.prototype.join.call(host, '-')", "1-2-3")]
    [TestCase("Array.prototype.indexOf.call(host, 3)", 2d)]
    [TestCase("Array.prototype.filter.call(host, function (x) { return x > 1; }).join('-')", "2-3")]
    [TestCase("Array.prototype.map.call(host, function (x) { return x * 2; }).join('-')", "2-4-6")]
    [TestCase("Array.prototype.slice.call(host, 1).join('-')", "2-3")]
    [TestCase("Array.from(host).join('-')", "1-2-3")]
    [TestCase("[...host].join('-')", "1-2-3")]
    [TestCase("host.length", 3d)]
    [TestCase("host[1]", 2d)]
    [TestCase("JSON.stringify(host)", "[1,2,3]")]
    public void ReadsThroughAnExposedContractAreUnchanged(string script, object expected)
    {
        var engine = CreateEngine("IndexedItems", "IList<int>", out _);

        engine.Evaluate(script).ToObject().Should().Be(expected);
    }

    /// <summary>
    /// The element keys of the view now enumerate as an array's do. Through the plain wrapper the same object
    /// reported its reflected CLR member names instead — <c>IndexOf</c>, <c>Insert</c>, <c>RemoveAt</c> — for
    /// a target script sees as a list.
    /// </summary>
    [Test]
    public void AnExposedCollectionEnumeratesItsPositions()
    {
        var engine = CreateEngine("IndexedItems", "IList<int>", out _);

        engine.Evaluate("JSON.stringify(Object.keys(host))").AsString().Should().Be("[\"0\",\"1\",\"2\"]");
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyNames(host))").AsString().Should().Be("[\"0\",\"1\",\"2\"]");
    }

    /// <summary>
    /// <see cref="IList{T}"/> declares a settable indexer, <c>Add</c> and <c>RemoveAt</c>, so a collection
    /// exposed under it is writable and growable — which is what the typed wrapper the contract names gives
    /// it, and what the plain wrapper could not.
    /// </summary>
    [Test]
    public void AWritableExposedContractIsWritableAndGrowable()
    {
        var engine = CreateEngine("IndexedItems", "IList<int>", out var items);

        engine.Execute("host[0] = 9");
        items.Should().Equal(new[] { 9, 2, 3 }, "IList<int> has a settable indexer");

        engine.Evaluate("Array.prototype.push.call(host, 4)").AsNumber().Should().Be(4);
        items.Should().Equal(new[] { 9, 2, 3, 4 }, "IList<int> has Add");

        engine.Evaluate("Array.prototype.pop.call(host)").AsNumber().Should().Be(4);
        items.Should().Equal(new[] { 9, 2, 3 }, "IList<int> has RemoveAt");

        engine.Execute("host.length = 5");
        items.Should().Equal(new[] { 9, 2, 3, 0, 0 });
    }

    /// <summary>
    /// The other half. <see cref="IReadOnlyList{T}"/> declares a get-only indexer and no mutator at all, so a
    /// <see cref="List{T}"/> handed to script under it must refuse every mutation — and refuse it as a
    /// JavaScript error, not as a CLR exception. #3384 gave the refusal to a <c>ListWrapper</c> that took its
    /// writability from the target; the exposure now gets the wrapper that says so from its type argument.
    /// </summary>
    [TestCase("Array.prototype.push.call(host, 4)")]
    [TestCase("Array.prototype.pop.call(host)")]
    [TestCase("Array.prototype.reverse.call(host)")]
    [TestCase("Array.prototype.splice.call(host, 0, 0)")]
    [TestCase("host.length = 5")]
    public void AReadOnlyExposedContractRefusesMutationWithAJavaScriptError(string script)
    {
        var engine = CreateEngine("List<int>", "IReadOnlyList<int>", out var items);

        Caught.Exception(() => engine.Execute(script)).Should().Match(e => e == null || e is JavaScriptException,
            "{0} must answer script, not the CLR", script);
        items.Should().Equal(new[] { 1, 2, 3 }, "the collection is untouched");
    }

    [Test]
    public void AReadOnlyExposedContractStillReads()
    {
        var engine = CreateEngine("List<int>", "IReadOnlyList<int>", out _);

        engine.Evaluate("host.length").AsNumber().Should().Be(3);
        engine.Evaluate("host[1]").AsNumber().Should().Be(2);
        engine.Evaluate("Array.prototype.join.call(host, '-')").AsString().Should().Be("1-2-3");
        engine.Evaluate("host[3]").IsUndefined().Should().BeTrue();

        engine.Execute("host[0] = 9");
        engine.Evaluate("host[0]").AsNumber().Should().Be(1, "an element write through a read-only contract is refused");
    }

    /// <summary>
    /// Whatever the exposure names, nothing script can do to the collection leaves a CLR exception to the
    /// embedder. The two rows are the two contracts that name a view — a growable one and a read-only one —
    /// so the same sixteen operations are answered from opposite ends of what the exposure permits.
    /// </summary>
    [TestCase("IndexedItems", "IList<int>")]
    [TestCase("List<int>", "IReadOnlyList<int>")]
    public void NoOperationOnAnExposedCollectionLetsAClrExceptionPastScript(string target, string exposedAs)
    {
        foreach (var script in new[]
                 {
                     "host[3] = 9",
                     "host['3'] = 9",
                     "host[-1] = 9",
                     "Array.prototype.pop.call(host)",
                     "Array.prototype.shift.call(host)",
                     "Array.prototype.unshift.call(host, 0)",
                     "Array.prototype.fill.call(host, 9)",
                     "Array.prototype.reverse.call(host)",
                     "Array.prototype.sort.call(host, function (a, b) { return b - a; })",
                     "Array.prototype.splice.call(host, 0, 1)",
                     "Array.prototype.push.call(host, 4)",
                     "Array.from(host).join('-')",
                     "[...host].join('-')",
                     "host.length = 5",
                     "delete host[3]",
                     "JSON.stringify(host)",
                 })
        {
            var engine = CreateEngine(target, exposedAs, out _);

            Caught.Exception(() => engine.Execute(script)).Should().Match(e => e == null || e is JavaScriptException,
                "{0} on a collection exposed as {1} must answer script, not the CLR", script, exposedAs);
        }
    }

    /// <summary>
    /// The same exposure through the hook an embedder actually configures rather than through a hand-made
    /// wrapper: <see cref="Options.WrapObjectDelegate"/> receives the target and returns the wrapper, and
    /// narrowing what script sees is the reason the hook exists.
    /// </summary>
    [Test]
    public void AWrapObjectHandlerCanExposeAHostCollectionAsAGenericInterface()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
            options.Interop.WrapObjectHandler = static (e, target, _) => target is List<int>
                ? ObjectWrapper.Create(e, target, typeof(IReadOnlyList<int>))
                : ObjectWrapper.Create(e, target);
        });
        engine.SetValue("host", new List<int> { 1, 2, 3 });

        engine.Evaluate("Array.prototype.join.call(host, '-')").AsString().Should().Be("1-2-3");
        Caught.Exception(() => engine.Execute("Array.prototype.push.call(host, 4)"))
            .Should().BeOfType<JavaScriptException>("the handler narrowed the exposure to a read-only contract");
    }

    private static Engine CreateEngine(string target, string? exposedAs, out IEnumerable<int> host)
    {
        var instance = (IEnumerable<int>) Target(target);
        host = instance;
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("host", ObjectWrapper.Create(engine, instance, ExposedType(exposedAs)));
        return engine;
    }

    private static object Target(string target) => target switch
    {
        "IndexedItems" => new IndexedItems(1, 2, 3),
        "ReadOnlyItems" => new ReadOnlyItems(1, 2, 3),
        "List<int>" => new List<int> { 1, 2, 3 },
        "int[]" => new[] { 1, 2, 3 },
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "unknown target"),
    };

    private static Type? ExposedType(string? exposedAs) => exposedAs switch
    {
        null => null,
        "IList<int>" => typeof(IList<int>),
        "IReadOnlyList<int>" => typeof(IReadOnlyList<int>),
        "ICollection<int>" => typeof(ICollection<int>),
        "IEnumerable<int>" => typeof(IEnumerable<int>),
        _ => throw new ArgumentOutOfRangeException(nameof(exposedAs), exposedAs, "unknown exposure"),
    };

    /// <summary>
    /// A host collection that implements <see cref="IList{T}"/> and the non-generic <see cref="ICollection"/>
    /// but <em>not</em> the non-generic <see cref="IList"/>. Every part of that is load-bearing: the generic
    /// interface is what gives the type an index, the non-generic <see cref="ICollection"/> is where
    /// <c>ArrayOperations</c> reads the count, and the absence of the non-generic <see cref="IList"/> is what
    /// left the exposure with no <c>ListWrapper</c> to fall back to and therefore no view at all.
    /// </summary>
    private sealed class IndexedItems : IList<int>, ICollection
    {
        private readonly List<int> _items;

        public IndexedItems(params int[] items) => _items = [.. items];

        public int this[int index] { get => _items[index]; set => _items[index] = value; }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(int item) => _items.Add(item);

        public void Clear() => _items.Clear();

        public bool Contains(int item) => _items.Contains(item);

        public void CopyTo(int[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public int IndexOf(int item) => _items.IndexOf(item);

        public void Insert(int index, int item) => _items.Insert(index, item);

        public bool Remove(int item) => _items.Remove(item);

        public void RemoveAt(int index) => _items.RemoveAt(index);

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        void ICollection.CopyTo(Array array, int index) => ((ICollection) _items).CopyTo(array, index);
    }

    /// <summary>The read-only sibling, reachable only through <see cref="IReadOnlyList{T}"/>.</summary>
    private sealed class ReadOnlyItems : IReadOnlyList<int>
    {
        private readonly List<int> _items;

        public ReadOnlyItems(params int[] items) => _items = [.. items];

        public int this[int index] => _items[index];

        public int Count => _items.Count;

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
}
