#nullable enable

using System.Collections.Generic;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers the host shapes reached through a <em>key</em> rather than a member name: a CLR dictionary and a
/// declared indexer. Both are served by compiled delegates where one can be built and by reflection
/// everywhere else, so every assertion here is written against an oracle that cannot move — the state of the
/// host object itself, read straight from C# — and each behaviour is exercised on a host whose shape takes
/// the compiled lane and on one whose shape provably cannot.
/// <para>
/// The lane-declining shapes are the two the builders refuse: a closed generic interface that is not visible
/// (a non-public value type parameter), and a value-type receiver — a compiled call would run against the
/// unboxed copy, so a write through it would not reach the boxed instance the wrapper holds. The struct pair
/// below is what pins that.
/// </para>
/// </summary>
public class HostKeyedMemberAccessTests
{
    private static Engine CreateEngine(object host)
    {
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        engine.SetValue("host", host);
        return engine;
    }

    #region 1. non-string-keyed dictionary: read, contains, write, delete

    [Fact]
    public void IntKeyedDictionaryRoundTripsThroughTheEngine()
    {
        var dictionary = new Dictionary<int, string> { [1] = "one", [2] = "two" };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("host[1]").AsString().Should().Be("one");
        engine.Evaluate("typeof host[3]").AsString().Should().Be("undefined");

        engine.Evaluate("1 in host").AsBoolean().Should().BeTrue();
        engine.Evaluate("3 in host").AsBoolean().Should().BeFalse();

        engine.Execute("host[3] = 'three';");
        dictionary[3].Should().Be("three");
        engine.Evaluate("3 in host").AsBoolean().Should().BeTrue();

        engine.Execute("delete host[1];");
        dictionary.ContainsKey(1).Should().BeFalse();
        engine.Evaluate("1 in host").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void IntKeyedDictionaryOfANonVisibleValueTypeBehavesIdentically()
    {
        // IDictionary<int, Hidden> is not a visible type, so no compiled delegate can be built for any of
        // its members and every operation below runs on the reflection fallback
        var dictionary = new Dictionary<int, Hidden> { [1] = new Hidden(1), [2] = new Hidden(2) };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("host[1].Value").AsNumber().Should().Be(1);
        engine.Evaluate("typeof host[3]").AsString().Should().Be("undefined");

        engine.Evaluate("1 in host").AsBoolean().Should().BeTrue();
        engine.Evaluate("3 in host").AsBoolean().Should().BeFalse();

        engine.Execute("delete host[1];");
        dictionary.ContainsKey(1).Should().BeFalse();
        engine.Evaluate("1 in host").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void IntKeyedDictionaryAcceptsAValueTypedValue()
    {
        var dictionary = new Dictionary<int, long>();
        var engine = CreateEngine(dictionary);

        engine.Execute("host[1] = 7;");

        dictionary[1].Should().Be(7L);
        engine.Evaluate("host[1]").AsNumber().Should().Be(7);
    }

    [Fact]
    public void IntKeyedDictionaryAcceptsANullValue()
    {
        var dictionary = new Dictionary<int, string?>();
        var engine = CreateEngine(dictionary);

        engine.Execute("host[1] = null;");

        dictionary.Should().ContainKey(1).WhoseValue.Should().BeNull();
    }

    [Fact]
    public void IntKeyedDictionaryAcceptsANullableValue()
    {
        // a null for a Nullable<T> value keeps its meaning; a null for a plain value type is refused before
        // the write is attempted, exactly as before
        var nullable = new Dictionary<int, int?>();
        var nullableEngine = CreateEngine(nullable);
        nullableEngine.Execute("host[1] = null; host[2] = 5;");
        nullable[1].Should().BeNull();
        nullable[2].Should().Be(5);

        var plain = new Dictionary<int, int>();
        var plainEngine = CreateEngine(plain);
        plainEngine.Execute("host[1] = null;");
        plain.Should().BeEmpty();
    }

    [Fact]
    public void DictionaryWithAReferenceKeyRoundTrips()
    {
        // the key arrives as the wrapped CLR instance itself, so it is already an instance of the key type
        var key = new Key();
        var dictionary = new Dictionary<Key, string> { [key] = "one" };
        var engine = CreateEngine(dictionary);
        engine.SetValue("key", key);

        engine.Evaluate("host[key]").AsString().Should().Be("one");
        engine.Evaluate("key in host").AsBoolean().Should().BeTrue();

        engine.Execute("host[key] = 'changed';");
        dictionary[key].Should().Be("changed");

        engine.Execute("delete host[key];");
        dictionary.ContainsKey(key).Should().BeFalse();
    }

    #endregion

    #region 2. string-keyed dictionary: the write and delete lanes

    [Fact]
    public void StringKeyedDictionaryRoundTripsThroughTheEngine()
    {
        var dictionary = new Dictionary<string, string> { ["a"] = "one" };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("host.a").AsString().Should().Be("one");

        engine.Execute("host.b = 'two';");
        dictionary["b"].Should().Be("two");

        engine.Execute("host.a = 'changed';");
        dictionary["a"].Should().Be("changed");

        engine.Execute("delete host.a;");
        dictionary.ContainsKey("a").Should().BeFalse();
        engine.Evaluate("typeof host.a").AsString().Should().Be("undefined");
    }

    [Fact]
    public void StringKeyedDictionaryOfAValueTypeRoundTrips()
    {
        var dictionary = new Dictionary<string, int> { ["a"] = 1 };
        var engine = CreateEngine(dictionary);

        engine.Execute("host.b = 2;");
        dictionary["b"].Should().Be(2);

        engine.Evaluate("host.b").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("a,b");
    }

    #endregion

    #region 3. declared indexer

    [Fact]
    public void IndexerWithAContainsProbeAnswersHitsAndMisses()
    {
        var registry = new Registry();
        registry.Add("a", "one");
        var engine = CreateEngine(registry);

        engine.Evaluate("host.a").AsString().Should().Be("one");
        engine.Evaluate("typeof host.missing").AsString().Should().Be("undefined");
        engine.Evaluate("'a' in host").AsBoolean().Should().BeTrue();

        engine.Execute("host.b = 'two';");
        registry.Read("b").Should().Be("two");
        engine.Evaluate("host.b").AsString().Should().Be("two");
    }

    [Fact]
    public void IndexerWithoutAProbeTurnsAKeyNotFoundIntoUndefined()
    {
        // no ContainsKey next to the indexer, so the miss arrives as an exception out of the getter; the
        // compiled delegate rethrows it raw where reflection wrapped it, and both must still read undefined
        var bag = new Bag();
        bag.Add("a", "one");
        var engine = CreateEngine(bag);

        engine.Evaluate("host.a").AsString().Should().Be("one");
        engine.Evaluate("typeof host.missing").AsString().Should().Be("undefined");
    }

    [Fact]
    public void IndexerFailureOtherThanAMissStillSurfaces()
    {
        var engine = CreateEngine(new ThrowingIndexer());

        var act = () => engine.Evaluate("host.anything");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void StructIndexerWritesReachTheWrappedInstance()
    {
        // a value-type receiver declines every compiled lane: a compiled call would unbox a copy, and this
        // write - which stores into a field of the struct itself - would be lost
        var engine = CreateEngine(new StructBag(string.Empty));

        engine.Evaluate("typeof host.anything").AsString().Should().Be("string");
        engine.Evaluate("host.anything").AsString().Should().BeEmpty();

        engine.Execute("host.anything = 'written';");

        engine.Evaluate("host.anything").AsString().Should().Be("written");
    }

    [Fact]
    public void IntIndexerDoesNotShadowANamedMember()
    {
        // the member-name probe cannot be handed to a this[int], so the named member answers - the same
        // result the probe produced by throwing and being swallowed
        var engine = CreateEngine(new ListLike());

        engine.Evaluate("host.Count").AsNumber().Should().Be(3);
        engine.Evaluate("host[1]").AsString().Should().Be("item1");
    }

    [Fact]
    public void StringIndexerStillShadowsANamedMember()
    {
        // the documented order: an indexer that can be handed the member name is probed before the member
        var engine = CreateEngine(new Shadowing());

        engine.Evaluate("host.Name").AsString().Should().Be("indexer:Name");
    }

    #endregion

    #region hosts

    private struct Hidden
    {
        public Hidden(int value) => Value = value;

        public int Value { get; set; }
    }

    public sealed class Key
    {
        public string Name => "key";
    }

    public sealed class Registry
    {
        private readonly Dictionary<string, string> _items = new();

        public string this[string key]
        {
            get => _items[key];
            set => _items[key] = value;
        }

        public bool ContainsKey(string key) => _items.ContainsKey(key);

        public void Add(string key, string value) => _items[key] = value;

        public string Read(string key) => _items[key];
    }

    public sealed class Bag
    {
        private readonly Dictionary<string, string> _items = new();

        // no ContainsKey companion, so a miss throws KeyNotFoundException out of the getter
        public string this[string key]
        {
            get => _items[key];
            set => _items[key] = value;
        }

        public void Add(string key, string value) => _items[key] = value;
    }

    public sealed class ThrowingIndexer
    {
        public string this[string key] => throw new InvalidOperationException("boom");
    }

    public struct StructBag
    {
        private string _value;

        public StructBag(string value) => _value = value;

        public string this[string key]
        {
            get => _value;
            set => _value = value;
        }
    }

    public sealed class ListLike
    {
        public int Count => 3;

        public string this[int index] => "item" + index;
    }

    public sealed class Shadowing
    {
        public string Name => "member";

        public string this[string key] => "indexer:" + key;
    }

    #endregion
}
