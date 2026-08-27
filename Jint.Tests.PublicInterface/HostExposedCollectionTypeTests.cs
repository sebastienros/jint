#nullable enable

using System.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host object exposed to script under a collection <em>interface</em> rather than under its own type.
/// <c>ObjectWrapper.Create(engine, target, typeof(IList&lt;int&gt;))</c> is public API and is what a
/// <see cref="Options.WrapObjectDelegate"/> writes, so everything below runs on every target framework and
/// every leg — under a plain JIT, with no trimming and no Native AOT anywhere near it.
///
/// <para>
/// The exposure matters because it decides which wrapper is built.
/// <c>ResolveArrayLikeWrapperFactoryType</c> looks for <c>IList&lt;&gt;</c> among the exposed type's
/// <c>GetInterfaces()</c>, and an interface is not among its own — <c>typeof(IList&lt;int&gt;)</c> yields
/// <c>ICollection&lt;int&gt;</c>, <c>IEnumerable&lt;int&gt;</c> and <c>IEnumerable</c>. So no typed wrapper
/// is built; the target is not a non-generic <see cref="IList"/> either, so there is no
/// <c>ListWrapper</c> to fall back to; and the result is a plain <see cref="ObjectWrapper"/> whose type
/// descriptor still reports an integer index, which is exactly what
/// <c>ArrayOperations.IndexWrappedOperations</c> serves.
/// </para>
///
/// <para>
/// That lane was believed to be reachable only under Native AOT, where a value-type generic instantiation
/// cannot be built (#3362, #3393). It is not, and the two bugs #3381 repaired on it — the reflection
/// fallback handing <c>PropertyInfo.GetValue</c> the wrapper instead of the CLR target, and a
/// <c>SetLength</c> that threw a bare <see cref="NotSupportedException"/> where a read-only <c>length</c>
/// owes script a <c>TypeError</c> — were reachable by an embedder on any runtime. This file is the
/// coverage that says so (#3394).
/// </para>
/// </summary>
public class HostExposedCollectionTypeTests
{
    /// <summary>
    /// The exposure produces a plain wrapper, which is what routes the collection through the lane. Asserted
    /// rather than assumed: if a future change gives this exposure a typed wrapper, every read below still
    /// answers correctly from a different lane and this file would pin nothing.
    /// </summary>
    [Test]
    public void ExposingAHostCollectionAsAGenericInterfaceProducesAPlainWrapper()
    {
        var engine = new Engine(options => options.AllowClr());

        var wrapper = ObjectWrapper.Create(engine, new IndexedItems(1, 2, 3), typeof(IList<int>));

        wrapper.GetType().Should().Be<ObjectWrapper>(
            "the typed wrapper is not built for an exposed interface, and the target is not a non-generic IList either");
    }

    /// <summary>
    /// The read repair. Every element an <c>Array.prototype</c> generic sees comes back through the lane's
    /// reflection fallback — <c>PropertyInfo.GetValue</c> over <c>IList&lt;int&gt;.Item</c> — and the
    /// receiver has to be the CLR collection. Passing the wrapper was a <see cref="TargetException"/>
    /// waiting for its first caller.
    /// </summary>
    [TestCase("Array.prototype.join.call(host, '-')", "1-2-3")]
    [TestCase("Array.prototype.indexOf.call(host, 3)", 2d)]
    [TestCase("Array.prototype.filter.call(host, function (x) { return x > 1; }).join('-')", "2-3")]
    [TestCase("Array.prototype.map.call(host, function (x) { return x * 2; }).join('-')", "2-4-6")]
    [TestCase("Array.prototype.slice.call(host, 1).join('-')", "2-3")]
    [TestCase("host.length", 3d)]
    public void ArrayGenericsReadTheExposedCollectionThroughItsClrTarget(string script, object expected)
    {
        var engine = CreateEngine(new IndexedItems(1, 2, 3));

        engine.Evaluate(script).ToObject().Should().Be(expected);
    }

