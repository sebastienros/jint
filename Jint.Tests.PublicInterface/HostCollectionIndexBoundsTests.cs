#nullable enable

using System.Collections;
using System.Globalization;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host collection that has a count and an indexer of its own, and none of the three interfaces that would
/// give it an array-like <em>view</em>. It is wrapped plainly, so every index-shaped key resolves the
/// reflected indexer — which takes whatever index it parsed out of the key straight to the collection.
/// </summary>
/// <remarks>
/// <para>
/// #3384 gave an <c>ArrayLikeWrapper</c> ownership of every index-shaped key, so an out-of-range write is
/// refused rather than handed over. The refusal lives in the array-like view, and this shape does not get one:
/// a plain <see cref="Jint.Runtime.Interop.ObjectWrapper"/> still went to the indexer and let the collection's
/// own <see cref="ArgumentOutOfRangeException"/> out of <c>Evaluate</c>, where neither a script
/// <c>try</c>/<c>catch</c> nor a host <c>catch (JavaScriptException)</c> can see it (#3422).
/// </para>
/// <para>
/// The guard cannot be the array-like one, because a plain wrapper's indexer lane also serves
/// <c>Dictionary&lt;int, string&gt;</c>, where <c>d[99] = "x"</c> is a legitimate add. Both halves are pinned
/// here.
/// </para>
/// </remarks>
public class HostCollectionIndexBoundsTests
{
    /// <summary>Keys naming a position a three-element window cannot address, in both spellings.</summary>
    public static IEnumerable<string> UnaddressableIndices => ["3", "'3'", "10", "-1", "'-1'", "'08'", "'+3'"];

    [TestCaseSource(nameof(UnaddressableIndices))]
    public void ReadingAnIndexTheCollectionCannotAddressAnswersUndefined(string key)
    {
        var engine = CreateEngine(out _);

        engine.Evaluate($"x[{key}]").Should().Be(JsValue.Undefined,
            "there is no element at {0}, and a hole reads undefined rather than raising the collection's own exception", key);
    }

    [TestCaseSource(nameof(UnaddressableIndices))]
    public void WritingAnIndexTheCollectionCannotAddressIsTheOrdinarySetRefusal(string key)
    {
        var engine = CreateEngine(out var window);

        engine.Execute($"x[{key}] = 9");
        engine.Evaluate($"Reflect.set(x, {key}, 9)").AsBoolean().Should().BeFalse();

        var thrown = Caught.Exception(() => engine.Execute($"'use strict'; x[{key}] = 9;"));
        thrown.Should().BeOfType<JavaScriptException>("a refused [[Set]] in strict mode is a TypeError");
        ((JavaScriptException) thrown!).Error.Get("name").AsString().Should().Be("TypeError");

        window.Should().Equal(new[] { 1, 2, 3 }, "nothing reached the collection");
    }

    [TestCaseSource(nameof(UnaddressableIndices))]
    public void EveryExistenceLaneAgreesThatSuchAnIndexIsAbsent(string key)
    {
        var engine = CreateEngine(out _);

        engine.Evaluate($"[({key} in x), x.hasOwnProperty({key}), x.propertyIsEnumerable({key}), Object.getOwnPropertyDescriptor(x, {key}) !== undefined].join(',')")
            .AsString().Should().Be("false,false,false,false",
                "{0} is not a position of the collection, so no lane may report it as one", key);
    }

