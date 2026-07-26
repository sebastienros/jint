#nullable enable
using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Exercises reading values out of dictionary-shaped host objects, which go through a compiled
/// <c>TryGetValue</c> delegate where one can be built and through reflection everywhere else. Every
/// assertion here is written against the behavior that predates the compiled lane — a hit, a miss, a stored
/// null and a value-typed value must be indistinguishable between the two, in particular a miss on a
/// value-typed dictionary must never surface the default value the out parameter carries.
/// </summary>
public class InteropDictionaryValueAccessTests
{
    private static Engine CreateEngine(object host)
    {
        var engine = new Engine();
        engine.SetValue("d", host);
        return engine;
    }

    #region 1. reference-typed values

    [Fact]
    public void ReadsAStringValue()
    {
        var engine = CreateEngine(new Dictionary<string, string> { ["a"] = "one" });

        engine.Evaluate("d.a").Should().Be("one");
        engine.Evaluate("d['a']").Should().Be("one");
    }

    [Fact]
    public void MissingKeyIsUndefined()
    {
        var engine = CreateEngine(new Dictionary<string, string> { ["a"] = "one" });

        engine.Evaluate("typeof d.missing").Should().Be("undefined");
        engine.Evaluate("d.missing === undefined").Should().Be(true);
        engine.Evaluate("'missing' in d").Should().Be(false);
        engine.Evaluate("'a' in d").Should().Be(true);
    }

    [Fact]
    public void StoredNullIsNull()
    {
        var engine = CreateEngine(new Dictionary<string, string?> { ["a"] = null });

        engine.Evaluate("d.a").Should().Be(JsValue.Null);
        engine.Evaluate("d.a === null").Should().Be(true);
        engine.Evaluate("'a' in d").Should().Be(true);
    }

    [Fact]
    public void ReadsAfterMutation()
    {
        var dictionary = new Dictionary<string, string>();
        var engine = CreateEngine(dictionary);

        engine.Evaluate("typeof d.a").Should().Be("undefined");
        dictionary["a"] = "added";
        engine.Evaluate("d.a").Should().Be("added");
        dictionary["a"] = "changed";
        engine.Evaluate("d.a").Should().Be("changed");
        dictionary.Remove("a");
        engine.Evaluate("typeof d.a").Should().Be("undefined");
    }

    #endregion

    #region 2. value-typed values

    [Fact]
    public void ReadsAnIntValue()
    {
        var engine = CreateEngine(new Dictionary<string, int> { ["a"] = 1, ["zero"] = 0 });

        engine.Evaluate("d.a").Should().Be(1);
        engine.Evaluate("d.zero").Should().Be(0);
    }

    [Fact]
    public void MissingValueTypedKeyIsUndefinedNotTheDefaultValue()
    {
        var engine = CreateEngine(new Dictionary<string, int> { ["a"] = 1 });

        engine.Evaluate("typeof d.missing").Should().Be("undefined");
        engine.Evaluate("d.missing === 0").Should().Be(false);
    }

    [Fact]
    public void ReadsAStructValue()
    {
        var engine = CreateEngine(new Dictionary<string, DateTimeOffset>
        {
            ["a"] = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero),
        });