    /// <summary>
    /// The contrast that explains why the receiver bug survived: <c>host[1]</c> resolves the type's own
    /// indexer through the ordinary member lane and never touches <c>IndexWrappedOperations</c>.
    /// </summary>
    [Test]
    public void AnElementReadDoesNotUseTheSameLaneAsTheGenerics()
    {
        var engine = CreateEngine(new IndexedItems(1, 2, 3));

        engine.Evaluate("host[1]").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The write repair. <c>splice(0, 0)</c> is the shortest generic that reaches <c>SetLength</c> and
    /// nothing else: a delete count of zero with no items performs no element write and no delete, just
    /// <c>Set(O, "length", len, true)</c>. The wrapper has no <see cref="IList"/> to resize, so that write
    /// meets its read-only <c>length</c> and owes script a <c>TypeError</c> — where a bare
    /// <see cref="NotSupportedException"/> is invisible to a script <c>try</c>/<c>catch</c> and to a host
    /// <c>catch (JavaScriptException)</c> alike.
    /// </summary>
    [TestCase("Array.prototype.splice.call(host, 0, 0)")]
    [TestCase("Array.prototype.push.call(host, 4)")]
    public void AGenericThatWouldResizeTheExposedCollectionThrowsACatchableTypeError(string script)
    {
        var items = new IndexedItems(1, 2, 3);
        var engine = CreateEngine(items);

        var thrown = Caught.Exception(() => engine.Execute(script));

        thrown.Should().BeOfType<JavaScriptException>();
        ((JavaScriptException) thrown!).Error.Get("name").AsString().Should().Be("TypeError");
        items.Should().Equal(1, 2, 3);
    }

    [TestCase("Array.prototype.pop.call(host)")]
    [TestCase("Array.prototype.shift.call(host)")]
    [TestCase("Array.prototype.unshift.call(host, 0)")]
    [TestCase("Array.prototype.fill.call(host, 9)")]
    [TestCase("Array.prototype.reverse.call(host)")]
    [TestCase("Array.prototype.sort.call(host, function (a, b) { return b - a; })")]
    [TestCase("Array.prototype.splice.call(host, 0, 1)")]
    [TestCase("Array.from(host).join('-')")]
    [TestCase("[...host].join('-')")]
    [TestCase("host.length = 5")]
    [TestCase("delete host[3]")]
    [TestCase("JSON.stringify(host)")]
    public void NoOperationOnAnExposedCollectionLetsAClrExceptionPastScript(string script)
    {
        // A bare `host[3] = 9` is deliberately absent: it is not this lane but the plain wrapper's own
        // reflected-indexer write, which still hands an out-of-range index to the collection. Fixing that
        // is a decision about every indexed CLR type rather than about array-likes, so it is filed rather
        // than widened into this change.
        var engine = CreateEngine(new IndexedItems(1, 2, 3));

        var thrown = Caught.Exception(() => engine.Execute(script));

        thrown.Should().Match(e => e == null || e is JavaScriptException,
            "{0} must answer script, not the CLR", script);
    }

    /// <summary>
    /// The same exposure through the hook an embedder actually configures, rather than through a hand-made
    /// wrapper: <see cref="Options.WrapObjectDelegate"/> receives the target and returns the wrapper, and
    /// narrowing what script sees is the reason the hook exists.
    /// </summary>
    [Test]
    public void AWrapObjectHandlerCanExposeAHostCollectionAsAGenericInterface()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.WrapObjectHandler = static (e, target, _) => target is IndexedItems
                ? ObjectWrapper.Create(e, target, typeof(IList<int>))
                : ObjectWrapper.Create(e, target);
        });
        engine.SetValue("host", new IndexedItems(1, 2, 3));

        engine.Evaluate("Array.prototype.join.call(host, '-')").AsString().Should().Be("1-2-3");
        Caught.Exception(() => engine.Execute("Array.prototype.splice.call(host, 0, 0)"))
            .Should().BeOfType<JavaScriptException>();
    }

    private static Engine CreateEngine(IndexedItems items)
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("host", ObjectWrapper.Create(engine, items, typeof(IList<int>)));
        return engine;
    }

    /// <summary>
    /// A host collection that implements <see cref="IList{T}"/> and the non-generic <see cref="ICollection"/>
    /// but <em>not</em> the non-generic <see cref="IList"/>. Every part of that is load-bearing: the generic
    /// interface is what gives the type an index, the non-generic <see cref="ICollection"/> is where
    /// <c>ArrayOperations</c> reads the count, and the absence of the non-generic <see cref="IList"/> is what
    /// leaves the wrapper with nothing to resize and nothing to read elements through but reflection.
    /// </summary>
    private sealed class IndexedItems : IList<int>, ICollection, IEnumerable<int>
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
}
