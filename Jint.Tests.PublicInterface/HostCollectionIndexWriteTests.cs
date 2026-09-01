#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what an index names on a wrapped CLR collection: the same position whichever way script spelled it,
/// and never an index the collection is left to reject.
///
/// <para>
/// <c>x[3]</c> and <c>x["3"]</c> are one property key — <c>ToPropertyKey</c> makes both the String
/// <c>"3"</c> — so they must be one answer. Until #3384 they were two: a number key was answered by the
/// array-like view, and a string key by the reflected indexer, which took whatever index it parsed out of
/// the key straight to the collection and let its <see cref="ArgumentOutOfRangeException"/> past script.
/// </para>
///
/// <para>
/// The growable half is the other question the issue asks. A wrapped <see cref="List{T}"/> answers
/// <c>x.length = 5</c> by growing, so <c>x[3] = 9</c> — the same request, spelled the way a script author
/// spells it — grows too: the view is an extensible ordinary object and the position can exist, so
/// <c>CreateDataProperty</c> succeeds. A fixed-size or read-only target keeps the refusal #3381 and #3385
/// gave it.
/// </para>
/// </summary>
public class HostCollectionIndexWriteTests
{
    private static readonly string[] _growableShapes =
    [
        nameof(List<int>),
        "ListOfString",
        nameof(Collection<int>),
        nameof(HostList),
        nameof(ArrayList),
        nameof(HostUntypedList),
    ];

    public static TheoryData<string> GrowableShapes => new(_growableShapes);

