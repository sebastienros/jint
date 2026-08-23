using System.Collections;

namespace Jint.Tests.Runtime.ExtensionMethods;

/// <summary>
/// <see cref="EnumerableConversionMode.Snapshot"/> gives a sequence that has no count and no indexer the
/// array-like shape Array.prototype needs, the only way such a sequence can have one
/// (https://github.com/sebastienros/jint/issues/2987). Everything that already had a count keeps its live
/// exposure, and the default mode changes nothing.
/// </summary>
public class EnumerableSnapshotTests
{
    private static Engine CreateEngine(bool snapshot, bool withExtensions = true)
    {
        return new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            if (withExtensions)
            {
                options.AddExtensionMethods(typeof(ShadowingMapExtensions));
            }

            if (snapshot)
            {
                options.Interop.EnumerableConversion = EnumerableConversionMode.Snapshot;
            }
        });
    }

    [Fact]
    public void SnapshotMakesTheIssueReproWork()
    {
        var engine = CreateEngine(snapshot: true);
        engine.SetValue("coll", new List<string> { "Hello", "World" }.Select(y => y));

        // the receiver is array-like now, so the registered 'map' defers to Array.prototype.map (#2976) and
        // the projection is a real JS array carrying the native 'includes'
        engine.Evaluate("coll.map(x => x).includes('Hello')").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.map(x => x).includes('Nope')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void SnapshotIsArrayLikeButNotAnArray()
    {
        var engine = CreateEngine(snapshot: true, withExtensions: false);
        engine.SetValue("coll", new List<string> { "Hello", "World" }.Where(x => x.Length > 0));

        engine.Evaluate("Array.isArray(coll)").AsBoolean().Should().BeFalse();
        engine.Evaluate("coll instanceof Array").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.length").AsNumber().Should().Be(2);
        engine.Evaluate("coll[0]").AsString().Should().Be("Hello");
        engine.Evaluate("coll[5]").Should().Be(Native.JsValue.Undefined);
        engine.Evaluate("coll.join('-')").AsString().Should().Be("Hello-World");
        engine.Evaluate("coll.includes('World')").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.map(x => x + '!').join()").AsString().Should().Be("Hello!,World!");
        engine.Evaluate("JSON.stringify(coll)").AsString().Should().Be("""["Hello","World"]""");
    }

    [Fact]
    public void SnapshotIsFixedSize()
    {
        var engine = CreateEngine(snapshot: true, withExtensions: false);
        engine.SetValue("coll", new List<string> { "Hello" }.Where(_ => true));

        // the snapshot is an array view, so it behaves like every other fixed-size CLR array view
        Invoking(() => engine.Evaluate("coll.push('World')")).Should().Throw<Exception>();
    }

    [Fact]
    public void SequenceIsEnumeratedExactlyOnce()
    {
        var engine = CreateEngine(snapshot: true, withExtensions: false);
        var source = new CountingEnumerable(["Hello", "World"]);
        engine.SetValue("coll", source.Where(_ => true));

        source.EnumerationCount.Should().Be(1, "the snapshot is taken when the sequence crosses into script");

        engine.Evaluate("coll.length").AsNumber().Should().Be(2);
        engine.Evaluate("coll.join()").AsString().Should().Be("Hello,World");
        engine.Evaluate("[...coll].length").AsNumber().Should().Be(2);

        source.EnumerationCount.Should().Be(1, "every later read comes from the snapshot");
    }

    [Fact]
    public void LazyIsTheDefaultAndUnchanged()
    {
        var engine = CreateEngine(snapshot: false);
        var source = new CountingEnumerable(["Hello", "World"]);
        engine.SetValue("coll", source.Where(_ => true));

        source.EnumerationCount.Should().Be(0, "nothing is enumerated until script asks");

        engine.Evaluate("typeof coll.length").AsString().Should().Be("undefined");
        engine.Evaluate("typeof coll.includes").AsString().Should().Be("undefined");
        engine.Evaluate("typeof coll.map").AsString().Should().Be("function", "the registered extension supplies it");
        engine.Evaluate("Array.from(coll).join()").AsString().Should().Be("Hello,World");
    }

    [Fact]
    public void CountedCollectionsAreNotSnapshotted()
    {
        var engine = CreateEngine(snapshot: true, withExtensions: false);
        var set = new HashSet<string> { "Hello" };
        var list = new List<string> { "Hello" };
        engine.SetValue("set", set);
        engine.SetValue("list", list);

        // a HashSet<T> has a Count and already carries Array.prototype; it must stay the live collection
        engine.Evaluate("set.length").AsNumber().Should().Be(1);
        set.Add("World");
        engine.Evaluate("set.length").AsNumber().Should().Be(2);

        engine.Evaluate("list.push('World'); list.length").AsNumber().Should().Be(2);
        list.Should().Equal("Hello", "World");
    }

    [Fact]
    public void DictionariesAreNotSnapshotted()
    {
        var engine = CreateEngine(snapshot: true, withExtensions: false);
        var dictionary = new Dictionary<string, int> { ["a"] = 1 };
        engine.SetValue("dict", dictionary);

        // a dictionary enumerates as a sequence of pairs, which must not make it look like a list
        engine.Evaluate("dict.a").AsNumber().Should().Be(1);
        engine.Evaluate("typeof dict.length").AsString().Should().Be("undefined");
        dictionary["b"] = 2;
        engine.Evaluate("dict.b").AsNumber().Should().Be(2);
    }

    [Fact]
    public void NonGenericSequenceIsSnapshottedAsObjects()
    {
        var engine = CreateEngine(snapshot: true, withExtensions: false);
        engine.SetValue("coll", new NonGenericSequence());

        engine.Evaluate("coll.length").AsNumber().Should().Be(2);
        engine.Evaluate("coll.join('-')").AsString().Should().Be("Hello-World");
    }

    private sealed class CountingEnumerable(string[] items) : IEnumerable<string>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            EnumerationCount++;
            return ((IEnumerable<string>) items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class NonGenericSequence : IEnumerable
    {
        public IEnumerator GetEnumerator() => new[] { "Hello", "World" }.GetEnumerator();
    }
}