        engine.Evaluate("d.a.getUTCFullYear()").Should().Be(2020);
        engine.Evaluate("typeof d.missing").Should().Be("undefined");
    }

    [Fact]
    public void ReadsANullableValue()
    {
        var engine = CreateEngine(new Dictionary<string, int?> { ["a"] = 1, ["b"] = null });

        engine.Evaluate("d.a").Should().Be(1);
        engine.Evaluate("d.b").Should().Be(JsValue.Null);
        engine.Evaluate("typeof d.missing").Should().Be("undefined");
    }

    #endregion

    #region 3. dictionary shapes

    [Fact]
    public void ReadsThroughAReadOnlyDictionary()
    {
        IReadOnlyDictionary<string, string> host = new Dictionary<string, string> { ["a"] = "one" };
        var engine = CreateEngine(host);

        engine.Evaluate("d.a").Should().Be("one");
        engine.Evaluate("typeof d.missing").Should().Be("undefined");
    }

    [Fact]
    public void ReadsThroughAnExplicitInterfaceImplementation()
    {
        var engine = CreateEngine(new ExplicitDictionary());

        engine.Evaluate("d.a").Should().Be("one");
        engine.Evaluate("typeof d.missing").Should().Be("undefined");
    }

    [Fact]
    public void ReadsFromANonStringKeyedDictionary()
    {
        var engine = CreateEngine(new Dictionary<int, string> { [1] = "one" });

        engine.Evaluate("d[1]").Should().Be("one");
        engine.Evaluate("typeof d[2]").Should().Be("undefined");
    }

    [Fact]
    public void ReadsFromANonPublicValueTypedDictionary()
    {
        // the compiled lane declines when the closed generic interface is not visible, the reflection
        // fallback still has to answer
        var engine = CreateEngine(new Dictionary<string, Hidden> { ["a"] = new Hidden { Value = 3 } });

        engine.Evaluate("d.a.Value").Should().Be(3);
        engine.Evaluate("typeof d.missing").Should().Be("undefined");
    }

    [Fact]
    public void TraversesNestedDictionaries()
    {
        var host = new Dictionary<string, object>
        {
            ["b"] = new Dictionary<string, object>
            {
                ["c"] = new Dictionary<string, object>
                {
                    ["d"] = 42,
                },
            },
        };
        var engine = CreateEngine(host);

        engine.Evaluate("d.b.c.d").Should().Be(42);
        engine.Evaluate("typeof d.b.c.missing").Should().Be("undefined");
    }

    [Fact]
    public void EnumerationStillWorks()
    {
        var engine = CreateEngine(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

        engine.Evaluate("Object.keys(d).join(',')").Should().Be("a,b");
        engine.Evaluate("JSON.stringify(d)").Should().Be("{\"a\":1,\"b\":2}");
    }

    [Fact]
    public void WritesAndDeletesStillWork()
    {
        var dictionary = new Dictionary<string, int> { ["a"] = 1 };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("d.b = 2");
        dictionary["b"].Should().Be(2);
        engine.Evaluate("d.b").Should().Be(2);

        engine.Evaluate("delete d.b");
        dictionary.ContainsKey("b").Should().BeFalse();
        engine.Evaluate("typeof d.b").Should().Be("undefined");
    }

    #endregion

    #region 4. throwing implementations

    [Fact]
    public void KeyNotFoundFromTheImplementationIsAMiss()
    {
        var engine = CreateEngine(new ThrowingDictionary(new KeyNotFoundException()));

        engine.Evaluate("typeof d.anything").Should().Be("undefined");
    }

    [Fact]
    public void OtherFailuresStillSurface()
    {
        var engine = CreateEngine(new ThrowingDictionary(new InvalidOperationException("boom")));

        var act = () => engine.Evaluate("d.anything");
        act.Should().Throw<Exception>();
    }

    #endregion

    #region 5. the compiled reader itself

#if NET8_0_OR_GREATER

    [Fact]
    public void CompiledReaderIsBuiltForAVisibleDictionary()
    {
        var getter = Jint.Runtime.Interop.Reflection.CompiledDictionaryAccessor.GetValueGetter(
            typeof(IDictionary<string, string>).GetMethod("TryGetValue"));

        // this is the engagement probe: the behavioral tests above pass either way, only this says the
        // reflection Invoke was actually replaced
        getter.Should().NotBeNull();

        var dictionary = new Dictionary<string, string> { ["a"] = "one" };
        getter!(dictionary, "a", out var value).Should().BeTrue();
        value.Should().Be("one");
        getter(dictionary, "missing", out value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void CompiledReaderBoxesValueTypedValues()
    {
        var getter = Jint.Runtime.Interop.Reflection.CompiledDictionaryAccessor.GetValueGetter(
            typeof(IReadOnlyDictionary<string, int>).GetMethod("TryGetValue"));
        getter.Should().NotBeNull();

        IReadOnlyDictionary<string, int> dictionary = new Dictionary<string, int> { ["a"] = 1 };
        getter!(dictionary, "a", out var value).Should().BeTrue();
        value.Should().Be(1);

        // a miss still fills the out parameter with default(TValue), exactly as reflection did — the caller
        // is what has to ignore it
        getter(dictionary, "missing", out value).Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact]
    public void CompiledReaderDeclinesWhenTheClosedGenericIsNotVisible()
    {
        var method = typeof(IDictionary<,>).MakeGenericType(typeof(string), typeof(Hidden)).GetMethod("TryGetValue");

        Jint.Runtime.Interop.Reflection.CompiledDictionaryAccessor.GetValueGetter(method).Should().BeNull();
    }

    [Fact]
    public void CompiledReaderDeclinesWithoutAMethod()
    {
        Jint.Runtime.Interop.Reflection.CompiledDictionaryAccessor.GetValueGetter(null).Should().BeNull();
    }

#endif

    #endregion

    #region hosts

    private struct Hidden
    {
        public int Value { get; set; }
    }

    private sealed class ExplicitDictionary : IReadOnlyDictionary<string, string>
    {
        private readonly Dictionary<string, string> _inner = new() { ["a"] = "one" };

        string IReadOnlyDictionary<string, string>.this[string key] => _inner[key];

        IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => _inner.Keys;

        IEnumerable<string> IReadOnlyDictionary<string, string>.Values => _inner.Values;

        int IReadOnlyCollection<KeyValuePair<string, string>>.Count => _inner.Count;

        bool IReadOnlyDictionary<string, string>.ContainsKey(string key) => _inner.ContainsKey(key);

        bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value) => _inner.TryGetValue(key, out value!);

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => _inner.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    public sealed class ThrowingDictionary : IReadOnlyDictionary<string, string>
    {
        private readonly Exception _exception;

        public ThrowingDictionary(Exception exception) => _exception = exception;

        public string this[string key] => throw _exception;

        public IEnumerable<string> Keys => [];

        public IEnumerable<string> Values => [];

        public int Count => 0;

        // only TryGetValue misbehaves, so the failure observed is the one under test
        public bool ContainsKey(string key) => false;

        public bool TryGetValue(string key, out string value) => throw _exception;

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
