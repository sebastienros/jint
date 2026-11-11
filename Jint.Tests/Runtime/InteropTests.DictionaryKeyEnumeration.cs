#nullable enable

using System.Collections;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Dynamic;

namespace Jint.Tests.Runtime;

/// <summary>
/// Every script-visible enumeration of a wrapped string-keyed generic dictionary reads the target's key set
/// through <c>TypeDescriptor.KeysAccessor</c>, and that property is whichever <c>Keys</c> the first matching
/// interface declares. <see cref="IDictionary{TKey,TValue}"/> types it <see cref="ICollection{T}"/>;
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> types it <see cref="IEnumerable{T}"/>, and what an
/// <c>ImmutableDictionary</c> returns there is an iterator, which is no collection at all.
/// </summary>
public partial class InteropTests
{
    // The objects every shape below stands for, in Node: { b: "2", a: "1" }. Scripts sort what they read, because
    // the order of a CLR dictionary's keys is the dictionary's own (hash order for ImmutableDictionary, key order
    // for ImmutableSortedDictionary) and is not what these tests pin.
    //
    // https://tc39.es/ecma262/#sec-ordinaryownpropertykeys
    // https://tc39.es/ecma262/#sec-object.keys
    // https://tc39.es/ecma262/#sec-object.entries
    // https://tc39.es/ecma262/#sec-object.getownpropertynames
    // https://tc39.es/ecma262/#sec-enumerate-object-properties
    // https://tc39.es/ecma262/#sec-json.stringify
    // https://tc39.es/ecma262/#sec-copydataproperties
    // https://tc39.es/ecma262/#sec-object.assign
    public static TheoryData<string> StringKeyedDictionaryShapes
    {
        get
        {
            return new TheoryData<string>
            {
                // the reported failures: IReadOnlyDictionary<string, T>.Keys is an iterator, not a collection
                "ImmutableDictionary",
                "ImmutableSortedDictionary",
                "IteratorKeysReadOnlyDictionary",
                "ExplicitIteratorKeysReadOnlyDictionary",
                // shapes whose Keys is a collection under either reading (the builder resolves IDictionary's)
                "ImmutableDictionaryBuilder",
                "ReadOnlyDictionary",
                "Dictionary",
                "ExpandoObject",
#if NET8_0_OR_GREATER
                "FrozenDictionary",
#endif
            };
        }
    }

    private static object CreateStringKeyedDictionary(string shape)
    {
        var entries = new[]
        {
            new KeyValuePair<string, string>("b", "2"),
            new KeyValuePair<string, string>("a", "1"),
        };

        switch (shape)
        {
            case "ImmutableDictionary":
                return ImmutableDictionary.CreateRange(entries);
            case "ImmutableSortedDictionary":
                return ImmutableSortedDictionary.CreateRange(entries);
            case "ImmutableDictionaryBuilder":
                return ImmutableDictionary.CreateRange(entries).ToBuilder();
            case "IteratorKeysReadOnlyDictionary":
                return new IteratorKeysReadOnlyDictionary<string>(entries);
            case "ExplicitIteratorKeysReadOnlyDictionary":
                return new ExplicitIteratorKeysReadOnlyDictionary<string>(entries);
            case "ReadOnlyDictionary":
                return new ReadOnlyDictionary<string, string>(entries.ToDictionary(static e => e.Key, static e => e.Value));
            case "Dictionary":
                return entries.ToDictionary(static e => e.Key, static e => e.Value);
            case "ExpandoObject":
                var expando = new ExpandoObject();
                var members = (IDictionary<string, object?>) expando;
                foreach (var entry in entries)
                {
                    members[entry.Key] = entry.Value;
                }

                return expando;
#if NET8_0_OR_GREATER
            case "FrozenDictionary":
                return entries.ToDictionary(static e => e.Key, static e => e.Value).ToFrozenDictionary();
#endif
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
        }
    }