    /// <summary>
    /// Both spellings of every index-shaped key, so that a row cannot pass because the test happened to
    /// write the one the engine already answered.
    /// </summary>
    public static TheoryData<string, string> GrowableShapesAndIndexSpellings
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var shape in _growableShapes)
            {
                data.Add(shape, "3");
                data.Add(shape, "'3'");
            }

            return data;
        }
    }

    private static (object Host, Func<IReadOnlyList<string>> Read) CreateGrowableShape(string kind)
    {
        switch (kind)
        {
            case nameof(List<int>):
            {
                var items = new List<int> { 1, 2, 3 };
                return (items, () => Render(items));
            }
            case "ListOfString":
            {
                // a reference item type, whose grown slots are null rather than a zero
                var items = new List<string?> { "1", "2", "3" };
                return (items, () => Render(items));
            }
            case nameof(Collection<int>):
            {
                var items = new Collection<int> { 1, 2, 3 };
                return (items, () => Render(items));
            }
            case nameof(HostList):
            {
                var items = new List<int> { 1, 2, 3 };
                return (new HostList(items), () => Render(items));
            }
            case nameof(ArrayList):
            {
                var items = new ArrayList { 1, 2, 3 };
                return (items, () => Render(items.Cast<object?>()));
            }
            case nameof(HostUntypedList):
            {
                var items = new List<int> { 1, 2, 3 };
                return (new HostUntypedList(items), () => Render(items));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown shape");
        }
    }

    private static IReadOnlyList<string> Render<T>(IEnumerable<T> items)
        => items.Select(i => i?.ToString() ?? "null").ToList();

    private static Engine CreateEngine(object host, bool strict = false)
    {
        var engine = new Engine(options =>
        {
            options.Strict = strict;
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("x", host);
        return engine;
    }

    /// <summary>
    /// The reported defect: an index write at the end of a growable list raised the CLR's own
    /// <see cref="ArgumentOutOfRangeException"/> out of <c>Evaluate</c>, where a <c>length</c> write of the
    /// same size grew the list.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrowableShapesAndIndexSpellings))]
    public void AnIndexWriteAtTheEndOfAGrowableCollectionAppends(string shape, string index)
    {
        var (host, read) = CreateGrowableShape(shape);
        var engine = CreateEngine(host);

        engine.Execute("x[" + index + "] = 9");

        read().Should().Equal("1", "2", "3", "9");
        engine.Evaluate("x.length").AsNumber().Should().Be(4);
    }

    /// <summary>
    /// An index past the end makes room the way the <c>length</c> write does, so the two agree cell for
    /// cell — which is the whole point of the change, and what a JavaScript array's <c>a[5] = 9</c> means.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrowableShapesAndIndexSpellings))]
    public void AnIndexWritePastTheEndGrowsExactlyAsTheLengthWriteDoes(string shape, string index)
    {
        var key = index == "3" ? "5" : "'5'";

        var (throughIndex, readIndex) = CreateGrowableShape(shape);
        CreateEngine(throughIndex).Execute("x[" + key + "] = 9");

        var (throughLength, readLength) = CreateGrowableShape(shape);
        CreateEngine(throughLength).Execute("x.length = 6; x[" + key + "] = 9");

        readIndex().Should().Equal(readLength(), "growing through an index and growing through length are one request");
        readIndex().Should().HaveCount(6);
        readIndex()[5].Should().Be("9");
    }

    /// <summary>
    /// The ordinary ways a script copies into a host list. Both are specified as <c>Set(O, k, v, true)</c>
    /// over string keys, so both used to reach the reflected indexer and raise a CLR exception.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrowableShapes))]
    public void CopyingIntoAGrowableCollectionGrowsIt(string shape)
    {
        var (host, read) = CreateGrowableShape(shape);
        CreateEngine(host).Execute("Object.assign(x, { 3: 9 })");
        read().Should().Equal("1", "2", "3", "9");

        var (spreadHost, spreadRead) = CreateGrowableShape(shape);
        CreateEngine(spreadHost).Execute("var src = { 3: 9 }; for (var k in src) { x[k] = src[k]; }");
        spreadRead().Should().Equal("1", "2", "3", "9");
    }

    /// <summary>
    /// A key that is index-shaped but can never be a position of the view: negative, non-canonical, or past
    /// what the target can address. Each one used to be parsed by the reflected indexer and handed to the
    /// collection; the view owes script the ordinary <c>[[Set]]</c> refusal instead — silent outside strict
    /// mode, a <c>TypeError</c> inside it — because <c>x[-1]</c> reads <c>undefined</c> and <c>-1 in x</c>
    /// is <see langword="false"/>, so a position it could be read back from does not exist.
    /// </summary>
    [Theory]
    [InlineData("x[-1] = 9")]
    [InlineData("x['-1'] = 9")]
    [InlineData("x['08'] = 9")]
    [InlineData("x['+3'] = 9")]
    [InlineData("x[2147483648] = 9")]
    public void AnIndexTheViewCannotHoldIsRefusedRatherThanHandedToTheCollection(string operation)
    {
        var (host, read) = CreateGrowableShape(nameof(List<int>));

        CreateEngine(host).Evaluate(Probe(operation, strict: false)).AsString().Should().Be("no-throw");
        read().Should().Equal("1", "2", "3");

        CreateEngine(host, strict: true).Evaluate(Probe(operation, strict: true)).AsString().Should().Be("TypeError");
        read().Should().Equal("1", "2", "3");
    }

    /// <summary>
    /// The read side of the same key. <c>x["3"]</c> raised <see cref="ArgumentOutOfRangeException"/> out of
    /// <c>Evaluate</c> where <c>x[3]</c> read <c>undefined</c> — a plain read of an absent index, on every
    /// array-like wrapper including the read-only ones.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrowableShapes))]
    public void ReadingAnAbsentIndexIsUndefinedInEitherSpelling(string shape)
    {
        var (host, _) = CreateGrowableShape(shape);
        var engine = CreateEngine(host);

        foreach (var key in new[] { "3", "'3'", "-1", "'-1'", "'08'" })
        {
            engine.Evaluate("x[" + key + "] === undefined").AsBoolean()
                .Should().BeTrue("reading x[{0}] of a three-element view is a hole", key);
        }
    }

    /// <summary>
    /// <c>delete</c> of a position that is not there is a no-op that succeeds, and of one that is there
    /// resets the slot. Both spellings agree; before, the number spelling reached the collection with an
    /// out-of-range index and the string spelling refused an in-range one.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrowableShapes))]
    public void DeletingAnIndexAnswersFromTheViewInEitherSpelling(string shape)
    {
        var (absentHost, absentRead) = CreateGrowableShape(shape);
        var absent = CreateEngine(absentHost);
        absent.Evaluate("delete x[3]").AsBoolean().Should().BeTrue();
        absent.Evaluate("delete x['3']").AsBoolean().Should().BeTrue();
        absent.Evaluate("delete x[-1]").AsBoolean().Should().BeTrue();
        absentRead().Should().Equal("1", "2", "3");

        var (presentHost, presentRead) = CreateGrowableShape(shape);
        CreateEngine(presentHost).Evaluate("delete x['0']").AsBoolean().Should().BeTrue();
        presentRead().Should().HaveCount(3);
        presentRead()[0].Should().NotBe("1", "an in-range delete resets the slot, whichever way the index is spelled");
    }

    /// <summary>
    /// The refusals a growable collection keeps. Neither is about the index: one is the engine's write
    /// switch and the other is the object's own extensibility, and both are checked before the position is.
    /// </summary>
    [Theory]
    [InlineData("3")]
    [InlineData("'3'")]
    public void GrowthStillNeedsWritesEnabledAndAnExtensibleView(string index)
    {
        var items = new List<int> { 1, 2, 3 };
        // spelled out rather than left to the default: this branch ships Interop.AllowWrite on, so a bare
        // `new Engine()` would be testing the growth lane rather than the write switch that gates it
        var noWrites = new Engine(options => options.Interop.AllowWrite = false).SetValue("x", items);
        noWrites.Execute("x[" + index + "] = 9");
        items.Should().Equal(1, 2, 3);

        var frozen = CreateEngine(items);
        frozen.Execute("Object.preventExtensions(x); x[" + index + "] = 9");
        items.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// The fixed-size half, unchanged by the growth lane and now answering the string spelling too: an
    /// in-range element write works and anything outside the bounds is the <c>TypeError</c> #3381 gave it.
    /// A <c>T[]</c> live view used to raise <see cref="IndexOutOfRangeException"/> for
    /// <c>x['3'] = 9</c> and, worse, <see cref="ArgumentException"/> for the perfectly ordinary
    /// <c>x['2'] = 9</c> — the reflected indexer for a <c>T[]</c> is the object-typed
    /// <see cref="IList"/> one, so the write bypassed item-type coercion.
    /// </summary>
    [Fact]
    public void AFixedSizeViewAnswersBothSpellingsOfAnIndex()
    {
        var array = new[] { 1, 2, 3 };

        var writable = LiveViewEngine(array);
        writable.Execute("x['2'] = 9");
        array.Should().Equal(1, 2, 9);

        LiveViewEngine(array).Evaluate(Probe("x['3'] = 9", strict: false)).AsString().Should().Be("TypeError");
        LiveViewEngine(array).Evaluate(Probe("x[3] = 9", strict: false)).AsString().Should().Be("TypeError");
        array.Should().Equal(1, 2, 9);
    }

    private static Engine LiveViewEngine(object host)
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });
        engine.SetValue("x", host);
        return engine;
    }

    /// <summary>
    /// An array or list handed to script under a read-only contract must refuse element writes in either
    /// spelling. It used to refuse only the string one, and by accident: the reflected indexer of
    /// <see cref="IReadOnlyList{T}"/> is get-only, while the view took its writability from the target.
    /// </summary>
    [Theory]
    [InlineData("x[0] = 9")]
    [InlineData("x['0'] = 9")]
    [InlineData("x[3] = 9")]
    [InlineData("x['3'] = 9")]
    [InlineData("x.length = 5")]
    public void AnExposedReadOnlyContractRefusesElementWritesInEitherSpelling(string operation)
    {
        var array = new[] { 1, 2, 3 };

        ExposedReadOnly(array, strict: false).Evaluate(Probe(operation, strict: false)).AsString()
            .Should().Be("no-throw", "a failed [[Set]] is silent outside strict mode");
        array.Should().Equal(1, 2, 3);

        ExposedReadOnly(array, strict: true).Evaluate(Probe(operation, strict: true)).AsString()
            .Should().Be("TypeError", "a failed [[Set]] is a TypeError in strict mode");
        array.Should().Equal(1, 2, 3);
    }

    [Theory]
    [InlineData("x.push(9)")]
    [InlineData("Object.assign(x, { 3: 9 })")]
    public void AnExposedReadOnlyContractRefusesAGrowingGenericWithATypeError(string operation)
    {
        var array = new[] { 1, 2, 3 };

        ExposedReadOnly(array, strict: false).Evaluate(Probe(operation, strict: false)).AsString()
            .Should().Be("TypeError", "{0} is Set(O, k, v, true)", operation);
        array.Should().Equal(1, 2, 3);
    }

    private static Engine ExposedReadOnly(int[] array, bool strict)
    {
        var engine = new Engine(options =>
        {
            options.Strict = strict;
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("x", ObjectWrapper.Create(engine, array, typeof(IReadOnlyList<int>)));
        return engine;
    }

    /// <summary>
    /// The blanket statement the three issues in this family are all about: whatever the shape and however
    /// the index is spelled, what leaves <c>Evaluate</c> is a JavaScript error or nothing at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrowableShapes))]
    public void NoIndexOperationLetsAClrExceptionPastScript(string shape)
    {
        string[] operations =
        [
            "x[3] = 9", "x['3'] = 9", "x[5] = 9", "x['5'] = 9", "x[-1] = 9", "x['-1'] = 9", "x['08'] = 9",
            "x[2147483648] = 9", "x[3]", "x['3']", "x[-1]", "x['-1']", "delete x[3]", "delete x['3']",
            "delete x[-1]", "Object.assign(x, { 4: 9 })",
        ];

        foreach (var operation in operations)
        {
            foreach (var strict in new[] { false, true })
            {
                var (host, _) = CreateGrowableShape(shape);
                var engine = CreateEngine(host, strict);

                var thrown = Record.Exception(() => engine.Execute(operation));

                thrown.Should().Match(e => e == null || e is JavaScriptException,
                    "{0} on {1} must answer script, not the CLR", operation, shape);
            }
        }
    }

    private static string Probe(string operation, bool strict)
    {
        var prologue = strict ? "'use strict';\n" : "";
        return prologue
            + "(function () { try { " + operation + "; return 'no-throw'; } "
            + "catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";
    }

    /// <summary>
    /// A growable host list reaching the engine through the generic interface only.
    /// </summary>
    private sealed class HostList : IList<int>
    {
        private readonly List<int> _items;

        public HostList(List<int> items) => _items = items;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public int this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public void Add(int item) => _items.Add(item);

        public void Clear() => _items.Clear();

        public bool Contains(int item) => _items.Contains(item);

        public void CopyTo(int[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(int item) => _items.IndexOf(item);

        public void Insert(int index, int item) => _items.Insert(index, item);

        public bool Remove(int item) => _items.Remove(item);

        public void RemoveAt(int index) => _items.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A growable host list reaching the engine through the non-generic interface only, which is the
    /// untyped view every typed one degrades to under Native AOT.
    /// </summary>
    private sealed class HostUntypedList : IList
    {
        private readonly List<int> _items;

        public HostUntypedList(List<int> items) => _items = items;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public bool IsFixedSize => false;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public object? this[int index]
        {
            get => _items[index];
            set => _items[index] = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        public int Add(object? value)
        {
            _items.Add(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
            return _items.Count - 1;
        }

        public void Clear() => _items.Clear();

        public bool Contains(object? value) => _items.Contains(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));

        public void CopyTo(Array array, int index) => ((ICollection) _items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(object? value) => _items.IndexOf(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));

        public void Insert(int index, object? value) => _items.Insert(index, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));

        public void Remove(object? value) => _items.Remove(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));

        public void RemoveAt(int index) => _items.RemoveAt(index);
    }
}
