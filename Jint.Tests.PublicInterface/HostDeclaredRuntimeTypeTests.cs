#nullable enable

using System.Collections;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A value that crosses under a declared type that does not decide its exposure - <c>object</c>, System.Text.Json's
/// <c>JsonNode</c>, a collection of a keyed entry list's entries - is the object it is as its own type, and reading it
/// twice is one wrapper (follow-ups to #4524 and #4527).
/// </summary>
/// <remarks>
/// The expected answers are the control's: the same value crossing as its own type. For a list that is an array's,
/// as Node gives for <c>[1, 2]</c>: positions read through
/// <see href="https://tc39.es/ecma262/#sec-ordinaryget">OrdinaryGet</see>, <c>length</c> through
/// <see href="https://tc39.es/ecma262/#sec-lengthofarraylike">LengthOfArrayLike</see>, and
/// <see href="https://tc39.es/ecma262/#sec-array.prototype.push">Array.prototype.push</see> answering the new length.
/// For a keyed object it is <c>{ a: 1, b: 2 }</c>'s: <c>Object.keys</c> lists <c>a</c> and <c>b</c>
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see>) and
/// <see href="https://tc39.es/ecma262/#sec-json.stringify">JSON.stringify</see> gives <c>{"a":1,"b":2}</c>.
/// </remarks>
public class HostDeclaredRuntimeTypeTests
{
    /// <summary>
    /// The members a host hands the same list through: as itself (the control), and typed <c>object</c> by a property,
    /// a method, a field and a <c>Dictionary&lt;string, object&gt;</c> entry.
    /// </summary>
    public static IEnumerable<string> ListMembers => ["AsSelf", "AsObject", "GetObject()", "ObjectField", "Bag.list"];

    private const string ListReads = """
        [
            x[0],
            x['1'],
            0 in x,
            5 in x,
            x.length,
            Array.prototype.slice.call(x).join('|'),
            Object.getPrototypeOf(x) === Array.prototype,
            JSON.stringify(x),
            x.foo === undefined
        ].join(',')
        """;

    private const string ListWrites = "[(x[0] = 7, x[0]), x.push(3), x.length].join(',')";

    [TestCaseSource(nameof(ListMembers))]
    public void AListTypedObjectIsTheListItIsAsItself(string member)
    {
        var list = new List<int> { 1, 2 };
        var engine = CreateEngine(new ValueHost<List<int>>(list), member, allowWrite: true);

        engine.Evaluate(ListReads).AsString().Should().Be("1,2,true,false,2,1|2,true,[1,2],true");
        engine.Evaluate(ListWrites).AsString().Should().Be("7,3,3");
        string.Join(",", list).Should().Be("7,2,3");
    }

    [TestCaseSource(nameof(ListMembers))]
    public void AJArrayTypedObjectIsTheListItIsAsItself(string member)
    {
        var array = JArray.Parse("[1, 2]");
        var engine = CreateEngine(new ValueHost<JArray>(array), member, allowWrite: true);

        engine.Evaluate(ListReads).AsString().Should().Be("1,2,true,false,2,1|2,true,[1,2],true");
        engine.Evaluate(ListWrites).AsString().Should().Be("7,3,3");

        // a number written from script reaches a JToken as a double, as it does for the JArray crossing as itself
        array.ToString(Formatting.None).Should().Be("[7.0,2,3.0]");
    }

    /// <summary>
    /// A list typed <c>object</c> is still one wrapper read after read, under the identity map and the ring.
    /// </summary>
    [TestCaseSource(nameof(ListMembers))]
    public void ReadingAListTypedObjectTwiceIsTheSameObject(string member)
    {
        foreach (var (track, ring) in new[] { (true, false), (false, true) })
        {
            var engine = new Engine(options =>
            {
                options.Interop.TrackObjectWrapperIdentity = track;
                options.Interop.CacheRecentObjectWrappers = ring;
            }).SetValue("host", new ValueHost<List<int>>([1, 2]));

            ReferenceEquals(engine.Evaluate($"host.{member}"), engine.Evaluate($"host.{member}"))
                .Should().BeTrue("{0}, identity map {1}, ring {2}", member, track, ring);
        }
    }

