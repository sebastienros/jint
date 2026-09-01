#nullable enable

using System.Collections;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// One question — <em>does this index exist?</em> — asked of a wrapped CLR collection through every lane an
/// embedder's script can reach it: <c>in</c>, <c>hasOwnProperty</c>, the indexed read,
/// <c>Object.getOwnPropertyDescriptor</c>, <c>propertyIsEnumerable</c> and <c>Object.getOwnPropertyNames</c>.
/// </summary>
/// <remarks>
/// <para>
/// They have to give one answer. <see href="https://tc39.es/ecma262/#sec-ordinaryhasproperty">OrdinaryHasProperty</see>
/// is defined in terms of <c>[[GetOwnProperty]]</c>, so <c>3 in list</c> being <see langword="false"/> while
/// <c>list.hasOwnProperty(3)</c> is <see langword="true"/> on the same object is not a divergence between two
/// lanes an implementation may choose — it is a contradiction. It was the state of every array-like wrapper
/// until #3423: the view owned <c>Get</c>, <c>Set</c>, <c>HasProperty</c>, <c>Delete</c> and the key
/// enumerations, and left <c>[[GetOwnProperty]]</c> to <see cref="Jint.Runtime.Interop.ObjectWrapper"/>, which
/// resolves the reflected indexer and reports a descriptor for <em>any</em> parseable index.
/// </para>
/// <para>
/// Every shape below is an ordinary embedder exposure and every one of them reaches a different wrapper, which
/// is the point: the contradiction was in the shared base and so was in all of them.
/// </para>
/// </remarks>
public class HostCollectionIndexAgreementTests
{
    /// <summary>Every wrapper an array-like CLR target reaches, named by the exposure that produces it.</summary>
    public static TheoryData<string> Shapes =>
        new()
        {
            "List<int>",
            "int[]",
            "IReadOnlyList<int>",
            "IList<int>",
            "ArrayList",
        };

    /// <summary>
    /// Keys that name no position of a three-element view: past the end, far past it, negative, and the two
    /// integer-shaped spellings that are not canonical array indices.
    /// </summary>
    public static TheoryData<string> AbsentIndices => new() { "3", "10", "-1", "'3.5'", "'08'", "'+3'" };

    /// <summary>Both spellings of every position the view does have — <c>x[1]</c> and <c>x['1']</c> are one key.</summary>
    public static TheoryData<string> PresentIndices => new() { "0", "1", "2", "'0'", "'2'" };

    private const string AllAbsent = "false,false,false,false,false,false";
    private const string AllPresent = "true,true,true,true,true,true";

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryLaneAgreesThatAnAbsentIndexIsAbsent(string shape)
    {
        foreach (var key in AbsentIndices)
        {
            var engine = CreateEngine(shape);

            Lanes(engine, key).Should().Be(AllAbsent,
                "{0}[{1}] is not a position of the view, so no lane may report it as one", shape, key);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryLaneAgreesThatAPresentIndexIsPresent(string shape)
    {
        foreach (var key in PresentIndices)
        {
            var engine = CreateEngine(shape);

            Lanes(engine, key).Should().Be(AllPresent,
                "{0}[{1}] is a position of the view, so every lane reports it", shape, key);
        }
    }

    /// <summary>
    /// The descriptor lane resolved the reflected indexer and cached what it built, so a position that had been
    /// asked about once went on being reported after the collection shrank past it. Reading the range first is
    /// what makes the cached descriptor unreachable.
    /// </summary>
    [Fact]
    public void AnIndexStopsExistingWhenTheCollectionShrinksPastIt()
    {
        var engine = CreateEngine("List<int>");

        engine.Evaluate("x.hasOwnProperty(2)").AsBoolean().Should().BeTrue();

        engine.Execute("x.length = 1");

        Lanes(engine, "2").Should().Be(AllAbsent, "the third element is gone, and the cached descriptor for it is not the answer");
    }

    /// <summary>
    /// A position the view does not have cannot be defined into one either, or it would exist for
    /// <c>[[GetOwnProperty]]</c> and not for anything else. The refusal is what script already saw — the
    /// reflected indexer's descriptor was non-configurable — so this pins the answer, not a new one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void DefiningAnAbsentIndexIsRefused(string shape)
    {
        var engine = CreateEngine(shape);

        engine.Evaluate("Reflect.defineProperty(x, 5, { value: 42 })").AsBoolean().Should().BeFalse();
        Record.Exception(() => engine.Execute("Object.defineProperty(x, 5, { value: 42 })"))
            .Should().BeOfType<JavaScriptException>();
        Lanes(engine, "5").Should().Be(AllAbsent, "nothing was defined");
    }

    /// <summary>
    /// The one target shape that answers a string key from its own keys rather than by index — Newtonsoft's
    /// <c>JObject</c> is both an <c>IDictionary&lt;string, _&gt;</c> and an <c>IList&lt;_&gt;</c> — is stood in for
    /// here by a type with the same two interfaces. Its index-shaped keys are dictionary keys and must keep
    /// answering as such.
    /// </summary>
    [Fact]
    public void ADictionaryShapedArrayLikeStillAnswersFromItsKeys()
    {
        var engine = new Engine(options => options.AllowClr());
        engine.SetValue("x", new KeyedList { { "3", 42 } });

        engine.Evaluate("'3' in x").AsBoolean().Should().BeTrue();
        engine.Evaluate("x.hasOwnProperty('3')").AsBoolean().Should().BeTrue();
        engine.Evaluate("x['3']").AsNumber().Should().Be(42);
    }

    /// <summary>
    /// Named CLR members are not positions and were never part of the disagreement; asserted so that reading
    /// the index range first cannot be mistaken for owning every key.
    /// </summary>
    [Fact]
    public void ANamedMemberIsUnaffected()
    {
        var engine = CreateEngine("List<int>");

        engine.Evaluate("x.hasOwnProperty('Count')").AsBoolean().Should().BeTrue();
        engine.Evaluate("x.Count").AsNumber().Should().Be(3);
        engine.Evaluate("x.hasOwnProperty('NoSuchMember')").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// The six answers, in the order <see cref="AllAbsent"/> and <see cref="AllPresent"/> spell them, joined so
    /// that a failure names every lane that disagreed instead of stopping at the first.
    /// </summary>
    private static string Lanes(Engine engine, string key) => engine.Evaluate($$"""
        [
            ({{key}} in x),
            x.hasOwnProperty({{key}}),
            x[{{key}}] !== undefined,
            Object.getOwnPropertyDescriptor(x, {{key}}) !== undefined,
            x.propertyIsEnumerable({{key}}),
            Object.getOwnPropertyNames(x).indexOf(String({{key}})) >= 0
        ].join(',')
        """).AsString();

    private static Engine CreateEngine(string shape)
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });

        engine.SetValue("x", Target(shape));
        return engine;
    }

