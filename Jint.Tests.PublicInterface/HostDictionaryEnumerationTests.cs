#nullable enable

using System.Collections.Generic;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Everything a script can ask a wrapped string-keyed host dictionary <em>about</em> its keys rather than
/// for their values: <c>in</c>, <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c>, <c>Object.keys</c> /
/// <c>values</c> / <c>entries</c>, <c>for..in</c>, <c>Object.assign</c>, spread and <c>JSON.stringify</c>.
/// All of them route through the wrapper's own-property probe, which answers from the target's
/// <c>ContainsKey</c> instead of building the descriptor the read path would.
/// <para>
/// The point of asserting from here rather than in-repo is that every answer below is a script-visible
/// one, and the oracle is the host dictionary itself, read straight from C#. Nothing here depends on which
/// lane served the question, so the file stays valid whatever the wrapper does underneath.
/// </para>
/// </summary>
public class HostDictionaryEnumerationTests
{
    private static Dictionary<string, object> Document() => new(System.StringComparer.Ordinal)
    {
        ["id"] = 7d,
        ["name"] = "widget",
        ["nested"] = new Dictionary<string, object>(System.StringComparer.Ordinal) { ["deep"] = 1d },
    };

    private static Engine CreateEngine(object host)
    {
        var engine = new Engine();
        engine.SetValue("doc", host);
        return engine;
    }