    private static Engine CreateDictionaryEngine(string shape)
    {
        var engine = new Engine();
        engine.SetValue("d", CreateStringKeyedDictionary(shape));
        return engine;
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void ObjectKeysListsTheKeysOfAStringKeyedDictionary(string shape)
    {
        CreateDictionaryEngine(shape).Evaluate("Object.keys(d).sort().join()").AsString().Should().Be("a,b");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void ForInVisitsTheKeysOfAStringKeyedDictionary(string shape)
    {
        var engine = CreateDictionaryEngine(shape);
        engine.Evaluate("(function () { var seen = []; for (var p in d) { seen.push(p); } return seen.sort().join(); })()")
            .AsString().Should().Be("a,b");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void GetOwnPropertyNamesListsTheKeysOfAStringKeyedDictionary(string shape)
    {
        CreateDictionaryEngine(shape).Evaluate("Object.getOwnPropertyNames(d).sort().join()").AsString().Should().Be("a,b");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void ReflectOwnKeysListsTheKeysOfAStringKeyedDictionary(string shape)
    {
        CreateDictionaryEngine(shape).Evaluate("Reflect.ownKeys(d).sort().join()").AsString().Should().Be("a,b");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void ObjectEntriesPairsTheKeysOfAStringKeyedDictionaryWithTheirValues(string shape)
    {
        CreateDictionaryEngine(shape).Evaluate("Object.entries(d).map(e => e.join('=')).sort().join()").AsString().Should().Be("a=1,b=2");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void JsonStringifyWritesTheKeysOfAStringKeyedDictionary(string shape)
    {
        // read back through JSON.parse so that the dictionary's own key order is not what is compared
        var engine = CreateDictionaryEngine(shape);
        engine.Evaluate("var o = JSON.parse(JSON.stringify(d)); Object.keys(o).sort().map(k => k + '=' + o[k]).join()")
            .AsString().Should().Be("a=1,b=2");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void SpreadCopiesTheKeysOfAStringKeyedDictionary(string shape)
    {
        var engine = CreateDictionaryEngine(shape);
        engine.Evaluate("var o = { ...d }; Object.keys(o).sort().map(k => k + '=' + o[k]).join()")
            .AsString().Should().Be("a=1,b=2");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void ObjectAssignCopiesTheKeysOfAStringKeyedDictionary(string shape)
    {
        var engine = CreateDictionaryEngine(shape);
        engine.Evaluate("var o = Object.assign({}, d); Object.keys(o).sort().map(k => k + '=' + o[k]).join()")
            .AsString().Should().Be("a=1,b=2");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void ObjectValuesReadsTheValuesOfAStringKeyedDictionary(string shape)
    {
        CreateDictionaryEngine(shape).Evaluate("Object.values(d).sort().join()").AsString().Should().Be("1,2");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void GetOwnPropertyDescriptorsDescribesTheKeysOfAStringKeyedDictionary(string shape)
    {
        var engine = CreateDictionaryEngine(shape);
        engine.Evaluate("var ds = Object.getOwnPropertyDescriptors(d); Object.keys(ds).sort().map(k => k + '=' + ds[k].value).join()")
            .AsString().Should().Be("a=1,b=2");
    }

    [Theory]
    [MemberData(nameof(StringKeyedDictionaryShapes))]
    public void AKeyOfAStringKeyedDictionaryIsReadableByNameAndBySubscript(string shape)
    {
        // the lanes that never enumerate worked before and must keep working: they are the control for the rest
        var engine = CreateDictionaryEngine(shape);
        engine.Evaluate("d.a + d['b'] + ('a' in d) + d.hasOwnProperty('b') + ('c' in d)").AsString().Should().Be("12truetruefalse");
    }

    [Fact]
    public void ObjectKeysOfAnEmptyIteratorKeyedDictionaryIsEmpty()
    {
        var engine = new Engine();
        engine.SetValue("d", ImmutableDictionary<string, string>.Empty);
        engine.Evaluate("Object.keys(d).length").AsNumber().Should().Be(0);
        engine.Evaluate("Reflect.ownKeys(d).length").AsNumber().Should().Be(0);
        engine.Evaluate("JSON.stringify(d)").AsString().Should().Be("{}");
    }

    [Fact]
    public void AnImmutableDictionaryOfNonStringValuesEnumeratesToo()
    {
        // the key type is what makes the dictionary string-keyed; the value type is whatever the host has
        var engine = new Engine();
        engine.SetValue("d", ImmutableDictionary.CreateRange([
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2),
        ]));

        engine.Evaluate("Object.entries(d).map(e => e.join('=')).sort().join()").AsString().Should().Be("one=1,two=2");
        engine.Evaluate("JSON.stringify(Object.fromEntries(Object.entries(d).sort()))").AsString().Should().Be("{\"one\":1,\"two\":2}");
    }

    /// <summary>
    /// A string-keyed <see cref="IReadOnlyDictionary{TKey,TValue}"/> whose public <c>Keys</c> is an iterator,
    /// which is the shape <c>ImmutableDictionary</c> gives it and the one a cast to a collection cannot take.
    /// </summary>
    private sealed class IteratorKeysReadOnlyDictionary<TValue> : IReadOnlyDictionary<string, TValue>
    {
        private readonly List<KeyValuePair<string, TValue>> _entries;

        public IteratorKeysReadOnlyDictionary(IEnumerable<KeyValuePair<string, TValue>> entries)
        {
            _entries = new List<KeyValuePair<string, TValue>>(entries);
        }

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var entry in _entries)
                {
                    yield return entry.Key;
                }
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var entry in _entries)
                {
                    yield return entry.Value;
                }
            }
        }

        public int Count => _entries.Count;

        public TValue this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public bool TryGetValue(string key, out TValue value)
        {
            foreach (var entry in _entries)
            {
                if (entry.Key == key)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// The same dictionary with <c>Keys</c> declared only through the interface, so that the interface's own
    /// property is the only one there is to find.
    /// </summary>
    private sealed class ExplicitIteratorKeysReadOnlyDictionary<TValue> : IReadOnlyDictionary<string, TValue>
    {
        private readonly List<KeyValuePair<string, TValue>> _entries;

        public ExplicitIteratorKeysReadOnlyDictionary(IEnumerable<KeyValuePair<string, TValue>> entries)
        {
            _entries = new List<KeyValuePair<string, TValue>>(entries);
        }

        IEnumerable<string> IReadOnlyDictionary<string, TValue>.Keys
        {
            get
            {
                foreach (var entry in _entries)
                {
                    yield return entry.Key;
                }
            }
        }

        IEnumerable<TValue> IReadOnlyDictionary<string, TValue>.Values
        {
            get
            {
                foreach (var entry in _entries)
                {
                    yield return entry.Value;
                }
            }
        }

        public int Count => _entries.Count;

        public TValue this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public bool TryGetValue(string key, out TValue value)
        {
            foreach (var entry in _entries)
            {
                if (entry.Key == key)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