    private static object Target(string shape) => shape switch
    {
        "List<int>" => new List<int> { 1, 2, 3 },
        "int[]" => new[] { 1, 2, 3 },
        "IReadOnlyList<int>" => new ReadOnlyItems(1, 2, 3),
        "IList<int>" => new IndexedItems(1, 2, 3),
        "ArrayList" => new ArrayList { 1, 2, 3 },
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown shape"),
    };

    /// <summary>A host collection reachable only through <see cref="IReadOnlyList{T}"/>.</summary>
    private sealed class ReadOnlyItems : IReadOnlyList<int>
    {
        private readonly List<int> _items;

        public ReadOnlyItems(params int[] items) => _items = [.. items];

        public int this[int index] => _items[index];

        public int Count => _items.Count;

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }

    /// <summary>
    /// A host collection that implements <see cref="IList{T}"/> and the non-generic <see cref="ICollection"/>
    /// but not the non-generic <see cref="IList"/>.
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

    /// <summary>
    /// Both an <c>IDictionary&lt;string, int&gt;</c> and an <c>IList&lt;int&gt;</c>, the way Newtonsoft's
    /// <c>JObject</c> is: its index-shaped keys are dictionary keys.
    /// </summary>
    private sealed class KeyedList : IDictionary<string, int>, IList<int>
    {
        private readonly Dictionary<string, int> _byKey = new(StringComparer.Ordinal);

        public int this[string key] { get => _byKey[key]; set => _byKey[key] = value; }

        public int this[int index] { get => _byKey.Values.ElementAt(index); set => throw new NotSupportedException(); }

        public ICollection<string> Keys => _byKey.Keys;

        public ICollection<int> Values => _byKey.Values;

        public int Count => _byKey.Count;

        public bool IsReadOnly => false;

        public void Add(string key, int value) => _byKey.Add(key, value);

        public void Add(KeyValuePair<string, int> item) => _byKey.Add(item.Key, item.Value);

        public void Add(int item) => throw new NotSupportedException();

        public void Clear() => _byKey.Clear();

        public bool Contains(KeyValuePair<string, int> item) => _byKey.Contains(item);

        public bool Contains(int item) => _byKey.ContainsValue(item);

        public bool ContainsKey(string key) => _byKey.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<string, int>>) _byKey).CopyTo(array, arrayIndex);

        public void CopyTo(int[] array, int arrayIndex) => _byKey.Values.CopyTo(array, arrayIndex);

        public int IndexOf(int item) => throw new NotSupportedException();

        public void Insert(int index, int item) => throw new NotSupportedException();

        public bool Remove(string key) => _byKey.Remove(key);

        public bool Remove(KeyValuePair<string, int> item) => _byKey.Remove(item.Key);

        public bool Remove(int item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public bool TryGetValue(string key, out int value) => _byKey.TryGetValue(key, out value);

        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator() => _byKey.GetEnumerator();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => _byKey.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _byKey.GetEnumerator();
    }
}