    /// <summary>
    /// The members a host hands the same <c>JsonNode</c> through: as itself (the control), and typed <c>JsonNode</c>
    /// by a property, a method and a field.
    /// </summary>
    public static IEnumerable<string> NodeMembers => ["AsSelf", "AsNode", "GetNode()", "NodeField"];

    /// <summary>
    /// A <c>JsonObject</c> or <c>JsonArray</c> reached through a <c>JsonNode</c>-typed member is wrapped as itself, and
    /// reading the member twice must hand script the same wrapper, as it does for the node crossing as itself. Asked
    /// of the host by reference: script's <c>===</c> compares two wrappers' CLR targets
    /// (<see href="https://tc39.es/ecma262/#sec-isstrictlyequal">IsStrictlyEqual</see> sees one object either way).
    /// </summary>
    [TestCaseSource(nameof(NodeMembers))]
    public void ReadingAJsonNodeMemberTwiceIsTheSameObject(string member)
    {
        foreach (var json in new[] { """{ "a": 1 }""", "[1]" })
        {
            foreach (var (track, ring) in new[] { (true, false), (false, true) })
            {
                var node = JsonNode.Parse(json)!;
                var engine = new Engine(options =>
                {
                    options.Interop.TrackObjectWrapperIdentity = track;
                    options.Interop.CacheRecentObjectWrappers = ring;
                });

                if (node is JsonObject jsonObject)
                {
                    engine.SetValue("host", new NodeHost<JsonObject>(jsonObject));
                }
                else
                {
                    engine.SetValue("host", new NodeHost<JsonArray>((JsonArray) node));
                }

                ReferenceEquals(engine.Evaluate($"host.{member}"), engine.Evaluate($"host.{member}"))
                    .Should().BeTrue("{0} over {1}, identity map {2}, ring {3}", member, json, track, ring);
            }
        }
    }

    [TestCaseSource(nameof(NodeMembers))]
    public void AJsonNodeMemberIsTheNodeItIsAsItself(string member)
    {
        var engine = new Engine()
            .SetValue("objectHost", new NodeHost<JsonObject>((JsonObject) JsonNode.Parse("""{ "a": 1, "b": 2 }""")!))
            .SetValue("arrayHost", new NodeHost<JsonArray>((JsonArray) JsonNode.Parse("[1, 2]")!));

        engine.Evaluate($$"""
            const o = objectHost.{{member}};
            const a = arrayHost.{{member}};
            [
                o.a,
                Object.keys(o).join('|'),
                JSON.stringify(o),
                a.length,
                String(a[1]),
                Object.getPrototypeOf(a) === Array.prototype
            ].join(',')
            """).AsString().Should().Be("""1,a|b,{"a":1,"b":2},2,2,true""");
    }

    /// <summary>
    /// The members a host hands the same keyed entry list through - a dictionary that is also a list of its entries
    /// by the generic <c>IList&lt;KeyValuePair&lt;,&gt;&gt;</c> alone: as itself (the control), and as the entries it
    /// enumerates.
    /// </summary>
    public static IEnumerable<string> EntryListMembers => ["AsSelf", "AsEntries", "AsEntryCollection", "AsReadOnlyEntryCollection", "AsObject"];

    private const string KeyedObject = """1,a|b,a|b,{"a":1,"b":2},true,true,false""";

    private const string KeyedReads = """
        [
            x.a,
            Object.keys(x).join('|'),
            (() => { const keys = []; for (const k in x) keys.push(k); return keys.join('|'); })(),
            JSON.stringify(x),
            'b' in x,
            Object.getPrototypeOf(x) === Object.prototype,
            Array.isArray(x)
        ].join(',')
        """;

    [TestCaseSource(nameof(EntryListMembers))]
    public void AGenericEntryListIsAKeyedObjectWhateverItCrossesAs(string member)
    {
        var engine = new Engine().SetValue("host", new EntryListHost(new EntryList { ["a"] = 1, ["b"] = 2 }));
        engine.Execute($"var x = host.{member};");

        engine.Evaluate(KeyedReads).AsString().Should().Be(KeyedObject, "an entry list crossing as {0} is the object it is as itself", member);
    }

