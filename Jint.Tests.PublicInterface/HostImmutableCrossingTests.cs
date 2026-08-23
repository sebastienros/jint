using System.Collections;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Options.AddImmutableCrossing</c> is a host-facing promise: instances of the declared CLR types do not
/// change while they are exposed to the engine, and in exchange the engine may cache what it reads through
/// them. This project has no <c>InternalsVisibleTo</c>, so everything reached here is reachable by a third
/// party — the registration, its validation, and the whole observable of the promise, which is that a repeated
/// read stops arriving at the host at all.
/// </summary>
public class HostImmutableCrossingTests
{
    /// <summary>
    /// A read-only string-keyed dictionary that counts how often the engine asks it for a value. Restricted to
    /// the public surface on purpose: a real embedder projects a CLR dictionary exactly like this, and the
    /// count is the only thing the memo is observable through.
    /// </summary>
    private sealed class CountingDictionary : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _inner;

        public CountingDictionary(params (string Key, object Value)[] entries)
        {
            _inner = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (key, value) in entries)
            {
                _inner[key] = value;
            }
        }

        public int Probes { get; private set; }

        public bool TryGetValue(string key, out object value)
        {
            Probes++;
            return _inner.TryGetValue(key, out value!);
        }

        public object this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
        public IEnumerable<string> Keys => _inner.Keys;
        public IEnumerable<object> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    private static (Engine Engine, CountingDictionary Root, CountingDictionary Leaf) CreateGraph(Action<Options> configure = null)
    {
        var leaf = new CountingDictionary(("country", "FI"), ("city", "Tampere"));
        var root = new CountingDictionary(("customer", leaf), ("amount", 42));

        var engine = new Engine(options =>
        {
            configure?.Invoke(options);
            options.AddImmutableCrossing(typeof(IReadOnlyDictionary<string, object>));
        });
        engine.SetValue("record", root);

        return (engine, root, leaf);
    }

    [Fact]
    public void ADeclaredTypeIsReadOncePerKeyHoweverOftenScriptAsks()
    {
        var (engine, root, leaf) = CreateGraph();

        engine.Evaluate("for (var i = 0; i < 100; i++) { record.customer.country; } record.customer.country")
            .AsString().Should().Be("FI");

        root.Probes.Should().Be(1);
        leaf.Probes.Should().Be(1);
    }

    [Fact]
    public void WithoutTheDeclarationEveryReadReachesTheHost()
    {
        var leaf = new CountingDictionary(("country", "FI"));
        var root = new CountingDictionary(("customer", leaf));

        var engine = new Engine();
        engine.SetValue("record", root);
        engine.Evaluate("for (var i = 0; i < 10; i++) { record.customer.country; }");

        root.Probes.Should().Be(10);
        leaf.Probes.Should().Be(10);
    }

    [Fact]
    public void TheDeclarationSupersedesTheRecentWrapperRing()
    {
        // the ring is the mechanism the declaration exists to replace for nested walks, so turning it off
        // must not change the answer
        var (engine, root, leaf) = CreateGraph(options => options.Interop.CacheRecentObjectWrappers = false);

        engine.Evaluate("for (var i = 0; i < 10; i++) { record.customer.country; }");

        root.Probes.Should().Be(1);
        leaf.Probes.Should().Be(1);
    }

    [Fact]
    public void EnumerationStaysLiveFromTheTarget()
    {
        var inner = new Dictionary<string, object>(StringComparer.Ordinal) { ["a"] = 1 };
        var engine = new Engine(options => options.AddImmutableCrossing(typeof(Dictionary<string, object>)));
        engine.SetValue("record", inner);

        engine.Evaluate("record.a").AsNumber().Should().Be(1);

        // the key set is never memoized, whatever the promise says — correctness first
        inner["b"] = 2;
        engine.Evaluate("Object.keys(record).join(',')").AsString().Should().Be("a,b");
        engine.Evaluate("JSON.stringify(record)").AsString().Should().Be("""{"a":1,"b":2}""");
    }

    [Fact]
    public void AWriteThroughTheWrapperEvictsThatKeysMemo()
    {
        var inner = new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" };
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.AddImmutableCrossing(typeof(Dictionary<string, object>));
        });
        engine.SetValue("record", inner);

        engine.Evaluate("record.country").AsString().Should().Be("FI");

        // the host broke its own promise; the write still reaches the CLR object and the read after it is
        // coherent rather than stale
        engine.Evaluate("record.country = 'SE'; record.country").AsString().Should().Be("SE");
        inner["country"].Should().Be("SE");
    }

    [Fact]
    public void EachEngineMemoizesForItself()
    {
        var target = new CountingDictionary(("country", "FI"));

        var first = new Engine(options => options.AddImmutableCrossing(typeof(IReadOnlyDictionary<string, object>)));
        first.SetValue("record", target);
        first.Evaluate("record.country; record.country");
        target.Probes.Should().Be(1);

        var second = new Engine(options => options.AddImmutableCrossing(typeof(IReadOnlyDictionary<string, object>)));
        second.SetValue("record", target);
        second.Evaluate("record.country; record.country");

        // a wrapper — and therefore its memo — belongs to one engine; nothing crossed between them
        target.Probes.Should().Be(2);
    }

    [Fact]
    public void OneOptionsInstanceCanServeSeveralEngines()
    {
        var options = new Options().AddImmutableCrossing(typeof(IReadOnlyDictionary<string, object>));

        var first = new CountingDictionary(("country", "FI"));
        var second = new CountingDictionary(("country", "SE"));

        var engineA = new Engine(options);
        engineA.SetValue("record", first);
        var engineB = new Engine(options);
        engineB.SetValue("record", second);

        engineA.Evaluate("record.country; record.country").AsString().Should().Be("FI");
        engineB.Evaluate("record.country; record.country").AsString().Should().Be("SE");

        first.Probes.Should().Be(1);
        second.Probes.Should().Be(1);
    }

    [Fact]
    public void RegistrationValidatesItsArguments()
    {
        var noTypes = () => new Options().AddImmutableCrossing();
        noTypes.Should().Throw<ArgumentException>();

        var nullEntry = () => new Options().AddImmutableCrossing(typeof(Uri), null!);
        nullEntry.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeclaredTypesAreVisibleOnTheOptions()
    {
        var options = new Options().AddImmutableCrossing(typeof(Uri), typeof(IReadOnlyDictionary<string, object>));

        options.Interop.ImmutableCrossingTypes.Should().Equal(typeof(Uri), typeof(IReadOnlyDictionary<string, object>));
    }

    [Fact]
    public void ASystemTextJsonGraphReadsTheSameDeclaredOrNot()
    {
        const string Source = """{"value":{"customer":{"country":"FI","city":"Tampere"},"amount":42}}""";
        const string Walk = "record.value.customer.country + '/' + record.value.customer.city + '/' + record.value.amount";

        var undeclared = new Engine();
        undeclared.SetValue("record", System.Text.Json.Nodes.JsonNode.Parse(Source));

        // JsonObject is both IDictionary<string, JsonNode> and IList<JsonNode>, so it lands on the
        // array-like wrapper while still resolving members through the dictionary lane the memo serves
        var declared = new Engine(options => options.AddImmutableCrossing(typeof(System.Text.Json.Nodes.JsonObject)));
        declared.SetValue("record", System.Text.Json.Nodes.JsonNode.Parse(Source));

        var expected = undeclared.Evaluate(Walk).AsString();
        declared.Evaluate(Walk).AsString().Should().Be(expected);
        declared.Evaluate(Walk).AsString().Should().Be(expected);

        declared.Evaluate("Object.keys(record.value).join(',')").AsString().Should().Be("customer,amount");

        // JSON.stringify is deliberately not asserted here: on the .NET Framework target Jint has no
        // built-in JsonValue handling (it is NET8_0_OR_GREATER only), so a JsonNode graph is only
        // serializable with the explicit converter InteropTests.SystemTextJson registers. Serialization of a
        // declared graph is pinned on the plain-dictionary and Newtonsoft cases instead.
    }

    [Fact]
    public void ANewtonsoftGraphReadsTheSameDeclaredOrNot()
    {
        const string Source = """{"value":{"customer":{"country":"FI"},"amount":42}}""";
        const string Walk = "record.value.customer.country + '/' + record.value.amount";

        var undeclared = new Engine();
        undeclared.SetValue("record", Newtonsoft.Json.Linq.JObject.Parse(Source));

        var declared = new Engine(options => options.AddImmutableCrossing(typeof(Newtonsoft.Json.Linq.JObject)));
        declared.SetValue("record", Newtonsoft.Json.Linq.JObject.Parse(Source));

        var expected = undeclared.Evaluate(Walk).AsString();
        declared.Evaluate(Walk).AsString().Should().Be(expected);
        declared.Evaluate(Walk).AsString().Should().Be(expected);

        declared.Evaluate("Object.keys(record.value).join(',')").AsString().Should().Be("customer,amount");
    }
}
