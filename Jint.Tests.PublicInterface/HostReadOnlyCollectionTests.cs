#nullable enable

using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what a wrapped CLR collection that declares itself read-only owes script when it is asked to
/// change: a JavaScript error the script can catch, never the CLR's own
/// <see cref="NotSupportedException"/> escaping <c>Evaluate</c>.
///
/// <para>
/// Which JavaScript answer depends on the operation, not on the collection. <c>push</c>, <c>pop</c>,
/// <c>splice</c>, <c>sort</c> and <c>reverse</c> are specified in terms of <c>Set(O, k, v, true)</c> and
/// <c>DeletePropertyOrThrow</c>, so a refusal is a <c>TypeError</c> in either mode. A bare assignment —
/// <c>host[0] = 9</c>, <c>host.length = 5</c>, <c>delete host[0]</c> — is an ordinary <c>[[Set]]</c> or
/// <c>[[Delete]]</c> returning <see langword="false"/>, which is a <c>TypeError</c> in strict mode and
/// silent in sloppy mode. Both halves are asserted below, because "make everything throw" would be wrong
/// in the second one.
/// </para>
///
/// <para>
/// The contrast case is a collection that is fixed-size rather than read-only, which refuses the same
/// length changes and still accepts element writes. <see cref="ArraySegment{T}"/> is the shape that makes
/// the distinction load-bearing: it reports <see cref="ICollection{T}.IsReadOnly"/> as
/// <see langword="true"/> meaning only that it cannot grow, exactly as <c>T[]</c> does.
/// </para>
/// </summary>
public class HostReadOnlyCollectionTests
{
    private static Engine CreateEngine(object host, bool strict = false)
    {
        var engine = new Engine(options =>
        {
            options.Strict = strict;
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("host", host);
        return engine;
    }

    /// <summary>
    /// Every shape an embedder reaches for when it wants to hand script a collection it may read but not
    /// change. They resolve to two different wrappers — <c>ReadOnlyCollection&lt;T&gt;</c> and a host
    /// <see cref="IList{T}"/> to the generic list view, the rest to the read-only list view — and before
    /// #3382 the two disagreed about which of them leaked which CLR exception.
    /// </summary>
    private static readonly string[] _readOnlyShapes =
    [
        nameof(ReadOnlyCollection<int>),
        nameof(ImmutableList),
        nameof(ImmutableArray),
        nameof(HostReadOnlyList),
        nameof(HostReadOnlySequence),
        nameof(ArrayList),
    ];

    public static TestCases<string> ReadOnlyShapes
    {
        get
        {
            var data = new TestCases<string>();
            foreach (var shape in _readOnlyShapes)
            {
                data.Add(shape);
            }

            return data;
        }
    }

    private static (object Host, Func<IReadOnlyList<int>> Read) CreateReadOnlyShape(string kind)
    {
        switch (kind)
        {
            case nameof(ReadOnlyCollection<int>):
            {
                var items = new List<int> { 1, 2, 3 };
                return (new ReadOnlyCollection<int>(items), () => items);
            }
            case nameof(ImmutableList):
            {
                var items = ImmutableList.Create(1, 2, 3);
                return (items, () => items);
            }
            case nameof(ImmutableArray):
            {
                var items = ImmutableArray.Create(1, 2, 3);
                return (items, () => items);
            }
            case nameof(HostReadOnlyList):
            {
                var items = new List<int> { 1, 2, 3 };
                return (new HostReadOnlyList(items), () => items);
            }
            case nameof(HostReadOnlySequence):
            {
                var items = new List<int> { 1, 2, 3 };
                return (new HostReadOnlySequence(items), () => items);
            }
            case nameof(ArrayList):
            {
                var items = ArrayList.ReadOnly(new ArrayList { 1, 2, 3 });
                return (items, () => items.Cast<int>().ToList());
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown shape");
        }
    }

    /// <summary>
    /// The five <c>Array.prototype</c> generics that change a length. <c>sort</c> and <c>reverse</c> are
    /// spelled through <c>Array.prototype</c> deliberately: several of these shapes carry a CLR
    /// <c>Sort</c>/<c>Reverse</c> member of their own, and a wrapped member wins over the prototype, so
    /// <c>host.sort(…)</c> would measure member resolution rather than the refusal.
    /// </summary>
    public static TestCases<string, string> ThrowingOperations
    {
        get
        {
            var data = new TestCases<string, string>();
            foreach (var shape in _readOnlyShapes)
            {
                data.Add(shape, "host.push(4)");
                data.Add(shape, "host.pop()");
                data.Add(shape, "host.splice(0, 1)");
                data.Add(shape, "Array.prototype.sort.call(host, function (a, b) { return b - a; })");
                data.Add(shape, "Array.prototype.reverse.call(host)");
            }

            return data;
        }
    }

    /// <summary>
    /// The assignments, which the specification refuses differently: silently in sloppy mode.
    /// </summary>
    public static TestCases<string, string> AssigningOperations
    {
        get
        {
            var data = new TestCases<string, string>();
            foreach (var shape in _readOnlyShapes)
            {
                data.Add(shape, "host[0] = 9");
                data.Add(shape, "host[3] = 9");
                data.Add(shape, "host.length = 5");
                data.Add(shape, "host.length = 1");
                data.Add(shape, "delete host[0]");
            }

            return data;
        }
    }

    [TestCaseSource(nameof(ThrowingOperations))]
    public void AnArrayGenericThatMustGrowAReadOnlyCollectionThrowsACatchableTypeError(string shape, string operation)
    {
        var (host, read) = CreateReadOnlyShape(shape);

        foreach (var strict in new[] { false, true })
        {
            var engine = CreateEngine(host, strict);

            // the catch is script-side on purpose: a CLR NotSupportedException escaping Evaluate is
            // invisible to it, which is the whole defect (#3382)
            var outcome = engine.Evaluate(Probe(operation, strict)).AsString();

            outcome.Should().Be("TypeError", "{0} on a read-only {1} is Set(O, k, v, true)", operation, shape);
            read().Should().Equal(1, 2, 3);
        }
    }

    [TestCaseSource(nameof(AssigningOperations))]
    public void AnAssignmentToAReadOnlyCollectionIsRefusedSilentlyInSloppyModeAndThrowsInStrictMode(string shape, string operation)
    {
        var (host, read) = CreateReadOnlyShape(shape);

        CreateEngine(host).Evaluate(Probe(operation, strict: false)).AsString()
            .Should().Be("no-throw", "a failed [[Set]] is silent outside strict mode");
        read().Should().Equal(1, 2, 3);

        CreateEngine(host, strict: true).Evaluate(Probe(operation, strict: true)).AsString()
            .Should().Be("TypeError", "a failed [[Set]] is a TypeError in strict mode");
        read().Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// The refusal an embedder actually reads is the ordinary one a frozen JavaScript array gives — the
    /// element write is what fails, and it names the property. Nothing here invents an interop-specific
    /// message, which is the point: the refusal happens in <c>[[Set]]</c>, before the collection is
    /// reached at all.
    /// </summary>
    [TestCaseSource(nameof(ReadOnlyShapes))]
    public void TheRefusalIsTheOrdinaryReadOnlyPropertyTypeError(string shape)
    {
        var (host, _) = CreateReadOnlyShape(shape);
        var engine = CreateEngine(host);

        var message = engine.Evaluate("(function () { try { host.push(4); return ''; } catch (e) { return e.message; } })()").AsString();

        message.Should().Contain("read only property '3'");
    }

    [Test]
    public void AGrowableListStillGrows()
    {
        // the control: nothing above may be bought by refusing writes that were always legitimate
        var items = new List<int> { 1, 2, 3 };
        var engine = CreateEngine(items);

        engine.Execute("host.push(4); host[0] = 9; host.length = 6;");

        items.Should().Equal(9, 2, 3, 4, 0, 0);
    }

    /// <summary>
    /// <see cref="ArraySegment{T}"/> reports <see cref="ICollection{T}.IsReadOnly"/> as
    /// <see langword="true"/> to mean it cannot grow, the same lie <c>T[]</c> tells through the same
    /// interface, so it must be treated as fixed-size and keep its element writes.
    /// </summary>
    [Test]
    public void AnArraySegmentIsFixedSizeRatherThanReadOnly()
    {
        var array = new[] { 1, 2, 3 };
        var engine = CreateEngine(new ArraySegment<int>(array));

        engine.Evaluate(Probe("host.push(4)", strict: false)).AsString().Should().Be("TypeError");
        engine.Evaluate(Probe("host[0] = 9", strict: false)).AsString().Should().Be("no-throw");

        array.Should().Equal(9, 2, 3);
    }

    private static string Probe(string operation, bool strict)
    {
        var prologue = strict ? "'use strict';\n" : "";
        return prologue
            + "(function () { try { " + operation + "; return 'no-throw'; } "
            + "catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";
    }

    /// <summary>
    /// A host list that declares itself read-only through <see cref="ICollection{T}.IsReadOnly"/> and
    /// throws from every mutator, which is what the interface's contract asks of an implementer.
    /// </summary>
    private sealed class HostReadOnlyList : IList<int>
    {
        private readonly List<int> _items;

        public HostReadOnlyList(List<int> items) => _items = items;

        public int Count => _items.Count;

        public bool IsReadOnly => true;

        public int this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException("Collection is read-only.");
        }

        public void Add(int item) => throw new NotSupportedException("Collection is read-only.");

        public void Clear() => throw new NotSupportedException("Collection is read-only.");

        public bool Contains(int item) => _items.Contains(item);

        public void CopyTo(int[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(int item) => _items.IndexOf(item);

        public void Insert(int index, int item) => throw new NotSupportedException("Collection is read-only.");

        public bool Remove(int item) => throw new NotSupportedException("Collection is read-only.");

        public void RemoveAt(int index) => throw new NotSupportedException("Collection is read-only.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// The other half of the read-only surface: a type that offers no write at all, so the wrapper's
    /// refusal cannot come from the target and has to come from the engine.
    /// </summary>
    private sealed class HostReadOnlySequence : IReadOnlyList<int>
    {
        private readonly List<int> _items;

        public HostReadOnlySequence(List<int> items) => _items = items;

        public int Count => _items.Count;

        public int this[int index] => _items[index];

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