    [TestCaseSource(nameof(EntryListMembers))]
    public void ReadingAGenericEntryListMemberTwiceIsTheSameObject(string member)
    {
        foreach (var (track, ring) in new[] { (true, false), (false, true) })
        {
            var engine = new Engine(options =>
            {
                options.Interop.TrackObjectWrapperIdentity = track;
                options.Interop.CacheRecentObjectWrappers = ring;
            }).SetValue("host", new EntryListHost(new EntryList { ["a"] = 1 }));

            ReferenceEquals(engine.Evaluate($"host.{member}"), engine.Evaluate($"host.{member}"))
                .Should().BeTrue("{0}, identity map {1}, ring {2}", member, track, ring);
        }
    }

    /// <summary>
    /// What must not move: an ordinary dictionary declared as a collection of its entries keeps that collection's
    /// count-only view, one declared as the entries it enumerates stays keyed, and a list or a set declared as a
    /// collection keeps its view.
    /// </summary>
    [Test]
    public void CollectionViewsOfOrdinaryCollectionsAreUnchanged()
    {
        var engine = new Engine().SetValue("host", new CollectionHost());

        engine.Evaluate("""
            [
                host.Pairs.a,
                Object.getPrototypeOf(host.Pairs) === Object.prototype,
                host.PairCollection.length,
                JSON.stringify(host.PairCollection),
                Object.getPrototypeOf(host.PairCollection) === Array.prototype,
                host.Numbers.length,
                host.Numbers[1],
                Object.getPrototypeOf(host.Numbers) === Array.prototype,
                host.Set.length,
                Object.getPrototypeOf(host.Set) === Array.prototype
            ].join(',')
            """).AsString().Should().Be("1,true,2,[null,null],true,2,2,true,2,true");
    }

    private static Engine CreateEngine(object host, string member, bool allowWrite)
    {
        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite);
        engine.SetValue("host", host);
        engine.Execute($"var x = host.{member};");
        return engine;
    }

    public sealed class ValueHost<T>(T value) where T : class
    {
        public object ObjectField = value;

        public T AsSelf => (T) ObjectField;

        public object AsObject => ObjectField;

        public Dictionary<string, object> Bag { get; } = new() { ["list"] = value };

        public object GetObject() => ObjectField;
    }

    public sealed class NodeHost<T>(T node) where T : JsonNode
    {
        public JsonNode NodeField = node;

        public T AsSelf => (T) NodeField;

        public JsonNode AsNode => NodeField;

        public JsonNode GetNode() => NodeField;
    }

    public sealed class EntryListHost(EntryList list)
    {
        public EntryList AsSelf => list;

        public IEnumerable<KeyValuePair<string, int>> AsEntries => list;

        public ICollection<KeyValuePair<string, int>> AsEntryCollection => list;

        public IReadOnlyCollection<KeyValuePair<string, int>> AsReadOnlyEntryCollection => list;

        public object AsObject => list;
    }

    public sealed class CollectionHost
    {
        public IEnumerable<KeyValuePair<string, int>> Pairs { get; } = new Dictionary<string, int> { ["a"] = 1 };

        public ICollection<KeyValuePair<string, int>> PairCollection { get; } = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        public ICollection<int> Numbers { get; } = new List<int> { 1, 2 };

        public ICollection<int> Set { get; } = new HashSet<int> { 1, 2 };
    }

    /// <summary>
    /// A dictionary that is also a list of its entries through the generic contract alone, as an ordered dictionary
    /// written against <c>IList&lt;T&gt;</c> but not <see cref="IList"/> is.
    /// </summary>
    public sealed class EntryList : Dictionary<string, int>, IList<KeyValuePair<string, int>>
    {
        KeyValuePair<string, int> IList<KeyValuePair<string, int>>.this[int index]
        {
            get => this.ElementAt(index);
            set => throw new NotSupportedException();
        }

        int IList<KeyValuePair<string, int>>.IndexOf(KeyValuePair<string, int> item) => throw new NotSupportedException();

        void IList<KeyValuePair<string, int>>.Insert(int index, KeyValuePair<string, int> item) => throw new NotSupportedException();

        void IList<KeyValuePair<string, int>>.RemoveAt(int index) => throw new NotSupportedException();
    }
}