    [TestCaseSource(nameof(UnaddressableIndices))]
    public void DeletingSuchAnIndexSucceedsWithoutTouchingTheCollection(string key)
    {
        var engine = CreateEngine(out var window);

        engine.Evaluate($"delete x[{key}]").AsBoolean().Should().BeTrue("OrdinaryDelete returns true for a property that is not there");
        window.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// The contrast that says the guard is a bound and not a ban on integer keys: every position the
    /// collection does have still reads and writes through its own indexer.
    /// </summary>
    [TestCase("0", 1d)]
    [TestCase("'0'", 1d)]
    [TestCase("2", 3d)]
    [TestCase("'2'", 3d)]
    public void APositionTheCollectionHasIsUnaffected(string key, double expected)
    {
        var engine = CreateEngine(out var window);

        engine.Evaluate($"x[{key}]").AsNumber().Should().Be(expected);
        engine.Evaluate($"{key} in x").AsBoolean().Should().BeTrue();
        engine.Evaluate($"x.hasOwnProperty({key})").AsBoolean().Should().BeTrue();

        engine.Execute($"x[{key}] = 9");
        window[int.Parse(key.Trim('\''), CultureInfo.InvariantCulture)].Should().Be(9);
    }

    /// <summary>Named CLR members of the same target keep resolving; only index-shaped keys are the view's.</summary>
    [Test]
    public void NamedMembersAreUnaffected()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("x.Count").AsNumber().Should().Be(3);
        engine.Evaluate("x.length").AsNumber().Should().Be(3);
        engine.Evaluate("Array.prototype.join.call(x, '-')").AsString().Should().Be("1-2-3");
    }

    /// <summary>
    /// The other half of the trade-off. A wrapper's indexer lane also serves a dictionary, where a key outside
    /// what it currently holds is a legitimate add and refusing it would be the bug. `IsArrayLike` is what
    /// separates the two, and it is `false` for a dictionary.
    /// </summary>
    [Test]
    public void AnIntegerKeyedDictionaryStillAdds()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
        });
        var dictionary = new Dictionary<int, string> { [1] = "one" };
        engine.SetValue("x", dictionary);

        engine.Execute("x[99] = 'ninety-nine'");

        dictionary.Should().ContainKey(99).WhoseValue.Should().Be("ninety-nine");
        engine.Evaluate("x[99]").AsString().Should().Be("ninety-nine");
        engine.Evaluate("x[1]").AsString().Should().Be("one");
    }

    /// <summary>
    /// A string-keyed indexer on an array-like target is not the collection's positions, and the check reads
    /// the resolved accessor's index parameter rather than guessing from the key, so it stays out of the way.
    /// <c>headers["3"]</c> is the header named <c>3</c>, whatever the count happens to be.
    /// </summary>
    [Test]
    public void AStringKeyedIndexerOnAnArrayLikeTargetStillAnswers()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("x", new NamedValues { ["3"] = "the third", ["etag"] = "abc" });

        engine.Evaluate("x['3']").AsString().Should().Be("the third");
        engine.Evaluate("x['etag']").AsString().Should().Be("abc");
    }

    private static Engine CreateEngine(out Window window)
    {
        window = new Window(1, 2, 3);
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("x", window);
        return engine;
    }

    /// <summary>
    /// Bounded and indexed, and neither an <see cref="IList{T}"/>, an <see cref="IReadOnlyList{T}"/> nor a
    /// non-generic <see cref="IList"/> — so no array-like view is built for it and every part of that is
    /// load-bearing. <see cref="IReadOnlyCollection{T}"/> is what gives it a <c>Count</c>, and the declared
    /// indexer is what the reflected member lane resolves for an index-shaped key.
    /// </summary>
    public sealed class Window : IReadOnlyCollection<int>
    {
        private readonly List<int> _items;

        public Window(params int[] items) => _items = [.. items];

        public int this[int index] { get => _items[index]; set => _items[index] = value; }

        public int Count => _items.Count;

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }

    /// <summary>
    /// An array-like target whose indexer is keyed by <see cref="string"/>, the shape a
    /// <c>NameValueCollection</c> has.
    /// </summary>
    public sealed class NamedValues : IReadOnlyCollection<string>
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string this[string name]
        {
            get => _values.TryGetValue(name, out var value) ? value : string.Empty;
            set => _values[name] = value;
        }

        public int Count => _values.Count;

        public IEnumerator<string> GetEnumerator() => _values.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _values.Values.GetEnumerator();
    }
}
