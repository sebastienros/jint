#nullable enable

using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what <c>delete</c> does to a property a script defined on a wrapped CLR collection itself (#4457).
/// </summary>
/// <remarks>
/// <para>
/// A collection view refuses to change the collection when <c>Options.Interop.AllowWrite</c> is off, when
/// the collection declares itself read-only, and when the view is non-extensible. That refusal is about the
/// collection's elements and entries. A property the script put on the wrapper is an ordinary own property,
/// so <see href="https://tc39.es/ecma262/#sec-ordinarydelete">OrdinaryDelete</see> decides: removed if it is
/// configurable, kept (and a strict-mode <c>TypeError</c>) if it is not. Extensibility does not enter into it.
/// </para>
/// <para>
/// The expected values are what Node gives for the same script on an ordinary array, and a plain
/// <c>ObjectWrapper</c> (the three control shapes) already answered them before the fix.
/// </para>
/// </remarks>
public class HostCollectionScriptPropertyDeleteTests
{
    /// <summary>
    /// The collection views, one per wrapper class plus the read-only and dictionary-shaped variants, then the
    /// plain wrappers they must agree with.
    /// </summary>
    private static readonly string[] _shapes =
    [
        "List",
        "ReadOnlyCollection",
        "Array",
        "ImmutableArray",
        "ArrayList",
        "ReadOnlyList",
        "JObject",
        "HashSet",
        "Dictionary",
        "Object",
    ];

    /// <summary>
    /// The views that have elements an index can name; the dictionary-shaped view and the plain wrappers do not.
    /// </summary>
    private static readonly string[] _indexedShapes =
    [
        "List",
        "ReadOnlyCollection",
        "Array",
        "ImmutableArray",
        "ArrayList",
        "ReadOnlyList",
    ];

    private static readonly string[] _keys =
    [
        "Symbol.iterator",
        "Symbol('tag')",
        "'tag'",
    ];

    public static TestCases<string, bool, string> ShapesSettingsAndKeys
    {
        get
        {
            var data = new TestCases<string, bool, string>();
            foreach (var shape in _shapes)
            {
                foreach (var allowWrite in new[] { false, true })
                {
                    foreach (var key in _keys)
                    {
                        data.Add(shape, allowWrite, key);
                    }
                }
            }

            return data;
        }
    }

    public static TestCases<string, bool> ShapesAndSettings
    {
        get
        {
            var data = new TestCases<string, bool>();
            foreach (var shape in _shapes)
            {
                data.Add(shape, false);
                data.Add(shape, true);
            }

            return data;
        }
    }

    public static TestCases<string, bool, string> IndexedShapesSettingsAndIntegrityLevels
    {
        get
        {
            var data = new TestCases<string, bool, string>();
            foreach (var shape in _indexedShapes)
            {
                foreach (var allowWrite in new[] { false, true })
                {
                    data.Add(shape, allowWrite, "preventExtensions");
                    data.Add(shape, allowWrite, "freeze");
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Deleting an element, which this change leaves alone. Under <c>AllowWrite</c> a growable list resets the
    /// slot to the element type's default and answers <see langword="true"/>; every other view refuses.
    /// </summary>
    public static TestCases<string, bool, bool, string> ElementDeletes => new()
    {
        { "List", false, false, "1,2" },
        { "ReadOnlyCollection", false, false, "1,2" },
        { "Array", false, false, "1,2" },
        { "ImmutableArray", false, false, "1,2" },
        { "ArrayList", false, false, "1,2" },
        { "ReadOnlyList", false, false, "1,2" },
        { "List", true, true, "0,2" },
        { "ReadOnlyCollection", true, false, "1,2" },
        { "Array", true, false, "1,2" },
        { "ImmutableArray", true, false, "1,2" },
        { "ArrayList", true, true, "null,2" },
        { "ReadOnlyList", true, false, "1,2" },
    };

    [TestCaseSource(nameof(ShapesSettingsAndKeys))]
    public void AConfigurableScriptPropertyIsDeleted(string shape, bool allowWrite, string key)
    {
        foreach (var strict in new[] { false, true })
        {
            var (host, read) = CreateShape(shape);
            var before = read();
            var engine = CreateEngine(host, allowWrite);

            var outcome = engine.Evaluate(Run(strict,
                $$"""
                const key = {{key}};
                const value = function* () { yield 9; };
                Object.defineProperty(host, key, { value, configurable: true });
                const deleted = delete host[key];
                const after = Object.getOwnPropertyDescriptor(host, key);
                return deleted + ',' + (after === undefined || after.value !== value);
                """)).AsString();

            outcome.Should().Be("true,true", "OrdinaryDelete removes a configurable own property ({0}, strict: {1})", key, strict);
            read().Should().Be(before, "deleting the script's property leaves the collection alone");
        }
    }

    [TestCaseSource(nameof(ShapesSettingsAndKeys))]
    public void ANonConfigurableScriptPropertyStaysAndStrictModeThrows(string shape, bool allowWrite, string key)
    {
        var (host, read) = CreateShape(shape);
        var before = read();
        var engine = CreateEngine(host, allowWrite);
        engine.Execute($"var key = {key}; Object.defineProperty(host, key, {{ value: 1, configurable: false }});");

        engine.Evaluate("delete host[key]").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, key).value").Should().Be(1);
        engine.Evaluate(Run(strict: true, "delete host[key]; return 'no-throw';")).AsString()
            .Should().Be("TypeError", "a strict-mode delete of a non-configurable property throws");
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, key).value").Should().Be(1);
        read().Should().Be(before);
    }

    /// <summary>
    /// OrdinaryDelete does not read <c>[[Extensible]]</c>: <c>Object.preventExtensions</c> stops new properties
    /// from being added, not configurable ones from being removed.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndSettings))]
    public void ANonExtensibleWrapperStillDeletesAConfigurableScriptProperty(string shape, bool allowWrite)
    {
        var (host, read) = CreateShape(shape);
        var before = read();
        var engine = CreateEngine(host, allowWrite);

        var outcome = engine.Evaluate(Run(strict: true,
            """
            const symbol = Symbol('tag');
            Object.defineProperty(host, 'tag', { value: 1, configurable: true });
            Object.defineProperty(host, symbol, { value: 2, configurable: true });
            Object.preventExtensions(host);
            return [delete host.tag, delete host[symbol], 'tag' in host, symbol in host, Object.isExtensible(host)].join();
            """)).AsString();

        outcome.Should().Be("true,true,false,false,false");
        read().Should().Be(before);
    }

    [TestCaseSource(nameof(ElementDeletes))]
    public void DeletingAnElementIsUnchanged(string shape, bool allowWrite, bool expected, string contents)
    {
        var (host, read) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        engine.Evaluate("delete host[0]").Should().Be(expected);
        read().Should().Be(contents);

        if (!expected)
        {
            engine.Evaluate(Run(strict: true, "delete host[1]; return 'no-throw';")).AsString().Should().Be("TypeError");
            read().Should().Be(contents);
        }
    }

    /// <summary>
    /// A non-extensible or frozen view keeps refusing to delete its elements under every setting. An element's
    /// descriptor is non-configurable, so this is OrdinaryDelete's own answer, and it keeps a frozen view's
    /// contents fixed the way the element-write lane does.
    /// </summary>
    [TestCaseSource(nameof(IndexedShapesSettingsAndIntegrityLevels))]
    public void ANonExtensibleViewStillRefusesToDeleteAnElement(string shape, bool allowWrite, string integrityLevel)
    {
        var (host, read) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);
        engine.Execute($"Object.{integrityLevel}(host);");

        engine.Evaluate("delete host[0]").Should().Be(false);
        engine.Evaluate(Run(strict: true, "delete host[1]; return 'no-throw';")).AsString().Should().Be("TypeError");
        engine.Evaluate("delete host[5]").Should().Be(true, "a position the view does not have is not there to refuse");
        read().Should().Be("1,2");
    }

    /// <summary>
    /// What a view projects from the CLR side follows OrdinaryDelete too, as it does on a plain wrapper and on
    /// a writable view: a reflected property is non-configurable and stays, while a method's descriptor and the
    /// <c>length</c> forwarder are configurable, so deleting drops the wrapper's copy and the next read
    /// resolves the member from the collection again.
    /// </summary>
    [TestCase("List", false)]
    [TestCase("ReadOnlyCollection", false)]
    [TestCase("ReadOnlyCollection", true)]
    public void AViewsClrMembersFollowOrdinaryDeleteToo(string shape, bool allowWrite)
    {
        var (host, read) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        engine.Evaluate("delete host.Count").Should().Be(false, "a reflected property's descriptor is non-configurable");
        engine.Evaluate("host.Count").Should().Be(2);
        engine.Evaluate("delete host.Contains").Should().Be(true);
        engine.Evaluate("host.Contains(2)").Should().Be(true, "the method resolves from the collection again");
        engine.Evaluate("delete host.length").Should().Be(true);
        engine.Evaluate("host.length").Should().Be(2, "the count is still read from the collection");
        read().Should().Be("1,2");
    }

    /// <summary>
    /// The lane that refuses to change the collection must not reach it on the way to deleting the script's
    /// property either. A dictionary-shaped view's ordinary removal also removes the key from the dictionary; for
    /// a key the dictionary does not hold that is a no-op, unless the target's <c>Remove</c> throws.
    /// </summary>
    [Test]
    public void ARefusingDictionaryShapedViewDeletesTheScriptsPropertyWithoutCallingTheTarget()
    {
        var host = new KeyedList { 1, 2 };
        var engine = CreateEngine(host, allowWrite: true);

        var outcome = engine.Evaluate(Run(strict: false,
            """
            Object.defineProperty(host, 'tag', { value: 1, configurable: true });
            Object.preventExtensions(host);
            return [delete host.tag, 'tag' in host, delete host.entry].join();
            """)).AsString();

        outcome.Should().Be("true,false,false", "the dictionary's own entry is still refused on a non-extensible view");
        host.RemoveCalls.Should().Be(0);
        host.GetRange(0, host.Count).Should().Equal(1, 2);
    }

    /// <summary>
    /// The issue's own reproduction, under default options.
    /// </summary>
    [Test]
    public void TheIssuesReproduction()
    {
        var list = new List<int> { 1, 2 };
        var engine = new Engine().SetValue("list", list);

        engine.Evaluate(
            """
            Object.defineProperty(list, Symbol.iterator, { value: function* () { yield 9; }, configurable: true });
            [delete list[Symbol.iterator], [...list].join()].join(';')
            """).AsString().Should().Be("true;1,2", "the wrapper's own iterator is back once the script's is deleted");
    }

    private static Engine CreateEngine(object host, bool allowWrite)
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = allowWrite;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });
        engine.SetValue("host", host);
        return engine;
    }

    private static string Run(bool strict, string body)
    {
        var prologue = strict ? "'use strict';\n" : "";
        return "(function () { " + prologue + "try {\n" + body + "\n} catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";
    }

    private static (object Host, Func<string> Read) CreateShape(string kind)
    {
        switch (kind)
        {
            case "List":
            {
                var items = new List<int> { 1, 2 };
                return (items, () => string.Join(",", items));
            }
            case "ReadOnlyCollection":
            {
                var items = new List<int> { 1, 2 };
                return (new ReadOnlyCollection<int>(items), () => string.Join(",", items));
            }
            case "Array":
            {
                var items = new[] { 1, 2 };
                return (items, () => string.Join(",", items));
            }
            case "ImmutableArray":
            {
                var items = ImmutableArray.Create(1, 2);
                return (items, () => string.Join(",", items));
            }
            case "ArrayList":
            {
                var items = new ArrayList { 1, 2 };
                return (items, () => string.Join(",", items.Cast<object?>().Select(item => item?.ToString() ?? "null")));
            }
            case "ReadOnlyList":
            {
                var items = new List<int> { 1, 2 };
                return (new HostReadOnlySequence(items), () => string.Join(",", items));
            }
            case "JObject":
            {
                var items = new JObject { ["a"] = 1 };
                return (items, () => items.ToString(Formatting.None));
            }
            case "HashSet":
            {
                var items = new HashSet<int> { 1, 2 };
                return (items, () => string.Join(",", items));
            }
            case "Dictionary":
            {
                var items = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 };
                return (items, () => string.Join(",", items.Select(pair => pair.Key + "=" + pair.Value)));
            }
            case "Object":
            {
                var item = new HostRecord();
                return (item, () => item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown shape");
        }
    }

    public sealed class HostRecord
    {
        public int Value { get; set; } = 1;
    }

    /// <summary>
    /// A collection offering only <see cref="IReadOnlyList{T}"/>, which is what the read-only list view wraps.
    /// </summary>
    private sealed class HostReadOnlySequence(List<int> items) : IReadOnlyList<int>
    {
        public int Count => items.Count;

        public int this[int index] => items[index];

        public IEnumerator<int> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A list that is also a string-keyed dictionary, like Newtonsoft's <c>JObject</c>, with one entry and a
    /// <c>Remove</c> that counts its calls.
    /// </summary>
    private sealed class KeyedList : List<int>, IDictionary<string, int>
    {
        public int RemoveCalls { get; private set; }

        int IDictionary<string, int>.this[string key]
        {
            get => key == "entry" ? 7 : throw new KeyNotFoundException(key);
            set => throw new NotSupportedException();
        }

        ICollection<string> IDictionary<string, int>.Keys => ["entry"];

        ICollection<int> IDictionary<string, int>.Values => [7];

        int ICollection<KeyValuePair<string, int>>.Count => 1;

        bool ICollection<KeyValuePair<string, int>>.IsReadOnly => false;

        void IDictionary<string, int>.Add(string key, int value) => throw new NotSupportedException();

        void ICollection<KeyValuePair<string, int>>.Add(KeyValuePair<string, int> item) => throw new NotSupportedException();

        void ICollection<KeyValuePair<string, int>>.Clear() => throw new NotSupportedException();

        bool ICollection<KeyValuePair<string, int>>.Contains(KeyValuePair<string, int> item) => item.Key == "entry" && item.Value == 7;

        bool IDictionary<string, int>.ContainsKey(string key) => key == "entry";

        void ICollection<KeyValuePair<string, int>>.CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) => array[arrayIndex] = new("entry", 7);

        bool IDictionary<string, int>.Remove(string key)
        {
            RemoveCalls++;
            throw new InvalidOperationException("the refusing lane reached the target's Remove");
        }

        bool ICollection<KeyValuePair<string, int>>.Remove(KeyValuePair<string, int> item)
        {
            RemoveCalls++;
            throw new InvalidOperationException("the refusing lane reached the target's Remove");
        }

        bool IDictionary<string, int>.TryGetValue(string key, out int value)
        {
            value = 7;
            return key == "entry";
        }

        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator()
        {
            yield return new("entry", 7);
        }
    }
}