    [Fact]
    public void ExistenceQuestionsAnswerFromTheDictionary()
    {
        var engine = CreateEngine(Document());

        engine.Evaluate("'id' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("'nested' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("'missing' in doc").AsBoolean().Should().BeFalse();

        engine.Evaluate("doc.hasOwnProperty('name')").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.hasOwnProperty('missing')").AsBoolean().Should().BeFalse();
        engine.Evaluate("doc.propertyIsEnumerable('name')").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.propertyIsEnumerable('missing')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void EnumerationSeesEveryKeyAndNothingElse()
    {
        var engine = CreateEngine(Document());

        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("id,name,nested");
        engine.Evaluate("Object.entries(doc).map(function (e) { return e[0]; }).join()").AsString().Should().Be("id,name,nested");
        engine.Evaluate("Object.values(doc).length").AsNumber().Should().Be(3);

        engine.Evaluate("var seen = []; for (var k in doc) { seen.push(k); } seen.join();").AsString().Should().Be("id,name,nested");
        engine.Evaluate("Object.keys(Object.assign({}, doc)).join()").AsString().Should().Be("id,name,nested");
        engine.Evaluate("Object.keys({ ...doc }).join()").AsString().Should().Be("id,name,nested");
        engine.Evaluate("JSON.stringify(doc)").AsString().Should().Be("""{"id":7,"name":"widget","nested":{"deep":1}}""");
    }

    /// <summary>
    /// The wrapper never caches a dictionary member, because the dictionary can change under it. Anything
    /// answering existence from a snapshot would go stale the moment the host mutates the target, so a key
    /// added or removed CLR-side between two questions has to be reflected by the next one.
    /// </summary>
    [Fact]
    public void KeysAddedAndRemovedHostSideAreSeenImmediately()
    {
        var document = Document();
        var engine = CreateEngine(document);

        engine.Evaluate("'added' in doc").AsBoolean().Should().BeFalse();
        document["added"] = 1d;
        engine.Evaluate("'added' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("id,name,nested,added");

        document.Remove("name");
        engine.Evaluate("'name' in doc").AsBoolean().Should().BeFalse();
        engine.Evaluate("doc.hasOwnProperty('name')").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("id,nested,added");
    }

    /// <summary>
    /// A CLR member is still resolvable on a wrapped dictionary even though it does not enumerate, so a
    /// name the dictionary does not carry is not automatically absent.
    /// </summary>
    [Fact]
    public void ClrMembersStayVisibleWithoutEnumerating()
    {
        var engine = CreateEngine(Document());

        engine.Evaluate("'Count' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.Count").AsNumber().Should().Be(3);
        engine.Evaluate("Object.keys(doc).indexOf('Count')").AsNumber().Should().Be(-1);
    }

    /// <summary>
    /// A dictionary key that shadows a CLR member name resolves to the dictionary's value on the read path,
    /// so it must be the dictionary's answer that existence and enumerability come from too — the key
    /// enumerates, where the CLR member of that name would not.
    /// </summary>
    [Fact]
    public void ADictionaryKeyShadowingAClrMemberEnumerates()
    {
        var document = new Dictionary<string, object>(System.StringComparer.Ordinal) { ["Count"] = "shadow" };
        var engine = CreateEngine(document);

        engine.Evaluate("doc.Count").AsString().Should().Be("shadow");
        engine.Evaluate("'Count' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("Count");
    }

    /// <summary>
    /// A descriptor a script defined outranks the dictionary, so making a key non-enumerable has to remove
    /// it from every enumeration while leaving it present.
    /// </summary>
    [Fact]
    public void ADefinedDescriptorOutranksTheDictionary()
    {
        var engine = CreateEngine(Document());

        engine.Execute("Object.defineProperty(doc, 'name', { value: 'redefined', enumerable: false, configurable: true });");

        engine.Evaluate("'name' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.hasOwnProperty('name')").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.propertyIsEnumerable('name')").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("id,nested");
    }

    /// <summary>
    /// The immutability promise memoizes a descriptor per key, which must not change any of the answers —
    /// it is a caching declaration, not a semantic one.
    /// </summary>
    [Fact]
    public void TheImmutabilityPromiseDoesNotChangeAnyAnswer()
    {
        var engine = new Engine(options => options.AddImmutableCrossing(typeof(Dictionary<string, object>)));
        engine.SetValue("doc", Document());

        // read first, so the memo exists before anything asks about existence
        engine.Evaluate("doc.name").AsString().Should().Be("widget");

        engine.Evaluate("'name' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.hasOwnProperty('name')").AsBoolean().Should().BeTrue();
        engine.Evaluate("doc.propertyIsEnumerable('name')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'missing' in doc").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("id,name,nested");
    }

    /// <summary>
    /// A read-only dictionary reaches the same lane — <c>ContainsKey</c> is declared on
    /// <c>IReadOnlyDictionary&lt;,&gt;</c> too.
    /// </summary>
    [Fact]
    public void AReadOnlyDictionaryAnswersTheSameWay()
    {
        var engine = new Engine();
        engine.SetValue("doc", (IReadOnlyDictionary<string, object>) Document());

        engine.Evaluate("'id' in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("'missing' in doc").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("id,name,nested");
    }

    /// <summary>
    /// A dictionary that answers <c>ContainsKey</c> and <c>TryGetValue</c> consistently is all the lane
    /// assumes, and a custom implementation is what proves the lane is reading the target rather than
    /// anything Jint keeps. The counter is the evidence: the existence questions must reach
    /// <c>ContainsKey</c> and must not have to produce the value.
    /// </summary>
    [Fact]
    public void ExistenceQuestionsAskTheTargetWithoutReadingValues()
    {
        var document = new CountingDictionary { ["a"] = 1d, ["b"] = 2d };
        var engine = CreateEngine(document);

        document.ResetCounters();
        engine.Evaluate("'a' in doc").AsBoolean().Should().BeTrue();
        document.ContainsKeyCalls.Should().Be(1);
        document.TryGetValueCalls.Should().Be(0);

        document.ResetCounters();
        engine.Evaluate("Object.keys(doc).join()").AsString().Should().Be("a,b");
        document.TryGetValueCalls.Should().Be(0, "listing the keys never needs a value");

        document.ResetCounters();
        engine.Evaluate("Object.values(doc).length").AsNumber().Should().Be(2);
        document.TryGetValueCalls.Should().Be(2, "one per key, and only for the values actually returned");
    }

    /// <summary>
    /// Delegates to an inner dictionary and counts. It has to implement the interface itself rather than
    /// derive from <see cref="Dictionary{TKey,TValue}"/> with <c>new</c> members: the wrapper resolves
    /// <c>ContainsKey</c> and <c>TryGetValue</c> off <see cref="IDictionary{TKey,TValue}"/>, so a hiding
    /// member would never be the one called and the counters would sit at zero however the lane behaved.
    /// </summary>
    private sealed class CountingDictionary : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _inner = new(System.StringComparer.Ordinal);

        public int ContainsKeyCalls { get; private set; }
        public int TryGetValueCalls { get; private set; }

        public void ResetCounters()
        {
            ContainsKeyCalls = 0;
            TryGetValueCalls = 0;
        }

        public bool ContainsKey(string key)
        {
            ContainsKeyCalls++;
            return _inner.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            TryGetValueCalls++;
            return _inner.TryGetValue(key, out value!);
        }

        public object this[string key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public ICollection<string> Keys => _inner.Keys;
        public ICollection<object> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;

        public void Add(string key, object value) => _inner.Add(key, value);
        public bool Remove(string key) => _inner.Remove(key);
        public void Add(KeyValuePair<string, object> item) => _inner.Add(item.Key, item.Value);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<string, object> item) => _inner.ContainsKey(item.Key);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => ((IDictionary<string, object>) _inner).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, object> item) => _inner.Remove(item.Key);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    /// <summary>
    /// A non-string-keyed dictionary keeps the descriptor path for its keys — the lane is string-keyed —
    /// so this is the control that nothing about those answers moved.
    /// </summary>
    [Fact]
    public void ANonStringKeyedDictionaryIsUnaffected()
    {
        var engine = new Engine();
        engine.SetValue("doc", new Dictionary<int, string> { [1] = "one", [2] = "two" });

        engine.Evaluate("1 in doc").AsBoolean().Should().BeTrue();
        engine.Evaluate("3 in doc").AsBoolean().Should().BeFalse();
        engine.Evaluate("doc[2]").AsString().Should().Be("two");
    }
}
