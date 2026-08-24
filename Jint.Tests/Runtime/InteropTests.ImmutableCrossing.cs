using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    /// <summary>
    /// A string-keyed dictionary that counts how often the engine actually asks it for a value. It is the
    /// crossing memo's observable: a read that never reaches this type is a read the memo answered.
    /// </summary>
    public sealed class CountingDictionary : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _inner;

        public CountingDictionary(Dictionary<string, object> inner)
        {
            _inner = inner;
        }

        public int Probes { get; private set; }

        public bool TryGetValue(string key, out object value)
        {
            Probes++;
            return _inner.TryGetValue(key, out value);
        }

        public object this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
        public IEnumerable<string> Keys => _inner.Keys;
        public IEnumerable<object> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    /// <summary>Converts a <see cref="Uri"/> to its string form, and counts how often it is asked to.</summary>
    private sealed class CountingUriConverter : ObjectConverter
    {
        public int Conversions { get; private set; }

        public override bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue result)
        {
            if (value is Uri uri)
            {
                Conversions++;
                result = JsString.Create(uri.ToString());
                return true;
            }

            result = null;
            return false;
        }
    }

    private static Dictionary<string, object> BuildCustomerGraph()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["value"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["customer"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["country"] = "FI",
                    ["city"] = "Tampere",
                },
                ["amount"] = 42,
            },
        };
    }

    private static Engine ImmutableCrossingEngine(params Type[] types)
    {
        return new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.AddImmutableCrossing(types);
        });
    }

    [Fact]
    public void ImmutableCrossingAnswersRepeatedNestedReadsWithoutProbingTheHost()
    {
        var leaf = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["customer"] = leaf });

        var engine = ImmutableCrossingEngine(typeof(CountingDictionary));
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 50; i++) { record.customer.country; } record.customer.country")
            .AsString().Should().Be("FI");

        // one probe per level for the whole loop: the rest were answered from the memo
        root.Probes.Should().Be(1);
        leaf.Probes.Should().Be(1);
    }

    [Fact]
    public void UndeclaredTypeKeepsProbingOnEveryRead()
    {
        var leaf = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["customer"] = leaf });

        var engine = new Engine();
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 10; i++) { record.customer.country; }");

        // control: nothing declared, so every read reaches the host exactly as it did before
        root.Probes.Should().Be(10);
        leaf.Probes.Should().Be(10);
    }

    [Fact]
    public void ImmutableCrossingCoversImplementationsOfADeclaredInterface()
    {
        var leaf = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["customer"] = leaf });

        // the declaration names the interface, the values are a type implementing it
        var engine = ImmutableCrossingEngine(typeof(IReadOnlyDictionary<string, object>));
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 10; i++) { record.customer.country; }");

        root.Probes.Should().Be(1);
        leaf.Probes.Should().Be(1);
    }

    [Fact]
    public void ImmutableCrossingIgnoresAnUnrelatedDeclaration()
    {
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });

        var engine = ImmutableCrossingEngine(typeof(Uri));
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 5; i++) { record.country; }");

        root.Probes.Should().Be(5);
    }

    private static (CountingDictionary Root, CountingDictionary[] Children) BuildWideGraph()
    {
        var children = new CountingDictionary[20];
        var inner = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
            inner["c" + i.ToString(CultureInfo.InvariantCulture)] = children[i];
        }

        return (new CountingDictionary(inner), children);
    }

    private const string WideWalkScript =
        """
        for (var pass = 0; pass < 2; pass++) {
            for (var i = 0; i < 20; i++) { root['c' + i].country; }
        }
        """;

    [Fact]
    public void ImmutableCrossingSurvivesMoreNodesThanTheRecentWrapperRingHolds()
    {
        var (root, children) = BuildWideGraph();

        var engine = ImmutableCrossingEngine(typeof(CountingDictionary));
        engine.SetValue("root", root);
        engine.Evaluate(WideWalkScript);

        // 20 children is far more than the 8-slot recent-wrapper ring holds, yet every node is read exactly
        // once: the memo is per wrapper and unbounded by the ring's capacity
        root.Probes.Should().Be(20);
        children.Should().OnlyContain(c => c.Probes == 1);
    }

    [Fact]
    public void UndeclaredWideWalkRereadsEveryNode()
    {
        var (root, children) = BuildWideGraph();

        var engine = new Engine();
        engine.SetValue("root", root);
        engine.Evaluate(WideWalkScript);

        // control: without the declaration each pass reaches the host again — this is the friction
        root.Probes.Should().Be(40);
        children.Should().OnlyContain(c => c.Probes == 2);
    }

    [Fact]
    public void WriteThroughEvictsTheMemoAndTheNextReadSeesTheNewValue()
    {
        var target = new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" };

        var engine = ImmutableCrossingEngine(typeof(Dictionary<string, object>));
        engine.SetValue("record", target);

        engine.Evaluate("record.country").AsString().Should().Be("FI");

        // the host broke the promise by letting script write; the write still reaches the CLR dictionary and
        // the memo must not answer with the pre-write value afterwards
        engine.Evaluate("record.country = 'SE'; record.country").AsString().Should().Be("SE");
        target["country"].Should().Be("SE");
    }

    [Fact]
    public void WriteThroughEvictsTheMemoForAReflectedMember()
    {
        var host = new MutableHost();

        var engine = ImmutableCrossingEngine(typeof(MutableHost));
        engine.SetValue("host", host);

        engine.Evaluate("host.name").AsString().Should().Be("first");
        engine.Evaluate("host.name = 'second'; host.name").AsString().Should().Be("second");
        host.name.Should().Be("second");
    }

    public sealed class MutableHost
    {
        public string name { get; set; } = "first";
    }

    [Fact]
    public void ImmutableCrossingMemoizesAReflectedMemberWithoutReinvokingTheGetter()
    {
        var host = new CountingHost();

        var engine = ImmutableCrossingEngine(typeof(CountingHost));
        engine.SetValue("host", host);

        engine.Evaluate("for (var i = 0; i < 20; i++) { host.child.x; }");

        host.Reads.Should().Be(1);
    }

    public sealed class CountingHost
    {
        private readonly PropertyMemoChild _child = new();

        public int Reads { get; private set; }

        public PropertyMemoChild child
        {
            get
            {
                Reads++;
                return _child;
            }
        }
    }

    [Fact]
    public void EnumerationStaysLiveFromTheTarget()
    {
        var target = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["a"] = 1,
            ["b"] = 2,
        };

        var engine = ImmutableCrossingEngine(typeof(Dictionary<string, object>));
        engine.SetValue("record", target);

        engine.Evaluate("record.a");
        engine.Evaluate("Object.keys(record).join(',')").AsString().Should().Be("a,b");

        // a key added CLR-side afterwards is still enumerated: the key set is never memoized
        target["c"] = 3;
        engine.Evaluate("Object.keys(record).join(',')").AsString().Should().Be("a,b,c");
        engine.Evaluate("JSON.stringify(record)").AsString().Should().Be("""{"a":1,"b":2,"c":3}""");
        engine.Evaluate("'b' in record").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.values(record).join(',')").AsString().Should().Be("1,2,3");
    }

    [Fact]
    public void JsonStringifyOfADeclaredGraphIsUnchanged()
    {
        var undeclared = new Engine();
        undeclared.SetValue("record", BuildCustomerGraph());
        var expected = undeclared.Evaluate("JSON.stringify(record)").AsString();

        var declared = ImmutableCrossingEngine(typeof(Dictionary<string, object>));
        declared.SetValue("record", BuildCustomerGraph());
        declared.Evaluate("record.value.customer.country");

        declared.Evaluate("JSON.stringify(record)").AsString().Should().Be(expected);
    }

    [Fact]
    public void FreezeStillWinsOverTheMemo()
    {
        var target = new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" };

        var engine = ImmutableCrossingEngine(typeof(Dictionary<string, object>));
        engine.SetValue("record", target);

        engine.Evaluate("record.country").AsString().Should().Be("FI");
        engine.Evaluate("Object.freeze(record)");

        // the frozen descriptor lives in the wrapper's own property store, which outranks the memo
        engine.Evaluate("Object.isFrozen(record)").AsBoolean().Should().BeTrue();
        engine.Evaluate("record.country").AsString().Should().Be("FI");
        engine.Evaluate("record.country = 'SE'; record.country").AsString().Should().Be("FI");
    }

    [Fact]
    public void DefinePropertyStillWinsOverTheMemo()
    {
        var target = new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" };

        var engine = ImmutableCrossingEngine(typeof(Dictionary<string, object>));
        engine.SetValue("record", target);

        engine.Evaluate("record.country").AsString().Should().Be("FI");
        engine.Evaluate("Object.defineProperty(record, 'country', { value: 'SE', configurable: true })");
        engine.Evaluate("record.country").AsString().Should().Be("SE");
    }

    [Fact]
    public void DeleteEvictsTheMemo()
    {
        var target = new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" };

        var engine = ImmutableCrossingEngine(typeof(Dictionary<string, object>));
        engine.SetValue("record", target);

        engine.Evaluate("record.country").AsString().Should().Be("FI");
        engine.Evaluate("delete record.country");

        target.ContainsKey("country").Should().BeFalse();
        engine.Evaluate("record.country").IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void ConverterClaimsTheChildValueOnceAndTheMemoKeepsTheConvertedResult()
    {
        var converter = new CountingUriConverter();
        var target = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["home"] = new Uri("https://example.org/"),
        };

        var engine = new Engine(options => options
            .AddObjectConverter(converter, typeof(Uri))
            .AddImmutableCrossing(typeof(Dictionary<string, object>)));
        engine.SetValue("record", target);

        engine.Evaluate("for (var i = 0; i < 10; i++) { record.home; } record.home")
            .AsString().Should().Be("https://example.org/");

        // the converter is honoured on the first read and the memo keeps its result, not the raw CLR value
        converter.Conversions.Should().Be(1);
    }

    [Fact]
    public void TwoEnginesOverTheSameTargetKeepIndependentMemos()
    {
        var target = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });

        var first = ImmutableCrossingEngine(typeof(CountingDictionary));
        first.SetValue("record", target);
        first.Evaluate("record.country; record.country");
        target.Probes.Should().Be(1);

        // a second engine has never read this object; nothing may answer for it from the first engine's memo
        var second = ImmutableCrossingEngine(typeof(CountingDictionary));
        second.SetValue("record", target);
        second.Evaluate("record.country; record.country");
        target.Probes.Should().Be(2);
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEngines()
    {
        var options = new Options().AddImmutableCrossing(typeof(CountingDictionary));

        var first = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
        var second = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "SE" });

        var engineA = new Engine(options);
        engineA.SetValue("record", first);
        var engineB = new Engine(options);
        engineB.SetValue("record", second);

        engineA.Evaluate("record.country; record.country").AsString().Should().Be("FI");
        engineB.Evaluate("record.country; record.country").AsString().Should().Be("SE");

        // both honour the declaration, and neither answered from the other's memo
        first.Probes.Should().Be(1);
        second.Probes.Should().Be(1);
    }

    [Fact]
    public void ImmutableCrossingSupersedesTheRecentWrapperCacheOptOut()
    {
        var leaf = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["customer"] = leaf });

        var engine = new Engine(options =>
        {
            options.Interop.CacheRecentObjectWrappers = false;
            options.AddImmutableCrossing(typeof(CountingDictionary));
        });
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 10; i++) { record.customer.country; }");

        root.Probes.Should().Be(1);
        leaf.Probes.Should().Be(1);
    }

    [Fact]
    public void ImmutableCrossingComposesWithIdentityTracking()
    {
        var leaf = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["customer"] = leaf });

        var engine = new Engine(options =>
        {
            options.Interop.TrackObjectWrapperIdentity = true;
            options.AddImmutableCrossing(typeof(CountingDictionary));
        });
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 10; i++) { record.customer.country; }");

        root.Probes.Should().Be(1);
        leaf.Probes.Should().Be(1);
    }

    [Fact]
    public void AddImmutableCrossingValidatesItsArguments()
    {
        var noTypes = () => new Options().AddImmutableCrossing();
        noTypes.Should().Throw<ArgumentException>();

        var nullEntry = () => new Options().AddImmutableCrossing(typeof(Uri), null);
        nullEntry.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnOpenGenericDeclarationClaimsNothing()
    {
        var root = new CountingDictionary(new Dictionary<string, object>(StringComparer.Ordinal) { ["country"] = "FI" });

        // a wrong claim here would serve stale reads, so the filter never guesses in the permissive direction
        var engine = ImmutableCrossingEngine(typeof(IReadOnlyDictionary<,>));
        engine.SetValue("record", root);

        engine.Evaluate("for (var i = 0; i < 5; i++) { record.country; }");

        root.Probes.Should().Be(5);
    }

#if NET
    [Fact]
    public void ImmutableCrossingRemovesThePerReadAllocationOfANestedWalk()
    {
        var script = Engine.PrepareScript("var s = 0; for (var i = 0; i < 200; i++) { s = record.value.customer.country; } 1");

        var declared = Measure(ImmutableCrossingEngine(typeof(Dictionary<string, object>)));
        var undeclared = Measure(new Engine());
        var loopBaseline = MeasureBaseline();

        // the whole per-read cost is gone: the declared walk allocates what the bare loop does, while the
        // undeclared one pays a conversion per level per read on top (measured 6816 vs 416 bytes)
        declared.Should().BeLessThan(loopBaseline + 200,
            $"200 memoized nested walks allocated {declared} bytes against a {loopBaseline} byte loop baseline");
        undeclared.Should().BeGreaterThan(declared * 4,
            $"the undeclared control allocated {undeclared} bytes, the declared engine {declared}");

        long Measure(Engine engine)
        {
            engine.SetValue("record", BuildCustomerGraph());
            return MeasureSteadyStateAllocation(engine, script);
        }

        long MeasureBaseline()
        {
            return MeasureSteadyStateAllocation(
                new Engine(),
                Engine.PrepareScript("var s = 0; for (var i = 0; i < 200; i++) { s = i; } 1"));
        }
    }

    /// <summary>
    /// Allocation of one steady-state evaluation. Two warm-up runs are required, not one: the interpreter's
    /// handler-tree caches only engage on the second evaluation of a script on a given engine, so measuring
    /// the second run reports cache population rather than steady state.
    /// </summary>
    private static long MeasureSteadyStateAllocation(Engine engine, Prepared<Script> script)
    {
        engine.Evaluate(script);
        engine.Evaluate(script);

        var before = GC.GetAllocatedBytesForCurrentThread();
        engine.Evaluate(script);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
#endif
}
